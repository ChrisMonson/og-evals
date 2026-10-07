"""Builds the results page into site/dist/: the page, the engine it plays in the
browser (WebAssembly) and the worlds it loads.

    python3 site/build.py               # everything
    python3 site/build.py --no-engine   # page and worlds only, keeping dist/engine

The worlds come from scenarios/ through the same loader the evals use, so the
repo must be installed (pip install -e .). The engine needs .NET 10 with the
wasm-tools workload (dotnet workload install wasm-tools).
"""

import argparse
import html
import json
import re
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


def quotes(data: dict) -> list:
    """The hero's quotes, each checked to be word for word in that model's
    answer after a game where it made the choice the quote is shown with."""
    chosen = json.loads((SITE / "src" / "quotes.json").read_text())
    models = [m["label"] for m in data["models"]]
    tasks = [t["key"] for t in data["tasks"]]
    for q in chosen:
        if not any(models[g["m"]] == q["model"] and tasks[g["t"]] == q["task"] and g["o"] == q["outcome"]
                   and g["b"] == (1 if q["brief"] == "escape" else 0) and q["text"] in g["d"] for g in data["games"]):
            sys.exit(f"quote not found in the data as stated: {q['model']}: {q['text'][:60]}")
    return chosen


def page(meta: dict) -> int:
    src = SITE / "src"
    data = (SITE / "snapshot" / "page-data.json").read_text()
    html = (src / "template.html").read_text()
    scripts = "\n".join((src / name).read_text() for name in ("score.js", "page.js", "play.js", "ascii.js"))
    html = html.replace("/*SCRIPTS*/", scripts, 1).replace("<!--PLAYER-->\n", (src / "play.html").read_text(), 1)
    html = html.replace("__QUOTES__", json.dumps(quotes(json.loads(data)), ensure_ascii=False), 1)
    html = html.replace("__DATA__", data, 1)
    html = html.replace("__PLAY__", json.dumps(meta), 1)
    (DIST / "index.html").write_text(html)
    (DIST / ".nojekyll").write_text("")
    return len(html)


def markdown(md: str) -> str:
    """Just enough Markdown for the essay: headings, paragraphs, numbered lists, bold, links."""
    def inline(text: str) -> str:
        text = html.escape(text, quote=False).replace("\\#", "#")
        text = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)
        return re.sub(r"\[([^\]]+)\]\(([^)\s]+)\)", r'<a href="\2">\1</a>', text)
    out, items = [], []
    for block in re.split(r"\n\s*\n", md.strip()):
        lines = [l.strip() for l in block.splitlines() if l.strip()]
        if not lines:
            continue
        if re.match(r"\d+\.\s", lines[0]):
            items += [inline(re.sub(r"^\d+\.\s+", "", l)) for l in lines if re.match(r"\d+\.\s", l)]
            continue
        if items:
            out.append("<ol>" + "".join(f"<li>{i}</li>" for i in items) + "</ol>"); items = []
        if lines[0].startswith("# "):
            out.append(f"<h2>{inline(lines[0][2:])}</h2>")
            lines = lines[1:]
        if lines:
            out.append(f"<p>{inline(' '.join(lines))}</p>")
    if items:
        out.append("<ol>" + "".join(f"<li>{i}</li>" for i in items) + "</ol>")
    return "\n".join(out)


def essay() -> None:
    """The making-of essay, as its own page in the results page's styles."""
    md = (SITE / "src" / "making-of.md").read_text()
    title = md.splitlines()[0].lstrip("# ").strip()
    body = markdown("\n".join(md.splitlines()[1:]))
    template = (SITE / "src" / "template.html").read_text()
    head = template[:template.index("</style>")]
    head = re.sub(r"<title>.*?</title>", f"<title>{html.escape(title)}</title>", head, count=1)
    head += """
.essay h2 { font-size: 1.35rem; margin-top: 1.6rem; }
.essay ol { margin: 0; padding-left: 1.4rem; display: flex; flex-direction: column; gap: 0.5rem; }
.essay .byline { font: 400 0.82rem var(--mono); color: var(--muted); }
.essay .back { font: 400 0.78rem var(--mono); color: var(--muted); text-decoration: none; }
.essay .back:hover { color: var(--fg); }
</style>"""
    page_html = f"""{head}
<main class="page essay">
  <div class="stack">
    <div class="topline"><div class="eyebrow">og-evals · making of</div><a class="back" href="./">← Back to the results</a></div>
    <h1>{html.escape(title)}</h1>
    <p class="byline">Chris Monson · October 2026</p>
{body}
  </div>
  <footer><p><a class="back" href="./">← Back to the results</a></p></footer>
</main>
"""
    (DIST / "making-of.html").write_text(page_html)


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
    essay()
    print(f"built {DIST.relative_to(ROOT)}/index.html ({size / 1e6:.2f} MB)")


if __name__ == "__main__":
    main()
