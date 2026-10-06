import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


DOCTOR_PATH = Path(__file__).parents[1] / "doctor.py"


def load_doctor():
    spec = importlib.util.spec_from_file_location("plv_doctor", DOCTOR_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


class DoctorTests(unittest.TestCase):
    def test_C00_missing_required_native_tool_fails(self):
        doctor = load_doctor()
        checks = [
            doctor.ToolCheck("git", True, "C:/git.exe", "2.0", True),
            doctor.ToolCheck("python", True, "C:/python.exe", "3.12", True),
            doctor.ToolCheck("cmake", False, None, None, True),
            doctor.ToolCheck("cxx", True, "C:/cl.exe", "19", True),
            doctor.ToolCheck("unity", False, None, None, True),
        ]
        readiness = doctor.evaluate_readiness(checks, allow_missing_unity=True)
        self.assertFalse(readiness.ready)
        self.assertIn("cmake", readiness.missing_required)
        self.assertNotIn("unity", readiness.missing_required)

    def test_C00_allow_missing_unity_is_explicit_not_a_false_detection(self):
        doctor = load_doctor()
        checks = [
            doctor.ToolCheck("git", True, "C:/git.exe", "2.0", True),
            doctor.ToolCheck("python", True, "C:/python.exe", "3.12", True),
            doctor.ToolCheck("cmake", True, "C:/cmake.exe", "3.30", True),
            doctor.ToolCheck("cxx", True, "C:/cl.exe", "19", True),
            doctor.ToolCheck("unity", False, None, None, True),
        ]
        readiness = doctor.evaluate_readiness(checks, allow_missing_unity=True)
        self.assertTrue(readiness.ready)
        self.assertEqual(["unity"], readiness.allowed_missing)

    def test_C00_report_records_real_status_and_scope(self):
        doctor = load_doctor()
        checks = [doctor.ToolCheck("git", True, "C:/git.exe", "2.0", True)]
        report = doctor.build_report(checks, doctor.evaluate_readiness(checks, False))
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "environment.json"
            doctor.write_report(report, target)
            decoded = json.loads(target.read_text(encoding="utf-8"))
        self.assertEqual(1, decoded["schemaVersion"])
        self.assertEqual("environment-discovery", decoded["scope"])
        self.assertTrue(decoded["tools"][0]["available"])


if __name__ == "__main__":
    unittest.main()
