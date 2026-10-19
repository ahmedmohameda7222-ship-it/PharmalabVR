using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PharmaLabVR.University;

namespace PharmaLabVR.Tests.EditMode
{
    // Real Unity EditMode test source; execution deferred until final local campaign.
    public sealed class UniversityStagingTransportTests
    {
        private const string Tenant = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
        private const string Session = "aaaa0000-0000-4000-8000-000000000051";
        private const string Artifact = "aaaa0000-0000-4000-8000-000000000082";
        private static readonly Uri Endpoint =
            new Uri("https://example.supabase.co/functions/v1/student-evidence-upload");

        private sealed class FakeHandler : HttpMessageHandler
        {
            public HttpStatusCode Status;
            public string ResponseBody;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(Status)
                {
                    Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
                });
            }
        }

        [Test]
        public void U04_ExactStagedRetryNeverAcknowledgesNativeOutbox()
        {
            var folder = Path.Combine(Path.GetTempPath(),
                "plv-staging-retry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var outbox = new UniversitySyncOutbox(Path.Combine(folder, "pending.plvoutbox"));
                Assert.That(outbox.Enqueue(new UniversityUploadIntent(
                    Tenant, "local-session", Artifact, "branch-a", "4",
                    new string('a', 64), new string('b', 64), new string('c', 64))),
                    Is.True);
                var handler = new FakeHandler
                {
                    Status = HttpStatusCode.OK,
                    ResponseBody = "{\"state\":\"staged_unverified\",\"alreadyStaged\":true}"
                };
                using (var client = new HttpClient(handler))
                {
                    var result = UniversityStagingTransport.UploadAsync(client, Endpoint,
                        Tenant, Session, Artifact, "student-token",
                        Encoding.UTF8.GetBytes("{\"native\":true}"))
                        .GetAwaiter().GetResult();
                    Assert.That(result, Is.EqualTo(UniversityStagingResult.StagedUnverified));
                }
                Assert.That(outbox.Pending().Count, Is.EqualTo(1),
                    "An exact quarantine retry is never a signed Native sync acknowledgement.");
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void U04_UnconfirmedReplayAndConflictingArtifactNeverCountAsStaged()
        {
            var handler = new FakeHandler
            {
                Status = HttpStatusCode.OK,
                ResponseBody = "{\"state\":\"staged_unverified\",\"alreadyStaged\":false}"
            };
            using (var client = new HttpClient(handler))
            {
                var rejected = UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "student-token",
                    Encoding.UTF8.GetBytes("{\"native\":true}"))
                    .GetAwaiter().GetResult();
                Assert.That(rejected, Is.EqualTo(UniversityStagingResult.Rejected));
                handler.Status = HttpStatusCode.Conflict;
                handler.ResponseBody = "{\"state\":\"staged_unverified\",\"alreadyStaged\":true}";
                var collision = UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "student-token",
                    Encoding.UTF8.GetBytes("{\"native\":true}"))
                    .GetAwaiter().GetResult();
                Assert.That(collision, Is.EqualTo(UniversityStagingResult.Rejected));
            }
        }
    }
}
