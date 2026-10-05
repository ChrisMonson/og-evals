"""Claude played through the Claude Code CLI, billed to the subscription.

    bin/eval.sh dark_cave --model claude-cli/claude-sonnet-5-5 -M effort=high
    bin/eval.sh dark_cave --model claude-cli/claude-haiku-4-5 -M thinking=off

`claude-cli/<model>` is a model name Inspect can log and nothing else: its
generate() refuses. The player solver sees it and hands the sample here, where
`claude -p` plays the game with:

- our system prompt in place of Claude Code's (--system-prompt replaces it);
- no built-in tools (--tools ""), and no MCP servers but ours (--strict-mcp-config);
- no settings, hooks, plugins or CLAUDE.md from this machine (--setting-sources
  with nothing in it, and an empty working directory);
- one tool, play, served over MCP from this process, so the game lives here
  with the sample and not in a process the CLI owns.

The loop is matched to Inspect's. When a reply ends without a tool call the CLI
ends its turn, and the game ends there as "stopped", as it does in Inspect's
loop. The harness's stopping rules end it otherwise, and a step is one model
response, as in Inspect (in evals/codex.py a step is one play call instead).
"""

import asyncio
import json
import logging
import os
import tempfile
from typing import Annotated

import uvicorn
from inspect_ai.model import (ChatMessageAssistant, ChatMessageSystem, ChatMessageTool, ChatMessageUser,
                              ContentReasoning, ContentText, ModelAPI, ModelOutput, modelapi)
from inspect_ai.solver import TaskState
from inspect_ai.tool import ToolCall
from mcp.server.mcpserver import MCPServer
from pydantic import Field

from .agent import AFTER_TIMEOUT, COMMAND_DESCRIPTION, DEBRIEF, INTENT_DESCRIPTION, PLAY_DESCRIPTION, Game

# How long a whole game may take in a CLI harness.
SESSION_TIMEOUT = 1800

# The MCP server logs every request at INFO; Inspect would show them all.
logging.getLogger("mcp").setLevel(logging.WARNING)


@modelapi(name="claude-cli")
def claude_cli():
    return ClaudeCLI


class ClaudeCLI(ModelAPI):
    def __init__(self, model_name: str, base_url=None, api_key=None, config=None, **model_args):
        super().__init__(model_name=model_name, base_url=base_url, api_key=api_key, config=config)
        self.effort = model_args.get("effort")
        # -M thinking=off disables thinking entirely, for models that take no
        # effort level.
        self.thinking = model_args.get("thinking")

    async def generate(self, input, tools, tool_choice, config) -> ModelOutput:
        raise RuntimeError("claude-cli models play only through the player solver")


async def serve(game: Game) -> tuple[uvicorn.Server, asyncio.Task, str]:
    """Serves the play tool over streamable HTTP on a free local port."""
    server = MCPServer("og")

    async def play(command: Annotated[str, Field(description=COMMAND_DESCRIPTION)]) -> str:
        return await game.play(command)

    async def play_with_intent(command: Annotated[str, Field(description=COMMAND_DESCRIPTION)],
                               intent: Annotated[str, Field(description=INTENT_DESCRIPTION)]) -> str:
        return await game.play(command, intent)

    server.add_tool(play_with_intent if game.asks_intent else play, name="play",
                    description=PLAY_DESCRIPTION, structured_output=False)
    app = server.streamable_http_app(json_response=True, stateless_http=True)
    web = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", port=0, log_level="warning", lifespan="on"))
    task = asyncio.create_task(web.serve())
    while not web.started:
        if task.done():
            task.result()
        await asyncio.sleep(0.05)
    port = web.servers[0].sockets[0].getsockname()[1]
    return web, task, f"http://127.0.0.1:{port}/mcp"


def _user(text: str) -> bytes:
    return (json.dumps({"type": "user", "message": {"role": "user", "content": text}}) + "\n").encode()


