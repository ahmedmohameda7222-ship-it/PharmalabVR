# Student Evidence Status (U04 source + Free deployment)

This is a **read-only** authenticated Edge Function at
`/functions/v1/student-evidence-status?institution_id=<institution-uuid>`.
The owner project is `hhbnlhceehgbskzdtorn`. Version 1 is ACTIVE with
platform JWT verification enabled. Requests also undergo a fresh Supabase
`auth.getUser()` lookup and read-only relational membership checks.

- Method: `GET` only (plus CORS preflight).
- Authentication: `Authorization: Bearer <access_token>` and a valid
  publishable key when required by the caller SDK.
- Authorization: active, email-verified student matching the specified
  institution, with institution itself active. All returned sessions
  and artifacts are obtained under the user's normal RLS policy.
- Body: bounded, metadata-only JSON lists; no signed evidence URLs,
  file bytes, grade writes, user data from other institutions or secrets.
- Responses use `Cache-Control: no-store`.

To test the pure policy use:

```sh
node --test supabase/functions/student-evidence-status/policy.test.mjs
```

**Status:** Node test verified via GitHub Actions; deployed code
inspected through Supabase. No real Auth users/enrolled students yet,
so authenticated HTTP and RLS integration remain **NOT_RUN**. This is
not the U04 trusted upload broker or a completed synchronization path.
The final Unity Editor/Player/XR campaign is also deferred.
