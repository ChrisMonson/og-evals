"""Every protocol game as one row, and the summary the results page reads.

    PYTHONPATH=. python3 bin/export.py [out_dir]     # default: results/

Writes games.csv (one row per game that played, check and main together) and
summary.json (per model: each scenario's measures by condition and brief, with
counts, so a page can draw them and show intervals). Errored games are left out
and counted; superseded logs are not read, being outside logs/.
"""

import csv
import json
import sys
from collections import defaultdict
from pathlib import Path

from inspect_ai.log import read_eval_log

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "bin"))
from reached import REACHED  # noqa: E402
from results import SCENARIOS, primary  # noqa: E402

# Lab and generation, oldest first within each lab.
MODELS = {
    "haiku-4-5": ("Anthropic", "Haiku 4.5", 1), "sonnet-4-6": ("Anthropic", "Sonnet 4.6", 2),
    "opus-4-6": ("Anthropic", "Opus 4.6", 3), "sonnet-5-5": ("Anthropic", "Sonnet 5.5", 4),
    "opus-5-5": ("Anthropic", "Opus 5.5", 5),
    "gpt-5.5": ("OpenAI", "GPT-5.5", 1), "gpt-5.6-terra": ("OpenAI", "GPT-5.6 Terra", 2),
    "gpt-6-luna": ("OpenAI", "GPT-6 Luna", 3), "gpt-6-astra": ("OpenAI", "GPT-6 Astra", 4),
    "gpt-6.1-sol": ("OpenAI", "GPT-6.1 Sol", 5),
    "gpt-oss-20b": ("Local", "gpt-oss-20b", 1),
}
LADDER = ["none", "asked", "bargained", "stole", "threatened", "attacked"]


def text(parts: list[dict]) -> str:
    return " ".join(p["text"] for p in parts or [] if "text" in p).strip()


def rows_for(model: str):
    for phase in ("check", "main"):
        for path in sorted((ROOT / "logs" / model / phase).glob("*.eval")):
            log = read_eval_log(str(path))
            task = log.eval.task
            parameter = SCENARIOS[task][0]
            for s in log.samples or []:
                if s.error:
                    yield {"model": model, "task": task, "errored": True}
                    continue
                c = s.scores["choices"].value
                m = s.scores["morality"].value
                cli = s.store.get("cli") or {}
                yield {
                    "model": model, "task": task, "errored": False, "phase": phase, "id": s.id, "epoch": s.epoch,
                    "condition": s.metadata[parameter], "brief": s.metadata["brief_name"],
                    "variant": s.metadata["variant"], "reached": bool(REACHED[task](c)),
                    "outcome": primary(task, c), "rung": LADDER[int(c.get("highest_rung", 0))] if task == "dark_cave" else "",
                    # Every flag, outcome and ending the game set, for filtering by any of them.
                    "flags": ";".join(sorted(k for k, v in c.items() if k != "highest_rung" and v and v >= 1)),
                    "ending": s.store.get("ended"),
                    "raised_morality": bool(m.get("raised_morality")),
                    "raised_morality_own_words": bool(m.get("raised_morality_own_words")),
                    "named_setup": bool(s.scores["awareness"].value.get("named_setup_own_words")),
                    "batched": int(s.scores["friction"].value.get("batched", 0)),
                    "commands": len(s.store.get("turns", [])) - 1,
                    "reasoning_tokens": sum((t.get("reasoning") or 0) for t in cli.get("tokens", [])) if cli.get("tokens") else "",
                    "debrief": text((s.store.get("debrief") or {}).get("answer")),
                }


def main(out: Path):
    out.mkdir(parents=True, exist_ok=True)
    games, errored = [], defaultdict(int)
    for model in MODELS:
        if not (ROOT / "logs" / model).exists():
            continue
        for row in rows_for(model):
            if row["errored"]:
                errored[(model, row["task"])] += 1
            else:
                games.append(row)

    fields = [k for k in games[0] if k != "errored"]
    with open(out / "games.csv", "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        w.writerows(games)

    summary = {"models": [], "scenarios": {t: {"parameter": p, "values": v} for t, (p, v) in SCENARIOS.items()}}
    for model, (lab, label, order) in MODELS.items():
        mine = [g for g in games if g["model"] == model]
        if not mine:
            continue
        entry = {"key": model, "lab": lab, "label": label, "order": order, "tasks": {}}
        for task in SCENARIOS:
            these = [g for g in mine if g["task"] == task]
            if not these:
                continue
            reached = [g for g in these if g["reached"]]
            cells = defaultdict(lambda: defaultdict(int))
            for g in reached:
                for key in ((g["condition"], g["brief"]), (g["condition"], "both"), ("all", g["brief"]), ("all", "both")):
                    cell = cells["|".join(key)]
                    cell["n"] += 1
                    cell[g["outcome"]] += 1
                    if task == "dark_cave":
                        cell["rung:" + g["rung"]] += 1
                    cell["moral"] += g["raised_morality"]
                    cell["moral_own"] += g["raised_morality_own_words"]
            entry["tasks"][task] = {"games": len(these), "reached": len(reached),
                                    "errored": errored[(model, task)], "complete": len(these) == 80 if task != "guild_trials" else len(these) == 60,
                                    "cells": {k: dict(v) for k, v in cells.items()}}
        summary["models"].append(entry)
    (out / "summary.json").write_text(json.dumps(summary, indent=1))
    print(f"{len(games)} games from {len(summary['models'])} models -> {out}/games.csv, summary.json")


if __name__ == "__main__":
    main(Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "results")
