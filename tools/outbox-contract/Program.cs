using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PharmaLabVR.University;

internal static class Program
{
    private static int assertions;
    private static void Check(bool valid, string message)
    {
        assertions++;
        if (!valid) throw new InvalidOperationException(message);
    }

    private static void MustReject(Action action, string message)
    {
        assertions++;
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException(message);
    }

    private static UniversityUploadIntent Sample(string tenant, string receipt = null,
        string sequence = "17")
    {
        return new UniversityUploadIntent(tenant, "local-session-1",
            "11111111-1111-4111-8111-111111111111", "native-branch-1",
            sequence, receipt ?? new string('a', 64), new string('b', 64),
            new string('c', 64));
    }

    private static void Main()
    {
        const string tenantA = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
        const string tenantB = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
        var directory = Path.Combine(Path.GetTempPath(), "plv-u04-outbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "pending.plvoutbox");
        try
        {
            var store = new UniversitySyncOutbox(path, 8192);
            Check(store.Pending().Count == 0, "Fresh offline outbox must be empty.");
            var a = Sample(tenantA);
            Check(store.Enqueue(a), "First offline Native receipt reference was not durably staged.");
            Check(File.Exists(path), "Enqueue reported success without a durable local file.");
            var committed = File.ReadAllBytes(path);
            Check(!store.Enqueue(a), "Exact retransmission must not create a second intent.");
            Check(Convert.ToBase64String(committed) == Convert.ToBase64String(File.ReadAllBytes(path)),
                "Exact replay modified confirmed durable bytes.");

            var restarted = new UniversitySyncOutbox(path, 8192);
            Check(restarted.Pending().Count == 1, "Reopening outbox lost pending Native receipt.");
            Check(restarted.Pending()[0].NativeEventSequence == "17",
                "Native revision changed during offline restart.");
            MustReject(() => restarted.Enqueue(Sample(tenantA, new string('d', 64))),
                "Same tenant/session/event ID with different receipt was overwritten.");
            Check(restarted.Pending().Count == 1, "Conflict silently added another intent.");
            Check(restarted.Enqueue(Sample(tenantB)),
                "Matching event IDs from distinct universities were collapsed.");
            Check(restarted.Pending().Count == 2,
                "Durable queue did not keep separate university intents.");

            MustReject(() => restarted.Enqueue(Sample("not-a-tenant")),
                "Malformed university ID was accepted.");
            MustReject(() => restarted.Enqueue(Sample(tenantA, null, "017")),
                "Noncanonical Native event sequence was accepted.");
            MustReject(() => restarted.Enqueue(Sample(tenantA, "not-a-sha256")),
                "Unverifiable Native receipt hash was accepted.");
            Check(restarted.Pending().Count == 2, "Rejected intents mutated confirmed outbox.");

            // Simulate failed staging: the confirmed file must remain intact.
            Directory.CreateDirectory(path + ".tmp");
            var previous = Convert.ToBase64String(File.ReadAllBytes(path));
            MustReject(() => restarted.Enqueue(new UniversityUploadIntent(
                tenantA, "local-session-1", "22222222-2222-4222-8222-222222222222",
                "native-branch-1", "18", new string('a', 64),
                new string('b', 64), new string('c', 64))),
                "Blocked staging unexpectedly acknowledged a new intent.");
            Check(previous == Convert.ToBase64String(File.ReadAllBytes(path)),
                "Stage failure replaced confirmed intents.");
            Directory.Delete(path + ".tmp");

            // A second producer must not write through an exclusive local
            // lease held by a first producer; this is deterministic, not a
            // flaky thread interleaving test.
            var third = new UniversityUploadIntent(tenantB, "local-session-2",
                "33333333-3333-4333-8333-333333333333", "native-branch-2",
                "19", new string('a', 64), new string('b', 64),
                new string('c', 64));
            var beforeLock = Convert.ToBase64String(File.ReadAllBytes(path));
            using (var ownedLock = new FileStream(path + ".lock",
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                MustReject(() => restarted.Enqueue(third),
                    "Concurrent producer bypassed the exclusive writer lease.");
                Check(beforeLock == Convert.ToBase64String(File.ReadAllBytes(path)),
                    "Locked producer rewrote confirmed intent bytes.");
            }
            Check(restarted.Enqueue(third),
                "Pending producer could not retry after released writer lease.");
            Check(new UniversitySyncOutbox(path, 8192).Pending().Count == 3,
                "Exclusive writer lease lost an independently queued event.");

            // U04: evidence digests must derive from real byte arrays before
            // a new upload intent is staged; a caller's SHA text is not proof.
            var receiptBytes = System.Text.Encoding.UTF8.GetBytes("native-receipt-v1");
            var packageBytes = System.Text.Encoding.UTF8.GetBytes("pinned-science-package");
            var evidenceBytes = System.Text.Encoding.UTF8.GetBytes("full-native-export-json");
            string Hash(byte[] bytes)
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "")
                        .ToLowerInvariant();
            }
            var verifiedIntent = new UniversityUploadIntent(tenantA,"local-session-2",
                "44444444-4444-4444-8444-444444444444","native-branch-2","21",
                Hash(receiptBytes),Hash(packageBytes),Hash(evidenceBytes));
            var verifier = new UniversitySyncOutbox(Path.Combine(directory,"verified.plvoutbox"));
            Check(verifier.EnqueueVerifiedEvidence(verifiedIntent,receiptBytes,
                packageBytes,evidenceBytes),
                "Actual Native bytes could not be checked before staging.");
            Check(verifier.Pending().Count == 1, "Verified receipt was not persisted.");
            MustReject(() => verifier.EnqueueVerifiedEvidence(verifiedIntent,receiptBytes,
                packageBytes,System.Text.Encoding.UTF8.GetBytes("altered-export")),
                "Tampered Native export was accepted.");
            MustReject(() => verifier.EnqueueVerifiedEvidence(verifiedIntent,null,
                packageBytes,evidenceBytes),
                "Missing Native receipt bytes were accepted.");
            Check(verifier.Pending().Count == 1,
                "Rejected evidence changed the durable outbox.");

            // U04 staging HTTP is deliberately not a Native attestation or
            // a queue acknowledgement. Use a fake HTTP handler, no network.
            var mock = new RecordingHandler();
            using (var client = new HttpClient(mock))
            {
                var endpoint = new Uri("https://example.supabase.co/functions/v1/student-evidence-upload");
                var uploadId = "aaaa0000-0000-4000-8000-000000000082";
                var registeredSessionId = "aaaa0000-0000-4000-8000-000000000051";
                var outcome = UniversityStagingTransport.UploadAsync(client, endpoint,
                    tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                    System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"))
                    .GetAwaiter().GetResult();
                Check(outcome == UniversityStagingResult.StagedUnverified,
                    "A successful staged response must remain UNVERIFIED.");
                Check(mock.RequestCount == 1 && mock.Method == "POST",
                    "Staging did not submit one authenticated POST.");
                Check(mock.ApiKey == "sb_publishable_test",
                    "Student staging HTTP request omitted the public Supabase gateway apikey.");
                Check(mock.Auth == "Bearer student-token" &&
                    mock.Session == registeredSessionId && mock.Artifact == uploadId,
                    "Staging request omitted identity or Authorization.");
                Check(mock.Body == "{\"export\":true}" &&
                    mock.BodyDigest == Hash(System.Text.Encoding.UTF8.GetBytes(mock.Body)),
                    "Staged evidence bytes or computed header SHA were altered.");
                Check(verifier.Pending().Count == 1,
                    "Quarantine-only result removed the Native outbox intent.");
                // U04 response must bind tenant, artifact and exact uploaded
                // digest; a vague staged status is not sufficient evidence
                // that *this* artifact reached quarantine.
                var firstStagingSha=Hash(System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"));
                MustReject(() => UniversityStagingTransport.UploadAsync(client, endpoint,
                        tenantA, registeredSessionId, uploadId, "sb_secret_service_credentials",
                        "student-token", evidenceBytes).GetAwaiter().GetResult(),
                    "Server secret was accepted as a Unity publishable apikey.");
                mock.ThrowTimeout = true;
                Check(UniversityStagingTransport.UploadAsync(client, endpoint,
                    tenantA, registeredSessionId, uploadId, "sb_publishable_test",
                    "student-token", evidenceBytes).GetAwaiter().GetResult()
                    == UniversityStagingResult.Rejected,
                    "Transport timeout must reject only the staging attempt.");
                mock.ThrowTimeout = false;
                Check(verifier.Pending().Count == 1,
                    "Timed-out quarantine attempt acknowledged the durable Native outbox.");
                MustReject(() => UniversityStagingTransport.UploadAsync(client, endpoint,
                        tenantA, registeredSessionId, uploadId, "bad\\r\\nkey",
                        "student-token", evidenceBytes).GetAwaiter().GetResult(),
                    "A public apikey header-injection payload was accepted.");
                mock.ResponseBody = "{\"state\":\"staged_unverified\","
                    + "\"institutionId\":\"" + tenantB + "\","
                    + "\"artifactId\":\"" + uploadId + "\","
                    + "\"sha256\":\"" + firstStagingSha + "\"}";
                Check(UniversityStagingTransport.UploadAsync(client, endpoint,
                      tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                      System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"))
                      .GetAwaiter().GetResult() == UniversityStagingResult.Rejected,
                    "Staging response from a foreign institution was accepted.");
                mock.ResponseBody = "{\"state\":\"staged_unverified\","
                    + "\"institutionId\":\"" + tenantA + "\","
                    + "\"artifactId\":\"" + uploadId + "\","
                    + "\"sha256\":\"" + new string('0',64) + "\"}";
                Check(UniversityStagingTransport.UploadAsync(client, endpoint,
                      tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                      System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"))
                      .GetAwaiter().GetResult() == UniversityStagingResult.Rejected,
                    "Staging response for different uploaded bytes was accepted.");
                mock.ResponseBody = "{\"state\":\"error\",\"detail\":{"
                    + "\"state\":\"staged_unverified\",\"alreadyStaged\":true},"
                    + "\"institutionId\":\"" + tenantA + "\","
                    + "\"artifactId\":\"" + uploadId + "\","
                    + "\"sha256\":\"" + firstStagingSha + "\"}";
                Check(UniversityStagingTransport.UploadAsync(client, endpoint,
                      tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                      System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"))
                      .GetAwaiter().GetResult() == UniversityStagingResult.Rejected,
                    "Nested staged status was mistaken for top-level approval.");
                mock.ResponseBody = "{\"state\":\"staged_unverified\","
                    + "\"institutionId\":\"" + tenantA + "\","
                    + "\"artifactId\":\"" + uploadId + "\","
                    + "\"sha256\":\"" + firstStagingSha + "\"}";
                Check(UniversityStagingTransport.UploadAsync(client, endpoint,
                      tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                      System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"))
                      .GetAwaiter().GetResult() == UniversityStagingResult.StagedUnverified,
                    "Matching server staging receipt was unexpectedly rejected.");


                // U04 exact duplicate is a staged-only retry, NEVER an
                // authenticated Native synchronization acknowledgement.
                mock.Status = HttpStatusCode.OK;
                mock.ResponseBody = "{\"state\":\"staged_unverified\",\"alreadyStaged\":true,"
                    + "\"institutionId\":\"" + tenantA + "\","
                    + "\"artifactId\":\"" + uploadId + "\","
                    + "\"sha256\":\"" + firstStagingSha + "\"}";
                Check(UniversityStagingTransport.UploadAsync(client, endpoint,
                      tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                      System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"))
                      .GetAwaiter().GetResult() == UniversityStagingResult.StagedUnverified,
                    "Verified exact replay was rejected as if first upload failed.");
                Check(verifier.Pending().Count == 1,
                    "Staging retry falsely acknowledged the durable Native outbox.");
                mock.ResponseBody = "{\"state\":\"staged_unverified\",\"alreadyStaged\":false,"
                    + "\"institutionId\":\"" + tenantA + "\","
                    + "\"artifactId\":\"" + uploadId + "\","
                    + "\"sha256\":\"" + firstStagingSha + "\"}";
                Check(UniversityStagingTransport.UploadAsync(client, endpoint,
                      tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                      System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"))
                      .GetAwaiter().GetResult() == UniversityStagingResult.Rejected,
                    "Bare 200 response lacking confirmed matching retry was accepted.");
                mock.ResponseBody = "{\"state\":\"staged_unverified\",\"alreadyStaged\":true,"
                    + "\"institutionId\":\"" + tenantA + "\","
                    + "\"artifactId\":\"" + uploadId + "\","
                    + "\"sha256\":\"" + firstStagingSha + "\"}";

                MustReject(() => UniversityStagingTransport.UploadAsync(client, endpoint,
                        tenantA, registeredSessionId, uploadId, "sb_publishable_test", "bad\r\ntoken",
                        evidenceBytes).GetAwaiter().GetResult(),
                    "A header-injection token was accepted.");
                MustReject(() => UniversityStagingTransport.UploadAsync(client,
                        new Uri("http://example.supabase.co/functions/v1/student-evidence-upload"),
                        tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                        evidenceBytes).GetAwaiter().GetResult(),
                    "HTTP plaintext leaked bearer credentials.");
                MustReject(() => UniversityStagingTransport.UploadAsync(client, endpoint,
                        tenantA, "local-session-2", uploadId, "sb_publishable_test", "student-token",
                        evidenceBytes).GetAwaiter().GetResult(),
                    "Unregistered local session label was used instead of a UUID.");
                MustReject(() => UniversityStagingTransport.UploadAsync(client, endpoint,
                        tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                        new byte[1048577]).GetAwaiter().GetResult(),
                    "Oversized Unity upload bypassed preflight size cap.");
                Check(mock.RequestCount == 7,
                    "Rejected local arguments reached HTTP, or expected response regressions were skipped.");
                mock.Status=HttpStatusCode.Forbidden;
                Check(UniversityStagingTransport.UploadAsync(client, endpoint,
                      tenantA, registeredSessionId, uploadId, "sb_publishable_test", "student-token",
                      System.Text.Encoding.UTF8.GetBytes("{\"export\":true}"))
                      .GetAwaiter().GetResult() == UniversityStagingResult.Rejected,
                    "Rejected HTTP response falsely acknowledged staging.");
                Check(verifier.Pending().Count == 1,
                    "Failed cloud staging modified the durable Native queue.");
            }

            // Server registration returns only an untrusted pending DB session
            // UUID. It must never be interpreted as a Native receipt or ack.
            var registrationHandler = new RecordingHandler();
            var newSessionUuid = "aaaa0000-0000-4000-8000-000000000051";
            registrationHandler.Status = HttpStatusCode.OK;
            registrationHandler.ResponseBody = "\"" + newSessionUuid + "\"";
            using (var authClient = new HttpClient(registrationHandler))
            {
                var rpcEndpoint = new Uri("https://example.supabase.co/rest/v1/rpc/university_register_staging_session");
                var registered = UniversityStagingSessionRegistration.RegisterPendingAsync(
                    authClient,rpcEndpoint,"sb_publishable_test","student-token",
                    tenantA,"local-session-2","native-branch-2",new string('b',64),
                    "aaaa0000-0000-4000-8000-000000000201").GetAwaiter().GetResult();
                Check(registered == newSessionUuid,"Pending staging RPC returned invalid session UUID.");
                Check(registrationHandler.ApiKey == "sb_publishable_test" &&
                    registrationHandler.Auth == "Bearer student-token" &&
                    registrationHandler.Body.Contains("\"p_local_session_id\":\"local-session-2\"") &&
                    registrationHandler.Method == "POST",
                    "Pending staging RPC omitted Auth/publishable key or local session identity.");
                Check(verifier.Pending().Count==1,
                    "Registering a cloud staging session cleared the Native outbox.");
                MustReject(() => UniversityStagingSessionRegistration.RegisterPendingAsync(
                    authClient,rpcEndpoint,"sb_publishable_test","student-token",
                    tenantA,"../local","native-branch-2",new string('b',64),
                    "aaaa0000-0000-4000-8000-000000000201").GetAwaiter().GetResult(),
                    "Traversal-like local session ID accepted by staging RPC transport.");
                MustReject(() => UniversityStagingSessionRegistration.RegisterPendingAsync(
                    authClient,rpcEndpoint,"sb_secret_service_credentials","student-token",
                    tenantA,"local-session-2","native-branch-2",new string('b',64),
                    "aaaa0000-0000-4000-8000-000000000201").GetAwaiter().GetResult(),
                    "Registration transport accepted a server secret as a publishable key.");
                registrationHandler.Status = HttpStatusCode.Forbidden;
                Check(UniversityStagingSessionRegistration.RegisterPendingAsync(
                    authClient,rpcEndpoint,"sb_publishable_test","student-token",
                    tenantA,"local-session-2","native-branch-2",new string('b',64),
                    "aaaa0000-0000-4000-8000-000000000201").GetAwaiter().GetResult()==null,
                    "Denied registration unexpectedly provided a cloud session ID.");
                Check(verifier.Pending().Count==1,
                    "Denied session registration removed pending Native evidence.");
            }

            var damaged = File.ReadAllBytes(path);
            damaged[damaged.Length - 2] ^= 0x01;
            File.WriteAllBytes(path, damaged);
            MustReject(() => new UniversitySyncOutbox(path, 8192).Pending(),
                "Tampered stored intent silently passed integrity verification.");
            MustReject(() => new UniversitySyncOutbox(path, 8192).Enqueue(Sample(tenantA)),
                "Corrupt outbox was silently overwritten.");
            Console.WriteLine("U04 offline outbox: " + assertions +
                " headless assertions passed; no Supabase connection or Unity/Native integration.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal int RequestCount;
        internal string Method;
        internal string Auth;
        internal string Session;
        internal string Artifact;
        internal string Body;
        internal string ApiKey;
        internal string ResponseBody;
        internal string BodyDigest;
        internal HttpStatusCode Status = HttpStatusCode.Created;
        internal bool ThrowTimeout;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ThrowTimeout) throw new TaskCanceledException("Simulated staging network timeout");
            RequestCount++;
            Method = request.Method.Method;
            Auth = request.Headers.Authorization?.ToString();
            Session = string.Join("", (request.Headers.Contains("x-session-id") ? request.Headers.GetValues("x-session-id") : Array.Empty<string>()));
            Artifact = string.Join("", (request.Headers.Contains("x-artifact-id") ? request.Headers.GetValues("x-artifact-id") : Array.Empty<string>()));
            BodyDigest = string.Join("", (request.Headers.Contains("x-evidence-sha256") ? request.Headers.GetValues("x-evidence-sha256") : Array.Empty<string>()));
            Body = await request.Content.ReadAsStringAsync();
            ApiKey = request.Headers.Contains("apikey")
                ? string.Join("",request.Headers.GetValues("apikey")) : null;
            var tenant = request.Headers.Contains("x-institution-id")
                ? string.Join("", request.Headers.GetValues("x-institution-id")) : "";
            var reply = ResponseBody ?? ("{\"state\":\"staged_unverified\","
                + "\"institutionId\":\"" + tenant + "\","
                + "\"artifactId\":\"" + Artifact + "\","
                + "\"sha256\":\"" + BodyDigest + "\"}");
            return new HttpResponseMessage(Status) {
                Content = new StringContent(reply)
            };
        }
    }
}
