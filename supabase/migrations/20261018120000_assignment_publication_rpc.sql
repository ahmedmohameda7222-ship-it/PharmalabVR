-- U03 section-bound immutable assignment publication.
begin;
create trigger immutable_published_assignment before update or delete on public.assignments
for each row execute function plv_private.immutable_published();
create function plv_private.university_publish_assignment(
 p_institution_id uuid,p_section_id uuid,p_lesson_version_id uuid,
 p_rubric_version_id uuid,p_title text,p_due_at timestamptz,p_request_id uuid)
returns uuid language plpgsql security definer set search_path = '' as $$
declare
 v_actor uuid := auth.uid();
 v_course uuid;
 v_old public.audit_events%rowtype;
 v_id uuid;
begin
 if v_actor is null or p_institution_id is null or p_section_id is null
 or p_lesson_version_id is null or p_rubric_version_id is null
 or p_title is null or length(btrim(p_title)) not between 2 and 200
 or p_request_id is null or (p_due_at is not null and p_due_at <= now())
 then raise exception 'Invalid assignment' using errcode='22023'; end if;
 if plv_private.active_member(p_institution_id) is distinct from true then
  raise exception 'Verified academic member required' using errcode='42501';
 end if;
 perform 1 from public.institution_memberships m where
 m.institution_id=p_institution_id and m.user_id=v_actor and m.status='active'
 and m.role in ('university_admin','instructor') for share;
 if not found then raise exception 'Instructor or admin required' using errcode='42501'; end if;
 select course_id into v_course from public.course_sections where
 institution_id=p_institution_id and id=p_section_id for share;
 if not found then raise exception 'Section unavailable' using errcode='42501'; end if;
 if plv_private.is_admin(p_institution_id) is distinct from true then
  perform 1 from public.section_staff st where
    st.institution_id=p_institution_id and st.section_id=p_section_id
    and st.user_id=v_actor and st.role = 'instructor' for share;
  if not found then raise exception 'Section instructor required' using errcode='42501'; end if;
 end if;
 perform 1 from public.lesson_versions l where l.institution_id=p_institution_id
 and l.course_id=v_course and l.id=p_lesson_version_id and l.published_at is not null for share;
 if not found then raise exception 'Pinned published lesson required' using errcode='42501'; end if;
 perform 1 from public.rubric_versions r where r.institution_id=p_institution_id
 and r.course_id=v_course and r.id=p_rubric_version_id and r.published_at is not null for share;
 if not found then raise exception 'Pinned published rubric required' using errcode='42501'; end if;
 select * into v_old from public.audit_events where
 institution_id=p_institution_id and request_id=p_request_id;
 if found then
  if v_old.action='AssignmentPublished' and v_old.actor_id=v_actor
   and v_old.target_type='assignments'
   and exists(select 1 from public.assignments a where
   a.id=v_old.target_id and a.institution_id=p_institution_id
   and a.section_id=p_section_id and a.course_id=v_course
   and a.lesson_version_id=p_lesson_version_id and a.rubric_version_id=p_rubric_version_id
   and a.title=btrim(p_title) and a.due_at is not distinct from p_due_at
   and a.published_at is not null) then return v_old.target_id; end if;
  raise exception 'Assignment request replay differs' using errcode='23505';
 end if;
 insert into public.assignments(institution_id,course_id,section_id,lesson_version_id,
 rubric_version_id,title,due_at,published_at)
 values(p_institution_id,v_course,p_section_id,p_lesson_version_id,
 p_rubric_version_id,btrim(p_title),p_due_at,now()) returning id into v_id;
 insert into public.audit_events(institution_id,actor_id,action,target_type,target_id,
 request_id,details) values(p_institution_id,v_actor,'AssignmentPublished','assignments',
 v_id,p_request_id,jsonb_build_object('sectionId',p_section_id::text,
 'lessonVersionId',p_lesson_version_id::text,'rubricVersionId',p_rubric_version_id::text));
 return v_id;
end;
$$;
revoke all on function plv_private.university_publish_assignment(uuid,uuid,uuid,uuid,text,timestamptz,uuid) from public,anon,authenticated;
grant execute on function plv_private.university_publish_assignment(uuid,uuid,uuid,uuid,text,timestamptz,uuid) to authenticated;
create function public.university_publish_assignment(p_institution_id uuid,p_section_id uuid,
 p_lesson_version_id uuid,p_rubric_version_id uuid,p_title text,p_due_at timestamptz,p_request_id uuid)
returns uuid language sql security invoker set search_path = '' as $$
select plv_private.university_publish_assignment($1,$2,$3,$4,$5,$6,$7);
$$;
revoke all on function public.university_publish_assignment(uuid,uuid,uuid,uuid,text,timestamptz,uuid) from public,anon,authenticated;
grant execute on function public.university_publish_assignment(uuid,uuid,uuid,uuid,text,timestamptz,uuid) to authenticated;
commit;
