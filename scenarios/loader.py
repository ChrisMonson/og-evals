"""Builds a world from a scenario file.

    scenario = Scenario.load("last_ferry")
    world = scenario.build(bystander="child", variant="a")
    scenario.labels, scenario.parameters

A scenario file (scenarios/<name>.yaml) has four parts:

    parameters  each parameter's values, and for each value the fields it sets:
                ${bystander.names}, ${variant.office}. ${bystander} alone is
                the value's own name.
    text        named passages, used as ${text.clerk}.
    world       the world definition the engine reads, with ${...} filled in.
    labels      what the scorers look for; not part of the world.

A string that is only a reference, "${bystander.names}", takes the referenced
value whole, so a list stays a list. A reference inside a longer string is
written into it. Keys are filled in too: {"${variant.office}": 2}.

A list entry with `when: {parameter: value or [values]}` is kept only for those
values, chosen or default. Two shorthands make worlds shorter to write: `id`
for interactionId, and an action written as `{describe: "..."}` for
{"action": "describe", "parameters": ["..."]}. Word groups ("words", "{give}")
are left for the engine.
"""

import re
from pathlib import Path

import yaml

HERE = Path(__file__).resolve().parent
REFERENCE = re.compile(r"\$\{([^}]+)\}")


class Scenario:
    def __init__(self, name: str, source: dict):
        self.name = name
        self.parameters: dict[str, dict] = source.get("parameters") or {}
        self.text: dict[str, str] = source.get("text") or {}
        self.world: dict = source["world"]
        self.labels: dict = source.get("labels") or {}

    @classmethod
    def load(cls, name: str) -> "Scenario":
        return cls(name, yaml.safe_load((HERE / f"{name}.yaml").read_text()))

    def values(self, parameter: str) -> list[str]:
        return list(self.parameters[parameter])

    def build(self, **chosen: str) -> dict:
        unknown = set(chosen) - set(self.parameters)
        if unknown:
            raise ValueError(f"{self.name} has no parameter {sorted(unknown)}; it has {sorted(self.parameters)}")
        scope: dict = {"text": self.text}
        for parameter, values in self.parameters.items():
            value = chosen.get(parameter, next(iter(values)))
            if value not in values:
                raise ValueError(f"{parameter} must be one of {list(values)}, not {value!r}")
            scope[parameter] = {"": value, **(values[value] or {})}
        # `when` sees every parameter's value, defaults included.
        return _resolve(self.world, scope, {parameter: scope[parameter][""] for parameter in self.parameters})


def _lookup(path: str, scope: dict):
    value = scope
    for part in path.split("."):
        if not isinstance(value, dict) or part not in value:
            raise KeyError(f"${{{path}}} names nothing")
        value = value[part]
    # ${bystander} alone is the chosen value's name.
    return value[""] if isinstance(value, dict) and "" in value else value


def _fill(text: str, scope: dict):
    whole = REFERENCE.fullmatch(text)
    if whole:
        value = _lookup(whole.group(1), scope)
        return _fill(value, scope) if isinstance(value, str) else value
    # Passages can refer to other passages.
    return REFERENCE.sub(lambda m: _fill(str(_lookup(m.group(1), scope)), scope), text)


def _wanted(entry, chosen: dict) -> bool:
    if not (isinstance(entry, dict) and "when" in entry):
        return True
    for parameter, allowed in entry["when"].items():
        allowed = allowed if isinstance(allowed, list) else [allowed]
        if chosen.get(parameter) not in allowed:
            return False
    return True


def _action(entry: dict) -> dict:
    if "action" in entry or len(entry) != 1:
        return entry
    (name, parameters), = entry.items()
    parameters = parameters if isinstance(parameters, list) else [parameters]
    return {"action": name, "parameters": [str(p) for p in parameters]}


def _resolve(node, scope: dict, chosen: dict, key: str | None = None):
    if isinstance(node, str):
        return _fill(node, scope)
    if isinstance(node, list):
        out = [_resolve(entry, scope, chosen) for entry in node if _wanted(entry, chosen)]
        if key == "actions":
            out = [_action(entry) for entry in out]
        return out
    if isinstance(node, dict):
        out = {}
        for k, v in node.items():
            if k == "when":
                continue
            k = "interactionId" if k == "id" else _fill(str(k), scope)
            out[k] = _resolve(v, scope, chosen, k)
        return out
    return node
