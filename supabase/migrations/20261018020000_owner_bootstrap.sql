-- U02 trusted owner bootstrap for a FIRST administrator in an existing
-- email-verified Supabase Auth account. No client, service_role or public RPC.
-- Deployment creates ONLY the callable private function, no tenant/users.
begin;
create function plv_private.owner_bootstrap_institution(
  p_slug text, p_name text, p_admin_user_id uuid, p_request_id uuid
) returns uuid language plpgsql security invoker set search_path = '' as $$
declare
  v_institution_id uuid;
begin
  -- Never trust user-supplied role labels or self-appointed administrators.
  -- This path is callable only by project SQL owner/postgres, not by API.
  if current_user <> 'postgres' then
    raise exception 'Owner SQL role required' using errcode = '42501';
  end if;
  if p_slug is null or p_slug !~ '^[a-z0-9][a-z0-9-]{1,62}$'
    or p_name is null or length(btrim(p_name)) not between 2 and 200
    or p_admin_user_id is null
    or p_admin_user_id = '00000000-0000-0000-0000-000000000000'::uuid
    or p_request_id is null
    or p_request_id = '00000000-0000-0000-0000-000000000000'::uuid then
    raise exception 'Invalid owner bootstrap request' using errcode = '22023';
  end if;
  -- A project owner account is NOT implicitly an auth.users identity.
  -- Sign up and verify email in the application before owner bootstrap.
  perform 1 from auth.users u
    where u.id = p_admin_user_id and u.email_confirmed_at is not null
    for share;
  if not found then
    raise exception 'Verified Supabase Auth user required' using errcode = '42501';
  end if;

  -- An existing institution slug is never silently changed or remapped.
  insert into public.institutions(slug,name,status)
    values (p_slug,btrim(p_name),'active')
    returning id into v_institution_id;
  insert into public.institution_memberships(
    institution_id,user_id,role,status,invited_by
  ) values (v_institution_id,p_admin_user_id,'university_admin','active',null);
  insert into public.audit_events(
    institution_id,actor_id,action,target_type,target_id,request_id,details
  ) values (
    v_institution_id,null,'InstitutionBootstrap','institutions',
    v_institution_id,p_request_id,
    pg_catalog.jsonb_build_object('bootstrap','owner_postgres',
      'adminUserId',p_admin_user_id::text,'initialRole','university_admin')
  );
  return v_institution_id;
end;
$$;
-- PostgreSQL grants: the function is NOT exposed via Data API.
-- The owning postgres role retains its implicit ability to execute.
revoke all on function plv_private.owner_bootstrap_institution(text,text,uuid,uuid)
  from public, anon, authenticated, service_role;
commit;
