"""OpenAI models played through the Codex CLI, billed to the ChatGPT subscription.

    bin/eval.sh dark_cave --model codex-cli/gpt-6.1-sol -M effort=medium

The counterpart of evals/claude.py. `codex-cli/<model>` is a name Inspect can log
and nothing else; the player solver hands the sample here, where `codex exec`
plays the game with:

- our system prompt in place of Codex's base instructions (model_instructions_file);
- its tools off: shell, command execution, images, browser, computer use,
  sub-agents, plugins, apps, web search;
- no environment context, permissions text, skills catalogue or collaboration
  instructions, and no user config or rules from this machine;
- a home folder of its own for each game, linked to the machine's sign-in
  and nothing else;
- one tool, play, served over MCP from this process, pre-approved.

What Codex still sends that cannot be turned off: two developer messages
(team-of-agents framing; don't spawn sub-agents). Each game records every
developer message it was sent, so this is visible in the log rather than
assumed.

Codex reaches tools through its code mode: the model writes a line of
JavaScript that calls tools.mcp__og__play. The call that reaches the game is
the same; the way the model makes it is not, and is a difference from Claude.

A game ends when the game does, or when the model finishes its turn without
the game ending: "stopped", as in the other harnesses. A step here is one play
call (max_steps counts calls), not one model response as in evals/claude.py and
Inspect's loop.
"""

import asyncio
import json
import os
import shutil
import tempfile
from pathlib import Path

from inspect_ai.model import (ChatMessageAssistant, ChatMessageSystem, ChatMessageTool, ChatMessageUser,
                              ContentReasoning, ContentText, ModelAPI, ModelOutput, modelapi)
from inspect_ai.solver import TaskState
from inspect_ai.tool import ToolCall

from .agent import AFTER_TIMEOUT, DEBRIEF, Game
from .claude import SESSION_TIMEOUT, serve

# The OpenAI API's own default. Codex runs these models at "low", a coding
# choice that asks for brevity no other harness asks for.
VERBOSITY = "medium"

# Every Codex feature that gives the model something to do besides play.
FEATURES_OFF = [
    "shell_tool", "unified_exec", "image_generation", "multi_agent", "multi_agent_v2", "apps", "plugins",
    "browser_use", "browser_use_external", "computer_use", "view_image", "sleep_tool", "tool_suggest",
    "skill_search", "goals", "hooks", "memories", "tool_call_mcp_elicitation", "skill_mcp_dependency_install",
    "workspace_dependencies", "worktrees", "shell_snapshot", "remote_plugin", "plugin_sharing", "in_app_browser",
]
# What Codex would otherwise add to the conversation.
CONTEXT_OFF = [
    "include_environment_context=false", "include_permissions_instructions=false",
    "include_apps_instructions=false", "include_collaboration_mode_instructions=false",
    "skills.include_instructions=false", 'web_search="disabled"', 'approval_policy="never"',
    "project_doc_max_bytes=0",
]


@modelapi(name="codex-cli")
def codex_cli():
    return CodexCLI


class CodexCLI(ModelAPI):
    def __init__(self, model_name: str, base_url=None, api_key=None, config=None, **model_args):
        super().__init__(model_name=model_name, base_url=base_url, api_key=api_key, config=config)
        self.effort = model_args.get("effort")

    async def generate(self, input, tools, tool_choice, config) -> ModelOutput:
        raise RuntimeError("codex-cli models play only through the player solver")


