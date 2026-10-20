using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PharmaLabVR.University
{
    // Server-supplied academic metadata only. The client never grants course
    // access; PostgreSQL RLS is the authority. Native lab work remains offline.
    public sealed class UniversityStudentAssignmentSummary
    {
        public string Id { get; }
        public string InstitutionId { get; }
        public string SectionId { get; }
        public string Title { get; }
        public DateTimeOffset PublishedAtUtc { get; }
        public DateTimeOffset? DueAtUtc { get; }

        internal UniversityStudentAssignmentSummary(string id, string institutionId,
            string sectionId, string title, DateTimeOffset publishedAtUtc,
            DateTimeOffset? dueAtUtc)
        {
            Id = id;
            InstitutionId = institutionId;
            SectionId = sectionId;
            Title = title;
            PublishedAtUtc = publishedAtUtc;
            DueAtUtc = dueAtUtc;
        }
    }

    // First U05 read-only student assignment inbox data path.
    // A membership snapshot is a UI hint, not proof of student authorization.
    // RLS must still enforce enrolled sections and published assignments.
    public static class UniversityStudentAssignmentInbox
    {
        private const int MaxResponseBytes = 131072;
        private static readonly Regex PublishableKey = new Regex(
            @"\Asb_publishable_[A-Za-z0-9_-]{1,497}\z", RegexOptions.CultureInvariant);
        private static readonly Regex AccessToken = new Regex(
            @"\A[A-Za-z0-9._~-]{1,8192}\z", RegexOptions.CultureInvariant);
        private static readonly IReadOnlyList<UniversityStudentAssignmentSummary> Empty =
            Array.Empty<UniversityStudentAssignmentSummary>();

        [DataContract]
        private sealed class AssignmentRow
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "institution_id")] public string InstitutionId { get; set; }
            [DataMember(Name = "section_id")] public string SectionId { get; set; }
            [DataMember(Name = "title")] public string Title { get; set; }
            [DataMember(Name = "published_at")] public string PublishedAt { get; set; }
            [DataMember(Name = "due_at")] public string DueAt { get; set; }
        }

        public static async Task<IReadOnlyList<UniversityStudentAssignmentSummary>> FetchPublishedAsync(
            HttpClient client, Uri supabaseProjectUrl, string publishableKey,
            string accessToken, UniversityMembershipSession membership,
            DateTimeOffset nowUtc, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (supabaseProjectUrl == null || !supabaseProjectUrl.IsAbsoluteUri ||
                supabaseProjectUrl.Scheme != Uri.UriSchemeHttps ||
                !supabaseProjectUrl.IsDefaultPort ||
                !supabaseProjectUrl.Host.EndsWith(".supabase.co",
                    StringComparison.OrdinalIgnoreCase) ||
                supabaseProjectUrl.Host.Length <= ".supabase.co".Length ||
                supabaseProjectUrl.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(supabaseProjectUrl.UserInfo) ||
                !string.IsNullOrEmpty(supabaseProjectUrl.Query) ||
                !string.IsNullOrEmpty(supabaseProjectUrl.Fragment))
                throw new ArgumentException("An HTTPS Supabase project base URL is required.");
            if (!PublishableKey.IsMatch(publishableKey ?? "") ||
                !AccessToken.IsMatch(accessToken ?? ""))
                throw new ArgumentException("Student Auth bearer and public gateway key are required.");

            if (membership == null) return Empty;
            var tenant = membership.ActiveInstitutionId;
            // This only limits stale/unselected student UI requests.
            // The server never trusts the role string supplied by a client.
            if (!membership.CanDisplayInstitution(tenant, nowUtc) ||
                membership.SelectedRole(nowUtc) != "student")
                return Empty;

            var url = new Uri(supabaseProjectUrl,
                "rest/v1/assignments?select=id%2Cinstitution_id%2Csection_id%2Ctitle%2Cpublished_at%2Cdue_at" +
                "&institution_id=eq." + tenant +
                "&published_at=not.is.null&order=due_at.asc&limit=100");

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("apikey", publishableKey);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                try
                {
                    using (var response = await client.SendAsync(request,
                        HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        if (response.StatusCode != HttpStatusCode.OK ||
                            response.Content == null ||
                            response.Content.Headers.ContentLength > MaxResponseBytes)
                            return Empty;
                        try
                        {
                            await response.Content.LoadIntoBufferAsync(MaxResponseBytes)
                                .ConfigureAwait(false);
                            var bytes = await response.Content.ReadAsByteArrayAsync()
                                .ConfigureAwait(false);
                            if (bytes.Length > MaxResponseBytes)
                                return Empty;
                            AssignmentRow[] rows;
                            using (var buffer = new MemoryStream(bytes, false))
                                rows = new DataContractJsonSerializer(
                                    typeof(AssignmentRow[])).ReadObject(buffer) as AssignmentRow[];
                            if (rows == null || rows.Length > 100)
                                return Empty;
                            var seen = new HashSet<string>(StringComparer.Ordinal);
                            var result = new List<UniversityStudentAssignmentSummary>(rows.Length);
                            foreach (var row in rows)
                            {
                                if (row == null || !CanonicalUuid(row.Id) ||
                                    !CanonicalUuid(row.SectionId) ||
                                    row.InstitutionId != tenant ||
                                    !CanonicalUuid(row.InstitutionId) ||
                                    string.IsNullOrWhiteSpace(row.Title) ||
                                    row.Title.Length > 200 ||
                                    !seen.Add(row.Id) ||
                                    !DateTimeOffset.TryParse(row.PublishedAt,
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.AssumeUniversal, out var publishedAt))
                                    return Empty;
                                DateTimeOffset? dueAt = null;
                                if (row.DueAt != null)
                                {
                                    if (!DateTimeOffset.TryParse(row.DueAt,
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.AssumeUniversal, out var parsedDueAt))
                                        return Empty;
                                    dueAt = parsedDueAt.ToUniversalTime();
                                }
                                result.Add(new UniversityStudentAssignmentSummary(
                                    row.Id, row.InstitutionId, row.SectionId, row.Title,
                                    publishedAt.ToUniversalTime(), dueAt));
                            }
                            return result.AsReadOnly();
                        }
                        catch (Exception error) when (error is SerializationException ||
                            error is System.Xml.XmlException ||
                            error is InvalidOperationException || error is HttpRequestException ||
                            error is IOException || error is ArgumentException)
                        {
                            return Empty;
                        }
                    }
                }
                catch (HttpRequestException) { return Empty; }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Empty;
                }
            }
        }

        private static bool CanonicalUuid(string text) =>
            text != null && Guid.TryParseExact(text, "D", out var guid) &&
            guid != Guid.Empty &&
            string.Equals(guid.ToString("D"), text, StringComparison.Ordinal);
    }
}
