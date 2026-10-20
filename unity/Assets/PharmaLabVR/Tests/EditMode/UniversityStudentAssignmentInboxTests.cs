using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PharmaLabVR.University;

namespace PharmaLabVR.Tests.EditMode
{
    // Authored Unity EditMode source; actual Unity run is deferred.
    // Handler models HTTP bytes only: it does not replace server Auth or RLS.
    public sealed class UniversityStudentAssignmentInboxTests
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            public string Body;
            public int Requests;
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Body, Encoding.UTF8, "application/json")
                });
            }
        }

        [Test]
        public void U05_StudentInboxIsTenantBoundAndNeverInventsOfflineAssignments()
        {
            const string tenant = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
            var clock = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
            var snapshot = new UniversityMembershipSession();
            Assert.That(snapshot.ReplaceSnapshot(
                "11111111-1111-4111-8111-111111111111", clock, clock.AddMinutes(4),
                new [] { new UniversityMembershipRecord(tenant, "student", "active") },
                clock), Is.True);
            Assert.That(snapshot.TrySelectInstitution(tenant, clock), Is.True);
            var handler = new FakeHandler
            {
                Body = "[{\"id\":\"11111111-1111-4111-8111-111111111121\",\"institution_id\":\"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa\",\"section_id\":\"11111111-1111-4111-8111-111111111122\",\"title\":\"Lab titration\",\"published_at\":\"2026-10-17T10:00:00Z\",\"due_at\":\"2026-11-01T10:00:00Z\"}]"
            };
            using (var client = new HttpClient(handler))
            {
                var endpoint = new Uri("https://example.supabase.co/");
                var authorized = UniversityStudentAssignmentInbox.FetchPublishedAsync(
                    client, endpoint, "sb_publishable_test", "student-token", snapshot, clock)
                    .GetAwaiter().GetResult();
                Assert.That(authorized.Count, Is.EqualTo(1));
                Assert.That(authorized[0].Title, Is.EqualTo("Lab titration"));
                handler.Body = "[{\"id\":\"11111111-1111-4111-8111-111111111121\",\"institution_id\":\"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb\",\"section_id\":\"11111111-1111-4111-8111-111111111122\",\"title\":\"Lab titration\",\"published_at\":\"2026-10-17T10:00:00Z\",\"due_at\":\"2026-11-01T10:00:00Z\"}]";
                Assert.That(UniversityStudentAssignmentInbox.FetchPublishedAsync(
                    client, endpoint, "sb_publishable_test", "student-token", snapshot, clock)
                    .GetAwaiter().GetResult(), Is.Empty,
                    "Foreign institution rows cannot be displayed.");
                var previousRequests = handler.Requests;
                Assert.That(UniversityStudentAssignmentInbox.FetchPublishedAsync(
                    client, endpoint, "sb_publishable_test", "student-token", snapshot,
                    clock.AddMinutes(6)).GetAwaiter().GetResult(), Is.Empty);
                Assert.That(handler.Requests, Is.EqualTo(previousRequests),
                    "Expired institutional context must not cause cloud requests.");
            }
        }
    }
}
