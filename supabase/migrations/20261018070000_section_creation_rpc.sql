-- U03 university-admin-only section creation for existing tenant course.
begin;
create function plv_private.university_create_section(
 p_institution_id uuid,p_course_id uuid,p_section_code text,p_request_id uuid
) returns uuid language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := (select auth.uid());
 v_prior public.audit_events%rowtype;
 v_created uuid;
begin
 if v_actor is null or p_institution_id is null or p_course_id is null
    or p_request_id is null
    or p_request_id='00000000-0000-0000-0000-000000000000'::uuid
    or p_section_code is null
    or p_section_code !~ '^[A-Z0-9][A-Z0-9_-]{0,19}$' then
   raise exception 'Invalid section identity' using errcode='22023';
 end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
   raise exception 'University administrator required' using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m
   where m.institution_id=p_institution_id and m.user_id=v_actor
     and m.role='university_admin' and m.status='active' for share;
 if not found then raise exception 'Administrator is inactive' using errcode='42501'; end if;
 perform 1 from public.courses c
   where c.institution_id=p_institution_id and c.id=p_course_id for share;
 if not found then
   raise exception 'No such course in this institution' using errcode='42501';
 end if;
 select * into v_prior from public.audit_events
   where institution_id=p_institution_id and request_id=p_request_id;
 if found then
   if v_prior.action='SectionCreated' and v_prior.actor_id=v_actor
      and v_prior.target_type='course_sections'
      and v_prior.details->>'courseId'=p_course_id::text
      and v_prior.details->>'sectionCode'=p_section_code
      and exists(select 1 from public.course_sections s
        where s.id=v_prior.target_id and s.institution_id=p_institution_id
          and s.course_id=p_course_id and s.section_code=p_section_code) then
     return v_prior.target_id;
   end if;
   raise exception 'Section request replay conflict' using errcode='23505';
 end if;
 insert into public.course_sections(institution_id,course_id,section_code)
   values(p_institution_id,p_course_id,p_section_code)
   returning id into v_created;
 insert into public.audit_events(
    institution_id,actor_id,action,target_type,target_id,request_id,details
 ) values (
    p_institution_id,v_actor,'SectionCreated','course_sections',v_created,
    p_request_id,pg_catalog.jsonb_build_object(
      'courseId',p_course_id::text,'sectionCode',p_section_code)
 );
 return v_created;
end;
$$;
revoke all on function plv_private.university_create_section(uuid,uuid,text,uuid)
 from public,anon,authenticated;
grant execute on function plv_private.university_create_section(uuid,uuid,text,uuid)
 to authenticated;
create function public.university_create_section(
 p_institution_id uuid,p_course_id uuid,p_section_code text,p_request_id uuid
) returns uuid language sql security invoker set search_path = '' as $$
 select plv_private.university_create_section($1,$2,$3,$4);
$$;
revoke all on function public.university_create_section(uuid,uuid,text,uuid)
 from public,anon,authenticated;
grant execute on function public.university_create_section(uuid,uuid,text,uuid)
 to authenticated;
commit;