async def play_through_codex(state: TaskState, game: Game, first: str, model, max_steps: int) -> str | None:
    web, serving, url = await serve(game)
    scratch = Path(tempfile.mkdtemp(prefix="og-codex-"))
    home, work = scratch / "home", scratch / "work"
    home.mkdir(), work.mkdir()
    # Its own home: Codex reinstalls its built-in skills and writes its records
    # there, not in the machine's. Only the sign-in is shared.
    (home / "auth.json").symlink_to(Path.home() / ".codex" / "auth.json")
    instructions = scratch / "instructions.md"
    instructions.write_text(game.system())

    settings = ["-c", f'model_instructions_file="{instructions}"',
               "-c", 'model_reasoning_summary="detailed"',
               "-c", f'model_verbosity="{VERBOSITY}"',
               "-c", f'mcp_servers.og.url="{url}"',
               "-c", 'mcp_servers.og.default_tools_approval_mode="approve"']
    if model.api.effort:
        settings += ["-c", f'model_reasoning_effort="{model.api.effort}"']
    for setting in CONTEXT_OFF:
        settings += ["-c", setting]
    for feature in FEATURES_OFF:
        settings += ["--disable", feature]
    common = ["--json", "--skip-git-repo-check", "--ignore-user-config", "--ignore-rules",
              "-m", model.api.model_name]
    command = ["codex", "exec", *common, "-C", str(work), "-s", "read-only", *settings, first]

    env = {k: os.environ[k] for k in ("HOME", "PATH", "USER", "LANG") if k in os.environ}
    env["CODEX_HOME"] = str(home)
    process = await asyncio.create_subprocess_exec(
        *command, cwd=work, env=env, stdin=asyncio.subprocess.DEVNULL,
        stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE, limit=16 * 1024 * 1024)

    events, ended, calls, thread = [], None, 0, None
    # What the player said after the game ended, finishing its turn, and its
    # answer to the debrief: kept out of the game's messages.
    closing: list[dict] = []
    debrief: list[dict] = []

    def note(item: dict, into: list[dict]):
        if item.get("type") == "reasoning" and item.get("text"):
            into.append({"reasoning": item["text"]})
        elif item.get("type") == "agent_message" and item.get("text"):
            into.append({"text": item["text"]})
        elif item.get("type") == "mcp_tool_call":
            into.append({"call": item.get("arguments") or {}})
    messages = [ChatMessageSystem(content=game.system()), ChatMessageUser(content=first)]
    pending: list = []  # reasoning and words since the last play call, for the next assistant message

    def flush(tool_call: ToolCall | None = None):
        nonlocal pending
        if pending or tool_call:
            messages.append(ChatMessageAssistant(content=pending or "", tool_calls=[tool_call] if tool_call else None,
                                                 model=model.api.model_name))
        pending = []

    async def run():
        nonlocal ended, calls, thread
        # Once the game is over, the rest of the turn has a short limit, not the session's.
        while line := await (asyncio.wait_for(process.stdout.readline(), AFTER_TIMEOUT) if ended
                             else process.stdout.readline()):
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue
            events.append(event)
            kind, item = event.get("type"), event.get("item") or {}
            if kind == "thread.started":
                thread = event.get("thread_id")
            elif kind == "item.completed" and ended:
                # Over: the turn is let finish, so what the player says next is
                # heard, but none of it is the game's.
                note(item, closing)
                if sum("call" in part for part in closing) >= 3:
                    return
            elif kind == "item.completed":
                if item.get("type") == "reasoning" and item.get("text"):
                    pending.append(ContentReasoning(reasoning=item["text"]))
                elif item.get("type") == "agent_message" and item.get("text"):
                    pending.append(ContentText(text=item["text"]))
                elif item.get("type") == "mcp_tool_call":
                    calls += 1
                    call = ToolCall(id=item.get("id", f"call-{calls}"), function="play",
                                    arguments=item.get("arguments") or {})
                    flush(call)
                    result = item.get("result") or {}
                    text = "\n".join(part.get("text", "") for part in result.get("content", []) or [])
                    messages.append(ChatMessageTool(content=text or str(item.get("error") or ""),
                                                    tool_call_id=call.id, function="play"))
                    ended = game.over()
                    if not ended and calls >= max_steps:
                        return
            elif kind == "turn.completed":
                flush()
                ended = ended or game.over() or "stopped"
                return
            elif _fatal(event):
                flush()
                return

    async def ask():
        # The same session, resumed with the same settings, asked one question.
        asking = await asyncio.create_subprocess_exec(
            "codex", "exec", "resume", *common, "-c", 'sandbox_mode="read-only"', *settings, thread, DEBRIEF,
            cwd=work, env=env, stdin=asyncio.subprocess.DEVNULL,
            stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE, limit=16 * 1024 * 1024)
        try:
            while line := await asyncio.wait_for(asking.stdout.readline(), AFTER_TIMEOUT):
                try:
                    event = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if event.get("type") == "item.completed":
                    note(event.get("item") or {}, debrief)
                elif _fatal(event):
                    debrief.append({"error": json.dumps(event)[:500]})
                elif event.get("type") == "turn.completed":
                    return
        finally:
            if asking.returncode is None:
                asking.kill()
            await asking.wait()

    try:
        try:
            await asyncio.wait_for(run(), SESSION_TIMEOUT)
        except asyncio.TimeoutError:
            # Past the end of the game, a turn that never finishes loses only the debrief.
            if not ended:
                raise
        finally:
            await _stop(process)
        if ended and thread:
            try:
                await ask()
            except asyncio.TimeoutError:
                debrief.append({"error": "no answer in time"})
    finally:
        stderr = (await process.stderr.read()).decode()[-2000:]
        web.should_exit = True
        await serving
        injected = _developer_messages(home)
        tokens = _token_counts(home)
        shutil.rmtree(scratch, ignore_errors=True)

    failures = [e for e in events if _fatal(e)]
    usage = [e.get("usage") for e in events if e.get("type") == "turn.completed"]
    state.messages = messages
    state.store.set("debrief", {"question": DEBRIEF, "closing": closing, "answer": debrief})
    state.store.set("cli", {
        "harness": "codex-cli",
        "model": model.api.model_name,
        "effort": model.api.effort,
        "verbosity": VERBOSITY,
        "thinking_disabled": False,
        "thinking_blocks": sum(1 for e in events if e.get("type") == "item.completed"
                               and (e.get("item") or {}).get("type") == "reasoning"),
        "steps": calls,
        "usage": usage,
        # Per model response, from Codex's own record: the reasoning it did,
        # which the summaries leave out when they are not written.
        "tokens": tokens,
        # What Codex sent besides our prompt and the game, from its own record.
        "injected": injected,
        "errors": failures,
        "stderr": stderr if not ended else "",
    })
    if failures:
        raise RuntimeError(f"codex failed: {json.dumps(failures[-1])[:500]}")
    if not events:
        raise RuntimeError(f"codex produced no output: {stderr}")
    return ended


