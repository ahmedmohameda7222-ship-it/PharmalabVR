-- U01/2: tenant-local reads only. All academic mutations remain server-only.
begin;
-- Non-exposed SECURITY DEFINER helpers avoid recursive membership RLS.
-- Explicit verified email + active membership; JWT metadata is NOT authority.
create function plv_private.active_member(p_tenant uuid)
returns boolean language sql stable security definer set search_path = '' as $$
 select exists (
  select 1 from public.institution_memberships m
  join public.institutions i on i.id = m.institution_id
  join auth.users u on u.id = m.user_id
  where m.institution_id = p_tenant and m.user_id = (select auth.uid())
    and m.status = 'active' and i.status = 'active'
    and u.email_confirmed_at is not null
 );
$$;
create function plv_private.is_admin(p_tenant uuid)
returns boolean language sql stable security definer set search_path = '' as $$
 select (select plv_private.active_member(p_tenant)) and exists (
  select 1 from public.institution_memberships m
  where m.institution_id = p_tenant and m.user_id = (select auth.uid())
    and m.status = 'active' and m.role = 'university_admin'
 );
$$;
create function plv_private.can_manage_section(p_tenant uuid, p_section uuid)
returns boolean language sql stable security definer set search_path = '' as $$
 select (select plv_private.active_member(p_tenant)) and
 ((select plv_private.is_admin(p_tenant)) or exists (
  select 1 from public.section_staff s
  join public.institution_memberships m
    on m.institution_id = s.institution_id and m.user_id = s.user_id
  where s.institution_id = p_tenant and s.section_id = p_section
    and s.user_id = (select auth.uid()) and m.status = 'active'
    and m.role in ('instructor','teaching_assistant')
 ));
$$;
create function plv_private.can_view_section(p_tenant uuid,p_section uuid)
returns boolean language sql stable security definer set search_path = '' as $$
 select (select plv_private.active_member(p_tenant)) and
 ((select plv_private.can_manage_section(p_tenant,p_section)) or exists (
  select 1 from public.enrollments e
  join public.institution_memberships m
    on m.institution_id = e.institution_id and m.user_id = e.student_user_id
  where e.institution_id = p_tenant and e.section_id = p_section
    and e.student_user_id = (select auth.uid()) and e.state = 'active'
    and m.status = 'active' and m.role = 'student'
 ));
$$;
create function plv_private.can_view_course(p_tenant uuid,p_course uuid)
returns boolean language sql stable security definer set search_path = '' as $$
 select (select plv_private.active_member(p_tenant)) and
 ((select plv_private.is_admin(p_tenant)) or exists (
  select 1 from public.course_sections s
  where s.institution_id = p_tenant and s.course_id = p_course
    and plv_private.can_view_section(p_tenant,s.id)
 ));
$$;
create function plv_private.can_read_artifact(
 p_tenant uuid,p_session uuid,p_submission uuid
) returns boolean language sql stable security definer set search_path = '' as $$
 select (select plv_private.active_member(p_tenant)) and
 (exists (select 1 from public.lab_sessions l
   where l.institution_id = p_tenant and l.id = p_session
     and l.user_id = (select auth.uid()))
  or (p_submission is not null and exists (
   select 1 from public.submissions s
   where s.institution_id = p_tenant and s.id = p_submission
     and s.lab_session_id = p_session
     and plv_private.can_manage_section(p_tenant,s.section_id)
  )));
$$;
revoke execute on all functions in schema plv_private from public, anon, authenticated;
grant execute on function plv_private.active_member(uuid) to authenticated;
grant execute on function plv_private.is_admin(uuid) to authenticated;
grant execute on function plv_private.can_manage_section(uuid,uuid) to authenticated;
grant execute on function plv_private.can_view_section(uuid,uuid) to authenticated;
grant execute on function plv_private.can_view_course(uuid,uuid) to authenticated;
grant execute on function plv_private.can_read_artifact(uuid,uuid,uuid) to authenticated;

-- Supabase's post-May-2026 Data API may require explicit grants.
grant select on public.institutions, public.profiles, public.institution_memberships,
 public.courses, public.course_sections, public.section_staff, public.enrollments,
 public.lesson_versions, public.rubric_versions, public.assignments,
 public.lab_sessions, public.submissions, public.grades, public.feedback,
 public.session_artifacts to authenticated;
grant insert(user_id,display_name), update(display_name)
 on public.profiles to authenticated;
create policy own_profile_read on public.profiles for select to authenticated
 using (user_id = (select auth.uid()));
