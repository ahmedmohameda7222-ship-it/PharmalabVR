using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
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

        private static readonly byte[] Export = Encoding.UTF8.GetBytes("{\"native\":true}");
        private static string ExportHash()
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Export))
                    .Replace("-", "").ToLowerInvariant();
        }
        private static string Reply(string tenant, string artifact, string digest,
            string state = "staged_unverified", bool retry = false)
        {
            return "{\"state\":\"" + state + "\",\"institutionId\":\""
                + tenant + "\",\"artifactId\":\"" + artifact
                + "\",\"sha256\":\"" + digest
                + "\",\"alreadyStaged\":" + (retry ? "true" : "false") + "}";
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            public HttpStatusCode Status;
            public string ResponseBody;
            public string ApiKey;
            public bool ThrowTimeout;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (ThrowTimeout)
                    throw new TaskCanceledException("Simulated network timeout");
                ApiKey = request.Headers.Contains("apikey")
                    ? string.Join("", request.Headers.GetValues("apikey")) : null;
                return Task.FromResult(new HttpResponseMessage(Status)
                {
                    Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
                });
            }
        }

        [Test]
        public void U04_PendingRegistrationTimeoutNeverReportsCloudIdentity()
        {
            var handler = new FakeHandler { ThrowTimeout = true };
            using (var client = new HttpClient(handler))
            {
                var result = UniversityStagingSessionRegistration.RegisterPendingAsync(
                    client,
                    new Uri("https://example.supabase.co/rest/v1/rpc/university_register_staging_session"),
                    "sb_publishable_test", "student-token", Tenant,
                    "local-session", "native-branch", new string('a', 64),
                    "aaaa0000-0000-4000-8000-000000000211")
                    .GetAwaiter().GetResult();
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public void U04_OversizedSessionRegistrationReplyNeverMintsCloudIdentity()
        {
            var handler = new FakeHandler
            {
                Status = HttpStatusCode.OK,
                ResponseBody = new string('x', 8192)
            };
            using (var client = new HttpClient(handler))
            {
                var registered = UniversityStagingSessionRegistration.RegisterPendingAsync(
                    client,
                    new Uri("https://example.supabase.co/rest/v1/rpc/university_register_staging_session"),
                    "sb_publishable_test", "student-token", Tenant,
                    "local-session", "native-branch", new string('a', 64),
                    "aaaa0000-0000-4000-8000-000000000211")
                    .GetAwaiter().GetResult();
                Assert.That(registered, Is.Null,
                    "A large gateway response is not a Native session receipt.");
            }
        }

        [Test]
        public void U04_StagingNetworkTimeoutRejectsWithoutProducingEvidence()
        {
            var handler = new FakeHandler { ThrowTimeout = true };
            using (var client = new HttpClient(handler))
            {
                var result = UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "sb_publishable_test", "student-token", Export)
                    .GetAwaiter().GetResult();
                Assert.That(result, Is.EqualTo(UniversityStagingResult.Rejected));
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
                    ResponseBody = Reply(Tenant, Artifact, ExportHash(), retry: true)
                };
                using (var client = new HttpClient(handler))
                {
                    var result = UniversityStagingTransport.UploadAsync(client, Endpoint,
                        Tenant, Session, Artifact, "sb_publishable_test", "student-token",
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
        public void U04_StagingReplyMustMatchTenantArtifactAndUploadedDigest()
        {
            var handler = new FakeHandler { Status = HttpStatusCode.Created };
            using (var client = new HttpClient(handler))
            {
                var changedTenant = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
                handler.ResponseBody = Reply(changedTenant, Artifact, ExportHash());
                Assert.That(UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "sb_publishable_test", "student-token", Export)
                    .GetAwaiter().GetResult(), Is.EqualTo(UniversityStagingResult.Rejected));

                handler.ResponseBody = Reply(Tenant, Session, ExportHash());
                Assert.That(UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "sb_publishable_test", "student-token", Export)
                    .GetAwaiter().GetResult(), Is.EqualTo(UniversityStagingResult.Rejected));

                handler.ResponseBody = Reply(Tenant, Artifact, new string('0', 64));
                Assert.That(UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "sb_publishable_test", "student-token", Export)
                    .GetAwaiter().GetResult(), Is.EqualTo(UniversityStagingResult.Rejected));

                handler.ResponseBody = "{\"state\":\"denied\",\"detail\":"
                    + Reply(Tenant, Artifact, ExportHash()) + "}";
                Assert.That(UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "sb_publishable_test", "student-token", Export)
                    .GetAwaiter().GetResult(), Is.EqualTo(UniversityStagingResult.Rejected));

                handler.ResponseBody = Reply(Tenant, Artifact, ExportHash());
                Assert.That(UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "sb_publishable_test", "student-token", Export)
                    .GetAwaiter().GetResult(), Is.EqualTo(UniversityStagingResult.StagedUnverified));
            }
        }

        [Test]
        public void U04_UnconfirmedReplayAndConflictingArtifactNeverCountAsStaged()
        {
            var handler = new FakeHandler
            {
                Status = HttpStatusCode.OK,
                ResponseBody = Reply(Tenant, Artifact, ExportHash())
            };
            using (var client = new HttpClient(handler))
            {
                var rejected = UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "sb_publishable_test", "student-token",
                    Encoding.UTF8.GetBytes("{\"native\":true}"))
                    .GetAwaiter().GetResult();
                Assert.That(handler.ApiKey, Is.EqualTo("sb_publishable_test"));
                Assert.That(rejected, Is.EqualTo(UniversityStagingResult.Rejected));
                handler.Status = HttpStatusCode.Conflict;
                handler.ResponseBody = Reply(Tenant, Artifact, ExportHash(), retry: true);
                var collision = UniversityStagingTransport.UploadAsync(client, Endpoint,
                    Tenant, Session, Artifact, "sb_publishable_test", "student-token",
                    Encoding.UTF8.GetBytes("{\"native\":true}"))
                    .GetAwaiter().GetResult();
                Assert.That(collision, Is.EqualTo(UniversityStagingResult.Rejected));
            }
        }
    }
}
