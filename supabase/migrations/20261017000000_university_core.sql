-- U01/1: tenant schema. Draft only: DO NOT apply until owner provisions a project.
-- All academic writes are reserved for audited U02/U03 trusted server actions.
begin;
create schema if not exists plv_private;
revoke all on schema plv_private from public, anon, authenticated;
grant usage on schema plv_private to authenticated;

create table public.institutions (
 id uuid primary key default gen_random_uuid(),
 slug text not null unique check (slug ~ '^[a-z0-9][a-z0-9-]{1,62}$'),
 name text not null check (length(btrim(name)) between 2 and 200),
 status text not null default 'active' check (status in ('active','suspended')),
 created_at timestamptz not null default now()
);
create table public.profiles (
 user_id uuid primary key references auth.users(id),
 display_name text not null check (length(btrim(display_name)) between 1 and 120),
 updated_at timestamptz not null default now()
);
-- One role per university per user; the same person may belong to many universities.
create table public.institution_memberships (
 institution_id uuid not null references public.institutions(id),
 user_id uuid not null references auth.users(id),
 role text not null check (role in ('university_admin','instructor','teaching_assistant','student')),
 status text not null default 'invited' check (status in ('invited','active','revoked')),
 invited_by uuid references auth.users(id),
 created_at timestamptz not null default now(),
 primary key (institution_id, user_id)
);
create index membership_user_idx on public.institution_memberships(user_id, institution_id);
create table public.courses (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null references public.institutions(id),
 code text not null check (length(btrim(code)) between 1 and 40),
 name text not null check (length(btrim(name)) between 1 and 200),
 term text not null,
 owner_user_id uuid,
 unique (institution_id,id),
 unique (institution_id,code,term),
 foreign key (institution_id,owner_user_id)
   references public.institution_memberships(institution_id,user_id)
);
create index courses_tenant_idx on public.courses(institution_id,term);
create table public.course_sections (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 course_id uuid not null,
 section_code text not null,
 unique (institution_id,id),
 unique (institution_id,id,course_id),
 unique (institution_id,course_id,section_code),
 foreign key (institution_id,course_id) references public.courses(institution_id,id)
);
create table public.section_staff (
 institution_id uuid not null,
 section_id uuid not null,
 user_id uuid not null,
 role text not null check (role in ('instructor','teaching_assistant')),
 primary key (institution_id,section_id,user_id),
 foreign key (institution_id,section_id) references public.course_sections(institution_id,id),
 foreign key (institution_id,user_id) references public.institution_memberships(institution_id,user_id)
);
create index staff_user_idx on public.section_staff(user_id,institution_id);
create table public.enrollments (
 institution_id uuid not null,
 section_id uuid not null,
 student_user_id uuid not null,
 state text not null default 'active' check (state in ('active','withdrawn')),
 primary key (institution_id,section_id,student_user_id),
 foreign key (institution_id,section_id) references public.course_sections(institution_id,id),
 foreign key (institution_id,student_user_id) references public.institution_memberships(institution_id,user_id)
);
create index student_enrollment_idx on public.enrollments(student_user_id,institution_id);

create table public.lesson_versions (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 course_id uuid not null,
 lesson_id uuid not null,
 version integer not null check (version > 0),
 author_id uuid not null,
 content_sha256 text not null check (content_sha256 ~ '^[0-9a-f]{64}$'),
 content_json jsonb not null check (jsonb_typeof(content_json) = 'object'),
 published_at timestamptz,
 unique (institution_id,id),
 unique (institution_id,id,course_id),
 unique (institution_id,lesson_id,version),
 foreign key (institution_id,course_id) references public.courses(institution_id,id),
 foreign key (institution_id,author_id) references public.institution_memberships(institution_id,user_id)
);
create table public.rubric_versions (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 course_id uuid not null,
 rubric_id uuid not null,
 version integer not null check (version > 0),
 author_id uuid not null,
 content_sha256 text not null check (content_sha256 ~ '^[0-9a-f]{64}$'),
 rubric_json jsonb not null check (jsonb_typeof(rubric_json) = 'object'),
 published_at timestamptz,
 unique (institution_id,id),
 unique (institution_id,id,course_id),
 unique (institution_id,rubric_id,version),
 foreign key (institution_id,course_id) references public.courses(institution_id,id),
 foreign key (institution_id,author_id) references public.institution_memberships(institution_id,user_id)
);
create table public.assignments (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 course_id uuid not null,
 section_id uuid not null,
 lesson_version_id uuid not null,
 rubric_version_id uuid not null,
 title text not null,
 due_at timestamptz,
 published_at timestamptz,
 unique (institution_id,id),
 unique (institution_id,id,section_id,course_id,rubric_version_id),
 foreign key (institution_id,section_id,course_id)
   references public.course_sections(institution_id,id,course_id),
 foreign key (institution_id,lesson_version_id,course_id)
   references public.lesson_versions(institution_id,id,course_id),
 foreign key (institution_id,rubric_version_id,course_id)
   references public.rubric_versions(institution_id,id,course_id)
);
create index assignments_section_idx on public.assignments(institution_id,section_id);

