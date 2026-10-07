# Results page

The page that presents the protocol results: the findings, the charts, an
explorer over every game, and a player that runs the engine in the browser
(compiled to WebAssembly) on the same worlds the models played. It is published
to GitHub Pages by `.github/workflows/pages.yml` on every push to `main` that
touches it.

- `src/`: the page. `template.html` holds the markup and styles; `score.js`,
  `page.js` and `play.js` are inlined into it, and `play.html` is the player.
  `making-of.md` is the essay published as `making-of.html`.
  `score.js` is a port of the scorers in `evals/score.py` and `bin/reached.py`,
  so a game played on the page is read the way a model's game was.
- `snapshot/`: the data the page draws. `games.csv` and `summary.json` are
  written by `bin/export.py` from the logs (which are not in git), and
  `page-data.json` by `make_data.py` from `games.csv`.
- `wasm/`: the engine wrapper the player calls (`Game.Start`, `Game.Send`).
- `build.py`: writes `dist/` (ignored by git): `index.html`, `engine/` and the
  `worlds/` the player loads, built from `scenarios/` by the evals' own loader.

## Building

Needs the repo installed (`pip install -e .`) and .NET 10 with the wasm-tools
workload (`dotnet workload install wasm-tools`).

```bash
python3 site/build.py                               # everything
python3 site/build.py --no-engine                   # page and worlds only
python3 -m http.server 8000 --directory site/dist   # then open localhost:8000
```

The player loads its files with `fetch`, so open the page through a server, not
as a file.

## Refreshing the data

After new runs:

```bash
PYTHONPATH=. python3 bin/export.py site/snapshot
python3 site/make_data.py
python3 site/build.py --no-engine
```

A model counts as complete on the page once it has every game in all three
scenarios. Models still to run are listed by `make_data.py` and shown as "not
yet run".
