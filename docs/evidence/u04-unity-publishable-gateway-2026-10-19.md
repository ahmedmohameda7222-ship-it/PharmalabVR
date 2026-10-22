# U04 Unity publishable gateway key — source-linked RED/GREEN

**Date**: 19 October 2026. **Scope**: C# Unity staging/registration transport; no Supabase migration or Edge redeployment.

The public Unity transport previously supplied `Authorization: Bearer` but no Supabase API-gateway `apikey`. The registration transport already supplied that header; both accepted arbitrary strings that could include high-privilege server secrets.

- **RED** `cd9757f20c9f6d9a6aad8f542d455a570a0aa8d4`: headless outbox test requires `apikey`; push `39192975812` panel `117869580471` failed exactly `Student staging HTTP request omitted the public Supabase gateway apikey.`
- **Functional fix** `1608462056939efaf1a50e065445b6b136438002`: `UniversityStagingTransport.UploadAsync` now takes a publishable key and sends the `apikey` header; 12 source-linked call sites and 8 deferred Unity EditMode call sites migrated.
- **RED secret-key exclusion** `1d6646179f70e69745ef143fdaa61921ebc82b21`: push `39193355042` panel `117870721391` failed `Server secret was accepted as a Unity publishable apikey.`
- **GREEN** `b85a781a02e0e40694e4cffea8eacfd5a4508521`: both staging and registration transport accept `sb_publishable_*` only. Push `39193500090` panel job `117871156437` **SUCCESS**, 57/57 outbox contract checks plus Node suites of 7, 10, 5, 4 tests, all 0 failed.

This format follows Supabase's client/server credential separation. It is a guard against accidental `sb_secret_*` usage, not proof of server-side access policy or a real Unity networking test.

**Unverified**: Exact-SHA native Windows/Ubuntu completion and PR workflows (review separately), Unity Editor/Player/XR, production user login/upload, trusted Native receipt attestation, academic promotion. G-U1, G-R1, P11.01 and PR #1 release gates stay OPEN; no merge.
