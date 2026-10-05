#!/usr/bin/env bash
# Runs an eval from this repo, writing logs to logs/ (or $LOG_DIR).
#
#   bin/eval.sh <task> --model <model> [inspect eval options...]
#   bin/eval.sh dark_cave --model claude-cli/claude-sonnet-5-5 -T victim=child
#
# Tasks: dark_cave, guild_trials, last_ferry. See README.md for models.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
usage() { sed -n '4,7p' "$0" | sed 's/^# *//' >&2; exit 2; }
[ $# -ge 1 ] || usage
TASK="$1"; shift
[[ " $* " == *" --model "* || " $* " == *" --model="* ]] || { echo "--model is required" >&2; usage; }

[ -x "$ROOT/build/og/Og.Cli" ] || dotnet publish "$ROOT/src/Og.Cli" -c Release -o "$ROOT/build/og" -v q --nologo

export PYTHONPATH="$ROOT${PYTHONPATH:+:$PYTHONPATH}"

cd "$ROOT"
# Inspect resolves --model before it loads the task file, so the claude-cli and
# codex-cli model APIs are registered first, then Inspect's own command line
# runs (inspect_ai._cli.main is private, so an Inspect upgrade may move it).
exec python3 -c 'import evals.claude, evals.codex; from inspect_ai._cli.main import main; main()' eval "evals/tasks.py@$TASK" --log-dir "${LOG_DIR:-$ROOT/logs}" "$@"
