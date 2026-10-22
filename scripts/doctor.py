#!/usr/bin/env python3
"""Discover PharmaLabVR build prerequisites without exposing credentials."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
from typing import NamedTuple


PINNED_UNITY = "6000.3.25f1"


class ToolCheck(NamedTuple):
    name: str
    available: bool
    path: str | None
    version: str | None
    required: bool
    detail: str | None = None


class Readiness(NamedTuple):
    ready: bool
    missing_required: list[str]
    allowed_missing: list[str]


def _first_existing(candidates: list[Path]) -> str | None:
    for candidate in candidates:
        if candidate.is_file():
            return str(candidate.resolve())
    return None


def _command_path(name: str, override: str | None = None) -> str | None:
    if override:
        candidate = Path(override)
        return str(candidate.resolve()) if candidate.is_file() else None
    return shutil.which(name)


def _version(path: str | None, arguments: list[str]) -> str | None:
    if not path:
        return None
    try:
        completed = subprocess.run(
            [path, *arguments],
            capture_output=True,
            text=True,
            timeout=10,
            check=False,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    output = (completed.stdout or completed.stderr).strip().splitlines()
    return output[0].strip() if output else None


def is_unity_editor_path(path: str | None) -> bool:
    return bool(path and path.replace("\\", "/").lower().endswith("/editor/unity.exe"))


def discover_tools() -> list[ToolCheck]:
    git = _command_path("git", os.environ.get("PLV_GIT"))
    cmake = _command_path("cmake", os.environ.get("PLV_CMAKE"))
    cxx = (
        _command_path("cl", os.environ.get("PLV_CXX"))
        or _command_path("clang++", os.environ.get("PLV_CXX"))
        or _command_path("g++", os.environ.get("PLV_CXX"))
    )
    unity_override = os.environ.get("PLV_UNITY")
    unity = _command_path("Unity", unity_override)
    if not is_unity_editor_path(unity):
        unity = None
    if not unity and os.name == "nt":
        unity = _first_existing(
            [
                Path("C:/Program Files/Unity " + PINNED_UNITY + "/Editor/Unity.exe"),
                Path("C:/Program Files/Unity/Hub/Editor") / PINNED_UNITY / "Editor/Unity.exe",
                Path("C:/Program Files/Unity Hub/Editor") / PINNED_UNITY / "Editor/Unity.exe",
            ]
        )
    gh = _command_path("gh", os.environ.get("PLV_GH"))
    gh_authenticated = False
    if gh:
        try:
            gh_authenticated = subprocess.run(
                [gh, "auth", "status"],
                capture_output=True,
                text=True,
                timeout=10,
                check=False,
            ).returncode == 0
        except (OSError, subprocess.SubprocessError):
            gh_authenticated = False

    return [
        ToolCheck("git", bool(git), git, _version(git, ["--version"]), True),
        ToolCheck("python", True, sys.executable, platform.python_version(), True),
        ToolCheck("cmake", bool(cmake), cmake, _version(cmake, ["--version"]), True),
        ToolCheck("cxx", bool(cxx), cxx, _version(cxx, ["--version"]), True),
        ToolCheck(
            "unity",
            bool(unity),
            unity,
            PINNED_UNITY if unity else None,
            True,
            "Pinned editor path detected; import/build remains the compatibility gate." if unity else "Pinned Unity Editor not found.",
        ),
        ToolCheck("github-cli", bool(gh), gh, _version(gh, ["--version"]), False),
        ToolCheck(
            "github-auth",
            gh_authenticated,
            None,
            None,
            False,
            "Authenticated" if gh_authenticated else "Unavailable or invalid; no token content inspected.",
        ),
        ToolCheck("physical-vr-headset", False, None, None, False, "EvidenceMissing by assignment context."),
    ]


def evaluate_readiness(checks: list[ToolCheck], allow_missing_unity: bool) -> Readiness:
    allowed = ["unity"] if allow_missing_unity else []
    missing = [check.name for check in checks if check.required and not check.available and check.name not in allowed]
    allowed_missing = [name for name in allowed if any(check.name == name and not check.available for check in checks)]
    return Readiness(not missing, missing, allowed_missing)


def build_report(checks: list[ToolCheck], readiness: Readiness) -> dict:
    return {
        "schemaVersion": 1,
        "scope": "environment-discovery",
        "capturedAtUtc": datetime.now(timezone.utc).isoformat(),
        "host": {
            "system": platform.system(),
            "release": platform.release(),
            "machine": platform.machine(),
        },
        "ready": readiness.ready,
        "missingRequired": readiness.missing_required,
        "allowedMissing": readiness.allowed_missing,
        "tools": [check._asdict() for check in checks],
        "claims": {
            "productBuilt": False,
            "physicalVrTested": False,
            "experimentalAccuracyEstablished": False,
        },
    }


def write_report(report: dict, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--allow-missing-unity", action="store_true")
    parser.add_argument("--json-out", type=Path)
    args = parser.parse_args()

    checks = discover_tools()
    readiness = evaluate_readiness(checks, args.allow_missing_unity)
    report = build_report(checks, readiness)
    if args.json_out:
        write_report(report, args.json_out)
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if readiness.ready else 1


if __name__ == "__main__":
    raise SystemExit(main())
