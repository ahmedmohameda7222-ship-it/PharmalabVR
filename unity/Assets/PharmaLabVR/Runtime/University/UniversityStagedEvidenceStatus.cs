using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PharmaLabVR.University
{
    // U04 server-side staging status is only a quarantine observation.
    // This adapter never returns a Native receipt or alters the durable outbox.
    public static class UniversityStagedEvidenceStatus
    {
        private const int MaxEvidenceBytes = 1048576;
        private const int MaxResponseBytes = 4096;
        private const string RpcPath =
            "/rest/v1/rpc/university_staged_evidence_status";
        private static readonly Regex Uuid = new Regex(
            @"\A[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z",
            RegexOptions.CultureInvariant);
        private static readonly Regex PublicGatewayKey = new Regex(
            @"\Asb_publishable_[A-Za-z0-9_-]{1,497}\z",
            RegexOptions.CultureInvariant);
        private static readonly Regex AccessToken = new Regex(
            @"\A[A-Za-z0-9._~-]{1,8192}\z",
            RegexOptions.CultureInvariant);

        [DataContract]
        private sealed class StatusReply
        {
            [DataMember(Name = "state")] public string State { get; set; }
            [DataMember(Name = "artifactId")] public string ArtifactId { get; set; }
            [DataMember(Name = "sha256")] public string Sha256 { get; set; }
            [DataMember(Name = "sizeBytes")] public long SizeBytes { get; set; }
        }

        public static async Task<UniversityStagingResult> ReconcileAsync(
            HttpClient client, Uri rpcEndpoint, string publishableKey,
            string accessToken, string institutionId, string artifactId,
            byte[] originalBytes, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (rpcEndpoint == null || !rpcEndpoint.IsAbsoluteUri ||
                rpcEndpoint.Scheme != Uri.UriSchemeHttps || !rpcEndpoint.IsDefaultPort ||
                !rpcEndpoint.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rpcEndpoint.AbsolutePath, RpcPath, StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(rpcEndpoint.UserInfo) ||
                !string.IsNullOrEmpty(rpcEndpoint.Query) ||
                !string.IsNullOrEmpty(rpcEndpoint.Fragment))
                throw new ArgumentException("Expected HTTPS Supabase staging status RPC.", nameof(rpcEndpoint));
            if (!Uuid.IsMatch(institutionId ?? "") || !Uuid.IsMatch(artifactId ?? "") ||
                !PublicGatewayKey.IsMatch(publishableKey ?? "") ||
                !AccessToken.IsMatch(accessToken ?? ""))
                throw new ArgumentException("Canonical university, artifact and public Auth credentials required.");
            if (originalBytes == null || originalBytes.Length < 1 ||
                originalBytes.Length > MaxEvidenceBytes)
                throw new InvalidDataException("Original bounded evidence bytes required.");

            string originalSha;
            using (var sha = SHA256.Create())
                originalSha = BitConverter.ToString(sha.ComputeHash(originalBytes))
                    .Replace("-", "").ToLowerInvariant();
            // Values are strict ASCII UUIDs; no arbitrary JSON interpolation.
            var payload = "{\"p_institution_id\":\"" + institutionId +
                "\",\"p_artifact_id\":\"" + artifactId + "\"}";
            using (var request = new HttpRequestMessage(HttpMethod.Post, rpcEndpoint))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("apikey", publishableKey);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                try
                {
                    using (var reply = await client.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false))
                    {
                        if (reply.StatusCode != HttpStatusCode.OK ||
                            reply.Content == null ||
                            reply.Content.Headers.ContentLength > MaxResponseBytes)
                            return UniversityStagingResult.Rejected;
                        try
                        {
                            await reply.Content.LoadIntoBufferAsync(MaxResponseBytes)
                                .ConfigureAwait(false);
                            var json = await reply.Content.ReadAsStringAsync()
                                .ConfigureAwait(false);
                            if (Encoding.UTF8.GetByteCount(json) > MaxResponseBytes)
                                return UniversityStagingResult.Rejected;
                            StatusReply status;
                            using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                                status = new DataContractJsonSerializer(typeof(StatusReply))
                                    .ReadObject(memory) as StatusReply;
                            if (status == null ||
                                !string.Equals(status.State, "staged_unverified", StringComparison.Ordinal) ||
                                !string.Equals(status.ArtifactId, artifactId, StringComparison.Ordinal) ||
                                !string.Equals(status.Sha256, originalSha, StringComparison.Ordinal) ||
                                status.SizeBytes != originalBytes.LongLength)
                                return UniversityStagingResult.Rejected;
                            return UniversityStagingResult.StagedUnverified;
                        }
                        catch (Exception error) when (error is SerializationException ||
                            error is InvalidOperationException || error is HttpRequestException ||
                            error is IOException || error is ArgumentException)
                        {
                            return UniversityStagingResult.Rejected;
                        }
                    }
                }
                catch (HttpRequestException) { return UniversityStagingResult.Rejected; }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return UniversityStagingResult.Rejected;
                }
            }
        }
    }
}
