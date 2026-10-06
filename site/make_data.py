"""snapshot/page-data.json, the data the page draws, from the export's games.csv.

    PYTHONPATH=. python3 bin/export.py site/snapshot   # from the logs
    python3 site/make_data.py
"""
import csv, json
from pathlib import Path
SNAP = Path(__file__).resolve().parent / "snapshot"
MODELS = [("haiku-4-5","Anthropic","Haiku 4.5"),("sonnet-4-6","Anthropic","Sonnet 4.6"),("opus-4-6","Anthropic","Opus 4.6"),
          ("sonnet-5-5","Anthropic","Sonnet 5.5"),("opus-5-5","Anthropic","Opus 5.5"),
          ("gpt-5.5","OpenAI","GPT-5.5"),("gpt-5.6-terra","OpenAI","GPT-5.6 Terra"),("gpt-6-luna","OpenAI","GPT-6 Luna"),
          ("gpt-6-astra","OpenAI","GPT-6 Astra"),("gpt-6.1-sol","OpenAI","GPT-6.1 Sol"),("gpt-oss-20b","Local","gpt-oss-20b")]
TASKS = {"dark_cave": ["thief","traveller","child","merchant"], "guild_trials": ["ample","tight","short"],
         "last_ferry": ["old man","mother","merchant","child"]}
TI = {t: i for i, t in enumerate(TASKS)}
idx = {k: i for i, (k, _, _) in enumerate(MODELS)}
games = []
for r in csv.DictReader(open(SNAP / "games.csv")):
    games.append({"m": idx[r["model"]], "t": TI[r["task"]], "c": TASKS[r["task"]].index(r["condition"]),
                  "b": 0 if r["brief"] == "open" else 1, "v": 0 if r["variant"] == "a" else 1, "rc": int(r["reached"] == "True"),
                  "o": r["outcome"], "r": r["rung"], "f": r["flags"], "mo": int(r["raised_morality"] == "True"),
                  "ow": int(r["raised_morality_own_words"] == "True"), "su": int(r["named_setup"] == "True"), "d": r["debrief"]})
counts = {}
for g in games: counts[(g["m"], g["t"])] = counts.get((g["m"], g["t"]), 0) + 1
full = {0: 80, 1: 60, 2: 80}
models = []
for i, (k, lab, label) in enumerate(MODELS):
    have = [counts.get((i, t), 0) for t in range(3)]
    status = "done" if all(have[t] >= full[t] for t in range(3)) else ("partial" if any(have) else "pending")
    models.append({"key": k, "lab": lab, "label": label, "status": status})
json.dump({"models": models, "tasks": [{"key": t, "values": v} for t, v in TASKS.items()], "games": games},
          open(SNAP / "page-data.json", "w"), separators=(",", ":"))
print(len(games), "games;", [m["label"] + ":" + m["status"] for m in models if m["status"] != "pending"])
