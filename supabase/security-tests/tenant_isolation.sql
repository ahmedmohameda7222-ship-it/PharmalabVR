-- PSQL ONLY, disposable LOCAL Supabase database ONLY. Never run on production.
-- Real PostgreSQL RLS enforcement: test fixtures roll back in all outcomes.
-- Command: psql "$LOCAL_SUPABASE_DATABASE_URL" -X -v ON_ERROR_STOP=1 -f supabase/security-tests/tenant_isolation.sql
begin;
-- Unambiguous synthetic identities; no real emails or accounts.
insert into auth.users(id,instance_id,aud,role,email,email_confirmed_at) values
 ('11111111-1111-4111-8111-111111111111','00000000-0000-0000-0000-000000000000','authenticated','authenticated','a-admin@example.invalid',now()),
 ('22222222-2222-4222-8222-222222222222','00000000-0000-0000-0000-000000000000','authenticated','authenticated','b-admin@example.invalid',now()),
 ('33333333-3333-4333-8333-333333333333','00000000-0000-0000-0000-000000000000','authenticated','authenticated','dual-role@example.invalid',now()),
 ('44444444-4444-4444-8444-444444444444','00000000-0000-0000-0000-000000000000','authenticated','authenticated','a-student@example.invalid',now()),
 ('55555555-5555-4555-8555-555555555555','00000000-0000-0000-0000-000000000000','authenticated','authenticated','b-student@example.invalid',now()),
 ('66666666-6666-4666-8666-666666666666','00000000-0000-0000-0000-000000000000','authenticated','authenticated','revoked@example.invalid',now()),
 ('77777777-7777-4777-8777-777777777777','00000000-0000-0000-0000-000000000000','authenticated','authenticated','new-student@example.invalid',now());

insert into public.institutions(id,slug,name) values
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','plv-test-a','Synthetic University A'),
 ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','plv-test-b','Synthetic University B');
insert into public.institution_memberships(institution_id,user_id,role,status) values
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','11111111-1111-4111-8111-111111111111','university_admin','active'),
 ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','22222222-2222-4222-8222-222222222222','university_admin','active'),
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','33333333-3333-4333-8333-333333333333','instructor','active'),
 ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','33333333-3333-4333-8333-333333333333','student','active'),
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','44444444-4444-4444-8444-444444444444','student','active'),
 ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','55555555-5555-4555-8555-555555555555','student','active'),
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','66666666-6666-4666-8666-666666666666','student','revoked');
insert into public.courses(id,institution_id,code,name,term,owner_user_id) values
 ('aaaa0000-0000-4000-8000-000000000001','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','LAB101','A Core','2026F','11111111-1111-4111-8111-111111111111'),
 ('bbbb0000-0000-4000-8000-000000000001','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','LAB101','B Core','2026F','22222222-2222-4222-8222-222222222222');
insert into public.course_sections(id,institution_id,course_id,section_code) values
 ('aaaa0000-0000-4000-8000-000000000011','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000001','A1'),
 ('aaaa0000-0000-4000-8000-000000000012','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000001','A2'),
 ('bbbb0000-0000-4000-8000-000000000011','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','bbbb0000-0000-4000-8000-000000000001','B1');
insert into public.section_staff(institution_id,section_id,user_id,role) values
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000011','33333333-3333-4333-8333-333333333333','instructor');
insert into public.enrollments(institution_id,section_id,student_user_id) values
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000011','44444444-4444-4444-8444-444444444444'),
 ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000011','66666666-6666-4666-8666-666666666666'),
 ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','bbbb0000-0000-4000-8000-000000000011','33333333-3333-4333-8333-333333333333'),
 ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','bbbb0000-0000-4000-8000-000000000011','55555555-5555-4555-8555-555555555555');
insert into public.lesson_versions(id,institution_id,course_id,lesson_id,version,author_id,content_sha256,content_json,published_at)
 values ('aaaa0000-0000-4000-8000-000000000021','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000001','aaaa0000-0000-4000-8000-000000000022',1,'11111111-1111-4111-8111-111111111111',repeat('a',64),'{}',now());
insert into public.rubric_versions(id,institution_id,course_id,rubric_id,version,author_id,content_sha256,rubric_json,published_at)
 values ('aaaa0000-0000-4000-8000-000000000031','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000001','aaaa0000-0000-4000-8000-000000000032',1,'11111111-1111-4111-8111-111111111111',repeat('b',64),'{}',now());
