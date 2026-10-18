-- U04 authenticated student self-reported Native source registration.
-- This creates a pending revision-zero staging identity, NOT an attested
-- Native state or a verified academic event. No direct table writes.
begin;
create function plv_private.university_register_staging_session(
 p_institution_id uuid,p_local_session_id text,p_native_branch_id text,
 p_science_package_sha256 text,p_request_id uuid
) returns uuid language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := (select auth.uid());
 v_prior public.audit_events%rowtype;
 v_session_id uuid;
begin
 if v_actor is null or p_institution_id is null or p_request_id is null
    or p_request_id='00000000-0000-0000-0000-000000000000'::uuid
    or p_local_session_id is null
    or p_local_session_id !~ '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$'
    or p_native_branch_id is null
    or p_native_branch_id !~ '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$'
    or position('..' in p_local_session_id)>0
    or position('..' in p_native_branch_id)>0
    or p_science_package_sha256 is null
    or p_science_package_sha256 !~ '^[0-9a-f]{64}$' then
   raise exception 'Invalid untrusted Native staging identity' using errcode='22023';
 end if;
 if plv_private.active_member(p_institution_id) is distinct from true then
   raise exception 'Active verified university membership required' using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m
   where m.institution_id=p_institution_id and m.user_id=v_actor
     and m.role = 'student' and m.status='active' for share;
 if not found then
   raise exception 'Active student membership required' using errcode='42501';
 end if;
 select * into v_prior from public.audit_events a
   where a.institution_id=p_institution_id and a.request_id=p_request_id;
 if found then
   if v_prior.action='StagingLabSessionCreated' and v_prior.actor_id=v_actor
      and v_prior.target_type='lab_sessions'
      and exists(select 1 from public.lab_sessions s
        where s.id=v_prior.target_id and s.institution_id=p_institution_id
          and s.user_id=v_actor
          and s.local_session_id=p_local_session_id
          and s.native_branch_id=p_native_branch_id
          and s.science_package_sha256=p_science_package_sha256) then
     return v_prior.target_id;
   end if;
   raise exception 'Staging request replay conflicts with original identity'
     using errcode='23505';
 end if;
 insert into public.lab_sessions(
   institution_id,user_id,local_session_id,native_branch_id,
   science_package_sha256,current_revision,sync_state
 ) values(p_institution_id,v_actor,p_local_session_id,p_native_branch_id,
   p_science_package_sha256,0,'pending')
 returning id into v_session_id;
 insert into public.audit_events(
   institution_id,actor_id,action,target_type,target_id,request_id,details
 ) values(p_institution_id,v_actor,'StagingLabSessionCreated',
   'lab_sessions',v_session_id,p_request_id,
   pg_catalog.jsonb_build_object(
     'localSessionId',p_local_session_id,'nativeBranchId',p_native_branch_id,
     'untrustedScientificPackageSha256',p_science_package_sha256,
     'attested',false,'nativeRevision',0));
 return v_session_id;
end;
$$;
revoke all on function plv_private.university_register_staging_session(
 uuid,text,text,text,uuid) from public,anon,authenticated;
grant execute on function plv_private.university_register_staging_session(
 uuid,text,text,text,uuid) to authenticated;
create function public.university_register_staging_session(
 p_institution_id uuid,p_local_session_id text,p_native_branch_id text,
 p_science_package_sha256 text,p_request_id uuid
) returns uuid language sql security invoker set search_path = '' as $$
 select plv_private.university_register_staging_session($1,$2,$3,$4,$5);
$$;
revoke all on function public.university_register_staging_session(
 uuid,text,text,text,uuid) from public,anon,authenticated;
grant execute on function public.university_register_staging_session(
 uuid,text,text,text,uuid) to authenticated;
commit;
