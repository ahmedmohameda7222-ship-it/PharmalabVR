# P11.02 — Research notebook code-first source increment (2026-10-16)

**Scope:** partial functionality, not P11.02 acceptance. English-only product. No Unity Editor/Player/XR tests at these code SHAs.

The production `NotebookService.cs` stores local versioned UTF-8 lines tagged with Native branch ID, event sequence, vessel ID and UTC timestamp, with SHA-256 checksums. The store bounds record count (512), note text (4096 characters) and file size (1 MiB), writes a staged file durably, atomically replaces the primary and preserves the prior confirmed backup. If the current primary is corrupt, `Read` may recover a confirmed backup, but `Append` fails closed rather than silently destroying the damaged current session's notes. The text guard rejects Arabic-script note text; no localization controls or RTL support exist.

The actual `SessionController.AppendNotebookNote` requires a valid current Native CoreSession, a matching current LabViewModel eventSequence, an admitted Notebook screen, and no active time Hold. It never sends material or chemistry commands. The Desktop research notebook offers typed note input and the XR Review route provides a read-only world panel summary; XR typing is not yet supported. This local annotation store is **not bundled into exported native session JSON**, so portable Save/Import/branch portability remains Open.

| Commit | Evidence |
| --- | --- |
| `88d893126130466a230b0065f5ecbfc8524d889a` | Notebook headless compile RED, run 38832765547 |
| `8c5c3d204c2cbe3036ac615555a93b654d8577bb` | Production NotebookService, real headless .NET disk assertions, push 38832952576 and PR 38832957789 both SUCCESS all three jobs |
| `6471d3159baf509aa7c86817c4bb6b9edd54aa63` | shared panel route source RED, push 38833166173 |
| `1cd320b40cc7d4996fd27698257937fcc5ab14cf` | Native event + branch sourced Unity Desktop/XR path; green headless panel-routing, push 38833340015; full Native conclusions inspect separately |
| `685025508fb2d3c5db23e301541a87995d51a4b5`, `f6b18f09145602590c4aaaf81dc59b539085aba4` | preserve UI error after failed note and correct Unicode-escape negative PlayMode test source; real Unity NOT_RUN |

**Next:** integrated native-session note bundling (with explicit bundle identity and durable atomicity), Review event-list/notebook references, branch-fork behavior, XR note input method, capability/lesson notes, test runner and Player click-through. Keep P11.02 Open and G-R1 Open.
