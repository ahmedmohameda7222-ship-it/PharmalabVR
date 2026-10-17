using System;
using NUnit.Framework;
using PharmaLabVR.University;

namespace PharmaLabVR.Tests.EditMode
{
    // Authored only. Unity Editor/EditMode is deferred to the final local campaign.
    // The same production type is already exercised by headless .NET tests in CI.
    public sealed class UniversityMembershipSessionTests
    {
        private const string UniversityA = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
        private const string UniversityB = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
        private const string UserId = "11111111-1111-4111-8111-111111111111";

        [Test]
        public void U02_MultiInstitutionSelectionNeverCarriesAnInstructorRoleToStudentTenant()
        {
            var now = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);
            var selector = new UniversityMembershipSession();
            Assert.That(selector.ReplaceSnapshot(UserId, now, now.AddMinutes(3),
                new[]
                {
                    new UniversityMembershipRecord(UniversityA, "instructor", "active"),
                    new UniversityMembershipRecord(UniversityB, "student", "active")
                }, now), Is.True);
            Assert.That(selector.TrySelectInstitution(UniversityA, now), Is.True);
            Assert.That(selector.SelectedRole(now), Is.EqualTo("instructor"));
            Assert.That(selector.TrySelectInstitution(UniversityB, now), Is.True);
            Assert.That(selector.SelectedRole(now), Is.EqualTo("student"));
            Assert.That(selector.CanDisplayInstitution(UniversityA, now.AddMinutes(4)), Is.False);
            Assert.That(selector.ActiveInstitutionId, Is.Empty);
        }

        [Test]
        public void U02_RevocationAndFailedRefreshClearOldInstitutionSelection()
        {
            var now = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);
            var selector = new UniversityMembershipSession();
            Assert.That(selector.ReplaceSnapshot(UserId, now, now.AddMinutes(3),
                new[] { new UniversityMembershipRecord(UniversityA, "instructor", "active") },
                now), Is.True);
            Assert.That(selector.TrySelectInstitution(UniversityA, now), Is.True);
            Assert.That(selector.ReplaceSnapshot(UserId, now.AddSeconds(1), now.AddMinutes(3),
                new[] { new UniversityMembershipRecord(UniversityA, "instructor", "revoked") },
                now.AddSeconds(1)), Is.True);
            Assert.That(selector.TrySelectInstitution(UniversityA, now.AddSeconds(1)), Is.False);
            Assert.That(selector.SelectedRole(now.AddSeconds(1)), Is.Empty);
            Assert.That(selector.ReplaceSnapshot(UserId, now.AddMinutes(1), now.AddMinutes(3),
                new[] { new UniversityMembershipRecord(UniversityA, "platform_operator", "active") },
                now.AddMinutes(1)), Is.False);
            Assert.That(selector.UserId, Is.Empty);
        }
    }
}