create policy own_profile_insert on public.profiles for insert to authenticated
 with check (user_id = (select auth.uid()));
create policy own_profile_update on public.profiles for update to authenticated
 using (user_id = (select auth.uid()))
 with check (user_id = (select auth.uid()));
create policy institution_read on public.institutions for select to authenticated
 using (plv_private.active_member(id));
create policy membership_read on public.institution_memberships for select to authenticated
 using (user_id = (select auth.uid()) or plv_private.is_admin(institution_id));
create policy course_read on public.courses for select to authenticated
 using (plv_private.can_view_course(institution_id,id));
create policy section_read on public.course_sections for select to authenticated
 using (plv_private.can_view_section(institution_id,id));
create policy staff_read on public.section_staff for select to authenticated
 using (plv_private.can_view_section(institution_id,section_id));
create policy enrollment_read on public.enrollments for select to authenticated
 using ((student_user_id = (select auth.uid()) and
   plv_private.active_member(institution_id)) or
   plv_private.can_manage_section(institution_id,section_id));
create policy lesson_read on public.lesson_versions for select to authenticated
 using (plv_private.is_admin(institution_id) or
  exists (select 1 from public.course_sections s
    where s.institution_id = lesson_versions.institution_id
      and s.course_id = lesson_versions.course_id
      and plv_private.can_manage_section(s.institution_id,s.id))
  or (published_at is not null and exists (
    select 1 from public.assignments a
    where a.institution_id = lesson_versions.institution_id
      and a.lesson_version_id = lesson_versions.id
      and a.published_at is not null
      and plv_private.can_view_section(a.institution_id,a.section_id))));
create policy rubric_read on public.rubric_versions for select to authenticated
 using (plv_private.is_admin(institution_id) or
  exists (select 1 from public.course_sections s
    where s.institution_id = rubric_versions.institution_id
      and s.course_id = rubric_versions.course_id
      and plv_private.can_manage_section(s.institution_id,s.id))
  or (published_at is not null and exists (
    select 1 from public.assignments a
    where a.institution_id = rubric_versions.institution_id
      and a.rubric_version_id = rubric_versions.id
      and a.published_at is not null
      and plv_private.can_view_section(a.institution_id,a.section_id))));
create policy assignment_read on public.assignments for select to authenticated
 using (plv_private.can_manage_section(institution_id,section_id)
  or (published_at is not null and
      plv_private.can_view_section(institution_id,section_id)));
create policy session_read on public.lab_sessions for select to authenticated
 using (user_id = (select auth.uid()) and
   plv_private.active_member(institution_id));
create policy submission_read on public.submissions for select to authenticated
 using ((student_id = (select auth.uid()) and
   plv_private.active_member(institution_id))
   or plv_private.can_manage_section(institution_id,section_id));
create policy grade_read on public.grades for select to authenticated
 using (exists (select 1 from public.submissions s
   where s.institution_id = grades.institution_id and s.id = grades.submission_id
     and (plv_private.can_manage_section(s.institution_id,s.section_id)
      or (s.student_id = (select auth.uid()) and grades.status = 'approved'
          and plv_private.active_member(s.institution_id)))));
create policy feedback_read on public.feedback for select to authenticated
 using (exists (select 1 from public.submissions s
   where s.institution_id = feedback.institution_id and s.id = feedback.submission_id
     and (plv_private.can_manage_section(s.institution_id,s.section_id)
      or (s.student_id = (select auth.uid()) and feedback.student_visible
          and plv_private.active_member(s.institution_id)))));
create policy artifact_read on public.session_artifacts for select to authenticated
 using (plv_private.can_read_artifact(institution_id,lab_session_id,submission_id));
-- audit_events intentionally has neither SELECT grant nor any client policy.

-- A private bucket and read-only Storage policy. There is NO client upload,
-- overwrite, delete or unrestricted list policy. U04 will supply server upload.
insert into storage.buckets(id,name,public,file_size_limit)
 values ('plv-university-evidence','plv-university-evidence',false,52428800)
 on conflict (id) do nothing;
do $$
begin
 if exists(select 1 from storage.buckets
   where id='plv-university-evidence' and public) then
  raise exception 'evidence bucket must be private';
 end if;
end $$;
create policy plv_evidence_read on storage.objects for select to authenticated
 using (bucket_id = 'plv-university-evidence'
  and exists (select 1 from public.session_artifacts a
    where a.object_key = storage.objects.name));
commit;
