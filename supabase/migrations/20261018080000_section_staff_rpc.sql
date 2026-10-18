-- U03 section staff assignment: active university admin only, same-tenant.
begin;
create function plv_private.university_assign_section_staff(
 p_institution_id uuid,p_section_id uuid,p_staff_user_id uuid,
 p_staff_role text,p_request_id uuid
) returns text language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := (select auth.uid());
 v_prior public.audit_events%rowtype;
begin
 if v_actor is null or p_institution_id is null or p_section_id is null
    or p_staff_user_id is null or p_request_id is null
    or p_request_id='00000000-0000-0000-0000-000000000000'::uuid
    or p_staff_role is null
    or p_staff_role not in ('instructor','teaching_assistant') then
   raise exception 'Invalid section staff identity' using errcode='22023';
 end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
   raise exception 'Active university admin required' using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m
   where m.institution_id=p_institution_id and m.user_id=v_actor
     and m.role='university_admin' and m.status='active' for share;
 if not found then raise exception 'Admin membership inactive' using errcode='42501'; end if;
 perform 1 from public.course_sections s
   where s.institution_id=p_institution_id and s.id=p_section_id for share;
 if not found then raise exception 'Section is outside institution' using errcode='42501'; end if;
 perform 1 from public.institution_memberships m
   join auth.users u on u.id=m.user_id
   where m.institution_id=p_institution_id and m.user_id=p_staff_user_id
     and m.role = p_staff_role and m.status='active'
     and u.email_confirmed_at is not null for share of m;
 if not found then raise exception 'Verified active staff membership required'
   using errcode='42501';
 end if;
 select * into v_prior from public.audit_events
   where institution_id=p_institution_id and request_id=p_request_id;
 if found then
   if v_prior.action='SectionStaffAssigned' and v_prior.actor_id=v_actor
      and v_prior.target_type='section_staff'
      and v_prior.target_id=p_staff_user_id
      and v_prior.details->>'sectionId'=p_section_id::text
      and v_prior.details->>'role'=p_staff_role
      and exists(select 1 from public.section_staff s
         where s.institution_id=p_institution_id
           and s.section_id=p_section_id and s.user_id=p_staff_user_id
           and s.role=p_staff_role) then
     return 'AlreadyAssigned';
   end if;
   raise exception 'Section staff request identity collision' using errcode='23505';
 end if;
 insert into public.section_staff(institution_id,section_id,user_id,role)
 values(p_institution_id,p_section_id,p_staff_user_id,p_staff_role);
 insert into public.audit_events(institution_id,actor_id,action,target_type,
    target_id,request_id,details)
 values(p_institution_id,v_actor,'SectionStaffAssigned','section_staff',
   p_staff_user_id,p_request_id,
   pg_catalog.jsonb_build_object('sectionId',p_section_id::text,
       'role',p_staff_role));
 return 'Assigned';
end;
$$;
revoke all on function plv_private.university_assign_section_staff(uuid,uuid,uuid,text,uuid)
 from public,anon,authenticated;
grant execute on function plv_private.university_assign_section_staff(uuid,uuid,uuid,text,uuid)
 to authenticated;
create function public.university_assign_section_staff(
 p_institution_id uuid,p_section_id uuid,p_staff_user_id uuid,
 p_staff_role text,p_request_id uuid
) returns text language sql security invoker set search_path = '' as $$
 select plv_private.university_assign_section_staff($1,$2,$3,$4,$5);
$$;
revoke all on function public.university_assign_section_staff(uuid,uuid,uuid,text,uuid)
 from public,anon,authenticated;
grant execute on function public.university_assign_section_staff(uuid,uuid,uuid,text,uuid)
 to authenticated;
commit;
