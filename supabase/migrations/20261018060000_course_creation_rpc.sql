-- U03 course creation; academic data is server-controlled and tenant-local.
begin;
create function plv_private.university_create_course(
 p_institution_id uuid,p_code text,p_name text,p_term text,p_request_id uuid
) returns uuid language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := (select auth.uid());
 v_prior public.audit_events%rowtype;
 v_created uuid;
begin
 if v_actor is null or p_institution_id is null or p_request_id is null
    or p_request_id = '00000000-0000-0000-0000-000000000000'::uuid
    or p_code is null or p_code !~ '^[A-Z0-9][A-Z0-9_-]{0,39}$'
    or p_term is null or p_term !~ '^[A-Za-z0-9_-]{1,32}$'
    or p_name is null or length(btrim(p_name)) not between 2 and 200 then
   raise exception 'Invalid university course identity' using errcode='22023';
 end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
   raise exception 'Active institution admin required' using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m
   where m.institution_id=p_institution_id and m.user_id=v_actor
     and m.role = 'university_admin' and m.status='active' for share;
 if not found then raise exception 'Institution admin changed' using errcode='42501'; end if;
 select * into v_prior from public.audit_events
   where institution_id=p_institution_id and request_id=p_request_id;
 if found then
   if v_prior.action='CourseCreated' and v_prior.actor_id=v_actor
      and v_prior.target_type='courses'
      and v_prior.details->>'code'=p_code
      and v_prior.details->>'name'=btrim(p_name)
      and v_prior.details->>'term'=p_term
      and exists(select 1 from public.courses c where
        c.institution_id=p_institution_id and c.id=v_prior.target_id
        and c.code=p_code and c.name=btrim(p_name)
        and c.term=p_term and c.owner_user_id=v_actor) then
     return v_prior.target_id;
   end if;
   raise exception 'Course request identity conflicts with prior command'
     using errcode='23505';
 end if;
 insert into public.courses(institution_id,code,name,term,owner_user_id)
 values(p_institution_id,p_code,btrim(p_name),p_term,v_actor)
 returning id into v_created;
 insert into public.audit_events(
   institution_id,actor_id,action,target_type,target_id,request_id,details
 ) values(p_institution_id,v_actor,'CourseCreated','courses',v_created,
   p_request_id,pg_catalog.jsonb_build_object(
     'code',p_code,'name',btrim(p_name),'term',p_term));
 return v_created;
end;
$$;
revoke all on function plv_private.university_create_course(uuid,text,text,text,uuid)
 from public,anon,authenticated;
grant execute on function plv_private.university_create_course(uuid,text,text,text,uuid)
 to authenticated;
create function public.university_create_course(
 p_institution_id uuid,p_code text,p_name text,p_term text,p_request_id uuid
) returns uuid language sql security invoker set search_path = '' as $$
 select plv_private.university_create_course($1,$2,$3,$4,$5);
$$;
revoke all on function public.university_create_course(uuid,text,text,text,uuid)
 from public,anon,authenticated;
grant execute on function public.university_create_course(uuid,text,text,text,uuid)
 to authenticated;
commit;
