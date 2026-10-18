-- U03 active enrollment: a verified university admin explicitly admits
-- an existing, email-confirmed student into a same-tenant section.
begin;
create function plv_private.university_enroll_student(
 p_institution_id uuid, p_section_id uuid, p_student_id uuid, p_request_id uuid
) returns text language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := (select auth.uid());
 v_old public.audit_events%rowtype;
begin
 if v_actor is null or p_institution_id is null or p_section_id is null
    or p_student_id is null or p_request_id is null
    or p_request_id = '00000000-0000-0000-0000-000000000000'::uuid then
   raise exception 'Missing enrollment identity' using errcode='22023';
 end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
   raise exception 'Active university admin required' using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m
   where m.institution_id=p_institution_id and m.user_id=v_actor
     and m.role='university_admin' and m.status='active' for share;
 if not found then raise exception 'Admin role changed' using errcode='42501'; end if;
 perform 1 from public.course_sections s
   where s.institution_id = p_institution_id and s.id = p_section_id
   for share;
 if not found then raise exception 'Section absent from tenant' using errcode='42501'; end if;
 perform 1 from public.institution_memberships m
   join auth.users u on u.id=m.user_id
   where m.institution_id=p_institution_id and m.user_id=p_student_id
     and m.role = 'student' and m.status = 'active'
     and u.email_confirmed_at is not null for share of m;
 if not found then raise exception 'Active verified student required' using errcode='42501'; end if;
 select * into v_old from public.audit_events
    where institution_id=p_institution_id and request_id=p_request_id;
 if found then
   if v_old.action='EnrollmentCreated' and v_old.actor_id=v_actor
      and v_old.target_type='enrollments' and v_old.target_id=p_student_id
      and v_old.details->>'sectionId'=p_section_id::text
      and exists(select 1 from public.enrollments e where
        e.institution_id=p_institution_id and e.section_id=p_section_id
        and e.student_user_id=p_student_id and e.state='active') then
       return 'AlreadyEnrolled';
   end if;
   raise exception 'Enrollment request replay mismatch' using errcode='23505';
 end if;
 insert into public.enrollments(institution_id,section_id,student_user_id,state)
    values(p_institution_id,p_section_id,p_student_id,'active');
 insert into public.audit_events(
    institution_id,actor_id,action,target_type,target_id,request_id,details
 ) values(
    p_institution_id,v_actor,'EnrollmentCreated','enrollments',
    p_student_id,p_request_id,
    pg_catalog.jsonb_build_object('sectionId',p_section_id::text,
      'studentId',p_student_id::text)
 );
 return 'Enrolled';
end;
$$;
revoke all on function plv_private.university_enroll_student(uuid,uuid,uuid,uuid)
 from public,anon,authenticated;
grant execute on function plv_private.university_enroll_student(uuid,uuid,uuid,uuid)
 to authenticated;
create function public.university_enroll_student(
 p_institution_id uuid,p_section_id uuid,p_student_id uuid,p_request_id uuid
) returns text language sql security invoker set search_path = '' as $$
 select plv_private.university_enroll_student($1,$2,$3,$4);
$$;
revoke all on function public.university_enroll_student(uuid,uuid,uuid,uuid)
 from public,anon,authenticated;
grant execute on function public.university_enroll_student(uuid,uuid,uuid,uuid)
 to authenticated;
commit;
