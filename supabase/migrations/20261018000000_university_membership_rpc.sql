-- U02: Membership lifecycle for existing, verified Supabase Auth identities.
-- SOURCE ONLY: no PharmaLabVR Supabase project exists; migration NOT APPLIED.
-- Initial tenant university_admin rows are provisioned by a trusted owner
-- migration, never granted through these authenticated RPCs.
begin;

alter table public.audit_events
 add column details jsonb not null default '{}'::jsonb
 check (jsonb_typeof(details) = 'object');
-- One idempotency key may not authorize multiple operations in one tenant.
create unique index audit_events_tenant_request_once
 on public.audit_events(institution_id, request_id);

-- No API INSERT/UPDATE privileges are granted on the academic tables.
-- All three operations are SECURITY DEFINER but check fresh Auth identity,
-- the normalized active membership row and an audited request identity.
create function public.university_invite_member(
 p_institution_id uuid, p_user_id uuid, p_role text, p_request_id uuid
) returns text language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := auth.uid();
 v_prior public.audit_events%rowtype;
begin
 if v_actor is null or p_institution_id is null or p_user_id is null or
    p_request_id is null or p_request_id =
      '00000000-0000-0000-0000-000000000000'::uuid or
    v_actor = p_user_id then
   raise exception 'Invalid university invitation identity'
     using errcode = '42501';
 end if;
 -- Admin role is NEVER available via university-admin invitations.
 if p_role is null or p_role not in
    ('instructor','teaching_assistant','student') then
   raise exception 'invitation role is not permitted'
     using errcode = '42501';
 end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
   raise exception 'active university admin is required'
     using errcode = '42501';
 end if;
 -- Lock the caller's own administrative membership until the transaction
 -- commits. Concurrent revocation cannot silently race this mutation.
 perform 1 from public.institution_memberships m
   where m.institution_id = p_institution_id and m.user_id = v_actor
     and m.status = 'active' and m.role = 'university_admin'
   for update;
 if not found then
   raise exception 'admin membership no longer active'
     using errcode = '42501';
 end if;
 select * into v_prior from public.audit_events
   where institution_id = p_institution_id and request_id = p_request_id;
 if found then
   if v_prior.action = 'MembershipInvite' and v_prior.actor_id = v_actor
      and v_prior.target_type = 'institution_memberships'
      and v_prior.target_id = p_user_id and
      v_prior.details->>'role' = p_role then
     return 'AlreadyInvited';
   end if;
   raise exception 'invitation request identity conflict' using errcode = '23505';
 end if;
 if not exists (select 1 from auth.users u
   where u.id = p_user_id and u.email_confirmed_at is not null) then
   raise exception 'Invitation target must be a verified Auth account'
     using errcode = '22023';
 end if;
 -- Fail closed rather than silently reinviting an active or revoked user.
 -- Separate audited recovery or role changes require explicit later RPCs.
 if exists (select 1 from public.institution_memberships m
   where m.institution_id = p_institution_id and m.user_id = p_user_id) then
   raise exception 'member already exists; invitation cannot change roles'
     using errcode = '23505';
 end if;
 insert into public.institution_memberships
   (institution_id,user_id,role,status,invited_by)
 values (p_institution_id,p_user_id,p_role,'invited',v_actor);
 insert into public.audit_events
   (institution_id,actor_id,action,target_type,target_id,request_id,details)
 values (p_institution_id,v_actor,'MembershipInvite',
   'institution_memberships',p_user_id,p_request_id,
   jsonb_build_object('role',p_role,'newStatus','invited'));
 return 'Invited';
end;
$$;

create function public.university_accept_invite(
 p_institution_id uuid, p_request_id uuid
) returns text language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := auth.uid();
 v_membership public.institution_memberships%rowtype;
 v_prior public.audit_events%rowtype;
