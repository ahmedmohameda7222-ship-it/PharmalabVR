using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PharmaLabVR.University
{
    // Receives only the database identity for a student SELF-REPORTED Native
    // source. This does not verify science, attest a receipt or acknowledge sync.
    public static class UniversityStagingSessionRegistration
    {
        private static readonly Regex CanonicalUuid = new Regex(
            @"\A[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z",
            RegexOptions.CultureInvariant);
        private static readonly Regex LocalToken = new Regex(
            @"\A[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}\z", RegexOptions.CultureInvariant);
        private static readonly Regex CanonicalSha = new Regex(
            @"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);
        private static readonly Regex BearerToken = new Regex(
            @"\A[A-Za-z0-9._~-]{1,8192}\z", RegexOptions.CultureInvariant);
        private static readonly Regex PublicKey = new Regex(
            @"\Asb_publishable_[A-Za-z0-9_-]{1,497}\z", RegexOptions.CultureInvariant);
        private const string RpcPath =
            "/rest/v1/rpc/university_register_staging_session";

        public static async Task<string> RegisterPendingAsync(
            HttpClient client, Uri rpcEndpoint, string publishableKey,
            string accessToken, string institutionId, string localSessionId,
            string nativeBranchId, string scientificPackageSha256, string requestId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (client == null || rpcEndpoint == null || !rpcEndpoint.IsAbsoluteUri ||
                rpcEndpoint.Scheme != Uri.UriSchemeHttps || !rpcEndpoint.IsDefaultPort ||
                !rpcEndpoint.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rpcEndpoint.AbsolutePath, RpcPath, StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(rpcEndpoint.Query) ||
                !string.IsNullOrEmpty(rpcEndpoint.Fragment) ||
                !string.IsNullOrEmpty(rpcEndpoint.UserInfo))
                throw new ArgumentException("A recognized HTTPS Supabase staging RPC is required.");
            if (!ValidId(institutionId) || !ValidId(requestId))
                throw new ArgumentException("Canonical institution and idempotency UUIDs are required.");
            if (!ValidLocal(localSessionId) || !ValidLocal(nativeBranchId))
                throw new ArgumentException("Local session and Native branch identifiers are invalid.");
            if (scientificPackageSha256 == null || !CanonicalSha.IsMatch(scientificPackageSha256))
                throw new InvalidDataException("A canonical scientific package SHA-256 is required.");
            if (publishableKey == null || !PublicKey.IsMatch(publishableKey) ||
                accessToken == null || !BearerToken.IsMatch(accessToken))
                throw new ArgumentException("Client publishable key and access token are required.");

            // All interpolated fields are strictly ASCII-validated above; no
            // untrusted arbitrary JSON text or Native commands are accepted.
            var payload = "{\"p_institution_id\":\"" + institutionId +
                "\",\"p_local_session_id\":\"" + localSessionId +
                "\",\"p_native_branch_id\":\"" + nativeBranchId +
                "\",\"p_science_package_sha256\":\"" + scientificPackageSha256 +
                "\",\"p_request_id\":\"" + requestId + "\"}";
            using (var request = new HttpRequestMessage(HttpMethod.Post, rpcEndpoint))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("apikey", publishableKey);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage received;
                try
                {
                    received = await client.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    return null;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // A timeout does not establish a registered session. The
                    // caller must retain the Native offline intent for retry.
                    return null;
                }
                using (var response = received)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        return null;
                    // An RPC UUID reply is 38 JSON bytes. Reject oversized
                    // responses before buffering; a gateway error or hostile
                    // body must never create a session identity or exhaust RAM.
                    const int MaxRpcReplyBytes = 128;
                    if (response.Content == null ||
                        response.Content.Headers.ContentLength > MaxRpcReplyBytes)
                        return null;
                    try
                    {
                        await response.Content.LoadIntoBufferAsync(MaxRpcReplyBytes)
                            .ConfigureAwait(false);
                    }
                    catch (HttpRequestException)
                    {
                        return null;
                    }
                    catch (InvalidOperationException)
                    {
                        return null;
                    }
                    var json = (await response.Content.ReadAsStringAsync()
                        .ConfigureAwait(false)).Trim();
                    if (json.Length != 38 || json[0] != '"' || json[37] != '"')
                        throw new InvalidDataException("Registration returned no canonical UUID.");
                    var uuid = json.Substring(1, 36);
                    if (!ValidId(uuid))
                        throw new InvalidDataException("Registration returned an invalid session UUID.");
                    return uuid;
                }
            }
        }

        private static bool ValidId(string id) => id != null && CanonicalUuid.IsMatch(id);
        private static bool ValidLocal(string text) =>
            text != null && LocalToken.IsMatch(text) && !text.Contains("..");
    }
}
