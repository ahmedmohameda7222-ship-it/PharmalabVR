-- U03: one instructor-approved, immutable, percentage grade per submission.
-- Never use a client role claim as grade authorization. This migration creates
-- a private SECURITY DEFINER implementation and public INVOKER delegate only.
begin;
create function plv_private.reject_approved_grade_change()
returns trigger language plpgsql set search_path = '' as $$
begin
  if old.status = 'approved' then
    raise exception 'Approved academic grades are immutable'
      using errcode = '23514';
  end if;
  return new;
end;
$$;
create trigger immutable_approved_grade
before update or delete on public.grades
for each row execute function plv_private.reject_approved_grade_change();

create function plv_private.university_approve_grade(
 p_institution_id uuid, p_submission_id uuid, p_score numeric, p_request_id uuid
) returns uuid language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := (select auth.uid());
 v_submission public.submissions%rowtype;
 v_audit public.audit_events%rowtype;
 v_grade public.grades%rowtype;
 v_approved_grade_id uuid;
begin
 if p_institution_id is null or p_submission_id is null or v_actor is null or
    p_request_id is null or
    p_request_id='00000000-0000-0000-0000-000000000000'::uuid or
    p_score is null or p_score::text = 'NaN' or
    p_score < 0 or p_score > 100 or
    pg_catalog.round(p_score,3) <> p_score then
   raise exception 'Invalid score or grade approval request'
     using errcode='22023';
 end if;

 -- An active verified institution and locked instructor/admin membership
 -- are required even when the caller knows a valid submission UUID.
 if plv_private.active_member(p_institution_id) is distinct from true then
   raise exception 'Active verified membership required'
     using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m
   where m.institution_id=p_institution_id and m.user_id=v_actor
     and m.status='active' and m.role in ('university_admin','instructor')
   for share;
 if not found then
   raise exception 'Instructor or university admin required'
     using errcode='42501';
 end if;
 select * into v_submission from public.submissions s
   where s.institution_id=p_institution_id and s.id=p_submission_id
   for share;
 if not found then
   raise exception 'Submission missing in selected university'
     using errcode='42501';
 end if;

 -- A course instructor is only entitled to their assigned section. Teaching
 -- assistants must not approve grades even when they can view submissions.
 if not plv_private.is_admin(p_institution_id) then
   perform 1 from public.section_staff s
     where s.institution_id=p_institution_id
       and s.section_id=v_submission.section_id
       and s.user_id=v_actor and s.role = 'instructor'
     for share;
   if not found then
     raise exception 'Active instructor assignment to section required'
       using errcode='42501';
   end if;
 end if;

 perform 1 from public.assignments a
    where a.id=v_submission.assignment_id
      and a.institution_id=p_institution_id
      and a.section_id=v_submission.section_id
      and a.published_at is not null
    for share;
 if not found then
   raise exception 'Submission assignment was not published'
     using errcode='42501';
 end if;
 perform 1 from public.rubric_versions r
    where r.institution_id=p_institution_id
      and r.id=v_submission.rubric_version_id
      and r.published_at is not null
    for share;
 if not found or v_submission.rubric_version_id is null then
   raise exception 'Published rubric version is required'
     using errcode='42501';
 end if;

 select * into v_audit from public.audit_events
   where institution_id=p_institution_id and request_id=p_request_id;
 if found then
   select * into v_grade from public.grades g
      where g.id=v_audit.target_id and g.institution_id=p_institution_id
        and g.submission_id=p_submission_id
        and g.status='approved' and g.score=p_score;
   if v_audit.action='GradeApproved' and
      v_audit.target_type='grades' and v_audit.actor_id=v_actor and
      v_audit.details->>'score' = p_score::text and
      found then
     return v_grade.id;
   end if;
   raise exception 'Grade request ID reused with differing payload'
      using errcode='23505';
 end if;

 insert into public.grades(institution_id,submission_id,rubric_version_id,
    grader_id,status,score)
 values(p_institution_id,p_submission_id,v_submission.rubric_version_id,
    v_actor,'approved',p_score)
 on conflict (institution_id,submission_id) do nothing
 returning id into v_approved_grade_id;
 if v_approved_grade_id is null then
   raise exception 'Grade already exists: changes require audited appeal'
     using errcode='23505';
 end if;

 insert into public.audit_events(institution_id,actor_id,action,target_type,
   target_id,request_id,details)
 values(p_institution_id,v_actor,'GradeApproved','grades',
   v_approved_grade_id,p_request_id,
   pg_catalog.jsonb_build_object('submissionId',p_submission_id::text,
     'rubricVersionId',v_submission.rubric_version_id::text,
     'score',p_score::text));
 return v_approved_grade_id;
end;
$$;

revoke all on function plv_private.university_approve_grade(uuid,uuid,numeric,uuid)
 from public, anon, authenticated;
grant execute on function plv_private.university_approve_grade(uuid,uuid,numeric,uuid)
 to authenticated;

create function public.university_approve_grade(
 p_institution_id uuid, p_submission_id uuid, p_score numeric, p_request_id uuid
) returns uuid language sql security invoker set search_path = '' as $$
 select plv_private.university_approve_grade($1,$2,$3,$4);
$$;
revoke all on function public.university_approve_grade(uuid,uuid,numeric,uuid)
 from public, anon, authenticated;
grant execute on function public.university_approve_grade(uuid,uuid,numeric,uuid)
 to authenticated;
commit;
