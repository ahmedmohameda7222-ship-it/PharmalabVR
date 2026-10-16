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
            Console.WriteLine("P11.02 headless NotebookService checks passed: " + assertions);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
