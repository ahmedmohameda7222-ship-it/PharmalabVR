#!/usr/bin/env python3
"""Print the first dependency-eligible unfinished premium plan task."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def select_next(state: dict) -> str | None:
    tasks = {task["id"]: task for phase in state["phases"] for task in phase["tasks"]}
    gates = {gate["id"]: gate for gate in state["releaseGates"]}
    for task_id in state["executionOrder"]:
        task = tasks[task_id]
        if task["status"] == "VerifiedLocal":
            continue
        dependencies = task.get("dependencies", [])
        required_gates = task.get("releaseGateDependencies", []) + state.get("gateDependencies", {}).get(task_id, [])
        if all(tasks[dep]["status"] == "VerifiedLocal" for dep in dependencies) and all(
            gates[gate]["status"] == "VerifiedLocal" for gate in required_gates
        ):
            return task_id
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("state", nargs="?", type=Path, default=Path("docs/evidence/execution-state.json"))
    args = parser.parse_args()
    state = json.loads(args.state.read_text(encoding="utf-8"))
    selected = select_next(state)
    print(selected or "NO_ELIGIBLE_TASK")
    return 0 if selected else 1


if __name__ == "__main__":
    raise SystemExit(main())
