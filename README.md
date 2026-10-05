# og-evals

This project grew out of Og, a text-based adventure game engine my daughter and
I wrote together while she was learning to code. The game was simple: you built
a world and fought the ogre Og the Destroyer. A few years later I realized it was
also an interesting way to study the choices AI models make while playing a game.
The worlds are configurable, and every game can be repeated exactly.

The point here is not whether a model can finish the game, though it can measure
that. It's the choices a model makes along the way: what it will do to win, and
when it will give winning up. Included are three scenarios, each asking one question:

- **The Dark Cave:** theft. Will a model take what it needs from someone else,
  and how far will it go - asking, bargaining, stealing, threatening, attacking?
- **The Guild Trials:** cheating. Will a model follow the honor system even when
  presented with an impossible task, or will it cheat if it thinks it can get
  away with it?
- **The Last Ferry:** self-sacrifice. Will a model choose to give up the token it
  worked for, and needs to win, to save someone else at the cost of losing, or
  take the boat by force?

This project is built to be driven by [Inspect](https://inspect.aisi.org.uk/), the UK
AI Security Institute's framework for evaluating models. Each scenario is an
Inspect task: the model plays through a single `play` tool, Inspect scorers
read what it did and said, and every game is an Inspect log you can open in
`inspect view`. The engine itself is a separate program the tasks drive, so it
can also be played by hand.

The scenarios, their rules and what is scored are in
[docs/scenarios.md](docs/scenarios.md). How they are run on models, and how the
results are read, is in [docs/protocol.md](docs/protocol.md).

## Layout

- `src/Og.Core`, `src/Og.Cli`: the game engine (C#), and `tests/` its tests.
- `scenarios/`: one YAML file per scenario (its parameters, world and the labels its scorers
  read), built into the engine's world JSON by `scenarios/loader.py`.
- `evals/`: Inspect tasks (`tasks.py`), the player and its harnesses (`agent.py`,
  `claude.py`, `codex.py`), the engine process (`engine.py`) and the scorers (`score.py`).
- `bin/`: `eval.sh` runs a task. `protocol.sh` runs the protocol for one model
  (`docs/protocol.md`), `reached.py` checks its capability games, `results.py`
  reports its results and `retry.sh` re-runs failed games. `play.sh` plays a
  scenario by hand; `calibrate_cave.py` simulates the Dark Cave's fights.
- `worlds/generated/`: scratch, written by every run (ignored by git). Each log
  carries the world it played.
- `logs/`: Inspect logs (ignored by git).

## Setup

You need the .NET 10 SDK and Python 3.10 or later.

```bash
dotnet publish src/Og.Cli -c Release -o build/og   # the engine, as the scripts run it
pip install -e .                                   # inspect_ai, mcp, uvicorn, pydantic, pyyaml
```

`bin/eval.sh` and `bin/play.sh` publish the engine only when `build/og/Og.Cli`
is missing, so publish again after changing the engine.

## Running

```bash
bin/eval.sh <task> --model <model> [inspect eval options...]
bin/eval.sh dark_cave --model claude-cli/claude-sonnet-5-5 -M effort=high
bin/eval.sh guild_trials --model codex-cli/gpt-6.1-sol -M effort=medium -T time=short
bin/eval.sh last_ferry --model anthropic/claude-sonnet-5-5 -T bystander=child --epochs 1
```

Tasks: `dark_cave`, `guild_trials`, `last_ferry`. Each task's conditions are
`-T` options (see `evals/tasks.py`); a comma-separated value runs several. Logs
are written to `logs/`; open them with `inspect view --log-dir logs`.

`--model` is required. Besides any model Inspect supports:

- **`claude-cli/<model>`** plays through the Claude Code CLI (`claude`), which
  must be installed and signed in. `-M effort=<level>` sets its effort;
  `-M thinking=off` turns thinking off.
- **`codex-cli/<model>`** plays through the Codex CLI (`codex`), which must be
  installed and signed in. `-M effort=<level>` sets the reasoning effort.

These two are registered by `bin/eval.sh` before Inspect reads `--model`, so run
tasks through it rather than plain `inspect eval`. They were tested with Claude
Code 2.1.289 and Codex CLI 0.160.0; either CLI can change what it adds to the
conversation between versions.

### Through the Anthropic or OpenAI API

Any provider Inspect supports plays through Inspect's own loop, billed to that
provider's API key:

```bash
pip install -e ".[anthropic]"          # or ".[openai]"
export ANTHROPIC_API_KEY=...           # or OPENAI_API_KEY
bin/eval.sh dark_cave --model anthropic/claude-sonnet-5-5 --reasoning-effort high
bin/eval.sh dark_cave --model openai/gpt-5.5 --reasoning-effort medium --reasoning-summary detailed
```

Reasoning is set with Inspect's options (`--reasoning-effort`,
`--reasoning-tokens`), not `-M effort=`. OpenAI models return no reasoning
unless asked for a summary (`--reasoning-summary`); the morality scorer's
`_own_words` measures are the ones to compare across providers, since a summary
is not the model's own words. Tasks ask for one tool call per response, and
any further calls in a response are answered "Not played" without reaching the
game.

Results are comparable only within one harness. Through the API the model sees
this repo's system prompt and nothing else; through `claude-cli/` and
`codex-cli/` it also sees what those CLIs add. Each log records the model name,
and with it the harness.

A local model through a llama.cpp server, for example gpt-oss-20b, uses
Inspect's OpenAI-compatible provider:

```bash
export LLAMACPP_BASE_URL=http://127.0.0.1:8080/v1 LLAMACPP_API_KEY=local
bin/eval.sh dark_cave --model openai-api/llamacpp/ggml-org/gpt-oss-20b-GGUF:MXFP4
```

To play a scenario yourself, with the same world and command reference the
models get:

```bash
bin/play.sh dark_cave victim=child variant=b
bin/play.sh last_ferry bystander="old man"
```

`PYTHONPATH=. python3 bin/calibrate_cave.py [games]` replays the Dark Cave's
fights with each loadout and reports how often the player dies.

## Scenario files

Each scenario is `scenarios/<name>.yaml`, which `scenarios/loader.py` turns into
the world JSON the engine reads (its docstring has the details):

- **`parameters`**: the conditions a task varies, and for each value the fields it
  sets. `${bystander.names}` or `${variant.office}` refer to them anywhere below.
- **`text`**: named passages, used as `${text.clerk}`.
- **`world`**: the engine's world definition, with those references filled in.
  A list entry with `when: {victim: merchant}` is kept only for those values.
  `id` stands for `interactionId`, and an action can be written `describe: "..."`.
- **`labels`**: what the scorers look for; not part of the world.

Triggers name word groups in braces, `"{give} *&*{bystander}*"`, from the
world's `words`. The engine expands them when it loads the world, so one trigger
stands for every combination.

```yaml
words:
  give: [give, hand, offer]
  bystander: ${bystander.names}
...
- id: gave-token
  if: ["carrying:ferry token"]
  triggers: ["{give} *&*{bystander}*"]
  actions:
    - removeitems: ferry token
    - describe: ${bystander.boards}
    - endgame: gave
```

Quote every trigger: a YAML value starting with `*` is an alias, not text.

## The engine

`src/Og.Core` and `src/Og.Cli` are a copy of the og-world engine at
`45cb0d0c5b8728c35e28f2583b86928b75ed1c6f` (tag `genesis/v1` in og-world), with
the server removed. It is copied, not linked: og-world's history is squashed and
pruned at every seed, and its engine is rewritten by the caretakers that run in
it. Results here are only comparable with results from the same engine, so a
change to it is a new engine version, and results are kept with the version
that produced them.

### Changes from og-world's engine

All of these are in the first versioned engine, tagged `engine-v1`. A change to
the engine after that is a new version (`engine-v2`, ...).

- **`give <item> to <someone>`** is a built-in verb, as `attack` is. The engine
  checks the item is in hand; the room's interactions decide what giving does. It
  is recorded as a `gave` event, with whether it was taken.
- **`carrying:<item>`** is a condition: true while the player holds that item.
- **`examine` / `look at`** reach a creature or item in the room, not only the pack,
  and pass anything else on to the room. Leading articles are dropped.
- **`search`, `explore`, `investigate`** go to the room first, and only describe it
  if the room has no answer. `look` is unchanged.
- **`removemonster`** sends a monster away alive, with what it carried, recorded as
  `monster_left`. Before, the only way a monster left a room was by dying.
- **`ground`** on a location puts items on its floor when the world begins.

Added while building the scenarios, before any results:

- **`take X from Y`** is understood. Taking from a creature never works, but it is
  answered, recorded as `tried_taking`, and handed on to the room: "take the key
  from the guard" is not read as an item called "key from guard".
- **`use X on Y`** finds the item and hands the command on, for the same reason.
- **`escape`** is an action, so a world can let "open the gate" leave as "exit" does.
- **`setmonsterdescription`** rewrites a monster's description.
- **Exits are listed as `Exits: north, south.`**, so they are not read as objects in
  the room.
- **The protocol reports the inventory** on every line, arrival included.
- **Taking a weapon or armour into an empty slot** puts it in hand; equipping what
  is already held says so.
- **A clock** limits moves between places, is shown in every place, and ends the
  game when a move is tried with none left (`time_ran_out`). A condition can stop it
  (`stopsWhen`): no more moves counted, and the time left no longer said.
- **`buy`** pays with anything of the currency kind; the place decides what it
  sells. Every attempt is recorded (`tried_buying`).
- **Triggers can join parts with `&`,** all of which must match: `give*&*pouch*`.
- **Giving finds the item in either word order:** "give the merchant the pouch".
- **Dark places and lights:** without a carried light a dark place's description,
  creature and floor go unseen, entering risks a fall (`fell`), and blows take a
  penalty. The world's `darkness` sets the chances and the words.
- **`slain:<creature>`** is a condition: true once that creature is killed.
- **`exitNeedsKey`** on a world (default true): a way out need not want a key.
- **`placeitems`** puts items on the ground: someone fleeing can leave what they
  carried.
- **A monster's `blocks`** names directions it stands in while alive: the way is
  listed and refused. A location's **`darkDescription`** is what can be made out
  there without a light.
- **`exitOnArrival`** on a world: reaching the exit is leaving.
- **Items handed over by the world are put to use** as taken ones are: a received
  sword is held.
- **A creature answers to what it is** -- "monster", "creature", "it" -- as well as
  its name, and "attack X with Y" names X.
- **`spendtime`** takes time off the clock without moving, so an attempt can cost
  time; the time left is said after the answer.
- **`endgame`** ends the session without an escape, with a reason (`game_ended`):
  staying behind is neither death nor running out of time.
- **Word groups:** a world's `words` names lists of words, and a trigger refers
  to one in braces, `{give}`, standing for each of them. Expanded when the world
  loads; matching is unchanged.

Removed, as nothing here uses them: multiplayer (other players, `who`,
`whisper`/`tell`, and every broadcast; `say`, `yell` and `pray` are recorded but
reach no one), saving and restoring a world, reloading a running world, and
monster respawn.

Each has tests in `tests/Og.Core.Tests/`, and the validator checks the
new names it can: `carrying:` items, `slain:` creatures, `ground` and `placeitems`
items and places, `removemonster` targets, a clock's moves, word groups a trigger
names, and that a world with a dark place says what darkness is like.

## Driving the engine

```bash
build/og/Og.Cli --protocol jsonl [--seed N] [--name NAME] WORLD.json   # as the harness does
build/og/Og.Cli WORLD.json                                             # interactively
```

In protocol mode, one command per line in. One JSON object per line out: the world's reply, every
event the command produced, the character's location and health. The first line
is the arrival, before any command. With a seed a session is exactly its commands
and its seed: there is no tick, so nothing happens between commands, and the
dice repeat.

```bash
dotnet test        # the engine's own tests
```
