#!/usr/bin/env bash
# Runs the protocol in docs/protocol.md for one model: all three scenarios,
# every condition, at the model's protocol settings.
#
#   bin/protocol.sh <model> check   # capability check: 1 game per condition (44 games)
#   bin/protocol.sh <model> main    # the run: 4 more per condition (176); the check's game makes 5
#   bin/protocol.sh <model> main last_ferry   # only these scenarios, to split a run across sessions
#
# Models: opus-5-5 sonnet-5-5 haiku-4-5 opus-4-6 sonnet-4-6
#         gpt-6.1-sol gpt-6-astra gpt-6-luna gpt-5.6-terra gpt-5.5 gpt-oss-20b
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
usage() { sed -n '5,11p' "$0" | sed 's/^# *//' >&2; exit 2; }
[ $# -ge 2 ] || usage
NAME="$1"; PHASE="$2"; shift 2
TASKS=("${@:-dark_cave guild_trials last_ferry}")
TASKS=(${TASKS[*]})
for TASK in "${TASKS[@]}"; do
  case "$TASK" in dark_cave|guild_trials|last_ferry) ;; *) echo "unknown scenario: $TASK" >&2; usage ;; esac
done
PARALLEL=4

case "$NAME" in
  opus-5-5|sonnet-5-5|opus-4-6|sonnet-4-6)
    MODEL=(--model "claude-cli/claude-$NAME" -M effort=medium) ;;
  haiku-4-5)  # takes no effort level; thinking on, as by default
    MODEL=(--model "claude-cli/claude-haiku-4-5") ;;
  gpt-6.1-sol|gpt-6-astra|gpt-6-luna|gpt-5.6-terra|gpt-5.5)
    MODEL=(--model "codex-cli/$NAME" -M effort=medium) ;;
  gpt-oss-20b)  # a local llama.cpp server at $LLAMACPP_BASE_URL, one game at a time
    : "${LLAMACPP_BASE_URL:?set LLAMACPP_BASE_URL to the llama.cpp server, e.g. http://127.0.0.1:8080/v1}"
    MODEL=(--model "openai-api/llamacpp/ggml-org/gpt-oss-20b-GGUF:MXFP4" --reasoning-effort medium)
    PARALLEL=1 ;;
  *) echo "unknown model: $NAME" >&2; usage ;;
esac

case "$PHASE" in
  check) RUN=(--epochs 1 -T seed=99) ;;  # its own seed: the run's games do not repeat the check's dice
  main)  RUN=(--epochs 4 -T seed=7) ;;
  *) echo "phase is check or main" >&2; usage ;;
esac

export LOG_DIR="$ROOT/logs/$NAME/$PHASE"
mkdir -p "$LOG_DIR"
{
  echo "model: $NAME (${MODEL[*]})"
  echo "phase: $PHASE (${RUN[*]}) scenarios: ${TASKS[*]}"
  echo "started: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "commit: $(git -C "$ROOT" describe --tags --always --dirty)"
  echo "claude: $(claude --version 2>/dev/null || echo none)"
  echo "codex: $(codex --version 2>/dev/null || echo none)"
} >> "$LOG_DIR/run.txt"

for TASK in "${TASKS[@]}"; do
  # A failed game is re-run (docs/protocol.md); it must not cancel the games beside it.
  "$ROOT/bin/eval.sh" "$TASK" "${MODEL[@]}" "${RUN[@]}" --max-samples "$PARALLEL" --display plain --no-fail-on-error
done
echo "finished: $(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$LOG_DIR/run.txt"

if [ "$PHASE" = check ]; then
  PYTHONPATH="$ROOT" python3 "$ROOT/bin/reached.py" "$LOG_DIR"
fi
