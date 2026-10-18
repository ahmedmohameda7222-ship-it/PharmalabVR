"""U01 source-only guardrails; these tests do NOT prove PostgreSQL RLS.

Full real-DB assertions live in supabase/security-tests/tenant_isolation.sql.
Run on a disposable local Supabase instance when one is available.
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
CORE = (ROOT / "supabase/migrations/20261017000000_university_core.sql").read_text(encoding="utf-8").lower()
RLS = (ROOT / "supabase/migrations/20261017000100_university_rls.sql").read_text(encoding="utf-8").lower()
FIXTURE = (ROOT / "supabase/security-tests/tenant_isolation.sql").read_text(encoding="utf-8").lower()
RPC_PATH = ROOT / "supabase/migrations/20261018000000_university_membership_rpc.sql"
RPC = RPC_PATH.read_text(encoding="utf-8").lower() if RPC_PATH.exists() else ""
HARDENING_PATH = ROOT / "supabase/migrations/20261018010000_university_rpc_hardening.sql"
HARDENING = HARDENING_PATH.read_text(encoding="utf-8").lower() if HARDENING_PATH.exists() else ""
SMOKE = (ROOT / "supabase/security-tests/live_no_data_smoke.sql").read_text(encoding="utf-8").lower()
U03_PATH = ROOT / "supabase/migrations/20261018030000_student_submission_rpc.sql"
U03 = U03_PATH.read_text(encoding="utf-8").lower() if U03_PATH.exists() else ""

TABLES = (
    "institutions", "profiles", "institution_memberships", "courses",
    "course_sections", "section_staff", "enrollments", "lesson_versions",
    "rubric_versions", "assignments", "lab_sessions", "submissions",
    "grades", "feedback", "session_artifacts", "audit_events",
)


class UniversitySqlGuardrails(unittest.TestCase):
    def test_u01_every_exposed_table_is_rls_enabled(self):
        declared = set(re.findall(r"create table public\.(\w+)", CORE))
        self.assertEqual(set(TABLES), declared)
        for table in TABLES:
            self.assertRegex(CORE, rf"alter table public\.{table}\s+enable row level security")

    def test_u01_default_deny_academic_mutations(self):
        self.assertIn("from anon, authenticated;", CORE)
        self.assertIn("grant select on public.institutions", RLS)
        self.assertIn("grant insert(user_id,display_name), update(display_name)", RLS)
        grants = re.findall(r"\bgrant\s+([\s\S]*?)\s+to\s+authenticated\s*;", RLS)
        writes = [grant for grant in grants if re.search(r"\b(insert|update|delete|truncate)\b", grant)]
        self.assertEqual(1, len(writes))
        self.assertIn("on public.profiles", writes[0])
        self.assertNotIn("create policy grade_write", RLS)
        self.assertNotIn("grant all", RLS)

    def test_u01_helpers_pin_search_path_and_restrict_execute(self):
        defs = re.findall(r"create function plv_private\.\w+\([\s\S]*?\)\s*returns boolean language sql stable security definer set search_path = ''", RLS)
        self.assertEqual(6, len(defs))
        self.assertIn("revoke execute on all functions in schema plv_private", RLS)
        self.assertIn("and m.user_id = (select auth.uid())", RLS)
        self.assertIn("u.email_confirmed_at is not null", RLS)
        self.assertNotIn("user_metadata", RLS.replace("user_metadata or", ""))
        self.assertNotIn("auth.jwt()", RLS)

    def test_u01_composite_references_bind_academic_tenant(self):
        for marker in (
            "foreign key (institution_id,section_id,course_id)",
            "foreign key (institution_id,assignment_id,section_id,course_id,rubric_version_id)",
            "foreign key (institution_id,lab_session_id,student_id)",
            "foreign key (institution_id,submission_id,rubric_version_id)",
            "foreign key (institution_id,submission_id,lab_session_id,user_id)",
        ):
            self.assertIn(marker, CORE)

    def test_u01_private_role_helpers_require_tenant_matching_ids(self):
        # Static early-warning guard only. Executable Postgres fixture below
        # must exercise both positive and forbidden helper calls with real RLS.
        section = RLS.split("create function plv_private.can_manage_section(", 1)[1].split("$;", 1)[0]
        course = RLS.split("create function plv_private.can_view_course(", 1)[1].split("$;", 1)[0]
        self.assertIn("from public.course_sections scope_section", section)
        self.assertIn("scope_section.institution_id = p_tenant", section)
        self.assertIn("scope_section.id = p_section", section)
        self.assertIn("from public.courses scope_course", course)
        self.assertIn("scope_course.institution_id = p_tenant", course)
        self.assertIn("scope_course.id = p_course", course)
        self.assertIn("forged other-tenant admin helper returned true", FIXTURE)

    def test_u01_private_storage_no_client_upload(self):
        self.assertIn("'plv-university-evidence',false,52428800", RLS)
        self.assertIn("create policy plv_evidence_read", RLS)
        self.assertNotRegex(RLS, r"create policy\s+\w+\s+on storage.objects\s+for (insert|update|delete)")
        self.assertIn("a.object_key = storage.objects.name", RLS)

    def test_u01_fixture_has_only_valid_postgres_do_dollar_quotes(self):
        # This guards real SQL syntax, not whether its RLS assertions pass.
        # A malformed DO $ ... END $; was previously shipped unexecuted.
        self.assertNotRegex(FIXTURE, r"(?m)^\s*(?:do|end)\s+\$(?!\$)")
        opening = len(re.findall(r"(?m)^do \$\$", FIXTURE))
        closing = len(re.findall(r"(?m)^end \$\$;$", FIXTURE))
        self.assertGreater(opening, 0)
        self.assertEqual(opening, closing)

    def test_u02_membership_rpc_is_server_checked_and_audited(self):
        self.assertIn("create function public.university_invite_member(", RPC)
        self.assertIn("create function public.university_accept_invite(", RPC)
        self.assertIn("create function public.university_revoke_member(", RPC)
        self.assertIn("security definer", RPC)
        self.assertIn("set search_path = ''", RPC)
        self.assertIn("plv_private.is_admin(p_institution_id)", RPC)
        self.assertIn("auth.uid()", RPC)
        self.assertIn("email_confirmed_at is not null", RPC)
        self.assertIn("for update", RPC)
        self.assertIn("insert into public.audit_events", RPC)
        self.assertIn("create unique index", RPC)
        self.assertIn("revoke all on function public.university_invite_member", RPC)
        self.assertIn("revoke all on function public.university_accept_invite", RPC)
        self.assertIn("revoke all on function public.university_revoke_member", RPC)
        self.assertNotIn("service_role", RPC)
        self.assertIn("grant execute on function public.university_invite_member", RPC)
        self.assertIn("grant execute on function public.university_accept_invite", RPC)
        self.assertIn("grant execute on function public.university_revoke_member", RPC)

    def test_u02_synthetic_postgres_cases_include_denied_cross_tenant_and_replay(self):
        for marker in (
            "nonadmin invite unexpectedly succeeded",
            "cross-tenant revocation unexpectedly succeeded",
            "invitation cannot grant university admin",
            "same request with different role unexpectedly succeeded",
            "invitation acceptance was not attributed to auth.uid",
            "revocation did not immediately deny tenant access",
            "accepted invitation was not audited",
            "revocation was not audited",
        ):
            self.assertIn(marker, FIXTURE)

    def test_u02_rpc_hardening_keeps_security_definer_out_of_public_api(self):
        # RED live-project evidence before the hardening: all three public
        # functions had prosecdef=true. Public interfaces must be INVOKER.
        for name, signature, args in (
            ("university_invite_member", "uuid,uuid,text,uuid", "$1,$2,$3,$4"),
            ("university_accept_invite", "uuid,uuid", "$1,$2"),
            ("university_revoke_member", "uuid,uuid,uuid", "$1,$2,$3"),
        ):
            self.assertIn(
                f"alter function public.{name}({signature}) set schema plv_private",
                " ".join(HARDENING.split())
            )
            self.assertIn(
                f"create function public.{name}(",
                HARDENING
            )
            self.assertIn(
                f"plv_private.{name}({args})",
                HARDENING
            )
        self.assertEqual(HARDENING.count("security invoker set search_path = ''"), 3)
        self.assertEqual(HARDENING.count("security definer"), 0)
        self.assertEqual(HARDENING.count("to authenticated;"), 6)
        self.assertIn("revoke all on function public.university_invite_member", HARDENING)
        self.assertIn("revoke all on function public.university_revoke_member", HARDENING)

    def test_u02_owner_bootstrap_cannot_be_executed_by_client_roles(self):
        bootstrap = (ROOT / "supabase/migrations/20261018020000_owner_bootstrap.sql").read_text(encoding="utf-8").lower()
        self.assertIn("create function plv_private.owner_bootstrap_institution(", bootstrap)
        self.assertIn("security invoker set search_path = ''", bootstrap)
        self.assertIn("if current_user <> 'postgres' then", bootstrap)
        self.assertIn("email_confirmed_at is not null", bootstrap)
        self.assertIn("'university_admin','active',null", bootstrap)
        self.assertIn("insert into public.audit_events(", bootstrap)
        self.assertIn("from public, anon, authenticated, service_role", bootstrap)
        self.assertNotIn("create function public.owner_bootstrap", bootstrap)
        self.assertIn("BOOTSTRAP_ANON_BYPASS".lower(), SMOKE)
        self.assertIn("BOOTSTRAP_AUTH_BYPASS".lower(), SMOKE)

    def test_u03_idempotent_submission_is_authorized_and_evidence_bound(self):
        # Real PostgreSQL execution is in the disposable two-tenant fixture.
        for marker in (
            "create function plv_private.university_submit_attempt(",
            "create function public.university_submit_attempt(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.active_member(p_institution_id)",
            "m.role = 'student'",
            "e.state = 'active'",
            "a.published_at is not null",
            "v_session.current_revision",
            "public.session_artifacts",
            "storage.objects",
            "s.sha256 = p_evidence_sha256",
            "insert into public.submissions",
            "on conflict (institution_id,student_id,idempotency_key) do nothing",
            "insert into public.audit_events",
            "'submissioncreated'",
            "grant execute on function public.university_submit_attempt",
            "revoke all on function public.university_submit_attempt",
        ):
            self.assertIn(marker, U03)
        self.assertNotIn("grant insert on public.submissions", U03)
        self.assertNotIn("service_role", U03)
        self.assertIn("for share", U03)
        self.assertIn("create unique index", U03)
        for marker in (
            "student_submission_accepted",
            "student_submission_retry_reused_id",
            "student_submission_changed_hash_rejected",
            "student_submission_other_tenant_rejected",
            "student_submission_nonstudent_rejected",
            "student_submission_not_audited",
        ):
            self.assertIn(marker, FIXTURE)

    def test_u03_live_no_data_smoke_denies_anon_and_unassigned_auth(self):
        self.assertIn("anon_submission_bypass", SMOKE)
        self.assertIn("unauthorized_submission_bypass", SMOKE)

    def test_u03_grade_approval_stays_in_private_authorized_rpc(self):
        grade_path = ROOT / "supabase/migrations/20261018040000_grade_approval_rpc.sql"
        grade = grade_path.read_text(encoding="utf-8").lower() if grade_path.exists() else ""
        for marker in (
            "create function plv_private.university_approve_grade(",
            "create function public.university_approve_grade(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.active_member(p_institution_id)",
            "m.role in ('university_admin','instructor')",
            "s.role = 'instructor'",
            "insert into public.grades",
            "insert into public.audit_events",
            "create trigger immutable_approved_grade",
            "grant execute on function public.university_approve_grade",
        ):
            self.assertIn(marker, grade)
        self.assertNotIn("grant insert on public.grades", grade)

    def test_u03_admin_enrollment_is_private_scoped_audited(self):
        migration = ROOT / "supabase/migrations/20261018050000_section_enrollment_rpc.sql"
        sql = migration.read_text(encoding="utf-8").lower() if migration.exists() else ""
        for required in (
            "create function plv_private.university_enroll_student(",
            "create function public.university_enroll_student(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.is_admin(p_institution_id)",
            "m.role = 'student'",
            "u.email_confirmed_at is not null",
            "s.institution_id = p_institution_id",
            "insert into public.enrollments",
            "insert into public.audit_events",
            "'enrollmentcreated'",
            "grant execute on function public.university_enroll_student",
            "revoke all on function public.university_enroll_student",
        ):
            self.assertIn(required, sql)
        self.assertNotIn("grant insert on public.enrollments", sql)
        self.assertIn("anon_enrollment_bypass", SMOKE)
        self.assertIn("unauthorized_enrollment_bypass", SMOKE)
        self.assertIn("enrollment_admin_pass", FIXTURE)
        self.assertIn("enrollment_cross_tenant_rejected", FIXTURE)
        self.assertIn("enrollment_student_rejected", FIXTURE)

    def test_u03_admin_course_creation_is_tenant_scoped_and_audited(self):
        source = ROOT / "supabase/migrations/20261018060000_course_creation_rpc.sql"
        sql = source.read_text(encoding="utf-8").lower() if source.exists() else ""
        for required in (
            "create function plv_private.university_create_course(",
            "create function public.university_create_course(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.is_admin(p_institution_id)",
            "m.role = 'university_admin'",
            "insert into public.courses",
            "insert into public.audit_events",
            "'coursecreated'",
            "revoke all on function public.university_create_course",
            "grant execute on function public.university_create_course",
        ):
            self.assertIn(required, sql)
        self.assertNotIn("grant insert on public.courses", sql)
        for required in (
            "anon_course_bypass",
            "unauthorized_course_bypass",
        ):
            self.assertIn(required, SMOKE)
        for required in (
            "course_admin_pass",
            "course_cross_tenant_rejected",
            "course_student_rejected",
            "course exact retry not idempotent",
        ):
            self.assertIn(required, FIXTURE)

    def test_u01_sql_fixture_has_tenants_auth_impersonation_and_rollback(self):
        for marker in (
            "insert into auth.users", "set local role authenticated;",
            "cross-tenant section fk unexpectedly succeeded",
            "duplicate idempotency key unexpectedly succeeded",
            "student grade update unexpectedly succeeded",
            "revoked membership leaked grade",
            "client evidence upload unexpectedly succeeded",
            "reset role;", "rollback;",
        ):
            self.assertIn(marker, FIXTURE)
        self.assertIn("psql only, disposable local supabase database only", FIXTURE)


if __name__ == "__main__":
    unittest.main()