insert into public.assignments(id,institution_id,course_id,section_id,lesson_version_id,rubric_version_id,title,published_at)
 values ('aaaa0000-0000-4000-8000-000000000041','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000001','aaaa0000-0000-4000-8000-000000000011','aaaa0000-0000-4000-8000-000000000021','aaaa0000-0000-4000-8000-000000000031','Test Titration',now());
insert into public.lab_sessions(id,institution_id,user_id,local_session_id,native_branch_id,science_package_sha256) values
 ('aaaa0000-0000-4000-8000-000000000051','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','44444444-4444-4444-8444-444444444444','local-a','native-a',repeat('c',64)),
 ('bbbb0000-0000-4000-8000-000000000051','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','55555555-5555-4555-8555-555555555555','local-b','native-b',repeat('d',64));
insert into public.submissions(id,institution_id,course_id,section_id,assignment_id,rubric_version_id,student_id,lab_session_id,idempotency_key,evidence_sha256)
 values ('aaaa0000-0000-4000-8000-000000000061','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000001','aaaa0000-0000-4000-8000-000000000011','aaaa0000-0000-4000-8000-000000000041','aaaa0000-0000-4000-8000-000000000031','44444444-4444-4444-8444-444444444444','aaaa0000-0000-4000-8000-000000000051','aaaa0000-0000-4000-8000-000000000069',repeat('e',64));
insert into public.grades(id,institution_id,submission_id,rubric_version_id,grader_id,status,score)
 values ('aaaa0000-0000-4000-8000-000000000071','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000061','aaaa0000-0000-4000-8000-000000000031','33333333-3333-4333-8333-333333333333','approved',92);
insert into public.session_artifacts(id,institution_id,lab_session_id,user_id,submission_id,object_key,size_bytes,sha256,content_type)
 values
 ('aaaa0000-0000-4000-8000-000000000081','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000051','44444444-4444-4444-8444-444444444444','aaaa0000-0000-4000-8000-000000000061','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/aaaa0000-0000-4000-8000-000000000081',64,repeat('e',64),'application/json'),
 ('bbbb0000-0000-4000-8000-000000000081','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','bbbb0000-0000-4000-8000-000000000051','55555555-5555-4555-8555-555555555555',null,'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb/bbbb0000-0000-4000-8000-000000000081',64,repeat('f',64),'application/json');
insert into storage.objects(bucket_id,name) values
 ('plv-university-evidence','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/aaaa0000-0000-4000-8000-000000000081'),
 ('plv-university-evidence','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb/bbbb0000-0000-4000-8000-000000000081');

-- Real Postgres composite FKs, not a mocked authorization method.
do $$
begin
 begin
  insert into public.course_sections(id,institution_id,course_id,section_code)
   values (gen_random_uuid(),'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'bbbb0000-0000-4000-8000-000000000001','FORGED');
  raise exception 'cross-tenant section FK unexpectedly succeeded';
 exception when foreign_key_violation then null;
 end;
 begin
  insert into public.submissions(institution_id,course_id,section_id,assignment_id,
   rubric_version_id,student_id,lab_session_id,idempotency_key,evidence_sha256)
  values ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'aaaa0000-0000-4000-8000-000000000001','aaaa0000-0000-4000-8000-000000000011',
    'aaaa0000-0000-4000-8000-000000000041','aaaa0000-0000-4000-8000-000000000031',
    '44444444-4444-4444-8444-444444444444','aaaa0000-0000-4000-8000-000000000051',
    'aaaa0000-0000-4000-8000-000000000069',repeat('f',64));
  raise exception 'duplicate idempotency key unexpectedly succeeded';
 exception when unique_violation then null;
 end;
end $$;

