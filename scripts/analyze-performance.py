#!/usr/bin/env python3
"""Analyze real PharmaLabVR Player captures without inventing missing measurements."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path


def percentile(values: list[float], fraction: float) -> float:
    ordered = sorted(values)
    if not ordered:
        raise ValueError("frameMs must contain real samples")
    index = max(0, min(len(ordered) - 1, math.ceil(fraction * len(ordered)) - 1))
    return ordered[index]


def analyze(capture: dict) -> dict:
    target = capture.get("target")
    if target not in {"WindowsDesktop", "SimulatedXR", "WindowsVR", "AndroidVR"}:
        raise ValueError("unknown target")
    if capture.get("actualPlayerRun") is not True:
        return {"schemaVersion": 1, "target": target, "status": "EvidenceMissing", "reason": "actual Player run not recorded"}
    if capture.get("warmupSeconds", 0) < 300:
        raise ValueError("five-minute warmup is required")
    runs = capture.get("runs")
    if not isinstance(runs, list) or len(runs) != 3:
        raise ValueError("exactly three measured runs are required")
    frame_samples: list[float] = []
    recurring_holds = 0
    peak_memory_mib = 0.0
    for run in runs:
        if run.get("durationSeconds", 0) < 1800:
            raise ValueError("each measured run must be at least 30 minutes")
        samples = run.get("frameMs")
        if not isinstance(samples, list) or not samples:
            raise ValueError("each run requires frameMs samples")
        if any(not isinstance(value, (int, float)) or not math.isfinite(value) or value <= 0 for value in samples):
            raise ValueError("frame samples must be positive finite numbers")
        frame_samples.extend(float(value) for value in samples)
        recurring_holds += int(run.get("recurringComputeHolds", 0))
        peak_memory_mib = max(peak_memory_mib, float(run.get("peakMemoryMiB", 0.0)))
    p95 = percentile(frame_samples, 0.95)
    p99 = percentile(frame_samples, 0.99)
    budget = 1000.0 / (60.0 if target in {"WindowsDesktop", "SimulatedXR"} else 72.0)
    passed = p95 <= budget and recurring_holds == 0
    return {
        "schemaVersion": 1,
        "target": target,
        "status": "Passed" if passed else "Failed",
        "actualPlayerRun": True,
        "sampleCount": len(frame_samples),
        "frameBudgetMs": budget,
        "p95FrameMs": p95,
        "p99FrameMs": p99,
        "recurringComputeHolds": recurring_holds,
        "peakMemoryMiB": peak_memory_mib,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("capture", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = analyze(json.loads(args.capture.read_text(encoding="utf-8")))
    encoded = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        args.output.write_text(encoded + "\n", encoding="utf-8")
    else:
        print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
