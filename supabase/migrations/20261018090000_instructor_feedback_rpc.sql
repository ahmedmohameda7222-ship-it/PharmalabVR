-- U03: staff-only append-only feedback on a real immutable submission.
-- Explicitly separate release-to-student from grades and Native evidence.
begin;
create function plv_private.immutable_instructor_feedback()
returns trigger language plpgsql set search_path = '' as $function$
begin
  raise exception 'Instructor feedback is immutable; publish a new feedback entry'
    using errcode='23514';
end;
$function$;
create trigger immutable_instructor_feedback
before update or delete on public.feedback
for each row execute function plv_private.immutable_instructor_feedback();

create function plv_private.university_record_feedback(
 p_institution_id uuid, p_submission_id uuid, p_body text,
 p_student_visible boolean, p_request_id uuid
) returns uuid language plpgsql security definer set search_path = '' as $function$
declare
 v_actor uuid := (select auth.uid());
 v_submission public.submissions%rowtype;
 v_prior public.audit_events%rowtype;
 v_id uuid;
begin
 if v_actor is null or p_institution_id is null or p_submission_id is null
    or p_request_id is null
    or p_request_id='00000000-0000-0000-0000-000000000000'::uuid
    or p_student_visible is null or p_body is null
    or length(btrim(p_body)) < 1 or length(p_body) > 12000 then
   raise exception 'Invalid academic feedback input' using errcode='22023';
 end if;
 -- Fresh verified Auth and normalized active role, never user_metadata.
 if plv_private.active_member(p_institution_id) is distinct from true then
   raise exception 'Active verified university membership required'
     using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m
  where m.institution_id=p_institution_id and m.user_id=v_actor
    and m.status='active' and m.role in ('university_admin','instructor')
  for share;
 if not found then
   raise exception 'Instructor or university admin role required'
     using errcode='42501';
 end if;
 select * into v_submission from public.submissions s
  where s.institution_id=p_institution_id and s.id=p_submission_id
  for share;
 if not found then
   raise exception 'Submission not found within institution'
     using errcode='42501';
 end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
   perform 1 from public.section_staff s
    where s.institution_id=p_institution_id and s.section_id=v_submission.section_id
      and s.user_id=v_actor and s.role = 'instructor'
    for share;
   if not found then
     raise exception 'Instructor is not assigned to submission section'
       using errcode='42501';
   end if;
 end if;
 -- Defensive consistency: submitted assignment must remain its published one.
 perform 1 from public.assignments a
  where a.institution_id=p_institution_id
    and a.id=v_submission.assignment_id
    and a.section_id=v_submission.section_id
    and a.published_at is not null
  for share;
 if not found then
   raise exception 'Submission has no published assignment'
     using errcode='42501';
 end if;
 -- An immutable audit request ID is the concurrency/idempotency fence.
 select * into v_prior from public.audit_events a
   where a.institution_id=p_institution_id and a.request_id=p_request_id;
 if found then
   if v_prior.action='FeedbackRecorded'
      and v_prior.actor_id=v_actor and v_prior.target_type='feedback'
      and exists(select 1 from public.feedback f
        where f.institution_id=p_institution_id and f.id=v_prior.target_id
          and f.submission_id=p_submission_id and f.author_id=v_actor
          and f.body=p_body and f.student_visible=p_student_visible) then
      return v_prior.target_id;
   end if;
   raise exception 'Feedback request ID reused for differing content'
     using errcode='23505';
 end if;
 insert into public.feedback(
    institution_id,submission_id,author_id,body,student_visible)
 values(p_institution_id,p_submission_id,v_actor,p_body,p_student_visible)
 returning id into v_id;
 insert into public.audit_events(
    institution_id,actor_id,action,target_type,target_id,request_id,details)
 values(p_institution_id,v_actor,'FeedbackRecorded','feedback',v_id,p_request_id,
    pg_catalog.jsonb_build_object('submissionId',p_submission_id::text,
      'studentVisible',p_student_visible));
 return v_id;
end;
$function$;
revoke all on function plv_private.university_record_feedback(uuid,uuid,text,boolean,uuid)
 from public,anon,authenticated;
grant execute on function plv_private.university_record_feedback(uuid,uuid,text,boolean,uuid)
 to authenticated;

create function public.university_record_feedback(
 p_institution_id uuid, p_submission_id uuid, p_body text,
 p_student_visible boolean, p_request_id uuid
) returns uuid language sql security invoker set search_path = '' as $function$
 select plv_private.university_record_feedback($1,$2,$3,$4,$5);
$function$;
revoke all on function public.university_record_feedback(uuid,uuid,text,boolean,uuid)
 from public,anon,authenticated;
grant execute on function public.university_record_feedback(uuid,uuid,text,boolean,uuid)
 to authenticated;
commit;
