-- U04 quarantined artifact staging is deliberately separate from
-- `session_artifacts`: staging is NOT Native attestation or academic evidence.
begin;
create table public.evidence_upload_staging (
 id uuid primary key default gen_random_uuid(),
 institution_id uuid not null references public.institutions(id),
 lab_session_id uuid not null,
 student_id uuid not null,
 object_key text not null unique,
 size_bytes bigint not null check (size_bytes between 1 and 1048576),
 sha256 text not null check (sha256 ~ '^[0-9a-f]{64}$'),
 content_type text not null check (content_type = 'application/json'),
 staged_at timestamptz not null default now(),
 check (object_key = institution_id::text || '/' || id::text),
 unique (institution_id,id),
 foreign key (institution_id,lab_session_id,student_id)
   references public.lab_sessions(institution_id,id,user_id)
);
create index evidence_staging_owner_idx
 on public.evidence_upload_staging(institution_id,student_id,lab_session_id);
alter table public.evidence_upload_staging enable row level security;
revoke all on public.evidence_upload_staging from public, anon, authenticated;
-- Only the Edge server's service role may stage metadata.
-- No client policies, no public RPC and no grant to authenticated.
grant select,insert on public.evidence_upload_staging to service_role;
create trigger immutable_staged_evidence
 before update or delete on public.evidence_upload_staging
 for each row execute function plv_private.immutable_record();
insert into storage.buckets(id,name,public,file_size_limit,allowed_mime_types)
 values ('plv-university-evidence-staging',
         'plv-university-evidence-staging',false,1048576,
         array['application/json']::text[])
 on conflict (id) do nothing;
do $$
begin
 if not exists (select 1 from storage.buckets where
   id='plv-university-evidence-staging' and public=false
   and file_size_limit=1048576
   and allowed_mime_types = array['application/json']::text[]) then
   raise exception 'U04 private bounded staging bucket not established';
 end if;
end $$;
-- No SELECT/INSERT/UPDATE/DELETE Storage policy for this staging bucket.
-- U03 submissions continue to accept only vetted records in
-- public.session_artifacts AND real Storage objects in the separate
-- `plv-university-evidence` bucket. Quarantined objects cannot qualify.
commit;