set local role authenticated;
select set_config('request.jwt.claim.sub','44444444-4444-4444-8444-444444444444',true);
do $$
begin
 if (select count(*) from public.courses) <> 1 then raise exception 'A student course isolation failed'; end if;
 if (select count(*) from public.course_sections) <> 1 then raise exception 'A student section isolation failed'; end if;
 if (select count(*) from public.grades) <> 1 then raise exception 'student own approved grade missing'; end if;
 if (select count(*) from storage.objects where bucket_id='plv-university-evidence') <> 1 then raise exception 'A student Storage read isolation failed'; end if;
 begin
  insert into public.grades(institution_id,submission_id,rubric_version_id,grader_id,score)
   values ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','aaaa0000-0000-4000-8000-000000000061',
     'aaaa0000-0000-4000-8000-000000000031','44444444-4444-4444-8444-444444444444',100);
  raise exception 'student grade insert unexpectedly succeeded';
 exception when insufficient_privilege then null;
 end;
 begin
  update public.grades set score=0;
  raise exception 'student grade update unexpectedly succeeded';
 exception when insufficient_privilege then null;
 end;
 begin
  delete from public.submissions;
  raise exception 'student submission delete unexpectedly succeeded';
 exception when insufficient_privilege then null;
 end;
 begin
  insert into storage.objects(bucket_id,name)
   values ('plv-university-evidence','forged');
  raise exception 'client evidence upload unexpectedly succeeded';
 exception when insufficient_privilege then null;
 end;
end $$;

-- One person is an A instructor but a B student, with no A2 staff appointment.
select set_config('request.jwt.claim.sub','33333333-3333-4333-8333-333333333333',true);
do $$
begin
 if (select count(*) from public.course_sections) <> 2 then raise exception 'dual-tenant section scope failed'; end if;
 if (select count(*) from public.submissions) <> 1 then raise exception 'instructor A1 review scope failed'; end if;
 if (select count(*) from public.grades) <> 1 then raise exception 'instructor grade read failed'; end if;
 if (select count(*) from storage.objects where bucket_id='plv-university-evidence') <> 1 then raise exception 'instructor evidence scope failed'; end if;
end $$;

select set_config('request.jwt.claim.sub','55555555-5555-4555-8555-555555555555',true);
do $$
begin
 if (select count(*) from public.courses) <> 1 then raise exception 'B student course isolation failed'; end if;
 if (select count(*) from public.grades) <> 0 then raise exception 'B student leaked A grade'; end if;
 if (select count(*) from storage.objects where bucket_id='plv-university-evidence') <> 1 then raise exception 'B student Storage scope failed'; end if;
end $$;

select set_config('request.jwt.claim.sub','66666666-6666-4666-8666-666666666666',true);
do $$
begin
 if (select count(*) from public.course_sections) <> 0 then raise exception 'revoked membership leaked section'; end if;
 if (select count(*) from public.assignments) <> 0 then raise exception 'revoked membership leaked assignment'; end if;
 if (select count(*) from public.grades) <> 0 then raise exception 'revoked membership leaked grade'; end if;
end $$;
-- An A admin must not authorize a B section or course by mismatching
-- the institution/record pair sent to a private helper (forged reference).
select set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
do $$
begin
 if plv_private.can_manage_section('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
       'bbbb0000-0000-4000-8000-000000000011') or
    plv_private.can_view_section('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
       'bbbb0000-0000-4000-8000-000000000011') or
    plv_private.can_view_course('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
       'bbbb0000-0000-4000-8000-000000000001') then
  raise exception 'forged other-tenant admin helper returned true';
 end if;
 if not plv_private.can_manage_section('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
       'aaaa0000-0000-4000-8000-000000000011') or
    not plv_private.can_view_course('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
       'aaaa0000-0000-4000-8000-000000000001') then
  raise exception 'real tenant admin section/course helper was denied';
 end if;
end $$;

-- U02 SECURITY ACCEPTANCE: invitation, acceptance, revocation are server RPC only.
-- This is authored against the real database but remains NOT_RUN until local
-- Supabase/PostgreSQL is provisioned. Never execute synthetic users in production.
select set_config('request.jwt.claim.sub','33333333-3333-4333-8333-333333333333',true);
do $$
begin
 begin
  perform public.university_invite_member(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77777777-7777-4777-8777-777777777777',
    'instructor','77770000-0000-4000-8000-000000000001');
  raise exception 'nonadmin invite unexpectedly succeeded';
 exception when insufficient_privilege then null;
 end;
end $$;

