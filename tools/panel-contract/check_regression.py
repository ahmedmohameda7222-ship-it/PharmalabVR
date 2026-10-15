"""Mutation check: prove the actual C# routing regression test detects an old bypass.

Edits a CI runner worktree temporarily; never commits or updates the remote branch.
Only exercises ScreenRouter / LabPanelController with test-only adapters, not Unity or Native.
"""
from pathlib import Path
import subprocess
import re

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "unity/Assets/PharmaLabVR/Runtime/UI/LabPanelController.cs"
PROJECT = ROOT / "tools/panel-contract/PanelContract.csproj"
GOOD = "if (driver.IsNativePaused && !driver.ContinueSession()) return false;"
BAD = ("if (router.Current.ScreenId == ScreenId.Pause && "
       "driver.IsNativePaused && !driver.ContinueSession()) return false;")


def exercise():
    cleaned = subprocess.run(["dotnet", "clean", str(PROJECT), "-c", "Release"],
                             cwd=ROOT, capture_output=True, text=True)
    if cleaned.returncode:
        raise RuntimeError("headless clean failed: " + cleaned.stdout + cleaned.stderr)
    return subprocess.run(["dotnet", "run", "--project", str(PROJECT),
                           "--configuration", "Release"],
                          cwd=ROOT, capture_output=True, text=True)


def main():
    original = SOURCE.read_bytes()
    text = original.decode("utf-8")
    if text.count(GOOD) != 1:
        raise RuntimeError("Expected native-pause guard not found exactly once")
    try:
        SOURCE.write_text(text.replace(GOOD, BAD), encoding="utf-8")
        red = exercise()
        output = red.stdout + red.stderr
        if red.returncode == 0 or "Review return failed to resume the paused authority" not in output:
            raise RuntimeError("Mutation was not detected: exit=" + str(red.returncode) +
                               " output=" + output[-3000:])
        print("RED verified: the old Review-return bypass failed the headless routing test " +
              "(exit=" + str(red.returncode) + ")")
    finally:
        SOURCE.write_bytes(original)
    green = exercise()
    output = green.stdout + green.stderr
    if green.returncode != 0 or re.search(r"headless routing checks: ([1-9][0-9]*) passed", output) is None:
        raise RuntimeError("Restored GREEN failed: exit=" + str(green.returncode) +
                           " output=" + output[-3000:])
    if SOURCE.read_bytes() != original:
        raise RuntimeError("Production source was not restored byte-for-byte")
    print("GREEN verified: exact committed source restored; all headless routing assertions pass")


if __name__ == "__main__":
    main()
