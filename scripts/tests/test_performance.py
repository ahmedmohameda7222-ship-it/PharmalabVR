import importlib.util
import pathlib
import unittest


SCRIPT = pathlib.Path(__file__).parents[1] / "analyze-performance.py"
SPEC = importlib.util.spec_from_file_location("performance", SCRIPT)
performance = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(performance)


class PerformanceAnalysisTests(unittest.TestCase):
    def test_K01_missing_player_run_stays_evidence_missing(self):
        result = performance.analyze({"target": "WindowsVR", "actualPlayerRun": False})
        self.assertEqual(result["status"], "EvidenceMissing")

    def test_K02_three_complete_desktop_runs_are_measured(self):
        capture = {
            "target": "WindowsDesktop",
            "actualPlayerRun": True,
            "warmupSeconds": 300,
            "runs": [
                {"durationSeconds": 1800, "frameMs": [10.0, 11.0, 12.0], "recurringComputeHolds": 0, "peakMemoryMiB": 512}
                for _ in range(3)
            ],
        }
        result = performance.analyze(capture)
        self.assertEqual(result["status"], "Passed")
        self.assertEqual(result["sampleCount"], 9)
        self.assertEqual(result["p99FrameMs"], 12.0)

    def test_K03_short_or_incomplete_capture_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "three measured runs"):
            performance.analyze({"target": "AndroidVR", "actualPlayerRun": True, "warmupSeconds": 300, "runs": []})


if __name__ == "__main__":
    unittest.main()
