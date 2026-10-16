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
            var missingRevisionRefused = false;
            try { notes.ExportBranch(path, "research-branch", "7"); }
            catch (InvalidDataException) { missingRevisionRefused = true; }
            Require(missingRevisionRefused, "Notebook export accepted a missing primary or later Native event.");
            Console.WriteLine("P11.02 headless NotebookService checks passed: " + assertions);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
