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
objects. On success, status 201 and `state: "staged_unverified"` mean the
bytes are **quarantined**, not a scientific or academic attestation.
Retrying the same artifact ID never overwrites an existing object.

The staging table and bucket have no direct authenticated or anonymous client
policies. The `service_role`/server secret is used only inside the Edge
Function and must never be exposed to Unity or frontend clients.
A later trusted server receipt/Native-origin verifier is required before
promoting any staging bytes into the original protected academic evidence bucket.

Headless admission and request streaming regressions:
```sh
node --test supabase/functions/student-evidence-upload/intent.test.mjs
node --test supabase/functions/student-evidence-upload/protocol.test.mjs
```

The isolated PostgreSQL fixture exercises quarantine bucket/table denial.
No real test students or verified Native export producer exist yet, so an
authorized production HTTP upload remains **NOT_RUN**, regardless of function
deployment status. Final Unity Editor/Player/XR tests remain deferred.

## Register a pending source identity
Before staging, a real authenticated student may invoke the public RPC `university_register_staging_session` with their institution UUID, local session token (1–128 safe ASCII characters), Native branch token, 64-character lowercase science package SHA-256 and stable UUID request ID. Its return value is the *server session UUID* required in the staging HTTP header `x-session-id`. The RPC creates **only** a revision-zero pending self-claimed identity; it is not an authenticated Native receipt, and retries with changed data fail. The real client Auth/HTTP workflow remains untested because production has no Auth student accounts.
