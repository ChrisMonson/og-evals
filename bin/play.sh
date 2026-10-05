#!/usr/bin/env bash
# Play a scenario yourself, at a terminal, as the models do.
#
#   bin/play.sh <scenario> [key=value ...]
#   bin/play.sh dark_cave victim=child variant=b
#   bin/play.sh guild_trials time=short
#   bin/play.sh last_ferry bystander="old man"
#
# The key=value pairs choose the scenario's parameters. The world is built
# fresh from the scenario's source, so it is the same one the eval plays.
# Type "quit" to stop.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[ $# -ge 1 ] || { sed -n '4,7p' "$0" | sed 's/^# *//' >&2; exit 2; }

[ -x "$ROOT/build/og/Og.Cli" ] || dotnet publish "$ROOT/src/Og.Cli" -c Release -o "$ROOT/build/og" -v q --nologo

cd "$ROOT"
export PYTHONPATH="$ROOT${PYTHONPATH:+:$PYTHONPATH}"
exec python3 -c '
import os, sys

from evals.agent import COMMANDS
from evals.engine import BINARY
from evals.tasks import materialise
from scenarios.loader import Scenario

name, pairs = sys.argv[1], sys.argv[2:]
args = {}
for pair in pairs:
    key, sep, value = pair.partition("=")
    if not sep:
        sys.exit(f"build arguments are key=value, not {pair!r}")
    args[key] = value
world = Scenario.load(name).build(**args)
path, _ = materialise(f"play-{name}", world)

print(" ".join([name, *pairs]) + "\n\n" + COMMANDS + "\n\nType \"quit\" to stop.\n", flush=True)
os.execv(BINARY, [str(BINARY), path])
' "$@"
