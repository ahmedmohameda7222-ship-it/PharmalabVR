# U04 quarantined student evidence upload

This function accepts a *bounded, untrusted JSON export* from a verified
student with active institution membership and an owned pending lab session.
It uses the student's current Auth access token for authorization, then a
server-only Supabase secret to write the bytes to a **separate private staging
bucket** and immutable staging metadata. It never writes academic
`session_artifacts`, never signs a Native receipt, never marks an outbox item
synced, and never approves a grade.

The route is `POST /functions/v1/student-evidence-upload`. Send the raw
`application/json` file bytes (not a base64 wrapper). Required headers:

- `Authorization: Bearer <student-access-token>`
- `x-institution-id: <canonical-uuid>`
- `x-session-id: <canonical-uuid>`
- `x-artifact-id: <fresh-canonical-uuid>`
- `x-evidence-sha256: <64 lowercase hex SHA-256 of the exact body bytes>`
- `Content-Type: application/json`

There is a hard 1 MiB streaming size cap, including when no
`Content-Length` is provided. The function accepts only valid UTF-8 JSON
objects. On the first upload, HTTP 201 with `state: "staged_unverified"` means the bytes are **quarantined**, not scientific or academic attestation. If the same upload is retried after a lost response, HTTP 200 with `alreadyStaged: true` is returned **only** when the server matches the immutable same-student/session/tenant/artifact metadata and re-downloads the private stored object to verify actual SHA-256 and JSON bytes. Changed scope or bytes returns 409; storage/read faults fail closed. Neither 200 nor 201 acknowledges Native outbox sync, and existing objects are never overwritten.

The staging table and bucket have no direct authenticated or anonymous client
policies. The `service_role`/server secret is used only inside the Edge
Function and must never be exposed to Unity or frontend clients.
A later trusted server receipt/Native-origin verifier is required before
promoting any staging bytes into the original protected academic evidence bucket.

Headless admission and request streaming regressions:
```sh
node --test supabase/functions/student-evidence-upload/intent.test.mjs
node --test supabase/functions/student-evidence-upload/protocol.test.mjs
node --test supabase/functions/student-evidence-upload/reconcile.test.mjs
```

The isolated PostgreSQL fixture exercises quarantine bucket/table denial.
No real test students or verified Native export producer exist yet, so an
authorized production HTTP upload remains **NOT_RUN**, regardless of function
deployment status. Final Unity Editor/Player/XR tests remain deferred.

## Register a pending source identity
Before staging, a real authenticated student may invoke the public RPC `university_register_staging_session` with their institution UUID, local session token (1–128 safe ASCII characters), Native branch token, 64-character lowercase science package SHA-256 and stable UUID request ID. Its return value is the *server session UUID* required in the staging HTTP header `x-session-id`. The RPC creates **only** a revision-zero pending self-claimed identity; it is not an authenticated Native receipt, and retries with changed data fail. The real client Auth/HTTP workflow remains untested because production has no Auth student accounts.

## Student session registration before staging

Use a student's live bearer token and public `sb_publishable_` key to invoke `public.university_register_staging_session` through PostgREST, receiving its **cloud lab session UUID**. Do not substitute the local Native session label for this UUID. `UniversityStagingSessionRegistration` and `UniversityStagingTransport` are source-linked Unity runtime classes with .NET fake-HTTP tests. The only permitted server reply after upload is a quarantined `StagedUnverified` status; the Native outbox must remain Pending. No authorized live student HTTP upload or Unity Player verification has occurred.

## Staging registration audit and revision-zero gate

As of Edge version 3 the server requires more than a user-controlled `pending` lab session row. A server-only query to the immutable private `audit_events` table must confirm `StagingLabSessionCreated` for the same institution, student, and session with `attested=false` and `nativeRevision=0`. The current lab session must also have `current_revision=0`. Missing, mismatched, superseded or unaudited sessions are denied **before storage upload**. This is **admission provenance**, not authoritative scientific evidence or a Native receipt signature. No authorized HTTP integration against a real Auth student has yet been performed.

## Exact retry evidence (v4)
Headless Node and source-linked .NET outbox contract tests passed on GitHub Actions push `39181138169`; see `docs/evidence/u04-exact-retry-2026-10-19.md`. Additional Unity EditMode test source exists but Unity execution is deferred. Owner Supabase Free currently has zero real students/sessions/uploads, so authenticated HTTP replay under production networking is **NOT_RUN**. After any 200 retry, local durable Native outbox remains Pending.

## Isolated real HTTP integration (v5)

`supabase/security-tests/staging_http_smoke.mjs` runs in the disposable GitHub PostgreSQL workflow with **localhost-only** Supabase CLI keys and temporary synthetic GoTrue students. It exercises verified password login, session registration RPC, Edge request, private Storage and RLS: 201 initial upload, 200 exact retry with private byte verification, 409 changed body, 403 another student, 422 incorrect digest, client staging table SELECT denied and academic `session_artifacts` empty. Both push `39189809292` and PR `39189813884` succeeded. No owner production Auth user was created or contacted by the test. The exact duplicate recovery path additionally recognizes Storage's legacy 400 Asset Already Exists error, but returns 200 only after checking immutable staging metadata and downloaded private object bytes.

Owner Free `student-evidence-upload` is ACTIVE v5 with `verify_jwt=true` and bundle SHA-256 `1e5dd0ff9c06992d0365a0135122a96940974df592ef4d765a852878ef7b2e12`. **Production authorized student HTTP and Native-origin attestation are still NOT_RUN.** Neither a first staging reply nor an exact retry clears the durable Native outbox or produces academic-grade evidence. Unity Editor/Player/XR execution remains deferred.

## Unity gateway credential contract

Unity `UniversityStagingTransport.UploadAsync` and
`UniversityStagingSessionRegistration.RegisterPendingAsync` now require a
**publishable** `sb_publishable_*` API key, separately from the user's Auth
access token. The client sends `apikey` with the bearer; a `sb_secret_*`
or legacy service-role JWT is refused by the client and must never be embedded
in Unity builds. The legacy anon JWT is not accepted by these Unity adapters;
migrate clients to a publishable key before integration. This repairs a
source-linked headless transport defect and does **not** imply production
student HTTP verification or a trusted Native attestation.
