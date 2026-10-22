"""Mutation check: prove the actual C# routing regression test detects an old bypass.

Edits a CI runner worktree temporarily; never commits or updates the remote branch.
Only exercises production routing and pure drop-parcel view policies with test-only adapters, not Unity or Native.
"""
from pathlib import Path
import subprocess
import re

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "unity/Assets/PharmaLabVR/Runtime/UI/LabPanelController.cs"
DROP_SOURCE = ROOT / "unity/Assets/PharmaLabVR/Runtime/Tools/DropParcelState.cs"
PROJECT = ROOT / "tools/panel-contract/PanelContract.csproj"
MUTATIONS = [
    (
        "if (driver.IsNativePaused && !driver.ContinueSession()) return false;",
        "if (router.Current.ScreenId == ScreenId.Pause && "
        "driver.IsNativePaused && !driver.ContinueSession()) return false;",
        # The new ScreenRouter pause admission fails the bypass at the
        # Request(Lab) result before the legacy post-navigation assertion.
        # It still proves the mutated controller cannot fake Native Continue.
        "Recorded Review return refused",
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
    drop_original = DROP_SOURCE.read_bytes()
    good = "volumeM3 <= 0.0 || parcelId == LastLandedParcelId)"
    bad = "volumeM3 <= 0.0 || false)"
    drop_content = drop_original.decode("utf-8")
    if drop_content.count(good) != 1:
        raise RuntimeError("Expected stale-drop suppression guard missing exactly once")
    try:
        DROP_SOURCE.write_text(drop_content.replace(good, bad), encoding="utf-8")
        red = exercise()
        output = red.stdout + red.stderr
        if red.returncode == 0 or "Stale Native snapshot resurrected an already landed drop" not in output:
            raise RuntimeError("Pending parcel replay mutation not detected; exit=" +
                               str(red.returncode) + " output=" + output[-3000:])
        print("RED verified: stale committed drop cannot be resurrected (exit=" +
              str(red.returncode) + ")")
    finally:
        DROP_SOURCE.write_bytes(drop_original)
    # Independently mutate restore-epoch edge detection. Repeated held
    # snapshots must not be mistaken for newly imported sessions.
    good = "reason != lastHoldReason)"
    bad = "true)"
    drop_content = drop_original.decode("utf-8")
    if drop_content.count(good) != 1:
        raise RuntimeError("Missing once-per-restore-epoch guard")
    try:
        DROP_SOURCE.write_text(drop_content.replace(good, bad), encoding="utf-8")
        red = exercise()
        output = red.stdout + red.stderr
        if red.returncode == 0 or "Repeated SessionLoad snapshot erased landed-parcel suppression" not in output:
            raise RuntimeError("Repeated hold mutation not detected; exit=" +
                               str(red.returncode) + " output=" + output[-3000:])
        print("RED verified: repeated Native hold is not a new restore (exit=" +
              str(red.returncode) + ")")
    finally:
        DROP_SOURCE.write_bytes(drop_original)
    green = exercise()
    output = green.stdout + green.stderr
    if green.returncode != 0 or re.search(r"headless routing checks: ([1-9][0-9]*) passed", output) is None:
        raise RuntimeError("Restored GREEN failed: exit=" + str(green.returncode) +
                           " output=" + output[-3000:])
    if SOURCE.read_bytes() != original or DROP_SOURCE.read_bytes() != drop_original:
        raise RuntimeError("Production source not restored byte-for-byte")
    print("GREEN verified: exact source restored; all headless routing assertions pass")


if __name__ == "__main__":
    main()
