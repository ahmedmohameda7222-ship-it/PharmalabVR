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
MUTATIONS = [
    (
        "if (driver.IsNativePaused && !driver.ContinueSession()) return false;",
        "if (router.Current.ScreenId == ScreenId.Pause && "
        "driver.IsNativePaused && !driver.ContinueSession()) return false;",
        "Review return failed to resume the paused authority",
        "paused Review return"
    ),
    (
        "case PanelAction.ConfirmSetup:\n                        return boot.ConfirmSetup();",
        "case PanelAction.ConfirmSetup:\n                        boot.ConfirmSetup();\n                        return true;",
        "Unavailable Boot falsely returned success",
        "unavailable Boot scene confirmation"
    ),
    (
        "case PanelAction.Recenter:\n                    return sessions.TryRecenterView();",
        "case PanelAction.Recenter:\n                    sessions.TryRecenterView();\n                    return true;",
        "Failed XR recenter falsely returned success",
        "failed XR recenter false-success"
    ),
    (
        "case PanelAction.Load:\n                    return sessions.LoadSavedSession();",
        "case PanelAction.Load:\n                    sessions.LoadSavedSession();\n                    return true;",
        "Unsupported/missing saved file falsely returned Load success",
        "failed VR Load false-success"
    ),
]


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
    content = original.decode("utf-8")
    for good, bad, expected_diagnosis, label in MUTATIONS:
        if content.count(good) != 1:
            raise RuntimeError("Expected contract guard missing exactly once: " + label)
        try:
            SOURCE.write_text(content.replace(good, bad), encoding="utf-8")
            red = exercise()
            output = red.stdout + red.stderr
            if red.returncode == 0 or expected_diagnosis not in output:
                raise RuntimeError("Mutation not detected for " + label + "; exit=" +
                                   str(red.returncode) + " output=" + output[-3000:])
            print("RED verified: " + label + " (exit=" + str(red.returncode) + ")")
        finally:
            SOURCE.write_bytes(original)
    green = exercise()
    output = green.stdout + green.stderr
    if green.returncode != 0 or re.search(r"headless routing checks: ([1-9][0-9]*) passed", output) is None:
        raise RuntimeError("Restored GREEN failed: exit=" + str(green.returncode) +
                           " output=" + output[-3000:])
    if SOURCE.read_bytes() != original:
        raise RuntimeError("Production source not restored byte-for-byte")
    print("GREEN verified: exact source restored; all headless routing assertions pass")


if __name__ == "__main__":
    main()
