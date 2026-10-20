using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        // U05 student inbox: read-only RLS-scoped assignment data, not a role
        // authorization decision or offline Native chemistry operation.
        var auth = new UniversityMembershipSession();
        Check(auth.ReplaceSnapshot(User, now, now.AddMinutes(4),
            ActiveTenants(), now) && auth.TrySelectInstitution(B, now),
            "U05 student could not select their active university.");
        var studentAssignment = "{\\"id\\":\\"11111111-1111-4111-8111-111111111121\\"," +
            "\\"institution_id\\":\\"" + B +
            "\\",\\"section_id\\":\\"11111111-1111-4111-8111-111111111122\\"," +
            "\\"title\\":\\"Introductory titration\\"," +
            "\\"published_at\\":\\"2026-10-17T10:00:00Z\\"," +
            "\\"due_at\\":\\"2026-11-01T10:00:00Z\\"}";
        using (var inboxHandler = new InboxHandler {
            Response = "[" + studentAssignment + "]" })
        using (var inboxClient = new HttpClient(inboxHandler))
        {
            var endpoint = new Uri("https://example.supabase.co/");
            var assignmentList = UniversityStudentAssignmentInbox.FetchPublishedAsync(
                inboxClient, endpoint, "sb_publishable_test", "student-token",
                auth, now).GetAwaiter().GetResult();
            Check(assignmentList.Count == 1 &&
                assignmentList[0].Title == "Introductory titration" &&
                assignmentList[0].InstitutionId == B,
                "Published same-tenant assignment was not displayed.");
            Check(inboxHandler.ApiKey == "sb_publishable_test" &&
                inboxHandler.Auth == "Bearer student-token" &&
                inboxHandler.Method == "GET" &&
                inboxHandler.Url.Contains("institution_id=eq." + B) &&
                inboxHandler.Url.Contains("published_at=not.is.null"),
                "Student inbox request omitted bearer/public key or tenant/publication filter.");

            inboxHandler.Response = "[" + studentAssignment.Replace(B, A) + "]";
            Check(UniversityStudentAssignmentInbox.FetchPublishedAsync(inboxClient,
                endpoint, "sb_publishable_test", "student-token", auth, now)
                .GetAwaiter().GetResult().Count == 0,
                "Student inbox displayed an unauthorized foreign-university row.");

            inboxHandler.Response = "[" + studentAssignment + "," + studentAssignment + "]";
            Check(UniversityStudentAssignmentInbox.FetchPublishedAsync(inboxClient,
                endpoint, "sb_publishable_test", "student-token", auth, now)
                .GetAwaiter().GetResult().Count == 0,
                "Duplicate academic assignment IDs were admitted.");

            inboxHandler.Response = "[" + studentAssignment.Replace(
                "\\"published_at\\":\\"2026-10-17T10:00:00Z\\"",
                "\\"published_at\\":null") + "]";
            Check(UniversityStudentAssignmentInbox.FetchPublishedAsync(inboxClient,
                endpoint, "sb_publishable_test", "student-token", auth, now)
                .GetAwaiter().GetResult().Count == 0,
                "An unpublished assignment leaked into the student inbox.");

            inboxHandler.Response = "[" + studentAssignment + "]";
            inboxHandler.Status = HttpStatusCode.Forbidden;
            Check(UniversityStudentAssignmentInbox.FetchPublishedAsync(inboxClient,
                endpoint, "sb_publishable_test", "student-token", auth, now)
                .GetAwaiter().GetResult().Count == 0,
                "Server RLS denial was displayed as a confirmed assignment.");
            inboxHandler.Status = HttpStatusCode.OK;
            inboxHandler.ThrowTimeout = true;
            Check(UniversityStudentAssignmentInbox.FetchPublishedAsync(inboxClient,
                endpoint, "sb_publishable_test", "student-token", auth, now)
                .GetAwaiter().GetResult().Count == 0,
                "Network timeout became a fictional academic assignment.");
            inboxHandler.ThrowTimeout = false;
            var requestCount = inboxHandler.Requests;
            Check(UniversityStudentAssignmentInbox.FetchPublishedAsync(inboxClient,
                endpoint, "sb_publishable_test", "student-token", auth,
                now.AddMinutes(6)).GetAwaiter().GetResult().Count == 0 &&
                inboxHandler.Requests == requestCount,
                "Expired tenant membership caused an authenticated HTTP request.");
            Check(auth.ReplaceSnapshot(User, now, now.AddMinutes(4),
                ActiveTenants(), now) && auth.TrySelectInstitution(A, now),
                "Instructor membership fixture could not be restored.");
            Check(UniversityStudentAssignmentInbox.FetchPublishedAsync(inboxClient,
                endpoint, "sb_publishable_test", "student-token", auth, now)
                .GetAwaiter().GetResult().Count == 0 &&
                inboxHandler.Requests == requestCount,
                "Instructor role in another tenant was used as a student inbox entitlement.");
            var secretRejected = false;
            try {
                UniversityStudentAssignmentInbox.FetchPublishedAsync(inboxClient,
                    endpoint, "sb_secret_wrong", "student-token", auth, now)
                    .GetAwaiter().GetResult();
            } catch (ArgumentException) { secretRejected = true; }
            Check(secretRejected && inboxHandler.Requests == requestCount,
                "A server secret reached the Unity student inbox HTTP client.");
        }
        Console.WriteLine("U05 student inbox test assertions exercised; no Auth/RLS live access.");
        Console.WriteLine("U02 headless presentation-state assertions: " + checks +
            " passed; NO real Supabase Auth/RLS/Unity or server authorization was exercised.");
    }

    private sealed class InboxHandler : HttpMessageHandler
    {
        public string Response;
        public string ApiKey;
        public string Auth;
        public string Method;
        public string Url;
        public int Requests;
        public bool ThrowTimeout;
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ThrowTimeout) throw new TaskCanceledException("Simulated network timeout");
            Requests++;
            ApiKey = request.Headers.Contains("apikey")
                ? string.Join("", request.Headers.GetValues("apikey")) : null;
            Auth = request.Headers.Authorization?.ToString();
            Method = request.Method.Method;
            Url = request.RequestUri.ToString();
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Response ?? "[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
