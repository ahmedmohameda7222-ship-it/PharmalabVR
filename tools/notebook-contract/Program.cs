// Headless tests link the real NotebookService production file; no Unity,
 // Native chemistry or Editor/Player execution is inferred from these checks.
using System;
using System.IO;
using PharmaLabVR.Session;

internal static class Program
{
    private static int assertions;
    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "plv-p11-notebook-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "session.plv-notes");
        var notes = new NotebookService();
        try
        {
            var first = notes.Append(path, "research-branch", "7", "receiver",
                "I predict the pH will decrease after transfer.");
            Require(first.EventSequence == "7" && first.BranchId == "research-branch",
                "Native event reference was not preserved in the note.");
            var second = notes.Append(path, "research-branch", "8", "source",
                "Observed after command 8: stock bottle remains closed.");
            Require(second.EventSequence == "8", "Note event sequence was changed.");
            var reread = notes.Read(path, "research-branch");
            Require(reread.Count == 2 && reread[0].Text.Contains("predict") &&
                reread[1].VesselId == "source", "Committed notebook notes did not round-trip.");
            Require(notes.Read(path, "another-branch").Count == 0,
                "Notebook content leaked across Native branches.");
            var rejectedRevision = false;
            try { notes.Append(path, "research-branch", "08", "receiver", "invalid"); }
            catch (ArgumentException) { rejectedRevision = true; }
            Require(rejectedRevision, "Noncanonical Native event reference was accepted.");
            var rejectedScript = false;
            try { notes.Append(path, "research-branch", "9", "receiver", "\u0639\u0631\u0628\u064a"); }
            catch (ArgumentException) { rejectedScript = true; }
            Require(rejectedScript, "Arabic-script product notebook text was accepted.");
            Require(notes.Read(path, "research-branch").Count == 2,
                "Rejected note modified the confirmed durable notebook.");
            var file = File.ReadAllText(path);
            File.WriteAllText(path, file.Replace("PLV-NOTEBOOK-V1", "PLV-NOTEBOOK-X1"));
            Require(notes.Read(path, "research-branch").Count == 1,
                "Corrupt primary did not open the previous confirmed notebook backup.");
            var rejectedUnsafeOverwrite = false;
            try { notes.Append(path, "research-branch", "9", "receiver", "Do not overwrite backup"); }
            catch (InvalidDataException) { rejectedUnsafeOverwrite = true; }
            Require(rejectedUnsafeOverwrite,
                "Tampered current notebook was silently replaced instead of failing closed.");
            // P11.03: one portable artifact must contain a real Native-export
            // byte payload together with only the selected Native branch's notes.
            // It must not depend on another local sidecar file after export.
            File.WriteAllText(path, file);
            var packed = Path.Combine(root, "portable.plvpack");
            const string native = "{\"schemaVersion\":1,\"branchId\":\"research-branch\",\"eventSequence\":\"8\"}";
            var notesForEight = notes.ExportBranch(path, "research-branch", "8");
            PortableSessionArchive.Write(packed, native, notesForEight, "research-branch", "8");
            File.Delete(path);
            File.Delete(path + ".bak");
            var recovered = PortableSessionArchive.Read(packed);
            Require(recovered.BranchId == "research-branch" && recovered.EventSequence == "8" &&
                recovered.NativeJson == native && recovered.NotebookSnapshot == notesForEight,
                "Portable archive did not round-trip Native bytes and selected notes.");
            Require(recovered.NotebookSnapshot.Contains("PLV-NOTEBOOK-V1"),
                "Portable archive silently dropped notes.");
            var bytes = File.ReadAllBytes(packed);
            bytes[bytes.Length - 1] ^= 0x01;
            File.WriteAllBytes(packed, bytes);
            var corruptRefused = false;
            try { PortableSessionArchive.Read(packed); }
            catch (InvalidDataException) { corruptRefused = true; }
            Require(corruptRefused, "Corrupt portable archive passed checksum verification.");
            var mismatchedBranchRefused = false;
            try { PortableSessionArchive.Write(packed, native, notesForEight, "foreign-branch", "8"); }
            catch (InvalidDataException) { mismatchedBranchRefused = true; }
            Require(mismatchedBranchRefused, "Portable archive accepted a foreign declared Native branch.");
            var futureNoteRefused = false;
            try { NotebookService.ValidatePortableSnapshot(notesForEight, "research-branch", "7"); }
            catch (InvalidDataException) { futureNoteRefused = true; }
            Require(futureNoteRefused, "Portable notes included an event after the exported Native revision.");
            Require(notes.ExportBranch(path, "research-branch", "7") == "PLV-NOTEBOOK-V1\n",
                "A new session with no notebook records cannot export a valid empty note snapshot.");
            // P11.02: recorded-only Review must expose actual Native command
            // receipts, not project a scripted endpoint or invent event times.
            const string recorded = @"{""schemaVersion"":1,""branchId"":""research-branch"",
              ""scientificPackage"":{""identity"":""minteq-test""},
              ""observations"":[{""vesselId"":""receiver"",""observableId"":""pH"",
              ""support"":""Supported"",""freshness"":""Current"",
              ""computationState"":""Ready"",""valueAvailable"":true,""value"":7.1}],
              ""commandReceipts"":[
              {""commandSequence"":""2"",""canonicalCommand"":{""schemaVersion"":1,
               ""branchId"":""research-branch"",""commandSequence"":""2"",""type"":""PrepareStock""},
               ""terminalOutcome"":{""schemaVersion"":1,""branchId"":""research-branch"",
               ""commandSequence"":""2"",""type"":""CommandOutcome"",""accepted"":false,""code"":""Rejected""}},
              {""commandSequence"":""1"",""canonicalCommand"":{""schemaVersion"":1,
               ""branchId"":""research-branch"",""commandSequence"":""1"",""type"":""CreateVessel""},
               ""terminalOutcome"":{""schemaVersion"":1,""branchId"":""research-branch"",
               ""commandSequence"":""1"",""type"":""CommandOutcome"",""accepted"":true,""code"":""Accepted""}}]}";
            var review = RecordedSessionReview.FromJson(recorded);
            Require(review.CommandReceipts.Count == 2 &&
                review.CommandReceipts[0].CommandSequence == "1" &&
                review.CommandReceipts[1].CommandSequence == "2" &&
                review.CommandReceipts[1].Accepted == false &&
                review.CommandReceipts[1].CommandType == "PrepareStock",
                "Recorded Review did not preserve ordered real Native accepted/rejected receipts.");
            // P11.02: recorded-only Review must reject malformed UTF-8 instead
            // of presenting a silently changed Native identity.
            var invalidReviewFile = Path.Combine(root, "bad-encoding.plv.json");
            var utf8Identity = System.Text.Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"branchId\":\"branchA\",\"scientificPackage\":{\"identity\":\"db\"},\"observations\":[]}");
            var letter = Array.IndexOf(utf8Identity, (byte)'A');
            Require(letter >= 0, "Invalid UTF-8 fixture has no target byte.");
            utf8Identity[letter] = 0xff;
            File.WriteAllBytes(invalidReviewFile, utf8Identity);
            var invalidUtf8Rejected = false;
            try { RecordedSessionReview.Open(invalidReviewFile); }
            catch (InvalidDataException) { invalidUtf8Rejected = true; }
            Require(invalidUtf8Rejected,
                "Recorded-only Review accepted a malformed UTF-8 Native branch ID.");
            var changedDocument = System.Text.Json.Nodes.JsonNode.Parse(recorded);
            changedDocument["commandReceipts"][0]["terminalOutcome"]["branchId"] = "other";
            var mismatchedReceipt = changedDocument.ToJsonString();
            var mismatchRejected = false;
            try { RecordedSessionReview.FromJson(mismatchedReceipt); }
            catch (InvalidDataException) { mismatchRejected = true; }
            Require(mismatchRejected, "Recorded Review admitted a foreign Native command outcome.");
            // P11.03: CSV is a provenance-rich export of Native observation
            // and command receipts, not an invented pH curve/endpoint.
            var csv = SessionExportService.RenderCsv(review);
            Require(csv.StartsWith("schemaVersion,recordKind,branchId,") &&
                csv.Contains("observation,research-branch,") &&
                csv.Contains("command,research-branch,2,") &&
                csv.Contains("PrepareStock") && csv.Contains("Rejected"),
                "CSV omitted recorded Native values or rejected command receipts.");
            var unsupportedJson = System.Text.Json.Nodes.JsonNode.Parse(recorded);
            unsupportedJson["observations"][0]["support"] = "Unsupported";
            var unavailable = SessionExportService.RenderCsv(
                RecordedSessionReview.FromJson(unsupportedJson.ToJsonString()));
            Require(!unavailable.Contains("7.099999") &&
                unavailable.Split('\n')[1].EndsWith(","),
                "Unsupported scientific pH was exported as a valid numeric result.");
            var lowPhJson = System.Text.Json.Nodes.JsonNode.Parse(recorded);
            lowPhJson["observations"][0]["value"] = -0.5;
            var lowPhCsv = SessionExportService.RenderCsv(
                RecordedSessionReview.FromJson(lowPhJson.ToJsonString()));
            Require(lowPhCsv.Split('\n')[1].EndsWith(",-0.5"),
                "A finite scientific CSV value lost its numeric sign during escaping.");
            // P09.02/P11.02: the recorded-only Review must never display
            // stale numeric pH even if Native retains a finite historical value.
            var staleDocument = System.Text.Json.Nodes.JsonNode.Parse(recorded);
            staleDocument["observations"][0]["freshness"] = "Stale";
            var staleReview = RecordedSessionReview.FromJson(staleDocument.ToJsonString());
            Require(staleReview.Observations[0].DisplayValue == "--",
                "Recorded-only Review showed stale Native pH as a current number.");
            var failedDocument = System.Text.Json.Nodes.JsonNode.Parse(recorded);
            failedDocument["observations"][0]["computationState"] = "Failed";
            Require(RecordedSessionReview.FromJson(failedDocument.ToJsonString())
                .Observations[0].DisplayValue == "--",
                "Recorded-only Review displayed a failed scientific calculation.");
            Require(review.Observations[0].DisplayValue == "7.10",
                "A valid Current Native observation lost its recorded numeric display.");
            var exportedCsv = Path.Combine(root, "native-results.csv");
            SessionExportService.WriteCsv(exportedCsv, review);
            Require(File.ReadAllText(exportedCsv) == csv &&
                !File.Exists(exportedCsv + ".tmp"),
                "Atomic scientific CSV export did not round-trip the real recorded data.");
            // P11.03: compare two *recorded* Native-backed snapshots; no
            // invented interpolation, model conversion or stale numeric delta.
            var sourceRun = System.Text.Json.Nodes.JsonNode.Parse(recorded);
            sourceRun["observations"][0]["modelId"] = "IPhreeqc-research";
            sourceRun["observations"][0]["databaseIdentity"] = "minteq-test";
            var candidateRun = System.Text.Json.Nodes.JsonNode.Parse(sourceRun.ToJsonString());
            candidateRun["branchId"] = "second-run";
            candidateRun["commandReceipts"] = null;
            candidateRun["observations"][0]["value"] = 6.7;
            var firstRun = RecordedSessionReview.FromJson(sourceRun.ToJsonString());
            var secondRun = RecordedSessionReview.FromJson(candidateRun.ToJsonString());
            var compared = RecordedRunComparison.Compare(firstRun, secondRun);
            Require(compared.Rows.Count == 1 &&
                compared.Rows[0].BaselineDisplay == "7.10" &&
                compared.Rows[0].CandidateDisplay == "6.70" &&
                compared.Rows[0].DeltaAvailable &&
                Math.Abs(compared.Rows[0].Delta - (-0.4)) < 1e-10,
                "Two authentic supported Native observations did not compare correctly.");
            candidateRun["observations"][0]["freshness"] = "Stale";
            var staleComparison = RecordedRunComparison.Compare(firstRun,
                RecordedSessionReview.FromJson(candidateRun.ToJsonString()));
            Require(staleComparison.Rows[0].CandidateDisplay == "--" &&
                !staleComparison.Rows[0].DeltaAvailable,
                "Stale recorded Native pH was turned into an invented comparison delta.");
            candidateRun["observations"][0]["freshness"] = "Current";
            candidateRun["scientificPackage"]["identity"] = "different-model-database";
            var differentPackage = RecordedRunComparison.Compare(firstRun,
                RecordedSessionReview.FromJson(candidateRun.ToJsonString()));
            Require(!differentPackage.Rows[0].DeltaAvailable &&
                differentPackage.Rows[0].CandidateDisplay == "6.70",
                "Different scientific packages generated an unverified cross-model delta.");
            // Confirmed backup-only recorded comparison must remain usable when
            // the primary checkpoint is malformed, and fail closed otherwise.
            var comparisonFile = Path.Combine(root, "compare.plv.json");
            File.WriteAllText(comparisonFile, "{\"schemaVersion\":-1}");
            File.WriteAllText(comparisonFile + ".bak", sourceRun.ToJsonString());
            var fromBackup = RecordedRunComparison.CompareSaved(comparisonFile, secondRun);
            Require(fromBackup.Rows.Count == 1 && fromBackup.Rows[0].DeltaAvailable,
                "A real saved Native backup could not provide the recorded baseline.");
            File.WriteAllText(comparisonFile + ".bak", "{\"schemaVersion\":-1}");
            // P11.02: missing scientific packages must still allow a recorded
            // Review from the confirmed backup, never a forced Native restore.
            File.WriteAllText(comparisonFile + ".bak", sourceRun.ToJsonString());
            var recordedBackup = RecordedSessionReview.OpenRecoverable(comparisonFile);
            Require(recordedBackup.BranchId == "research-branch" &&
                recordedBackup.Observations[0].DisplayValue == "7.10",
                "Confirmed saved backup was unavailable for recorded-only Review.");
            File.WriteAllText(comparisonFile + ".bak", sourceRun.ToJsonString());
            File.Delete(comparisonFile);
            var onlyBackup = RecordedSessionReview.OpenRecoverable(comparisonFile);
            Require(onlyBackup.BranchId == "research-branch",
                "A missing primary prevented confirmed backup-only recorded Review.");
            File.WriteAllText(comparisonFile, "{\"schemaVersion\":-1}");
            File.WriteAllText(comparisonFile + ".bak", "{\"schemaVersion\":-1}");
            var refusedCorruptSavedPair = false;
            try { RecordedRunComparison.CompareSaved(comparisonFile, secondRun); }
            catch (InvalidDataException) { refusedCorruptSavedPair = true; }
            Require(refusedCorruptSavedPair,
                "Invalid primary and backup produced an invented comparison baseline.");
            // P06.02 Native session review/compare caches are presentation
            // data and must be cleared on accepted restore or new authority.
            // A failed Native operation does not advance the restoration epoch.
            var reviewCache = new NativeReviewAuthorityCache<string, string>();
            var authorityA = new object();
            var authorityB = new object();
            reviewCache.Observe(authorityA, 3UL);
            reviewCache.Review = "Native pre-restart review";
            reviewCache.Comparison = "Native pre-restart comparison";
            reviewCache.Observe(authorityA, 3UL);
            Require(reviewCache.Review != null && reviewCache.Comparison != null,
                "Unchanged Native authority unexpectedly invalidated recorded-only caches.");
            reviewCache.Observe(authorityA, 4UL);
            Require(reviewCache.Review == null && reviewCache.Comparison == null,
                "Same CoreDriver Native restart leaked prior review/comparison cache.");
            reviewCache.Review = "Current restored run";
            reviewCache.Comparison = "Current comparison";
            reviewCache.Observe(authorityA, 4UL);
            Require(reviewCache.Review != null && reviewCache.Comparison != null,
                "Re-reading identical Native epoch erased the active recorded review.");
            reviewCache.Observe(authorityB, 4UL);
            Require(reviewCache.Review == null && reviewCache.Comparison == null,
                "Different Native CoreDriver with identical epoch reused review/comparison.");
            reviewCache.Review = "No longer in scene";
            reviewCache.Comparison = "Old branch";
            reviewCache.Observe(null, 0UL);
            Require(reviewCache.Review == null && reviewCache.Comparison == null,
                "Detached Native authority exposed old Review and comparison.");
            reviewCache.Observe(null, 0UL);
            Require(reviewCache.Review == null && reviewCache.Comparison == null,
                "Repeated missing Native context created a recorded-only cache.");
            // An accepted durable Save or new saved-baseline context must
            // invalidate only the old saved-run comparison, not the Native
            // recorded Review of the same unchanged authority.
            reviewCache.Observe(authorityA, 5UL);
            reviewCache.Review = "Current Native recorded observations";
            reviewCache.Comparison = "Old saved baseline";
            reviewCache.InvalidateComparison();
            Require(reviewCache.Review == "Current Native recorded observations" &&
                reviewCache.Comparison == null,
                "Durable saved baseline change retained obsolete comparison or lost current Review.");
            reviewCache.Comparison = "Old course's saved baseline";
            reviewCache.InvalidateComparison();
            Require(reviewCache.Comparison == null &&
                reviewCache.Review == "Current Native recorded observations",
                "Switching saved session context retained a previous course comparison.");
            Console.WriteLine("P11.02 headless NotebookService checks passed: " + assertions);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
