using System;
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
    // Real Unity NUnit source: Editor execution deferred to final local campaign.
    public sealed class UniversityStagedEvidenceStatusTests
    {
        private const string Tenant = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
        private const string Artifact = "aaaa0000-0000-4000-8000-000000000083";
        private static readonly Uri Endpoint = new Uri(
            "https://example.supabase.co/rest/v1/rpc/university_staged_evidence_status");
        private static readonly byte[] Bytes = Encoding.UTF8.GetBytes("{\"native\":true}");

        private sealed class Handler : HttpMessageHandler
        {
            public string Reply;
            public HttpStatusCode Status = HttpStatusCode.OK;
            public bool Timeout;
            public string ApiKey;
            public string Authorization;
            public string RequestBody;

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (Timeout) throw new TaskCanceledException("Simulated loss of response.");
                ApiKey = request.Headers.Contains("apikey")
                    ? string.Join("", request.Headers.GetValues("apikey")) : null;
                Authorization = request.Headers.Authorization?.ToString();
                RequestBody = await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(Status) {
                    Content = new StringContent(Reply, Encoding.UTF8, "application/json")
                };
            }
        }

        private static string Digest()
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string Reply(string state, string artifact, string sha, long size)
        {
            return "{\"state\":\"" + state + "\",\"artifactId\":\"" + artifact +
                "\",\"sha256\":\"" + sha + "\",\"sizeBytes\":" + size + "}";
        }
        private static UniversityStagingResult Read(Handler handler, string key = "sb_publishable_test")
        {
            using (var client = new HttpClient(handler))
                return UniversityStagedEvidenceStatus.ReconcileAsync(
                    client, Endpoint, key, "student-token", Tenant, Artifact, Bytes)
                    .GetAwaiter().GetResult();
        }

        [Test]
        public void U04_MatchingPrivateStagingMetadataIsUnverifiedOnly()
        {
            var h = new Handler { Reply = Reply("staged_unverified", Artifact, Digest(), Bytes.Length) };
            Assert.That(Read(h), Is.EqualTo(UniversityStagingResult.StagedUnverified));
            Assert.That(h.ApiKey, Is.EqualTo("sb_publishable_test"));
            Assert.That(h.Authorization, Is.EqualTo("Bearer student-token"));
            Assert.That(h.RequestBody, Does.Contain("\"p_artifact_id\""));
            Assert.That(h.RequestBody, Does.Contain(Artifact));
        }

        [Test]
        public void U04_ForgedSyncOrDifferentDigestCannotAcknowledgeOutbox()
        {
            var h = new Handler { Reply = Reply("synced", Artifact, Digest(), Bytes.Length) };
            Assert.That(Read(h), Is.EqualTo(UniversityStagingResult.Rejected));
            h.Reply = Reply("staged_unverified", Artifact, new string('0', 64), Bytes.Length);
            Assert.That(Read(h), Is.EqualTo(UniversityStagingResult.Rejected));
            h.Reply = Reply("staged_unverified", Artifact, Digest(), Bytes.Length + 1);
            Assert.That(Read(h), Is.EqualTo(UniversityStagingResult.Rejected));
        }

        [Test]
        public void U04_ForbiddenTimeoutOrSecretKeyCannotRecoverStaging()
        {
            var h = new Handler { Status = HttpStatusCode.Forbidden,
                Reply = Reply("staged_unverified", Artifact, Digest(), Bytes.Length) };
            Assert.That(Read(h), Is.EqualTo(UniversityStagingResult.Rejected));
            h.Status = HttpStatusCode.OK;
            h.Timeout = true;
            Assert.That(Read(h), Is.EqualTo(UniversityStagingResult.Rejected));
            h.Timeout = false;
            Assert.Throws<ArgumentException>(() => Read(h, "sb_secret_not_for_unity"));
        }
    }
}
