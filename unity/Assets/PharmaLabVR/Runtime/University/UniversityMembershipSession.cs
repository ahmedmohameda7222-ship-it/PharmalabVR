using System;
using System.Collections.Generic;

namespace PharmaLabVR.University
{
    // Returned membership data is ONLY a presentation hint. A Unity or web
    // client cannot authorize a submission, upload, grade, or enrollment.
    // Every such operation requires fresh server-side Auth + relational checks.
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

    // Does not store tokens, fetch records, perform network requests or affect
    // Native C++ lab sessions. This is a short-lived tenant-selection UI cache,
    // not a Supabase RLS or credential verification implementation.
    public sealed class UniversityMembershipSession
    {
        private const int MaximumMemberships = 128;
        private static readonly TimeSpan MaximumSnapshotAge = TimeSpan.FromMinutes(5);
        private readonly Dictionary<string, string> activeRoles =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private DateTimeOffset fetchedAt;
        private DateTimeOffset authExpiresAt;
        private bool hasSnapshot;

        public string UserId { get; private set; } = string.Empty;
        public string ActiveInstitutionId { get; private set; } = string.Empty;

        // Caller must supply actual Auth and membership query results; this
        // method does NOT verify their authenticity. Rejections clear old roles.
        public bool ReplaceSnapshot(string userId, DateTimeOffset fetchedAtUtc,
            DateTimeOffset authExpiresAtUtc, IReadOnlyList<UniversityMembershipRecord> memberships,
            DateTimeOffset nowUtc)
        {
            SignOut();
            if (!CanonicalUuid(userId) || memberships == null ||
                memberships.Count > MaximumMemberships || fetchedAtUtc > nowUtc ||
                nowUtc - fetchedAtUtc > MaximumSnapshotAge ||
                authExpiresAtUtc <= nowUtc)
                return false;

            var observed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in memberships)
            {
                if (record == null || !CanonicalUuid(record.InstitutionId) ||
                    !ValidRole(record.Role) || !ValidStatus(record.Status) ||
                    !observed.Add(record.InstitutionId))
                {
                    SignOut();
                    return false;
                }

                if (record.Status == "active")
                    activeRoles.Add(record.InstitutionId, record.Role);
            }

            UserId = userId;
            fetchedAt = fetchedAtUtc;
            authExpiresAt = authExpiresAtUtc;
            hasSnapshot = true;
            return true;
        }

        public bool TrySelectInstitution(string institutionId, DateTimeOffset nowUtc)
        {
            if (!CanDisplayInstitution(institutionId, nowUtc)) return false;
            ActiveInstitutionId = institutionId;
            return true;
        }

        public bool CanDisplayInstitution(string institutionId, DateTimeOffset nowUtc)
        {
            if (!SnapshotFresh(nowUtc))
            {
                SignOut();
                return false;
            }
            return CanonicalUuid(institutionId) && activeRoles.ContainsKey(institutionId);
        }

        public string SelectedRole(DateTimeOffset nowUtc)
        {
            if (!CanDisplayInstitution(ActiveInstitutionId, nowUtc))
                return string.Empty;
            return activeRoles[ActiveInstitutionId];
        }

        public void SignOut()
        {
            activeRoles.Clear();
            UserId = string.Empty;
            ActiveInstitutionId = string.Empty;
            fetchedAt = default;
            authExpiresAt = default;
            hasSnapshot = false;
        }

        private bool SnapshotFresh(DateTimeOffset nowUtc) =>
            hasSnapshot && nowUtc >= fetchedAt && nowUtc < authExpiresAt &&
            nowUtc - fetchedAt <= MaximumSnapshotAge;

        private static bool CanonicalUuid(string value)
        {
            return value != null && Guid.TryParseExact(value, "D", out var parsed) &&
                   parsed != Guid.Empty &&
                   string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal);
        }

        private static bool ValidRole(string role) =>
            role == "university_admin" || role == "instructor" ||
            role == "teaching_assistant" || role == "student";

        private static bool ValidStatus(string status) =>
            status == "active" || status == "invited" || status == "revoked";
    }
}
