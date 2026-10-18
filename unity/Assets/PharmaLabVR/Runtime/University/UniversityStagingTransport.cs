using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PharmaLabVR.University
{
    // Transport ONLY for quarantining untrusted bytes. 201 means STAGED,
    // never Native receipt validation or a durable cloud-sync acknowledgement.
    // Deliberately cannot mutate UniversitySyncOutbox or Native CoreSession.
    public enum UniversityStagingResult { Rejected, StagedUnverified }

    public static class UniversityStagingTransport
    {
        private const int MaxBytes = 1048576;
        private static readonly Regex CanonicalUuid = new Regex(
            @"^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
            RegexOptions.CultureInvariant);
        private static readonly Regex BearerToken = new Regex(
            @"^[A-Za-z0-9._~-]{1,8192}$", RegexOptions.CultureInvariant);
        private const string UploadPath = "/functions/v1/student-evidence-upload";

        public static async Task<UniversityStagingResult> UploadAsync(
            HttpClient http, Uri endpoint, string institutionId,
            string registeredSessionId, string artifactId, string studentAccessToken,
            byte[] rawJsonExport, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (http == null || endpoint == null || !endpoint.IsAbsoluteUri ||
                endpoint.Scheme != Uri.UriSchemeHttps || !endpoint.IsDefaultPort ||
                !endpoint.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(endpoint.AbsolutePath, UploadPath, StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment) ||
                !string.IsNullOrEmpty(endpoint.UserInfo))
                throw new ArgumentException("A recognized HTTPS Supabase staging endpoint is required.");
            if (!ValidId(institutionId) || !ValidId(registeredSessionId) || !ValidId(artifactId))
                throw new ArgumentException("Canonical tenant, registered session and artifact UUIDs required.");
            if (studentAccessToken == null || !BearerToken.IsMatch(studentAccessToken))
                throw new ArgumentException("Valid student access token required.");
            if (rawJsonExport == null || rawJsonExport.Length < 1 ||
                rawJsonExport.Length > MaxBytes)
                throw new InvalidDataException("U04 quarantine export must be 1 byte to 1 MiB.");

            string sha;
            using (var hasher = SHA256.Create())
            {
                sha = BitConverter.ToString(hasher.ComputeHash(rawJsonExport))
                    .Replace("-", "").ToLowerInvariant();
            }
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", studentAccessToken);
                request.Headers.Add("x-institution-id", institutionId);
                request.Headers.Add("x-session-id", registeredSessionId);
                request.Headers.Add("x-artifact-id", artifactId);
                request.Headers.Add("x-evidence-sha256", sha);
                request.Content = new ByteArrayContent(rawJsonExport);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using (var response = await http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false))
                {
                    return response.StatusCode == HttpStatusCode.Created
                        ? UniversityStagingResult.StagedUnverified
                        : UniversityStagingResult.Rejected;
                }
            }
        }

        private static bool ValidId(string id) =>
            id != null && CanonicalUuid.IsMatch(id);
    }
}