begin
 if v_actor is null or p_institution_id is null or p_request_id is null
    or p_request_id = '00000000-0000-0000-0000-000000000000'::uuid then
   raise exception 'Invalid invitation acceptance identity'
     using errcode = '42501';
 end if;
 if not exists (select 1 from auth.users u
    where u.id = v_actor and u.email_confirmed_at is not null) or
    not exists (select 1 from public.institutions i
       where i.id = p_institution_id and i.status = 'active') then
   raise exception 'Verified Auth and an active institution are required'
     using errcode = '42501';
 end if;
 select * into v_membership from public.institution_memberships
   where institution_id = p_institution_id and user_id = v_actor
   for update;
 if not found or v_membership.status not in ('invited','active') then
   raise exception 'No current invitation for this Auth identity'
     using errcode = '42501';
 end if;
 select * into v_prior from public.audit_events
   where institution_id = p_institution_id and request_id = p_request_id;
 if found then
   if v_prior.action = 'MembershipAccept' and v_prior.actor_id = v_actor
     and v_prior.target_id = v_actor and
     v_prior.target_type = 'institution_memberships' and
     v_membership.status = 'active' then
       return 'AlreadyAccepted';
   end if;
   raise exception 'accept request identity conflict' using errcode = '23505';
 end if;
 if v_membership.status <> 'invited' or v_membership.invited_by is null then
   raise exception 'Invitation is absent or has already ended'
     using errcode = '42501';
 end if;
 -- Refuse orphaned invitations issued by an administrator who was revoked.
 if not exists (select 1 from public.institution_memberships m
   where m.institution_id = p_institution_id
     and m.user_id = v_membership.invited_by
     and m.role = 'university_admin' and m.status = 'active') then
   raise exception 'Original inviting administrator is no longer active'
     using errcode = '42501';
 end if;
 update public.institution_memberships
    set status = 'active'
    where institution_id = p_institution_id and user_id = v_actor;
 insert into public.audit_events
   (institution_id,actor_id,action,target_type,target_id,request_id,details)
 values (p_institution_id,v_actor,'MembershipAccept',
   'institution_memberships',v_actor,p_request_id,
   jsonb_build_object('role',v_membership.role,'newStatus','active'));
 return 'Accepted';
end;
$$;

create function public.university_revoke_member(
 p_institution_id uuid, p_user_id uuid, p_request_id uuid
) returns text language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := auth.uid();
 v_membership public.institution_memberships%rowtype;
 v_prior public.audit_events%rowtype;
begin
 if v_actor is null or p_institution_id is null or p_user_id is null or
    p_request_id is null or p_request_id =
      '00000000-0000-0000-0000-000000000000'::uuid
    or p_user_id = v_actor then
   raise exception 'Invalid membership revocation identity'
     using errcode = '42501';
 end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
   raise exception 'active university admin is required'
     using errcode = '42501';
 end if;
 perform 1 from public.institution_memberships m
   where m.institution_id = p_institution_id and m.user_id = v_actor
     and m.role = 'university_admin' and m.status = 'active'
   for update;
 if not found then
   raise exception 'admin membership no longer active'
     using errcode = '42501';
 end if;
 select * into v_membership from public.institution_memberships
   where institution_id = p_institution_id and user_id = p_user_id
   for update;
 if not found or v_membership.role = 'university_admin' then
   -- Administrators are provisioned or revoked only via trusted owner path.
   raise exception 'Membership does not exist or is administrator-protected'
     using errcode = '42501';
 end if;
 select * into v_prior from public.audit_events
   where institution_id = p_institution_id and request_id = p_request_id;
 if found then
   if v_prior.action = 'MembershipRevoke' and v_prior.actor_id = v_actor
      and v_prior.target_type = 'institution_memberships'
      and v_prior.target_id = p_user_id
      and v_membership.status = 'revoked' then
      return 'AlreadyRevoked';
   end if;
   raise exception 'revoke request identity conflict' using errcode = '23505';
 end if;
 if v_membership.status = 'revoked' then
   raise exception 'membership already revoked' using errcode = '42501';
 end if;
 update public.institution_memberships
   set status = 'revoked'
   where institution_id = p_institution_id and user_id = p_user_id;
 insert into public.audit_events
   (institution_id,actor_id,action,target_type,target_id,request_id,details)
 values (p_institution_id,v_actor,'MembershipRevoke',
   'institution_memberships',p_user_id,p_request_id,
   jsonb_build_object('role',v_membership.role,'previousStatus',
     v_membership.status,'newStatus','revoked'));
 return 'Revoked';
end;
$$;

-- PostgreSQL grants, not a frontend role check. The functions are the
-- only Auth-facing membership mutation endpoints in this increment.
revoke all on function public.university_invite_member(uuid,uuid,text,uuid)
 from public, anon, authenticated;
revoke all on function public.university_accept_invite(uuid,uuid)
 from public, anon, authenticated;
revoke all on function public.university_revoke_member(uuid,uuid,uuid)
 from public, anon, authenticated;
grant execute on function public.university_invite_member(uuid,uuid,text,uuid)
 to authenticated;
grant execute on function public.university_accept_invite(uuid,uuid)
 to authenticated;
grant execute on function public.university_revoke_member(uuid,uuid,uuid)
 to authenticated;
commit;
