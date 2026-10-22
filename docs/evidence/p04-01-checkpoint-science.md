# P04.01 — checkpoint science generations

Functional SHA `3271fdfc2277da1b3efb4e97578de1fcecd7ac68`. Status **VerifiedLocal** for the Research IPhreeqc product session and Unity Lab scene. This is not experimental pH validation.

The `P04_01` Native ABI test prepares real minteq HCl and NaOH, observes acidic pH 1.10723 at material revision 1, creates a checkpoint, transfers base, and observes pH 12.7877 at revision 2. Before the fix, restoring the checkpoint left revision 2/Pending and displayed the base result; the red test failed its restored-revision and acid-value assertions. The production fix increments `scienceDependencyGeneration`, clears the old observation/budget state and cached result, offers fresh requests for restored inventories, and rejects worker completions from the previous generation. The test now checks both a completed newer-state result and a result still in flight when RestartCheckpoint occurs. A scheduler regression checks the same-revision stale completion cannot take ownership after generation reset.

The Unity `P04_01` PlayMode scene test prepares HCl through `CoreDriver`, waits for Current, makes a native base transfer, sees the MeasurementView change, restarts the checkpoint, sees the newer pH disappear immediately (`--`), and then sees the original value return after actual IPhreeqc recomputation. This is a scene-driven model/measurement journey, not a physical instrument or person-operated Player run.

Local evidence copied under ignored `artifacts/evidence-p04-01/` so later builds do not overwrite it:

| File | SHA-256 | Result |
|---|---|---|
| `native-oct11-science-generation.txt` | `05003A7C21D41203806F76A5F3CD5264E5E3C191764635344F9EC7A19E67307A` | Native Release 72/72, 15559/15559 assertions |
| `ctest-oct11-science-generation.txt` | `CB1765F120A9527DDC421F79DC2DD7294390A82E1FDB0991AAFEED8E7304C411` | CTest 1/1 |
| `PlayMode.xml` | `89A7E32EA74C6D907598C8B0E5D585048094F363EE78291BED54913247E85F22` | PlayMode 18/18 |
| `unity-WindowsDesktop.log` | `440438EFA55DDCA3D83E544C56B0E71D90C32B7444494C8E6FFF599605FFB6C8` | WindowsDesktop build Success |
| `pharmalab_core.dll` | `69BAEC40647BB26D9F2952D832CC71ED814122AE9FB9146B69EFEAF6CC3915BE` | Native Windows Player plugin |
| `PharmaLabVR.Runtime.dll` | `EFAB81D19B9EDFD4912E7D6C9F1BDF9DA7049E652FDBF092E3824ED162D4E44B` | Managed Windows Player runtime |

Python continuation/doctor/performance tests: 8/8. No new Player screenshot was taken. Current code still lacks P04.02 package identity checks and contextual receiver inspection, and O10-N2 discrete-command science admission remains open. `AcceptedPlanner` is not assigned here.
