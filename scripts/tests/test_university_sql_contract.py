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

    def test_u03_section_creation_is_tenant_scoped_and_audited(self):
        source = ROOT / "supabase/migrations/20261018070000_section_creation_rpc.sql"
        sql = source.read_text(encoding="utf-8").lower() if source.exists() else ""
        for marker in (
            "create function plv_private.university_create_section(",
            "create function public.university_create_section(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.is_admin(p_institution_id)",
            "c.institution_id=p_institution_id",
            "insert into public.course_sections",
            "insert into public.audit_events",
            "'sectioncreated'",
            "revoke all on function public.university_create_section",
            "grant execute on function public.university_create_section",
        ):
            self.assertIn(marker, sql)
        self.assertNotIn("grant insert on public.course_sections", sql)
        for marker in ("anon_section_bypass", "unauthorized_section_bypass"):
            self.assertIn(marker, SMOKE)
        for marker in ("section_admin_pass", "section_cross_tenant_rejected",
                       "section_student_rejected", "section retry altered id"):
            self.assertIn(marker, FIXTURE)

    def test_u03_instructor_assignment_is_admin_and_tenant_checked(self):
        path = ROOT / "supabase/migrations/20261018080000_section_staff_rpc.sql"
        sql = path.read_text(encoding="utf-8").lower() if path.exists() else ""
        for marker in (
            "create function plv_private.university_assign_section_staff(",
            "create function public.university_assign_section_staff(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.is_admin(p_institution_id)",
            "m.role = p_staff_role",
            "u.email_confirmed_at is not null",
            "insert into public.section_staff",
            "insert into public.audit_events",
            "'sectionstaffassigned'",
            "revoke all on function public.university_assign_section_staff",
            "grant execute on function public.university_assign_section_staff",
        ):
            self.assertIn(marker, sql)
        self.assertNotIn("grant insert on public.section_staff", sql)
        for marker in ("anon_staff_bypass", "unauthorized_staff_bypass"):
            self.assertIn(marker, SMOKE)
        for marker in ("staff_student_rejected", "staff_cross_tenant_rejected",
                       "staff_admin_pass", "staff exact retry changed result"):
            self.assertIn(marker, FIXTURE)

    def test_u03_instructor_feedback_is_immutable_and_idempotent(self):
        path = ROOT / "supabase/migrations/20261018090000_instructor_feedback_rpc.sql"
        sql = path.read_text(encoding="utf-8").lower() if path.exists() else ""
        for marker in (
            "create function plv_private.university_record_feedback(",
            "create function public.university_record_feedback(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.active_member(p_institution_id)",
            "m.role in ('university_admin','instructor')",
            "s.role = 'instructor'",
            "insert into public.feedback",
            "insert into public.audit_events",
            "'feedbackrecorded'",
            "create trigger immutable_instructor_feedback",
            "revoke all on function public.university_record_feedback",
            "grant execute on function public.university_record_feedback",
        ):
            self.assertIn(marker, sql)
        self.assertNotIn("grant insert on public.feedback", sql)
        for marker in ("anon_feedback_bypass", "unauthorized_feedback_bypass"):
            self.assertIn(marker, SMOKE)
        for marker in ("feedback_student_rejected", "feedback_foreign_tenant_rejected",
                       "feedback_instructor_accepted", "feedback_retry_same_id",
                       "feedback_changed_payload_rejected", "feedback_immutable",
                       "feedback_audited_once"):
            self.assertIn(marker, FIXTURE)

    def test_u03_published_lesson_binds_canonical_json_digest_and_staff(self):
        path = ROOT / "supabase/migrations/20261018100000_lesson_publication_rpc.sql"
        sql = path.read_text(encoding="utf-8").lower() if path.exists() else ""
        for marker in (
            "create function plv_private.university_publish_lesson_version(",
            "create function public.university_publish_lesson_version(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.active_member(p_institution_id)",
            "m.role in ('university_admin','instructor')",
            "st.role = 'instructor'",
            "c.institution_id=p_institution_id",
            "extensions.digest(p_content_json::text,'sha256')",
            "insert into public.lesson_versions",
            "insert into public.audit_events",
            "'lessonpublished'",
            "revoke all on function public.university_publish_lesson_version",
            "grant execute on function public.university_publish_lesson_version",
        ):
            self.assertIn(marker, sql)
        self.assertNotIn("grant insert on public.lesson_versions", sql)
        for marker in ("anon_lesson_bypass","unauthorized_lesson_bypass"):
            self.assertIn(marker, SMOKE)
        for marker in ("lesson_student_rejected", "lesson_foreign_tenant_rejected",
                       "lesson_instructor_published", "lesson_retry_stable",
                       "lesson_bad_digest_rejected", "lesson_immutable_published"):
            self.assertIn(marker, FIXTURE)

    def test_u03_published_rubric_is_canonical_and_staff_authorized(self):
        path = ROOT / "supabase/migrations/20261018110000_rubric_publication_rpc.sql"
        sql = path.read_text(encoding="utf-8").lower() if path.exists() else ""
        for required in (
            "create function plv_private.university_publish_rubric_version(",
            "create function public.university_publish_rubric_version(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.active_member(p_institution_id)",
            "m.role in ('university_admin','instructor')",
            "st.role = 'instructor'",
            "extensions.digest(p_rubric_json::text,'sha256')",
            "insert into public.rubric_versions",
            "insert into public.audit_events",
            "'rubricpublished'",
            "revoke all on function public.university_publish_rubric_version",
            "grant execute on function public.university_publish_rubric_version",
        ):
            self.assertIn(required, sql)
        self.assertNotIn("grant insert on public.rubric_versions", sql)
        for name in ("anon_rubric_bypass","unauthorized_rubric_bypass"):
            self.assertIn(name, SMOKE)
        for name in ("rubric_student_rejected","rubric_foreign_tenant_rejected",
                     "rubric_instructor_published","rubric_retry_stable",
                     "rubric_bad_digest_rejected","rubric_immutable_published"):
            self.assertIn(name, FIXTURE)

    def test_u03_published_assignment_pins_same_tenant_curriculum(self):
        path = ROOT / "supabase/migrations/20261018120000_assignment_publication_rpc.sql"
        sql = path.read_text(encoding="utf-8").lower() if path.exists() else ""
        for marker in (
            "create function plv_private.university_publish_assignment(",
            "create function public.university_publish_assignment(",
            "security definer set search_path = ''",
            "security invoker set search_path = ''",
            "plv_private.active_member(p_institution_id)",
            "st.role = 'instructor'",
            "l.published_at is not null",
            "r.published_at is not null",
            "insert into public.assignments",
            "insert into public.audit_events",
            "'assignmentpublished'",
            "create trigger immutable_published_assignment",
            "revoke all on function public.university_publish_assignment",
            "grant execute on function public.university_publish_assignment",
        ):
            self.assertIn(marker, sql)
        self.assertNotIn("grant insert on public.assignments", sql)
        for marker in ("anon_assignment_bypass", "unauthorized_assignment_bypass"):
            self.assertIn(marker, SMOKE)
        for marker in ("assignment_student_rejected", "assignment_foreign_tenant_rejected",
                       "assignment_instructor_published", "assignment_retry_stable",
                       "assignment_changed_retry_rejected", "assignment_immutable_published",
                       "assignment_student_visible"):
            self.assertIn(marker, FIXTURE)

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
