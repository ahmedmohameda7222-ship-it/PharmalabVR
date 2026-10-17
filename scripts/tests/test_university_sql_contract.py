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
