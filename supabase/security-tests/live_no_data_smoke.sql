-- PharmaLabVR LIVE no-data authorization smoke test.
-- Safe on an empty deployment; no real Auth users or academic rows are seeded.
-- Execute as project SQL owner. All checks run inside a rollback transaction.
-- This does NOT replace disposable two-university seeded RLS verification.
begin;

-- Anonymous callers must not invoke any of the privileged RPC endpoints.
set local role anon;
do $plv$
begin
  begin
    perform plv_private.owner_bootstrap_institution('test-school','Fake School',
      '11111111-1111-4111-8111-111111111111'::uuid,
      'dddddddd-dddd-4ddd-8ddd-dddddddddddd'::uuid);
    raise exception 'BOOTSTRAP_ANON_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_approve_grade(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'::uuid, 90,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'ANON_GRADE_PERMISSION_FAILURE' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_create_course(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'CHEM999', 'Research Chemistry', '2026F',
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'ANON_COURSE_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_enroll_student(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'aaaa0000-0000-4000-8000-000000000012'::uuid,
      '44444444-4444-4444-8444-444444444444'::uuid,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'ANON_ENROLLMENT_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_submit_attempt(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'aaaa0000-0000-4000-8000-000000000041'::uuid,
      'aaaa0000-0000-4000-8000-000000000051'::uuid,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid,
      repeat('a',64));
    raise exception 'ANON_SUBMISSION_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_invite_member(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'::uuid,
      'student', 'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'ANON_INVITE_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_accept_invite(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'ANON_ACCEPT_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_revoke_member(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'::uuid,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'ANON_REVOKE_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
end
$plv$;

-- Authenticated role alone is not enough: nonexistent Auth/membership must fail.
set local role authenticated;
select set_config('request.jwt.claim.sub',
  '11111111-1111-4111-8111-111111111111',true);
do $plv$
begin
  begin
    perform plv_private.owner_bootstrap_institution('test-school','Fake School',
      '11111111-1111-4111-8111-111111111111'::uuid,
      'dddddddd-dddd-4ddd-8ddd-dddddddddddd'::uuid);
    raise exception 'BOOTSTRAP_AUTH_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_invite_member(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'::uuid,
      'student', 'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'UNAUTHORIZED_INVITE_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_accept_invite(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'UNAUTHORIZED_ACCEPT_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_submit_attempt(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'aaaa0000-0000-4000-8000-000000000041'::uuid,
      'aaaa0000-0000-4000-8000-000000000051'::uuid,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid,
      repeat('a',64));
    raise exception 'UNAUTHORIZED_SUBMISSION_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_approve_grade(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'::uuid, 90,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'UNASSIGNED_GRADE_PERMISSION_FAILURE' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_create_course(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'CHEM999', 'Research Chemistry', '2026F',
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'UNAUTHORIZED_COURSE_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_enroll_student(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'aaaa0000-0000-4000-8000-000000000012'::uuid,
      '44444444-4444-4444-8444-444444444444'::uuid,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'UNAUTHORIZED_ENROLLMENT_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
  begin
    perform public.university_revoke_member(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'::uuid,
      'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'::uuid,
      'cccccccc-cccc-4ccc-8ccc-cccccccccccc'::uuid);
    raise exception 'UNAUTHORIZED_REVOKE_BYPASS' using errcode='P0001';
  exception when insufficient_privilege then null;
  end;
end
$plv$;
reset role;
rollback;
