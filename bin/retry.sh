#!/usr/bin/env bash
# Re-runs a log's failed games, as `inspect eval-retry` does, with the
# claude-cli and codex-cli models registered.
#
#   bin/retry.sh <log file> [inspect eval-retry options...]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[ $# -ge 1 ] || { sed -n '5p' "$0" | sed 's/^# *//' >&2; exit 2; }
export PYTHONPATH="$ROOT${PYTHONPATH:+:$PYTHONPATH}"
cd "$ROOT"
exec python3 -c 'import evals.claude, evals.codex; from inspect_ai._cli.main import main; main()' eval-retry "$@"