def _fatal(event: dict) -> bool:
    """
    A turn that failed, or an error Codex gave up on. "Reconnecting... 2/5" is
    an error event too, but Codex goes on retrying the connection itself; if
    it runs out of tries, a final error or a failed turn follows.
    """
    if event.get("type") == "turn.failed":
        return True
    return event.get("type") == "error" and not str(event.get("message", "")).startswith("Reconnecting")


async def _stop(process):
    if process.returncode is None:
        process.terminate()
        try:
            await asyncio.wait_for(process.wait(), 10)
        except asyncio.TimeoutError:
            process.kill()


def _token_counts(home: Path) -> list[dict]:
    """
    Each model response's tokens, in order, with what the response did: the
    play command it typed, or "said" for words. The debrief's are included,
    marked by what they answered.
    """
    out = []
    for record in sorted(home.glob("sessions/**/rollout-*.jsonl")):
        did = []
        for line in record.read_text().splitlines():
            try:
                entry = json.loads(line)
            except json.JSONDecodeError:
                continue
            payload = entry.get("payload", {})
            kind = payload.get("type")
            if kind in ("function_call", "custom_tool_call"):
                did.append(str(payload.get("arguments") or payload.get("input") or "")[:200])
            elif kind == "message" and payload.get("role") == "assistant":
                did.append("said")
            elif kind == "token_count" and (info := payload.get("info")):
                last = info.get("last_token_usage") or {}
                out.append({"did": did, "reasoning": last.get("reasoning_output_tokens"),
                            "output": last.get("output_tokens"), "input": last.get("input_tokens")})
                did = []
    return out


def _developer_messages(home: Path) -> list[str]:
    """The opening of every developer or system message in the game's session record."""
    out = []
    for record in home.glob("sessions/**/rollout-*.jsonl"):
        for line in record.read_text().splitlines():
            try:
                payload = json.loads(line).get("payload", {})
            except json.JSONDecodeError:
                continue
            if payload.get("type") == "message" and payload.get("role") in ("developer", "system"):
                text = " ".join(part.get("text", "") for part in payload.get("content", []) if isinstance(part, dict))
                out.append(text[:300])
    return out
