# U04 local Auth / Edge / Storage HTTP integration — 19 October 2026

**Scope:** Quarantine-only. No Native scientific attestation, academic `session_artifacts`, synchronization acknowledgment or completed release gate.

## RED → GREEN

- C# staged reply RED `76ffa25fe9e812937427f85a2cdc546d255cc53c` (push `39188207382`): fake HTTP server response from another institution was accepted. GREEN `c2527c876a94a3d7026c9d4a82cb10a46d57790d`: structured JSON parser validates the institution, artifact and SHA-256 of the actual upload, distinguishes first 201 from exact 200 and bounds response buffering. Headless panel SUCCESS; Unity EditMode test source updated `1297f1ffd2fc35151f64adb96cb4b72ac1f89dc5` but Unity Editor execution remains **NOT_RUN**.
- Local real-HTTP test `supabase/security-tests/staging_http_smoke.mjs` introduced at `2f36343c690dc7123006322aa2bec0b5afdba4f7` using localhost-only CLI keys, temporary GoTrue Auth users and the actual Edge/Storage stack. Initial push `39189042480` showed first upload 201 but exact retry 503.
- Node Storage duplicate RED `d231367dad5b2cc4b7e6c50e5e180992d8b188c4` / GREEN `2631daace700a67bd872e4db9cf825f952217e4c` verified code precedence. Additional RED `77596eca57e83596b2ac0893c09b4e3fbe047227` for legacy 400 Asset Already Exists; GREEN `c9636358ef68f92b35a4255b5587cf1785a69cf3` classifies duplicate only as a *candidate* for independent immutable staging metadata and downloaded private byte hash verification.
- GitHub push `39189809292` (job `117859910977`) and PR `39189813884` **SUCCESS**: real local GoTrue sign-in and student session RPC, Edge+Storage first upload 201, exact private-byte retry 200, altered bytes 409, different student 403, invalid digest 422, no direct student staging table read, and **zero academic `session_artifacts`**. 17/17 migrations, 17 public RLS tables, 60 PostgreSQL DO blocks, final ROLLBACK and stack destruction without backups.

## Production Free deployment

Owner project `hhbnlhceehgbskzdtorn`: `student-evidence-upload` **ACTIVE v5**, `verify_jwt=true`, bundle `1e5dd0ff9c06992d0365a0135122a96940974df592ef4d765a852878ef7b2e12`. SQL verified zero production Auth users, lab sessions, staged rows/objects, academic evidence and audit rows. Staging bucket private; Security Advisor no WARN (two intentional INFO for deny-all audit/staging tables).

**G-U1, G-R1, P11.01 still OPEN.** Production student HTTP pilot, concurrent/fault testing, trusted Native-origin signed receipt, academic promotion and final Unity Editor/Player/XR campaign still NOT_RUN or NOT_IMPLEMENTED. PR #1 remains unmerged; English-only product unchanged.
