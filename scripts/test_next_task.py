"""P00_02: continuation must respect real dependencies and release gates."""

import importlib.util
import json
from pathlib import Path
import unittest


script = Path(__file__).with_name("next-task.py")
spec = importlib.util.spec_from_file_location("plv_next_task", script)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ContinuationTests(unittest.TestCase):
    def test_P00_02_current_ledger_and_release_gate(self):
        state = json.loads(Path("docs/evidence/execution-state.json").read_text(encoding="utf-8"))
        tasks = {task["id"]: task for phase in state["phases"] for task in phase["tasks"]}
        for task in tasks.values():
            task["status"] = "Open"
        self.assertEqual(module.select_next(state), "P00.01")
        tasks["P00.01"]["status"] = "VerifiedLocal"
        self.assertEqual(module.select_next(state), "P00.02")
        tasks["P00.02"]["status"] = "VerifiedLocal"
        self.assertEqual(module.select_next(state), "P01.01")
        for task in tasks.values():
            task["status"] = "VerifiedLocal"
        tasks["P18.01"]["status"] = "Open"
        self.assertNotEqual(module.select_next(state), "P18.01")
        state["releaseGates"][0]["status"] = "VerifiedLocal"
        self.assertEqual(module.select_next(state), "P18.01")


if __name__ == "__main__":
    unittest.main()