-- Metadata only. Native checkpoint, material ledger and solver stay local.
create table public.lab_sessions (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 user_id uuid not null,
 local_session_id text not null check (length(local_session_id) between 1 and 128),
 native_branch_id text not null check (length(native_branch_id) between 1 and 128),
 current_revision bigint not null default 0 check (current_revision >= 0),
 science_package_sha256 text not null check (science_package_sha256 ~ '^[0-9a-f]{64}$'),
 sync_state text not null default 'pending' check (sync_state in ('pending','synced','conflict')),
 unique (institution_id,id),
 unique (institution_id,id,user_id),
 unique (institution_id,user_id,local_session_id),
 foreign key (institution_id,user_id) references public.institution_memberships(institution_id,user_id)
);
create table public.submissions (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 course_id uuid not null,
 section_id uuid not null,
 assignment_id uuid not null,
 rubric_version_id uuid not null,
 student_id uuid not null,
 lab_session_id uuid not null,
 idempotency_key uuid not null,
 evidence_sha256 text not null check (evidence_sha256 ~ '^[0-9a-f]{64}$'),
 submitted_at timestamptz not null default now(),
 unique (institution_id,id),
 unique (institution_id,id,rubric_version_id),
 unique (institution_id,id,lab_session_id,student_id),
 unique (institution_id,student_id,idempotency_key),
 foreign key (institution_id,assignment_id,section_id,course_id,rubric_version_id)
  references public.assignments(institution_id,id,section_id,course_id,rubric_version_id),
 foreign key (institution_id,section_id,student_id)
  references public.enrollments(institution_id,section_id,student_user_id),
 foreign key (institution_id,lab_session_id,student_id)
  references public.lab_sessions(institution_id,id,user_id)
);
create index submissions_section_idx on public.submissions(institution_id,section_id);
create table public.grades (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 submission_id uuid not null,
 rubric_version_id uuid not null,
 grader_id uuid not null,
 status text not null default 'draft' check (status in ('draft','approved')),
 score numeric(8,3) not null check (score >= 0),
 updated_at timestamptz not null default now(),
 unique (institution_id,id),
 unique (institution_id,submission_id),
 foreign key (institution_id,submission_id,rubric_version_id)
  references public.submissions(institution_id,id,rubric_version_id),
 foreign key (institution_id,grader_id)
  references public.institution_memberships(institution_id,user_id)
);
create table public.feedback (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 submission_id uuid not null,
 author_id uuid not null,
 body text not null check (length(body) between 1 and 12000),
 student_visible boolean not null default false,
 created_at timestamptz not null default now(),
 unique (institution_id,id),
 foreign key (institution_id,submission_id) references public.submissions(institution_id,id),
 foreign key (institution_id,author_id) references public.institution_memberships(institution_id,user_id)
);
create table public.session_artifacts (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null,
 lab_session_id uuid not null,
 user_id uuid not null,
 submission_id uuid,
 object_key text not null unique,
 size_bytes bigint not null check (size_bytes between 1 and 52428800),
 sha256 text not null check (sha256 ~ '^[0-9a-f]{64}$'),
 content_type text not null check (content_type in ('application/json','application/zip','video/mp4')),
 recorded_at timestamptz not null default now(),
 check (object_key = institution_id::text || '/' || id::text),
 unique (institution_id,id),
 foreign key (institution_id,lab_session_id,user_id)
  references public.lab_sessions(institution_id,id,user_id),
 foreign key (institution_id,submission_id,lab_session_id,user_id)
  references public.submissions(institution_id,id,lab_session_id,student_id)
);
create table public.audit_events (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null references public.institutions(id),
 actor_id uuid references auth.users(id),
 action text not null,
 target_type text not null,
 target_id uuid,
 request_id uuid not null,
 created_at timestamptz not null default now(),
 unique (institution_id,request_id,action,target_type)
);

create function plv_private.immutable_record()
returns trigger language plpgsql set search_path = '' as $$
begin
 raise exception 'record is immutable' using errcode = '23514';
end;
$$;
create trigger immutable_submission before update or delete on public.submissions
 for each row execute function plv_private.immutable_record();
create trigger immutable_artifact before update or delete on public.session_artifacts
 for each row execute function plv_private.immutable_record();
create trigger immutable_audit before update or delete on public.audit_events
 for each row execute function plv_private.immutable_record();
create function plv_private.immutable_published()
returns trigger language plpgsql set search_path = '' as $$
begin
 if old.published_at is not null then
  raise exception 'published curriculum is immutable' using errcode = '23514';
 end if;
 return new;
end;
$$;
create trigger immutable_published_lesson before update or delete on public.lesson_versions
 for each row execute function plv_private.immutable_published();
create trigger immutable_published_rubric before update or delete on public.rubric_versions
 for each row execute function plv_private.immutable_published();

-- Default deny every exposed table, including audit. Do not rely on UI hiding.
alter table public.institutions enable row level security;
alter table public.profiles enable row level security;
alter table public.institution_memberships enable row level security;
alter table public.courses enable row level security;
alter table public.course_sections enable row level security;
alter table public.section_staff enable row level security;
alter table public.enrollments enable row level security;
alter table public.lesson_versions enable row level security;
alter table public.rubric_versions enable row level security;
alter table public.assignments enable row level security;
alter table public.lab_sessions enable row level security;
alter table public.submissions enable row level security;
alter table public.grades enable row level security;
alter table public.feedback enable row level security;
alter table public.session_artifacts enable row level security;
alter table public.audit_events enable row level security;
revoke all on public.institutions, public.profiles, public.institution_memberships,
 public.courses, public.course_sections, public.section_staff, public.enrollments,
 public.lesson_versions, public.rubric_versions, public.assignments,
 public.lab_sessions, public.submissions, public.grades, public.feedback,
 public.session_artifacts, public.audit_events from anon, authenticated;
commit;
