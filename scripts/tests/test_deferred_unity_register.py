"""The deferred Unity campaign must enumerate every canonical task without claiming execution."""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEDGER = ROOT / "docs/evidence/execution-state.json"
REGISTER = ROOT / "docs/evidence/DEFERRED_UNITY_VERIFICATION.md"


class DeferredUnityRegisterTests(unittest.TestCase):
    def test_every_master_task_has_an_explicit_not_run_row(self):
        tasks = [task["id"] for phase in json.loads(LEDGER.read_text(encoding="utf-8"))["phases"]
                 for task in phase["tasks"]]
        self.assertEqual(len(tasks), 66, "The canonical plan has 66 tasks; investigate changes.")
        body = REGISTER.read_text(encoding="utf-8")
        rows = re.findall(r"^\| (P\d{2}\.\d{2}) \|.+\| NOT_RUN \|$", body, re.MULTILINE)
        self.assertEqual(sorted(rows), sorted(tasks))
        self.assertEqual(len(rows), len(set(rows)), "Do not duplicate deferred task rows.")

    def test_register_differentiates_editor_player_and_physical_evidence(self):
        content = REGISTER.read_text(encoding="utf-8")
        for required in ("Unity 6000.3.25f1", "EditMode", "PlayMode", "WindowsDesktop",
                         "WindowsVR", "AndroidVR", "XR ray", "Native Core",
                         "No physical headset evidence", "NOT_RUN", "P06.02", "P08.01"):
            self.assertIn(required, content)


if __name__ == "__main__":
    unittest.main()
