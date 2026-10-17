# Resume and status examples

`continue` resumes the first dependency-eligible unfinished task from the repository ledger. `nextTask` is a hint; actual dependencies and git state must be checked. The template is imported once, then preserved through commits and context changes.

## A truthful completed software task

```json
{
  "id": "P08.01",
  "status": "VerifiedLocal",
  "acceptanceScope": "Serialized laboratory shell; rendered Desktop and SimulatedXR. Apparatus interaction is a later task.",
  "codeCommit": "<actual-functional-SHA>",
  "evidence": ["<actual-rendered-image>", "<scene-reference-test-result>"],
  "plannerStatus": "NotReviewed",
  "nextTask": "P08.02"
}
```

Angle brackets in this example are explanatory placeholders. The real ledger must contain actual verified paths/SHAs, never these sample values. A phase is not accepted from this shell task alone.

## A true scientific-data blocker

```json
{
  "id": "P18.01",
  "status": "BlockedExternal",
  "blocker": {
    "scope": "A specific new instrument provider requiring independent validation data",
    "missing": "Name the exact required source/data and conditions",
    "requestedAction": "Name the needed licensed dataset or experimental evidence",
    "independentWork": "Continue a dependency-eligible catalog, instructor or other supported pack task"
  },
  "plannerStatus": "NotReviewed"
}
```

A missing implementation, failed test or unfinished scene is work to complete, not such a blocker. Unsupported reference entries cannot be counted as simulated compounds.

## What to report at the end of a turn

1. Actual HEAD and functional artifact SHA if different.
2. Last task verified, behavior and evidence scope.
3. Current task and exact next task.
4. Real rendered images when UI changed, and action-driven journey evidence.
5. Tests actually run, failures and actual external evidence gaps.
6. Ledger/NEXT paths and smallest next concrete action.

The implementation chat can self-verify local evidence. Only the planner/human grants planner acceptance. Final PR merge requires separate authorization. Ending a turn does not trigger automatic background execution.
