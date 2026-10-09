#!/usr/bin/env python3
"""Hash the exact local artifacts cited by the October implementation record."""

import hashlib
import json
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
FILES = [
    "artifacts/WindowsDesktop/PharmaLabVR.exe",
    "artifacts/WindowsVR/PharmaLabVR.exe",
    "artifacts/WindowsVR/PharmaLabVR_Data/Plugins/x86_64/openxr_loader.dll",
    "artifacts/AndroidVR/PharmaLabVR-research.apk",
    "unity/Assets/Plugins/x86_64/pharmalab_core.dll",
    "unity/Assets/Plugins/Android/arm64-v8a/libpharmalab_core.so",
    "unity/Assets/StreamingAssets/science/minteq.v4.dat",
    "artifacts/test-results/native-final-current.txt",
    "artifacts/test-results/ctest-final-current.txt",
    "artifacts/test-results/python-tooling-oct9-current.txt",
    "artifacts/test-results/EditMode-cylinder-fixed-v2.xml",
    "artifacts/test-results/PlayMode-cylinder-fixed.xml",
    "artifacts/logs/unity-WindowsDesktop-10d6dae.log",
    "artifacts/logs/unity-WindowsVR-10d6dae.log",
    "artifacts/logs/unity-AndroidVR-10d6dae.log",
    "artifacts/logs/windows-desktop-10d6dae.log",
    "artifacts/performance/windows-desktop-load-continue-10d6dae.json",
    "artifacts/sessions/autosave-10d6dae.plv.json",
    "artifacts/screenshots/windows-desktop-10d6dae-boot.png",
    "artifacts/screenshots/windows-desktop-10d6dae-lab.png",
    "artifacts/screenshots/windows-desktop-10d6dae-loaded-held.png",
    "artifacts/screenshots/windows-desktop-10d6dae-loaded-continued.png",
]


def digest(path: Path) -> str:
    sha = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            sha.update(chunk)
    return sha.hexdigest().upper()


def tree_record(relative: str) -> dict:
    directory = ROOT / relative
    paths = sorted(path for path in directory.rglob("*") if path.is_file())
    lines = [f"{digest(path).lower()}  {path.relative_to(directory).as_posix()}\n" for path in paths]
    tree_sha = hashlib.sha256("".join(lines).encode("utf-8")).hexdigest().upper()
    return {
        "path": relative,
        "files": len(paths),
        "bytes": sum(path.stat().st_size for path in paths),
        "treeSha256": tree_sha,
    }


def main() -> None:
    missing = [relative for relative in FILES if not (ROOT / relative).is_file()]
    if missing:
        raise SystemExit(f"Missing evidence files: {missing}")
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    records = [tree_record(name) for name in ("artifacts/WindowsDesktop", "artifacts/WindowsVR")]
    records.extend(
        {"path": relative, "bytes": (ROOT / relative).stat().st_size, "sha256": digest(ROOT / relative)}
        for relative in FILES
    )
    record = {
        "schemaVersion": 3,
        "testedCodeCommitSha": revision,
        "hashAlgorithm": "SHA-256",
        "treeDigestAlgorithm": "SHA-256 of UTF-8 sorted lines '<lowercase file sha256>  <directory-relative forward-slash path>\\n'",
        "artifacts": records,
    }
    destination = ROOT / "docs/evidence/artifact-manifest.json"
    destination.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {destination} with {len(records)} entries")


if __name__ == "__main__":
    main()
