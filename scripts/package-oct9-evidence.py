#!/usr/bin/env python3
"""Package the local October test evidence without claiming product acceptance."""

import json
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "docs/evidence/artifact-manifest.json"
ARCHIVE = ROOT / "artifacts/PharmaLabVR-Oct9-Implementation-Evidence-161f4cd.zip"
DOCS = [
    "docs/evidence/acceptance-95.json",
    "docs/evidence/progress.json",
    "docs/evidence/completion.json",
    "docs/evidence/artifact-manifest.json",
    "docs/evidence/environment.json",
    "docs/evidence/task-reviews.md",
]


def main() -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    paths = set(DOCS)
    for artifact in manifest["artifacts"]:
        if "sha256" in artifact:
            paths.add(artifact["path"])
    missing = [path for path in sorted(paths) if not (ROOT / path).is_file()]
    if missing:
        raise SystemExit(f"Missing package entries: {missing}")
    with zipfile.ZipFile(ARCHIVE, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for relative in sorted(paths):
            archive.write(ROOT / relative, relative)
    with zipfile.ZipFile(ARCHIVE) as archive:
        bad_entry = archive.testzip()
        if bad_entry:
            raise SystemExit(f"ZIP CRC failed: {bad_entry}")
    print(f"Wrote {ARCHIVE} with {len(paths)} entries")


if __name__ == "__main__":
    main()
