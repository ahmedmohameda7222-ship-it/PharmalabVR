-- U03 bounded student submission ingestion, backed by a pre-attested
-- U04 artifact. Source and schema migrate before client API is enabled.
-- Supabase stores evidence references, NOT Native chemistry or checkpoints.
begin;

-- One immutable Native session branch cannot be resubmitted to the same
-- assignment as a different attempt. A genuine fork needs a new session.
create unique index submissions_assignment_session_once
 on public.submissions(institution_id,assignment_id,student_id,lab_session_id);

create function plv_private.university_submit_attempt(
 p_institution_id uuid, p_assignment_id uuid, p_lab_session_id uuid,
 p_idempotency_key uuid, p_evidence_sha256 text
) returns uuid language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := (select auth.uid());
 v_assignment public.assignments%rowtype;
 v_session public.lab_sessions%rowtype;
 v_prior public.audit_events%rowtype;
 v_existing public.submissions%rowtype;
 v_submission uuid;
begin
 if v_actor is null or p_institution_id is null or p_assignment_id is null or
    p_lab_session_id is null or p_idempotency_key is null or
    p_idempotency_key = '00000000-0000-0000-0000-000000000000'::uuid or
    p_evidence_sha256 is null or
    p_evidence_sha256 !~ '^[0-9a-f]{64}$' then
   raise exception 'Invalid student submission identity or evidence digest'
     using errcode = '22023';
 end if;

 -- A JWT role or tenant ID alone never confers access. Membership and Auth
 -- email verification must be fresh. Check the actual role in this tenant.
 if plv_private.active_member(p_institution_id) is distinct from true then
   raise exception 'Active institution membership required' using errcode = '42501';
 end if;
 perform 1 from public.institution_memberships m
   where m.institution_id = p_institution_id and m.user_id = v_actor
     and m.role = 'student' and m.status = 'active'
   for share;
 if not found then
   raise exception 'Only an active university student may submit'
     using errcode = '42501';
 end if;

 -- An exact retry is safe to replay after the assignment deadline. Revoked
 -- institutional members are still denied even when knowing an old UUID.
 select * into v_prior from public.audit_events
    where institution_id=p_institution_id and request_id=p_idempotency_key;
 if found then
   if v_prior.action = 'SubmissionCreated' and
      v_prior.actor_id = v_actor and
      v_prior.target_type = 'submissions' and
      v_prior.details->>'assignmentId' = p_assignment_id::text and
      v_prior.details->>'sessionId' = p_lab_session_id::text and
      v_prior.details->>'evidenceSha256' = p_evidence_sha256 then
     select * into v_existing from public.submissions s
       where s.id = v_prior.target_id
         and s.institution_id = p_institution_id
         and s.student_id = v_actor
         and s.assignment_id = p_assignment_id
         and s.lab_session_id = p_lab_session_id
         and s.idempotency_key = p_idempotency_key
         and s.evidence_sha256 = p_evidence_sha256;
     if found then
       return v_existing.id;
     end if;
   end if;
   raise exception 'Submission idempotency key reused with different payload'
     using errcode = '23505';
 end if;

 -- Lock the stable row while checking the published curriculum and deadline.
 select * into v_assignment from public.assignments a
   where a.id = p_assignment_id and a.institution_id = p_institution_id
     and a.published_at is not null
     and (a.due_at is null or a.due_at >= pg_catalog.statement_timestamp())
   for share;
 if not found then
   raise exception 'Assignment missing, unpublished or past deadline'
     using errcode = '42501';
 end if;
 if not exists (
   select 1 from public.lesson_versions l
    where l.institution_id=p_institution_id
      and l.id=v_assignment.lesson_version_id and l.published_at is not null
 ) or not exists (
   select 1 from public.rubric_versions r
    where r.institution_id=p_institution_id
      and r.id=v_assignment.rubric_version_id and r.published_at is not null
 ) then
   raise exception 'Assignment references unpublished curriculum'
      using errcode = '42501';
 end if;

 -- The enrollment FK only checks identity, so enforce ACTIVE enrollment
 -- explicitly at submission time as well.
 perform 1 from public.enrollments e
   where e.institution_id=p_institution_id
     and e.section_id=v_assignment.section_id
     and e.student_user_id=v_actor and e.state = 'active'
   for share;
 if not found then
   raise exception 'Active enrollment is required for this section'
     using errcode = '42501';
 end if;

 select * into v_session from public.lab_sessions l
   where l.id = p_lab_session_id and l.institution_id=p_institution_id
     and l.user_id=v_actor
   for share;
 if not found then
   raise exception 'Student does not own a matching Native session'
     using errcode = '42501';
 end if;
 if v_session.current_revision < 1 then
   raise exception 'Native source has no committed event revision'
     using errcode = '42501';
 end if;

 -- U04 server attestation must first create immutable artifact metadata and
 -- a real private Storage object. A client-supplied digest alone is NEVER
 -- enough to create an academic submission. Hash-of-bytes verification is
 -- the responsibility of the future U04 signed upload broker.
 if not exists (
   select 1 from public.session_artifacts s
   join storage.objects o on o.bucket_id='plv-university-evidence'
                         and o.name=s.object_key
   where s.institution_id=p_institution_id
     and s.lab_session_id=p_lab_session_id
     and s.user_id=v_actor and s.submission_id is null
     and s.sha256 = p_evidence_sha256
     and s.content_type in ('application/json','application/zip')
 ) then
   raise exception 'Pre-attested private evidence artifact required'
     using errcode = '42501';
 end if;

 insert into public.submissions(
   institution_id,course_id,section_id,assignment_id,rubric_version_id,
   student_id,lab_session_id,idempotency_key,evidence_sha256
 ) values (
   p_institution_id,v_assignment.course_id,v_assignment.section_id,
   p_assignment_id,v_assignment.rubric_version_id,v_actor,
   p_lab_session_id,p_idempotency_key,p_evidence_sha256
 )
 on conflict (institution_id,student_id,idempotency_key) do nothing
 returning id into v_submission;

 if v_submission is null then
   -- Concurrent identical retries wait on the unique key; check the
   -- committed audit record before returning. Never silently accept a
   -- preexisting row written through a different trusted path.
   select * into v_existing from public.submissions s
      where s.institution_id=p_institution_id
        and s.student_id=v_actor and s.idempotency_key=p_idempotency_key;
   if not found or v_existing.assignment_id is distinct from p_assignment_id
      or v_existing.lab_session_id is distinct from p_lab_session_id
      or v_existing.evidence_sha256 is distinct from p_evidence_sha256 or
      not exists (select 1 from public.audit_events a
        where a.institution_id=p_institution_id and a.request_id=p_idempotency_key
          and a.action='SubmissionCreated' and a.actor_id=v_actor
          and a.target_type='submissions' and a.target_id=v_existing.id) then
     raise exception 'Concurrent submission request conflict'
       using errcode='23505';
   end if;
   return v_existing.id;
 end if;

 insert into public.audit_events(
   institution_id,actor_id,action,target_type,target_id,request_id,details
 ) values (
   p_institution_id,v_actor,'SubmissionCreated','submissions',
   v_submission,p_idempotency_key,
   pg_catalog.jsonb_build_object(
     'assignmentId',p_assignment_id::text,'sessionId',p_lab_session_id::text,
     'evidenceSha256',p_evidence_sha256,
     'nativeRevision',v_session.current_revision,
     'sciencePackageSha256',v_session.science_package_sha256)
 );
 return v_submission;
end;
$$;

revoke all on function plv_private.university_submit_attempt(uuid,uuid,uuid,uuid,text)
 from public, anon, authenticated;
grant execute on function plv_private.university_submit_attempt(uuid,uuid,uuid,uuid,text)
 to authenticated;

-- Public API has no privileged ownership, only a narrow INVOKER delegate.
create function public.university_submit_attempt(
 p_institution_id uuid, p_assignment_id uuid, p_lab_session_id uuid,
 p_idempotency_key uuid, p_evidence_sha256 text
) returns uuid language sql security invoker set search_path = '' as $$
 select plv_private.university_submit_attempt($1,$2,$3,$4,$5);
$$;
revoke all on function public.university_submit_attempt(uuid,uuid,uuid,uuid,text)
 from public, anon, authenticated;
grant execute on function public.university_submit_attempt(uuid,uuid,uuid,uuid,text)
 to authenticated;
commit;
