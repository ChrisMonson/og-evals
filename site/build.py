"""Builds the results page into site/dist/: the page, the engine it plays in the
browser (WebAssembly) and the worlds it loads.

    python3 site/build.py               # everything
    python3 site/build.py --no-engine   # page and worlds only, keeping dist/engine

The worlds come from scenarios/ through the same loader the evals use, so the
repo must be installed (pip install -e .). The engine needs .NET 10 with the
wasm-tools workload (dotnet workload install wasm-tools).
"""

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

SITE = Path(__file__).resolve().parent
ROOT = SITE.parent
DIST = SITE / "dist"
sys.path.insert(0, str(ROOT))

# The parameter each scenario varies; every value is built in both wordings.
PARAM = {"dark_cave": "victim", "guild_trials": "time", "last_ferry": "bystander"}


def worlds() -> dict:
    """Writes every world the player can load, and returns what the page needs
    to play them: the labels the scorers read and the text the models were given."""
    from evals.agent import COMMANDS
    from evals.tasks import BRIEFS, STOP
    from scenarios.loader import Scenario

    out = DIST / "worlds"
    shutil.rmtree(out, ignore_errors=True)
    out.mkdir(parents=True)
    meta = {"tasks": {}, "briefs": BRIEFS, "stop": STOP,
            "commands": COMMANDS.replace("type commands with the play tool, one at a time", "type one command at a time")}
    for task, param in PARAM.items():
        scenario = Scenario.load(task)
        values = scenario.values(param)
        meta["tasks"][task] = {"param": param, "values": values, "labels": scenario.labels, "worlds": {}}
        for value in values:
            for variant in scenario.values("variant"):
                name = f"{task}-{value.replace(' ', '-')}-{variant}.json"
                world = scenario.build(**{param: value, "variant": variant})
                (out / name).write_text(json.dumps(world, separators=(",", ":")))
                meta["tasks"][task]["worlds"][f"{value}|{variant}"] = "worlds/" + name
    return meta


def engine() -> None:
    """Publishes the engine to WebAssembly and keeps the runtime files the page loads.
    They go in engine/ rather than _framework/, which some hosts treat as private."""
    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run(["dotnet", "publish", str(SITE / "wasm"), "-c", "Release", "-o", tmp], check=True)
        out = DIST / "engine"
        shutil.rmtree(out, ignore_errors=True)
        out.mkdir(parents=True)
        for f in (Path(tmp) / "wwwroot" / "_framework").iterdir():
            if f.suffix not in (".br", ".gz"):
                shutil.copy2(f, out / f.name)


def page(meta: dict) -> int:
    src = SITE / "src"
    html = (src / "template.html").read_text()
    scripts = "\n".join((src / name).read_text() for name in ("score.js", "page.js", "play.js"))
    html = html.replace("/*SCRIPTS*/", scripts, 1).replace("<!--PLAYER-->\n", (src / "play.html").read_text(), 1)
    html = html.replace("__DATA__", (SITE / "snapshot" / "page-data.json").read_text(), 1)
    html = html.replace("__PLAY__", json.dumps(meta), 1)
    (DIST / "index.html").write_text(html)
    (DIST / ".nojekyll").write_text("")
    return len(html)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--no-engine", action="store_true", help="skip the .NET publish and keep dist/engine as it is")
    args = parser.parse_args()
    DIST.mkdir(exist_ok=True)
    if not args.no_engine:
        engine()
    elif not (DIST / "engine" / "dotnet.js").exists():
        sys.exit("dist/engine is missing; build once without --no-engine")
    size = page(worlds())
    print(f"built {DIST.relative_to(ROOT)}/index.html ({size / 1e6:.2f} MB)")


if __name__ == "__main__":
    main()