async def play_through_cli(state: TaskState, game: Game, first: str, model, max_steps: int) -> str | None:
    web, serving, url = await serve(game)
    workdir = tempfile.mkdtemp(prefix="og-cli-")
    config = json.dumps({"mcpServers": {"og": {"type": "http", "url": url}}})
    command = ["claude", "-p", "--verbose",
               "--input-format", "stream-json", "--output-format", "stream-json",
               "--model", model.api.model_name,
               "--system-prompt", game.system(),
               "--tools", "",
               "--strict-mcp-config", "--mcp-config", config,
               "--allowedTools", "mcp__og__play",
               "--setting-sources", "",
               "--no-session-persistence",
               # Claude Code asks for thinking with display "omitted", which
               # returns empty thinking blocks. Summarized returns what the
               # model reasoned, summarized; the thinking itself is the same.
               "--thinking-display", "summarized"]
    if model.api.effort:
        command += ["--effort", str(model.api.effort)]

    # A clean environment: run from inside a Claude Code session, the CLI
    # otherwise inherits that session's variables and adds what they describe
    # (its scratchpad, its entrypoint) to the conversation.
    env = {k: os.environ[k] for k in ("HOME", "PATH", "USER", "LANG", "ANTHROPIC_BASE_URL") if k in os.environ}
    # Claude Code prompts a quiet model to report to the user; a player has no user.
    env["CLAUDE_CODE_SILENT_TURN_REMINDER"] = "0"
    # Inspect reads -M values as YAML, so "off" arrives as False. Not given, it
    # is None -- which must leave thinking on.
    if model.api.thinking is False or (isinstance(model.api.thinking, str)
                                       and model.api.thinking.lower() in ("off", "false")):
        env["CLAUDE_CODE_DISABLE_THINKING"] = "1"
    process = await asyncio.create_subprocess_exec(
        *command, cwd=workdir, env=env, stdin=asyncio.subprocess.PIPE, stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE, limit=16 * 1024 * 1024)

    events, steps, ended = [], set(), None
    messages = [ChatMessageSystem(content=game.system()), ChatMessageUser(content=first)]
    # What the player said after the game ended, finishing its turn, and its
    # answer to the debrief: kept out of the game's messages.
    closing: list[dict] = []
    debrief: list[dict] = []

    def note(message: dict, into: list[dict]):
        for block in message.get("content", []):
            if block.get("type") == "thinking" and block.get("thinking"):
                into.append({"reasoning": block["thinking"]})
            elif block.get("type") == "text" and block.get("text"):
                into.append({"text": block["text"]})
            elif block.get("type") == "tool_use":
                into.append({"call": block.get("input", {})})

    async def run():
        nonlocal ended
        process.stdin.write(_user(first))
        await process.stdin.drain()
        # Once the game is over, the rest of the turn has a short limit, not the session's.
        while line := await (asyncio.wait_for(process.stdout.readline(), AFTER_TIMEOUT) if ended
                             else process.stdout.readline()):
            event = json.loads(line)
            events.append(event)
            kind = event.get("type")
            if kind == "assistant":
                message = event["message"]
                if ended:
                    note(message, closing)
                    # A player still typing into a finished game is let go after a few.
                    if sum("call" in part for part in closing) >= 3:
                        return
                    continue
                steps.add(message.get("id"))
                messages.append(_assistant(message))
                if len(steps) >= max_steps and not _calls(message):
                    return
            elif kind == "user":
                if ended:
                    continue
                messages.extend(_results(event["message"]))
                # Over: the turn is let finish, so what the player says next is
                # heard, but none of it is the game's.
                ended = game.over()
                if not ended and len(steps) >= max_steps:
                    return
            elif kind == "result":
                if ended or (ended := game.over()):
                    return
                if event.get("is_error") or len(steps) >= max_steps:
                    return
                # The turn ended without the game ending: the player stopped.
                ended = "stopped"
                return

    async def ask():
        process.stdin.write(_user(DEBRIEF))
        await process.stdin.drain()
        while line := await process.stdout.readline():
            event = json.loads(line)
            events.append(event)
            if event.get("type") == "assistant":
                note(event["message"], debrief)
            elif event.get("type") == "result":
                return

    try:
        try:
            await asyncio.wait_for(run(), SESSION_TIMEOUT)
        except asyncio.TimeoutError:
            # Past the end of the game, a turn that never finishes loses only the debrief.
            if not ended:
                raise
        asked_at = len(events)
        if ended and process.returncode is None:
            try:
                await asyncio.wait_for(ask(), AFTER_TIMEOUT)
            except asyncio.TimeoutError:
                debrief.append({"error": "no answer in time"})
    finally:
        if process.returncode is None:
            process.terminate()
            try:
                await asyncio.wait_for(process.wait(), 10)
            except asyncio.TimeoutError:
                process.kill()
        stderr = (await process.stderr.read()).decode()[-2000:]
        web.should_exit = True
        await serving

    init = next((e for e in events if e.get("type") == "system" and e.get("subtype") == "init"), {})
    # The game's results: a debrief that fails takes nothing from a game played.
    results = [e for e in events[:asked_at] if e.get("type") == "result"]
    state.messages = messages
    state.store.set("debrief", {"question": DEBRIEF, "closing": closing, "answer": debrief})
    state.store.set("cli", {
        "harness": "claude-cli",
        "thinking_disabled": "CLAUDE_CODE_DISABLE_THINKING" in env,
        "thinking_blocks": sum(1 for e in events if e.get("type") == "assistant"
                               for b in e["message"].get("content", []) if b.get("type") == "thinking"),
        "model": init.get("model"),
        "tools": init.get("tools"),
        "mcp_servers": init.get("mcp_servers"),
        "cost_usd": sum(r.get("total_cost_usd") or 0 for r in results),
        "usage": [r.get("usage") for r in results],
        "steps": len(steps),
        "errors": [r for r in results if r.get("is_error")],
        "stderr": stderr if not ended else "",
    })
    # A failed CLI turn (a lapsed login, a usage limit, an API error) is not a
    # choice the player made. It fails the sample rather than scoring as one.
    if not events:
        raise RuntimeError(f"claude produced no output: {stderr}")
    if failed := [r for r in results if r.get("is_error")]:
        raise RuntimeError(f"claude failed: {failed[-1].get('result') or failed[-1].get('subtype')}")
    if not ended and not steps:
        raise RuntimeError(f"claude ended without playing: {stderr}")
    return ended


def _calls(message: dict) -> list[dict]:
    return [b for b in message.get("content", []) if b.get("type") == "tool_use"]


def _assistant(message: dict) -> ChatMessageAssistant:
    content = []
    for block in message.get("content", []):
        if block.get("type") == "thinking" and block.get("thinking"):
            content.append(ContentReasoning(reasoning=block["thinking"]))
        elif block.get("type") == "text":
            content.append(ContentText(text=block["text"]))
    calls = [ToolCall(id=b["id"], function=b["name"].removeprefix("mcp__og__"), arguments=b.get("input", {}))
             for b in _calls(message)]
    return ChatMessageAssistant(content=content or "", tool_calls=calls or None, model=message.get("model"))


def _results(message: dict) -> list[ChatMessageTool]:
    out = []
    for block in message.get("content", []) if isinstance(message.get("content"), list) else []:
        if block.get("type") != "tool_result":
            continue
        body = block.get("content")
        text = body if isinstance(body, str) else "\n".join(b.get("text", "") for b in body or [])
        out.append(ChatMessageTool(content=text, tool_call_id=block.get("tool_use_id"), function="play"))
    return out
