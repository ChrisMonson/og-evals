"""How dangerous the Dark Cave is with each loadout.

    PYTHONPATH=. python3 bin/calibrate_cave.py [games]

Plays each loadout straight through: buy or take what it holds, walk in, fight
the beast to the end, walk out. No retreat -- the risk of committing, which is
what the merchant's warning is about. Reports deaths and how they happened.
"""

import asyncio
import sys
from collections import Counter

from evals.agent import Game
from evals.tasks import materialise
from scenarios.loader import Scenario

LOADOUTS = {"torch and sword": ["buy torch", "steal the pouch", "buy sword"],
            "torch only": ["buy torch"],
            "sword only": ["buy sword"],
            "neither": []}


async def play(path: str, prep: list[str], seed: int) -> str:
    g = Game({"world": path, "seed": 7, "brief": "", "stop": {"until_finished": True, "patience": 99,
                                                             "max_commands": 200}}, seed)
    await g.start()
    try:
        for command in prep + ["north", "north"]:
            await g.play(command)
            if g.over():
                return "died walking in"
        for _ in range(60):
            await g.play("attack the beast")
            if g.over():
                return "died fighting"
            if any(e["type"] == "monster_slain" for t in g.turns for e in t["events"]):
                break
        for command in ["north", "north", "exit"]:
            await g.play(command)
            if g.over() == "died":
                return "died walking out"
        return "out" if g.progress.finished else "stuck"
    finally:
        await g.close()


async def main(games: int):
    world = Scenario.load("dark_cave").build(victim="traveller", variant="a")
    beast, darkness = world["monsters"][0], world["darkness"]
    stats = {k: beast[k] for k in ("hitPoints", "armorClass", "attackStrength", "attackDice")}
    print(f"beast {stats}  "
          f"darkness: fall {darkness['fallChance']}% for 1-{darkness['fallDamage']}, attack -{darkness['attackPenalty']}")
    path, _ = materialise("calibrate-cave", world)
    for name, prep in LOADOUTS.items():
        results = Counter([await play(path, prep, seed) for seed in range(1, games + 1)])
        died = sum(n for r, n in results.items() if r.startswith("died"))
        print(f"  {name:<16} died {died / games:5.0%}   {dict(results)}")


if __name__ == "__main__":
    asyncio.run(main(int(sys.argv[1]) if len(sys.argv) > 1 else 200))
