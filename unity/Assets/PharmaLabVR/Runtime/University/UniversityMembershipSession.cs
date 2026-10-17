using System;
using System.Collections.Generic;

namespace PharmaLabVR.University
{
    // Gateway data is only a hint for which tenant UI to display.
    // It NEVER authorizes database writes, grade changes or evidence upload.
    public sealed class UniversityMembershipRecord
    {
        public string InstitutionId { get; }
        public string Role { get; }
        public string Status { get; }

        public UniversityMembershipRecord(string institutionId, string role, string status)
        {
            InstitutionId = institutionId;
            Role = role;
            Status = status;
        }
    }

    public sealed class UniversityMembershipSession
    {
        public string UserId { get; private set; } = string.Empty;
        public string ActiveInstitutionId { get; private set; } = string.Empty;

        public bool ReplaceSnapshot(string userId, DateTimeOffset fetchedAtUtc,
            DateTimeOffset authExpiresAtUtc, IReadOnlyList<UniversityMembershipRecord> memberships,
            DateTimeOffset nowUtc)
        {
            // Implementation follows failing headless contract tests.
            SignOut();
            return false;
        }

        public bool TrySelectInstitution(string institutionId, DateTimeOffset nowUtc) => false;
        public bool CanDisplayInstitution(string institutionId, DateTimeOffset nowUtc) => false;
        public string SelectedRole(DateTimeOffset nowUtc) => string.Empty;

        public void SignOut()
        {
            UserId = string.Empty;
            ActiveInstitutionId = string.Empty;
        }
    }
}
