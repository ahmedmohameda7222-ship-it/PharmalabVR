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
 ('66666666-6666-4666-8666-666666666666','00000000-0000-0000-0000-000000000000','authenticated','authenticated','revoked@example.invalid',now());

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
do $
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
end $;

reset role;
rollback;
-- Every DO block reaching here means the tested SQL checks passed.
