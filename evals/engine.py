"""One engine process per sample, spoken to over the JSON-lines protocol.

A game is stateful, so the process lives for the whole sample and every tool
call talks to the same one. Replaying the history on each call would not do:
a session is only repeatable as one unbroken run of commands with one seed.
"""

import asyncio
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BINARY = ROOT / "build" / "og" / "Og.Cli"


class EngineError(RuntimeError):
    pass


class Engine:
    def __init__(self, world: Path, seed: int, name: str, timeout: float = 30.0):
        self.world, self.seed, self.name, self.timeout = Path(world), seed, name, timeout
        self.process: asyncio.subprocess.Process | None = None

    async def start(self) -> dict:
        if not BINARY.exists():
            raise EngineError(f"no engine at {BINARY}; run: dotnet publish src/Og.Cli -c Release -o build/og")
        self.process = await asyncio.create_subprocess_exec(
            str(BINARY), "--protocol", "jsonl", "--seed", str(self.seed), "--name", self.name, str(self.world),
            stdin=asyncio.subprocess.PIPE, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE)
        return await self._read()

    async def send(self, command: str) -> dict:
        # One command is one line: a newline inside it would be two commands.
        line = " ".join(command.split())
        self.process.stdin.write((line + "\n").encode())
        await self.process.stdin.drain()
        return await self._read()

    async def _read(self) -> dict:
        try:
            raw = await asyncio.wait_for(self.process.stdout.readline(), self.timeout)
        except asyncio.TimeoutError:
            raise EngineError("the engine did not answer")
        if not raw:
            err = (await self.process.stderr.read()).decode()[-500:]
            raise EngineError(f"the engine exited: {err}")
        reply = json.loads(raw)
        if reply.get("type") == "error":
            raise EngineError(f"{reply.get('message')}: {reply.get('issues')}")
        return reply

    async def close(self):
        if self.process and self.process.returncode is None:
            self.process.stdin.close()
            try:
                await asyncio.wait_for(self.process.wait(), 5)
            except asyncio.TimeoutError:
                self.process.kill()
