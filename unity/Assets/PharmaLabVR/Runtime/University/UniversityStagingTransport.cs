using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PharmaLabVR.University
{
    // Pure HTTP staging adapter; NEVER a Native attestation or sync receipt.
    // The caller must retain the offline outbox until a separate trusted,
    // signed server acknowledgement is verified by a future implementation.
    public enum UniversityStagingResult
    {
        Rejected = 0,
        StagedUnverified = 1
    }

    public static class UniversityStagingTransport
    {
        private const int MaxJsonBytes = 1048576;
        private static readonly Regex Uuid = new Regex(
            @"\A[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z",
            RegexOptions.CultureInvariant);
        private static readonly Regex Bearer = new Regex(
            @"\A[A-Za-z0-9._-]{1,4096}\z", RegexOptions.CultureInvariant);

        public static async Task<UniversityStagingResult> UploadAsync(
            HttpClient client, Uri endpoint, string institutionId,
            string registeredSessionId, string artifactId, string accessToken,
            byte[] jsonBytes, CancellationToken cancellationToken = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (endpoint == null || !endpoint.IsAbsoluteUri ||
                endpoint.Scheme != Uri.UriSchemeHttps ||
                !endpoint.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(endpoint.AbsolutePath,
                    "/functions/v1/student-evidence-upload", StringComparison.Ordinal) ||
                endpoint.UserInfo.Length != 0 ||
                endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
                throw new ArgumentException("HTTPS Supabase staging endpoint required.", nameof(endpoint));

            if (!CanonicalId(institutionId) || !CanonicalId(registeredSessionId) ||
                !CanonicalId(artifactId) || string.IsNullOrEmpty(accessToken) ||
                !Bearer.IsMatch(accessToken))
                throw new ArgumentException("Invalid staging identity or bearer token.");
            if (jsonBytes == null || jsonBytes.Length == 0 ||
                jsonBytes.Length > MaxJsonBytes)
                throw new InvalidDataException("Invalid bounded staging JSON payload.");

            // No hashes from untrusted caller metadata: hash the actual bytes.
            string sha256;
            using (var sha = SHA256.Create())
                sha256 = BitConverter.ToString(sha.ComputeHash(jsonBytes))
                    .Replace("-", "").ToLowerInvariant();

            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("x-institution-id", institutionId);
                request.Headers.Add("x-session-id", registeredSessionId);
                request.Headers.Add("x-artifact-id", artifactId);
                request.Headers.Add("x-evidence-sha256", sha256);
                request.Content = new ByteArrayContent(jsonBytes);
                request.Content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/json");

                // A 201 is a *quarantine acknowledgement only*. Never infer
                // academic evidence or clear any durable Native outbox record.
                try
                {
                    using (var result = await client.SendAsync(request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false))
                    {
                        if (result.StatusCode != HttpStatusCode.Created)
                            return UniversityStagingResult.Rejected;
                        var text = await result.Content.ReadAsStringAsync()
                            .ConfigureAwait(false);
                        return text.Contains("\"state\":\"staged_unverified\"")
                            ? UniversityStagingResult.StagedUnverified
                            : UniversityStagingResult.Rejected;
                    }
                }
                catch (HttpRequestException)
                {
                    return UniversityStagingResult.Rejected;
                }
            }
        }

        private static bool CanonicalId(string value) =>
            !string.IsNullOrEmpty(value) && Uuid.IsMatch(value);
    }
}