select set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
do $$
begin
 if public.university_invite_member(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77777777-7777-4777-8777-777777777777',
    'student','77770000-0000-4000-8000-000000000002') <> 'Invited' then
   raise exception 'verified target invitation was not accepted';
 end if;
 if public.university_invite_member(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77777777-7777-4777-8777-777777777777',
    'student','77770000-0000-4000-8000-000000000002') <> 'AlreadyInvited' then
   raise exception 'exact invite retry was not idempotent';
 end if;
 begin
  perform public.university_invite_member(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77777777-7777-4777-8777-777777777777',
    'instructor','77770000-0000-4000-8000-000000000002');
  raise exception 'same request with different role unexpectedly succeeded';
 exception when unique_violation then null;
 end;
 begin
  perform public.university_invite_member(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77777777-7777-4777-8777-777777777777',
    'university_admin','77770000-0000-4000-8000-000000000003');
  raise exception 'invitation cannot grant university admin';
 exception when insufficient_privilege then null;
 end;
end $$;

select set_config('request.jwt.claim.sub','77777777-7777-4777-8777-777777777777',true);
do $$
begin
 if public.university_accept_invite(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77770000-0000-4000-8000-000000000004') <> 'Accepted' then
  raise exception 'invitation acceptance was not attributed to auth.uid';
 end if;
 if public.university_accept_invite(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77770000-0000-4000-8000-000000000004') <> 'AlreadyAccepted' then
  raise exception 'acceptance retry did not return terminal success';
 end if;
 if not plv_private.active_member('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa') then
  raise exception 'accepted membership is not active';
 end if;
end $$;

select set_config('request.jwt.claim.sub','22222222-2222-4222-8222-222222222222',true);
do $$
begin
 begin
  perform public.university_revoke_member(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77777777-7777-4777-8777-777777777777',
    '77770000-0000-4000-8000-000000000005');
  raise exception 'cross-tenant revocation unexpectedly succeeded';
 exception when insufficient_privilege then null;
 end;
end $$;

select set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
do $$
begin
 if public.university_revoke_member(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77777777-7777-4777-8777-777777777777',
    '77770000-0000-4000-8000-000000000006') <> 'Revoked' then
   raise exception 'authorized revocation failed';
 end if;
 if public.university_revoke_member(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    '77777777-7777-4777-8777-777777777777',
    '77770000-0000-4000-8000-000000000006') <> 'AlreadyRevoked' then
   raise exception 'revocation replay was not idempotent';
 end if;
end $$;

select set_config('request.jwt.claim.sub','77777777-7777-4777-8777-777777777777',true);
do $$
begin
 if plv_private.active_member('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa') or
    (select count(*) from public.institution_memberships
     where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
       and status='active') > 0 then
   raise exception 'revocation did not immediately deny tenant access';
 end if;
end $$;

reset role;
do $$
begin
 if (select count(*) from public.audit_events where
       institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
       and action='MembershipInvite' and
       target_id='77777777-7777-4777-8777-777777777777') <> 1 then
  raise exception 'accepted invitation was not audited';
 end if;
 if (select count(*) from public.audit_events where
       institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
       and action='MembershipRevoke' and
       target_id='77777777-7777-4777-8777-777777777777') <> 1 then
  raise exception 'revocation was not audited';
 end if;
end $$;

-- U03 positive and negative tests require a disposable database. Do NOT seed live.
-- New local Native session and already-attested private artifact, so earlier
-- pre-existing fixture records and their counts remain unchanged.
insert into public.lab_sessions(id,institution_id,user_id,local_session_id,
  native_branch_id,current_revision,science_package_sha256)
values ('aaaa0000-0000-4000-8000-000000000091',
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  '44444444-4444-4444-8444-444444444444',
  'local-u03','native-u03',2,repeat('a',64));
insert into public.session_artifacts(id,institution_id,lab_session_id,user_id,
  object_key,size_bytes,sha256,content_type)
values ('aaaa0000-0000-4000-8000-000000000092',
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  'aaaa0000-0000-4000-8000-000000000091',
  '44444444-4444-4444-8444-444444444444',
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/aaaa0000-0000-4000-8000-000000000092',
  128,repeat('d',64),'application/json');
insert into storage.objects(bucket_id,name)
values ('plv-university-evidence',
  'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/aaaa0000-0000-4000-8000-000000000092');

set local role authenticated;
select set_config('request.jwt.claim.sub','44444444-4444-4444-8444-444444444444',true);
do $$
declare
  first_id uuid;
  retry_id uuid;
begin
 first_id := public.university_submit_attempt(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'aaaa0000-0000-4000-8000-000000000041',
    'aaaa0000-0000-4000-8000-000000000091',
    'aaaa0000-0000-4000-8000-000000000093',repeat('d',64));
 if first_id is null then
   raise exception 'student_submission_accepted';
 end if;
 retry_id := public.university_submit_attempt(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'aaaa0000-0000-4000-8000-000000000041',
    'aaaa0000-0000-4000-8000-000000000091',
    'aaaa0000-0000-4000-8000-000000000093',repeat('d',64));
 if retry_id is distinct from first_id then
   raise exception 'student_submission_retry_reused_id';
 end if;
 begin
   perform public.university_submit_attempt(
     'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
     'aaaa0000-0000-4000-8000-000000000041',
     'aaaa0000-0000-4000-8000-000000000091',
     'aaaa0000-0000-4000-8000-000000000093',repeat('e',64));
   raise exception 'student_submission_changed_hash_rejected';
 exception when unique_violation then null;
 end;
 begin
   perform public.university_submit_attempt(
     'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
     'aaaa0000-0000-4000-8000-000000000041',
     'aaaa0000-0000-4000-8000-000000000091',
     'aaaa0000-0000-4000-8000-000000000094',repeat('d',64));
   raise exception 'student_submission_other_tenant_rejected';
 exception when insufficient_privilege then null;
 end;
end $$;
select set_config('request.jwt.claim.sub','33333333-3333-4333-8333-333333333333',true);
do $$
begin
 begin
  perform public.university_submit_attempt(
   'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
   'aaaa0000-0000-4000-8000-000000000041',
   'aaaa0000-0000-4000-8000-000000000091',
   'aaaa0000-0000-4000-8000-000000000095',repeat('d',64));
  raise exception 'student_submission_nonstudent_rejected';
 exception when insufficient_privilege then null;
 end;
end $$;
reset role;
do $$
begin
 if (select count(*) from public.audit_events
    where action='SubmissionCreated'
      and request_id='aaaa0000-0000-4000-8000-000000000093') <> 1 then
  raise exception 'student_submission_not_audited';
 end if;
end $$;

-- U03 grading: grade only a student's real submitted attempt with the
-- instructor assigned to the same institution AND section.
set local role authenticated;
select set_config('request.jwt.claim.sub','44444444-4444-4444-8444-444444444444',true);
do $$
begin
 begin
  perform public.university_approve_grade(
   'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
   'aaaa0000-0000-4000-8000-000000000061',88,
   'aaaa0000-0000-4000-8000-000000000101');
  raise exception 'student approved grade';
 exception when insufficient_privilege then null;
 end;
end $$;
select set_config('request.jwt.claim.sub','22222222-2222-4222-8222-222222222222',true);
do $$
begin
 begin
  perform public.university_approve_grade(
   'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
   'aaaa0000-0000-4000-8000-000000000061',88,
   'aaaa0000-0000-4000-8000-000000000102');
  raise exception 'foreign university admin approved grade';
 exception when insufficient_privilege then null;
 end;
end $$;
select set_config('request.jwt.claim.sub','33333333-3333-4333-8333-333333333333',true);
do $$
declare v_submission uuid; v_grade uuid; v_retry uuid;
begin
 select id into strict v_submission from public.submissions
  where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
    and idempotency_key='aaaa0000-0000-4000-8000-000000000093';
 v_grade := public.university_approve_grade(
   'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',v_submission,93.500,
   'aaaa0000-0000-4000-8000-000000000103');
 if v_grade is null then raise exception 'instructor grade missing'; end if;
 v_retry := public.university_approve_grade(
   'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',v_submission,93.500,
   'aaaa0000-0000-4000-8000-000000000103');
 if v_retry is distinct from v_grade then
   raise exception 'grade retry did not return same id';
 end if;
 begin
  perform public.university_approve_grade(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',v_submission,80,
    'aaaa0000-0000-4000-8000-000000000103');
  raise exception 'changed grade score accepted';
 exception when unique_violation then null;
 end;
 begin
  perform public.university_approve_grade(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',v_submission,101,
    'aaaa0000-0000-4000-8000-000000000104');
  raise exception 'grade outside 100 accepted';
 exception when invalid_parameter_value then null;
 end;
end $$;
reset role;
do $$
begin
 begin
  update public.grades set score=20 where institution_id=
     'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa' and
     submission_id=(select id from public.submissions where
       idempotency_key='aaaa0000-0000-4000-8000-000000000093');
  raise exception 'approved grade was mutable';
 exception when check_violation then null;
 end;
 if (select count(*) from public.audit_events where
    institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa' and
    action='GradeApproved' and
    request_id='aaaa0000-0000-4000-8000-000000000103') <> 1 then
  raise exception 'grade not audited exactly once';
 end if;
end $$;

-- U03 enrollment authority: local synthetic users only, fully rolled back.
-- A2 is initially empty; all real production client mutations stay forbidden.
set local role authenticated;
select set_config('request.jwt.claim.sub','44444444-4444-4444-8444-444444444444',true);
do $$
begin
  begin
    perform public.university_enroll_student(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'aaaa0000-0000-4000-8000-000000000012',
      '44444444-4444-4444-8444-444444444444',
      'aaaa0000-0000-4000-8000-000000000107');
    raise exception 'enrollment_student_rejected';
  exception when insufficient_privilege then null;
  end;
end $$;
select set_config('request.jwt.claim.sub','22222222-2222-4222-8222-222222222222',true);
do $$
begin
  begin
    perform public.university_enroll_student(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'aaaa0000-0000-4000-8000-000000000012',
      '44444444-4444-4444-8444-444444444444',
      'aaaa0000-0000-4000-8000-000000000108');
    raise exception 'enrollment_cross_tenant_rejected';
  exception when insufficient_privilege then null;
  end;
end $$;
select set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
do $$
declare
  first_result text;
  retry_result text;
begin
  first_result := public.university_enroll_student(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'aaaa0000-0000-4000-8000-000000000012',
    '44444444-4444-4444-8444-444444444444',
    'aaaa0000-0000-4000-8000-000000000109');
  if first_result is distinct from 'Enrolled' then
    raise exception 'enrollment_admin_pass';
  end if;
  retry_result := public.university_enroll_student(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'aaaa0000-0000-4000-8000-000000000012',
    '44444444-4444-4444-8444-444444444444',
    'aaaa0000-0000-4000-8000-000000000109');
  if retry_result is distinct from 'AlreadyEnrolled' then
    raise exception 'enrollment exact retry not idempotent';
  end if;
  begin
    perform public.university_enroll_student(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'aaaa0000-0000-4000-8000-000000000011',
      '44444444-4444-4444-8444-444444444444',
      'aaaa0000-0000-4000-8000-000000000109');
    raise exception 'enrollment conflict replay accepted';
  exception when unique_violation then null;
  end;
end $$;
reset role;
do $$
begin
  if (select count(*) from public.enrollments
    where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
      and section_id='aaaa0000-0000-4000-8000-000000000012'
      and student_user_id='44444444-4444-4444-8444-444444444444'
      and state='active') <> 1 then
    raise exception 'student not enrolled in target section';
  end if;
  if (select count(*) from public.audit_events
      where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
        and action='EnrollmentCreated'
        and request_id='aaaa0000-0000-4000-8000-000000000109') <> 1 then
    raise exception 'enrollment audit not exactly once';
  end if;
end $$;

-- U03 course creation: one academic tenant, no direct client INSERT.
set local role authenticated;
select set_config('request.jwt.claim.sub','44444444-4444-4444-8444-444444444444',true);
do $$
begin
  begin
    perform public.university_create_course(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'CHEM999','Advanced Chemistry','2026F',
      'aaaa0000-0000-4000-8000-000000000110');
    raise exception 'course_student_rejected';
  exception when insufficient_privilege then null;
  end;
end $$;
select set_config('request.jwt.claim.sub','22222222-2222-4222-8222-222222222222',true);
do $$
begin
  begin
    perform public.university_create_course(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'CHEM999','Advanced Chemistry','2026F',
      'aaaa0000-0000-4000-8000-000000000111');
    raise exception 'course_cross_tenant_rejected';
  exception when insufficient_privilege then null;
  end;
end $$;
select set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
do $$
declare
  created uuid;
  retry uuid;
begin
  created := public.university_create_course(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'CHEM999','Advanced Chemistry','2026F',
    'aaaa0000-0000-4000-8000-000000000112');
  if created is null then raise exception 'course_admin_pass'; end if;
  retry := public.university_create_course(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'CHEM999','Advanced Chemistry','2026F',
    'aaaa0000-0000-4000-8000-000000000112');
  if retry is distinct from created then
    raise exception 'course exact retry not idempotent';
  end if;
  begin
    perform public.university_create_course(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'CHEM999','Renamed Chemistry','2026F',
      'aaaa0000-0000-4000-8000-000000000112');
    raise exception 'Changed-name course replay accepted';
  exception when unique_violation then null;
  end;
end $$;
reset role;
do $$
begin
  if (select count(*) from public.courses
    where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
      and code='CHEM999' and name='Advanced Chemistry'
      and term='2026F') <> 1 then
    raise exception 'Course was not created in tenant';
  end if;
  if (select count(*) from public.audit_events where
    request_id='aaaa0000-0000-4000-8000-000000000112'
    and action='CourseCreated') <> 1 then
    raise exception 'Course created without exactly one audit event';
  end if;
end $$;


-- U03 Section creation: one parent course and same-tenant admin.
set local role authenticated;
select set_config('request.jwt.claim.sub','44444444-4444-4444-8444-444444444444',true);
do $$
begin
  begin
    perform public.university_create_section(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'aaaa0000-0000-4000-8000-000000000001',
      'S2','aaaa0000-0000-4000-8000-000000000113');
    raise exception 'section_student_rejected';
  exception when insufficient_privilege then null;
  end;
end $$;
select set_config('request.jwt.claim.sub','22222222-2222-4222-8222-222222222222',true);
do $$
begin
  begin
    perform public.university_create_section(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'aaaa0000-0000-4000-8000-000000000001',
      'S2','aaaa0000-0000-4000-8000-000000000114');
    raise exception 'section_cross_tenant_rejected';
  exception when insufficient_privilege then null;
  end;
end $$;
select set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
do $$
declare
  v_course uuid;
  v_section uuid;
  v_retry uuid;
begin
  select id into strict v_course from public.courses
    where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
      and code='CHEM999' and term='2026F';
  v_section := public.university_create_section(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',v_course,'S2',
    'aaaa0000-0000-4000-8000-000000000115');
  if v_section is null then raise exception 'section_admin_pass'; end if;
  v_retry := public.university_create_section(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',v_course,'S2',
    'aaaa0000-0000-4000-8000-000000000115');
  if v_retry is distinct from v_section then
    raise exception 'section retry altered id';
  end if;
  begin
    perform public.university_create_section(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',v_course,'S3',
      'aaaa0000-0000-4000-8000-000000000115');
    raise exception 'Conflicting section replay accepted';
  exception when unique_violation then null;
  end;
end $$;
reset role;
do $$
begin
  if (select count(*) from public.course_sections s
      join public.courses c on c.id=s.course_id
      where s.institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
        and s.section_code='S2' and c.code='CHEM999') <> 1 then
    raise exception 'Missing authorized section';
  end if;
  if (select count(*) from public.audit_events where
      action='SectionCreated'
      and request_id='aaaa0000-0000-4000-8000-000000000115') <> 1 then
    raise exception 'Section audit missing';
  end if;
end $$;

-- Instructor assignment to section A2: existing verified A instructor 333.
set local role authenticated;
select set_config('request.jwt.claim.sub','44444444-4444-4444-8444-444444444444',true);
do $$
begin
  begin
    perform public.university_assign_section_staff(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'aaaa0000-0000-4000-8000-000000000012',
      '33333333-3333-4333-8333-333333333333','instructor',
      'aaaa0000-0000-4000-8000-000000000116');
    raise exception 'staff_student_rejected';
  exception when insufficient_privilege then null;
  end;
end $$;
select set_config('request.jwt.claim.sub','22222222-2222-4222-8222-222222222222',true);
do $$
begin
  begin
    perform public.university_assign_section_staff(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'aaaa0000-0000-4000-8000-000000000012',
      '33333333-3333-4333-8333-333333333333','instructor',
      'aaaa0000-0000-4000-8000-000000000117');
    raise exception 'staff_cross_tenant_rejected';
  exception when insufficient_privilege then null;
  end;
end $$;
select set_config('request.jwt.claim.sub','11111111-1111-4111-8111-111111111111',true);
do $$
declare accepted text; repeated text;
begin
  accepted := public.university_assign_section_staff(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'aaaa0000-0000-4000-8000-000000000012',
    '33333333-3333-4333-8333-333333333333','instructor',
    'aaaa0000-0000-4000-8000-000000000118');
  if accepted is distinct from 'Assigned' then
    raise exception 'staff_admin_pass';
  end if;
  repeated := public.university_assign_section_staff(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    'aaaa0000-0000-4000-8000-000000000012',
    '33333333-3333-4333-8333-333333333333','instructor',
    'aaaa0000-0000-4000-8000-000000000118');
  if repeated is distinct from 'AlreadyAssigned' then
    raise exception 'staff exact retry changed result';
  end if;
  begin
    perform public.university_assign_section_staff(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      'aaaa0000-0000-4000-8000-000000000012',
      '33333333-3333-4333-8333-333333333333','teaching_assistant',
      'aaaa0000-0000-4000-8000-000000000119');
    raise exception 'role mismatch accepted';
  exception when insufficient_privilege then null;
  end;
end $$;
reset role;
do $$
begin
  if (select count(*) from public.section_staff where
    institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
    and section_id='aaaa0000-0000-4000-8000-000000000012'
    and user_id='33333333-3333-4333-8333-333333333333'
    and role='instructor') <> 1 then
    raise exception 'Staff assignment missing';
  end if;
  if (select count(*) from public.audit_events
    where request_id='aaaa0000-0000-4000-8000-000000000118'
    and action='SectionStaffAssigned') <> 1 then
    raise exception 'Staff assignment unaudited';
  end if;
end $$;

-- U03 immutable instructor feedback, on the already-submitted A student attempt.
set local role authenticated;
select set_config('request.jwt.claim.sub','44444444-4444-4444-8444-444444444444',true);
do $
declare sid uuid;
begin
  select id into strict sid from public.submissions
   where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
     and idempotency_key='aaaa0000-0000-4000-8000-000000000093';
  begin
    perform public.university_record_feedback(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',sid,
      'The titration endpoint is correct.',true,
      'aaaa0000-0000-4000-8000-000000000120');
    raise exception 'feedback_student_rejected';
  exception when insufficient_privilege then null;
  end;
end $;
select set_config('request.jwt.claim.sub','22222222-2222-4222-8222-222222222222',true);
do $
declare sid uuid;
begin
  select id into strict sid from public.submissions
   where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
     and idempotency_key='aaaa0000-0000-4000-8000-000000000093';
  begin
    perform public.university_record_feedback(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',sid,
      'Foreign review is forbidden.',true,
      'aaaa0000-0000-4000-8000-000000000121');
    raise exception 'feedback_foreign_tenant_rejected';
  exception when insufficient_privilege then null;
  end;
end $;
select set_config('request.jwt.claim.sub','33333333-3333-4333-8333-333333333333',true);
do $
declare sid uuid; first_id uuid; retry_id uuid;
begin
  select id into strict sid from public.submissions
   where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
     and idempotency_key='aaaa0000-0000-4000-8000-000000000093';
  first_id := public.university_record_feedback(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',sid,
    'The titration endpoint is correct.',true,
    'aaaa0000-0000-4000-8000-000000000122');
  if first_id is null then raise exception 'feedback_instructor_accepted'; end if;
  retry_id := public.university_record_feedback(
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',sid,
    'The titration endpoint is correct.',true,
    'aaaa0000-0000-4000-8000-000000000122');
  if first_id is distinct from retry_id then raise exception 'feedback_retry_same_id'; end if;
  begin
    perform public.university_record_feedback(
      'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',sid,
      'A different comment.',true,
      'aaaa0000-0000-4000-8000-000000000122');
    raise exception 'feedback_changed_payload_rejected';
  exception when unique_violation then null;
  end;
end $;
reset role;
do $
declare fb uuid;
begin
 select f.id into strict fb from public.feedback f
 join public.audit_events a on a.target_id=f.id
 where a.request_id='aaaa0000-0000-4000-8000-000000000122';
 begin
  update public.feedback set body='tampered' where id=fb;
  raise exception 'feedback_immutable';
 exception when check_violation then null;
 end;
 if (select count(*) from public.audit_events
   where institution_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
   and request_id='aaaa0000-0000-4000-8000-000000000122'
   and action='FeedbackRecorded') <> 1 then
   raise exception 'feedback_audited_once';
 end if;
end $;

rollback;
-- Every DO block reaching here means the tested SQL checks passed.
