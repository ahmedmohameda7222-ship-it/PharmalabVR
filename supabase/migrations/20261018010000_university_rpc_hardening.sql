-- U02 live security-advisor hardening: keep privileged membership writes
-- in an unexposed schema. PUBLIC RPCs are SECURITY INVOKER delegates only.
-- RED evidence: project hhbnlhceehgbskzdtorn had 3 public prosecdef=true
-- RPCs after the original deployment. No table data is changed.
begin;

alter function public.university_invite_member(uuid,uuid,text,uuid)
  set schema plv_private;
alter function public.university_accept_invite(uuid,uuid)
  set schema plv_private;
alter function public.university_revoke_member(uuid,uuid,uuid)
  set schema plv_private;

-- Only the explicit authenticated role can execute the inner functions.
revoke all on function plv_private.university_invite_member(uuid,uuid,text,uuid)
  from public, anon, authenticated;
revoke all on function plv_private.university_accept_invite(uuid,uuid)
  from public, anon, authenticated;
revoke all on function plv_private.university_revoke_member(uuid,uuid,uuid)
  from public, anon, authenticated;

grant execute on function plv_private.university_invite_member(uuid,uuid,text,uuid)
  to authenticated;
grant execute on function plv_private.university_accept_invite(uuid,uuid)
  to authenticated;
grant execute on function plv_private.university_revoke_member(uuid,uuid,uuid)
  to authenticated;

-- External PostgREST RPC endpoints are deliberately SECURITY INVOKER.
-- No dynamic SQL, credential passing, or caller-provided role escalation.
create function public.university_invite_member(
  p_institution_id uuid, p_user_id uuid, p_role text, p_request_id uuid
) returns text language sql security invoker set search_path = '' as $$
  select plv_private.university_invite_member($1,$2,$3,$4);
$$;

create function public.university_accept_invite(
  p_institution_id uuid, p_request_id uuid
) returns text language sql security invoker set search_path = '' as $$
  select plv_private.university_accept_invite($1,$2);
$$;

create function public.university_revoke_member(
  p_institution_id uuid, p_user_id uuid, p_request_id uuid
) returns text language sql security invoker set search_path = '' as $$
  select plv_private.university_revoke_member($1,$2,$3);
$$;

-- PostgreSQL default EXECUTE for PUBLIC is explicitly removed.
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
