using System;
using System.IO;
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
}
