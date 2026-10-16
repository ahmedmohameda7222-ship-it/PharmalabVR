// P11.01 headless disk boundary checks compiling the actual production SaveService.
// The injected CoreSession exercises only serialized bytes, not chemistry/import science.
using System;
using System.IO;
using PharmaLabVR.Core;
using PharmaLabVR.Session;

internal static class Program
{
    private static int checks;
    private static void Check(bool pass, string reason)
    {
        checks++;
        if (!pass) throw new InvalidOperationException(reason);
    }

    private static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "plv-p11-save-" + Guid.NewGuid().ToString("N"));
        var service = new SaveService();
        try
        {
            var target = service.Save(new CoreSession("journal:v1:first"), root, "session");
            Check(File.ReadAllText(target) == "journal:v1:first",
                "Initial SaveService disk bytes differ from the exported session.");
            service.Save(new CoreSession("journal:v1:second"), root, "session");
            Check(File.ReadAllText(target + ".bak") == "journal:v1:first",
                "Atomic replacement did not preserve the previous confirmed save.");
            File.WriteAllText(target, "corrupt");
            Check(service.Load(target, "fixture-database").Serialized == "journal:v1:first",
                "Corrupt primary did not recover from the confirmed backup.");
            File.WriteAllText(target, "journal:v1:confirmed");
            var rejectedPath = false;
            try { service.Save(new CoreSession("journal:v1:bad"), root, "../outside"); }
            catch (ArgumentException) { rejectedPath = true; }
            Check(rejectedPath, "Traversal-like save name was accepted.");

            // The import limit is 16 MiB. A Save must not claim success for bytes
            // that the same SaveService can never load, nor replace the good file.
            var tooLarge = new CoreSession("journal:v1:" + new string('x', 16 * 1024 * 1024));
            var rejectedSize = false;
            try { service.Save(tooLarge, root, "session"); }
            catch (InvalidDataException) { rejectedSize = true; }
            Check(rejectedSize, "Oversized save was reported successful.");
            Check(File.ReadAllText(target) == "journal:v1:confirmed",
                "Oversized save damaged the last confirmed session.");
            Check(!File.Exists(target + ".tmp"), "Oversized save left a temporary file.");

            // Emulate a filesystem that denies backup replacement after export
            // and temp-file staging. The last confirmed checkpoint must survive
            // and a failed Save must not leave a misleading partial .tmp.
            File.Delete(target + ".bak");
            Directory.CreateDirectory(target + ".bak");
            var replacementDenied = false;
            try { service.Save(new CoreSession("journal:v1:failed-replacement"), root, "session"); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { replacementDenied = true; }
            Check(replacementDenied, "An obstructed backup replacement was reported as a durable Save.");
            Check(File.ReadAllText(target) == "journal:v1:confirmed",
                "Failed atomic replacement corrupted the last confirmed checkpoint.");
            Check(!File.Exists(target + ".tmp"),
                "Failed atomic replacement left an uncommitted journal temporary file.");
            Directory.Delete(target + ".bak");

            // A failed/aborted replace may leave only the last confirmed backup.
            // SaveService.Load already recovers it, so the product's Load gate must
            // not hide that recoverable file when the primary is absent.
            service.Save(new CoreSession("journal:v1:backup-survivor"), root, "session");
            service.Save(new CoreSession("journal:v1:latest"), root, "session");
            File.Delete(target);
            Check(SaveService.HasSaveCandidate(target),
                "Valid backup-only Native save was hidden from the Pause/Load panel.");
            Check(service.Load(target, "fixture-database").Serialized == "journal:v1:backup-survivor",
                "Missing primary did not recover from the confirmed backup.");
            File.Delete(target + ".bak");
            Check(!SaveService.HasSaveCandidate(target),
                "Load panel advertised a save without primary or backup.");
            Console.WriteLine("P11.01 SaveService headless filesystem checks passed: " + checks);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
