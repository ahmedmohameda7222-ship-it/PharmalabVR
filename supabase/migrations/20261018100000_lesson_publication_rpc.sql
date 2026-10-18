-- U03: publish one canonical-jsonb, hash-verified immutable lesson version.
-- Native science remains locally authoritative; lessons are coursework only.
begin;
create function plv_private.university_publish_lesson_version(
 p_institution_id uuid, p_course_id uuid, p_lesson_id uuid,
 p_version integer, p_content_json jsonb, p_content_sha256 text,
 p_request_id uuid
) returns uuid language plpgsql security definer set search_path = '' as $body$
declare
 v_actor uuid := (select auth.uid());
 v_prior public.audit_events%rowtype;
 v_new_id uuid;
begin
 if v_actor is null or p_institution_id is null or p_course_id is null
    or p_lesson_id is null or p_request_id is null
    or p_request_id = '00000000-0000-0000-0000-000000000000'::uuid
    or p_lesson_id = '00000000-0000-0000-0000-000000000000'::uuid
    or p_version is null or p_version < 1 or p_version > 10000
    or p_content_json is null or jsonb_typeof(p_content_json) <> 'object'
    or length(p_content_json::text) > 131072
    or p_content_sha256 is null
    or p_content_sha256 !~ '^[0-9a-f]{64}$'
    or pg_catalog.encode(
      extensions.digest(p_content_json::text,'sha256'),'hex')
       <> p_content_sha256 then
    raise exception 'Lesson canonical JSON or SHA-256 is invalid'
      using errcode='22023';
 end if;
 if plv_private.active_member(p_institution_id) is distinct from true then
    raise exception 'Active verified institution membership required'
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
 perform 1 from public.courses c
    where c.institution_id=p_institution_id and c.id=p_course_id
    for share;
 if not found then
    raise exception 'Course does not belong to institution'
      using errcode='42501';
 end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
    perform 1 from public.section_staff st
      join public.course_sections cs
        on cs.institution_id=st.institution_id and cs.id=st.section_id
      where st.institution_id=p_institution_id and st.user_id=v_actor
        and st.role = 'instructor' and cs.course_id=p_course_id
      for share of st;
    if not found then
      raise exception 'Instructor is not assigned to course sections'
        using errcode='42501';
    end if;
 end if;
 -- Idempotency is checked against the persisted target and actor, not
 -- only the supplied request UUID or editable user metadata.
 select * into v_prior from public.audit_events
    where institution_id=p_institution_id and request_id=p_request_id;
 if found then
    if v_prior.action='LessonPublished'
       and v_prior.actor_id=v_actor and v_prior.target_type='lesson_versions'
       and exists(select 1 from public.lesson_versions v
          where v.institution_id=p_institution_id and v.id=v_prior.target_id
            and v.course_id=p_course_id and v.lesson_id=p_lesson_id
            and v.version=p_version and v.author_id=v_actor
            and v.content_sha256=p_content_sha256
            and v.content_json=p_content_json and v.published_at is not null) then
       return v_prior.target_id;
    end if;
    raise exception 'Lesson publication request ID reused with new content'
      using errcode='23505';
 end if;
 -- Same lesson identity may only progress through a single course.
 if exists(select 1 from public.lesson_versions v
    where v.institution_id=p_institution_id and v.lesson_id=p_lesson_id
      and v.course_id<>p_course_id) then
    raise exception 'Lesson identity belongs to another course'
      using errcode='42501';
 end if;
 if p_version > 1 and not exists (
    select 1 from public.lesson_versions v
    where v.institution_id=p_institution_id and v.course_id=p_course_id
      and v.lesson_id=p_lesson_id and v.version=p_version-1
      and v.published_at is not null
 ) then
   raise exception 'Previous published version required'
     using errcode='22023';
 end if;
 insert into public.lesson_versions(
   institution_id,course_id,lesson_id,version,author_id,
   content_sha256,content_json,published_at
 ) values(
   p_institution_id,p_course_id,p_lesson_id,p_version,v_actor,
   p_content_sha256,p_content_json,pg_catalog.now()
 ) returning id into v_new_id;
 insert into public.audit_events(
   institution_id,actor_id,action,target_type,target_id,request_id,details
 ) values(
   p_institution_id,v_actor,'LessonPublished','lesson_versions',v_new_id,
   p_request_id,pg_catalog.jsonb_build_object(
    'courseId',p_course_id::text,'lessonId',p_lesson_id::text,
    'version',p_version,'canonicalJsonSha256',p_content_sha256)
 );
 return v_new_id;
end;
$body$;
revoke all on function plv_private.university_publish_lesson_version(
 uuid,uuid,uuid,integer,jsonb,text,uuid) from public,anon,authenticated;
grant execute on function plv_private.university_publish_lesson_version(
 uuid,uuid,uuid,integer,jsonb,text,uuid) to authenticated;
create function public.university_publish_lesson_version(
 p_institution_id uuid, p_course_id uuid, p_lesson_id uuid,
 p_version integer, p_content_json jsonb, p_content_sha256 text,
 p_request_id uuid
) returns uuid language sql security invoker set search_path = '' as $body$
 select plv_private.university_publish_lesson_version($1,$2,$3,$4,$5,$6,$7);
$body$;
revoke all on function public.university_publish_lesson_version(
 uuid,uuid,uuid,integer,jsonb,text,uuid) from public,anon,authenticated;
grant execute on function public.university_publish_lesson_version(
 uuid,uuid,uuid,integer,jsonb,text,uuid) to authenticated;
commit;
