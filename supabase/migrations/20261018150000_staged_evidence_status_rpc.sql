-- U04: authenticated student metadata-only recovery after a lost Edge
-- response. The state is ALWAYS unverified; no signed URL, object key,
-- receipt signature, academic artifact or upload bytes are disclosed.
begin;
create function plv_private.university_staged_evidence_status(
 p_institution_id uuid,p_artifact_id uuid
) returns jsonb language plpgsql security definer set search_path = '' as $body$
declare
 v_actor uuid := (select auth.uid());
 v_artifact record;
begin
 if v_actor is null or p_institution_id is null or p_artifact_id is null then
   raise exception 'Missing staging status identity' using errcode='22023';
 end if;
 if plv_private.active_member(p_institution_id) is distinct from true then
   raise exception 'Active verified institution student required'
     using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m
  where m.institution_id=p_institution_id and m.user_id=v_actor
    and m.role = 'student' and m.status='active'
  for share;
 if not found then
   raise exception 'Active student role required' using errcode='42501';
 end if;
 select e.id,e.sha256,e.size_bytes into v_artifact
 from public.evidence_upload_staging e
 join public.lab_sessions s
   on s.institution_id=e.institution_id and s.id=e.lab_session_id
     and s.user_id=e.student_id
 where e.institution_id=p_institution_id and e.id=p_artifact_id
   and e.student_id=v_actor and s.sync_state='pending';
 if not found then
   -- Identical failure for missing, foreign-tenant and foreign-student IDs:
   -- do not expose existence, bucket path or metadata through errors.
   raise exception 'Staging metadata unavailable for this student'
     using errcode='42501';
 end if;
 return pg_catalog.jsonb_build_object(
   'state','staged_unverified',
   'artifactId',v_artifact.id::text,
   'sha256',v_artifact.sha256,
   'sizeBytes',v_artifact.size_bytes
 );
end;
$body$;
revoke all on function plv_private.university_staged_evidence_status(uuid,uuid)
 from public,anon,authenticated;
grant execute on function plv_private.university_staged_evidence_status(uuid,uuid)
 to authenticated;
create function public.university_staged_evidence_status(
 p_institution_id uuid,p_artifact_id uuid
) returns jsonb language sql security invoker set search_path = '' as $body$
 select plv_private.university_staged_evidence_status($1,$2);
$body$;
revoke all on function public.university_staged_evidence_status(uuid,uuid)
 from public,anon,authenticated;
grant execute on function public.university_staged_evidence_status(uuid,uuid)
 to authenticated;
commit;
