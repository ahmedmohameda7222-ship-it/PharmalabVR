using System;
using PharmaLabVR.University;

internal static class Program
{
    private static int checks;
    private const string A = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string B = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
    private const string Unknown = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";
    private const string User = "11111111-1111-4111-8111-111111111111";

    private static void Check(bool truth, string message)
    {
        checks++;
        if (!truth) throw new InvalidOperationException(message);
    }

    private static UniversityMembershipRecord[] ActiveTenants() => new[]
    {
        new UniversityMembershipRecord(A, "instructor", "active"),
        new UniversityMembershipRecord(B, "student", "active")
    };

    private static void Main()
    {
        var now = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);
        var context = new UniversityMembershipSession();
        Check(!context.CanDisplayInstitution(A, now),
            "No university may be shown before fresh authenticated membership retrieval.");
        Check(!context.TrySelectInstitution(A, now),
            "Unauthenticated tenant selection was accepted.");

        Check(context.ReplaceSnapshot(User, now, now.AddMinutes(3), ActiveTenants(), now),
            "Fresh two-tenant membership snapshot was refused.");
        Check(context.UserId == User, "Authenticated account identity lost.");
        Check(context.TrySelectInstitution(A, now), "Active A instructor not selectable.");
        Check(context.ActiveInstitutionId == A && context.SelectedRole(now) == "instructor",
            "Role was not scoped to university A.");
        Check(!context.TrySelectInstitution(Unknown, now),
            "Forged unrelated institution ID was selected.");
        Check(context.ActiveInstitutionId == A,
            "Rejected institution changed currently selected university.");
        Check(context.TrySelectInstitution(B, now), "Active B student not selectable.");
        Check(context.ActiveInstitutionId == B && context.SelectedRole(now) == "student",
            "A instructor role leaked into B student membership.");

        Check(!context.CanDisplayInstitution(A, now.AddMinutes(4)),
            "Expired authentication continued to show institutional data.");
        Check(context.ActiveInstitutionId == string.Empty && context.UserId == string.Empty,
            "Session expiry did not erase cached university selector.");

        Check(context.ReplaceSnapshot(User, now, now.AddMinutes(3), ActiveTenants(), now),
            "Fresh session could not be reinstalled.");
        Check(context.TrySelectInstitution(A, now), "Prior A membership not selectable.");
        var revoked = new[]
        {
            new UniversityMembershipRecord(A, "instructor", "revoked"),
            new UniversityMembershipRecord(B, "student", "active")
        };
        Check(context.ReplaceSnapshot(User, now.AddSeconds(10), now.AddMinutes(4),
            revoked, now.AddSeconds(10)), "Revocation refresh was rejected.");
        Check(context.ActiveInstitutionId == string.Empty,
            "Old selection survived a refreshed membership snapshot.");
        Check(!context.TrySelectInstitution(A, now.AddSeconds(10)),
            "Revoked instructor could select old institution.");
        Check(context.TrySelectInstitution(B, now.AddSeconds(10)),
            "Remaining B student membership became unusable.");
        Check(context.SelectedRole(now.AddSeconds(10)) == "student",
            "Role not refreshed after tenant revocation.");

        var invited = new[] { new UniversityMembershipRecord(A, "instructor", "invited") };
        Check(context.ReplaceSnapshot(User, now, now.AddMinutes(4), invited, now),
            "An identity without active membership must still allow offline practice.");
        Check(!context.TrySelectInstitution(A, now), "Invitation incorrectly grants membership.");
        Check(context.UserId == User, "Valid account must remain signed in without active tenants.");

        var duplicate = new[]
        {
            new UniversityMembershipRecord(A, "instructor", "active"),
            new UniversityMembershipRecord(A, "student", "active")
        };
        Check(!context.ReplaceSnapshot(User, now, now.AddMinutes(4), duplicate, now),
            "Ambiguous tenant roles were silently resolved.");
        Check(context.UserId == string.Empty && context.ActiveInstitutionId == string.Empty,
            "Failed membership refresh retained a stale institutional role.");
        Check(!context.ReplaceSnapshot(User, now.AddMinutes(2), now.AddMinutes(4),
            ActiveTenants(), now), "Future-dated membership refresh was accepted.");
        Check(!context.ReplaceSnapshot(User, now.AddMinutes(-6), now.AddMinutes(4),
            ActiveTenants(), now), "Over-age membership refresh was accepted.");
        Check(!context.ReplaceSnapshot(User, now, now.AddMinutes(-1), ActiveTenants(), now),
            "Expired Auth expiry was accepted.");
        Check(!context.ReplaceSnapshot("not-a-uuid", now, now.AddMinutes(4),
            ActiveTenants(), now), "Forged Auth user ID format accepted.");
        Check(!context.ReplaceSnapshot(User, now, now.AddMinutes(4),
            new[] { new UniversityMembershipRecord(Unknown, "platform_admin", "active") }, now),
            "Unrecognized tenant role was accepted.");
        Check(!context.ReplaceSnapshot(User, now, now.AddMinutes(4),
            new[] { new UniversityMembershipRecord("bad-uuid", "student", "active") }, now),
            "Malformed institution ID was accepted.");
        Check(!context.ReplaceSnapshot(User, now, now.AddMinutes(4), null, now),
            "Null membership list was accepted.");

        Check(context.ReplaceSnapshot(User, now, now.AddMinutes(4), ActiveTenants(), now),
            "Fresh session for explicit sign out failed.");
        Check(context.TrySelectInstitution(B, now), "B unavailable before sign out.");
        context.SignOut();
        Check(!context.CanDisplayInstitution(B, now) && context.SelectedRole(now) == string.Empty,
            "Sign-out left membership or role visible.");
        Console.WriteLine("U02 headless presentation-state assertions: " + checks +
            " passed; NO real Supabase Auth/RLS/Unity or server authorization was exercised.");
    }
}
