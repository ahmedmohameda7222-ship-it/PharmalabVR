using System;
using System.IO;
using NUnit.Framework;
using PharmaLabVR.University;

namespace PharmaLabVR.Tests.EditMode
{
    // Authored for the deferred final local Unity campaign. The same runtime
    // class is already source-linked by the .NET 8 headless contract project.
    public sealed class UniversitySyncOutboxTests
    {
        [Test]
        public void U04_OfflineNativeReceiptIntentSurvivesRestartButNeverClaimsSync()
        {
            var root = Path.Combine(Path.GetTempPath(), "plv-u04-unity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var path = Path.Combine(root, "pending.plvoutbox");
                var intent = new UniversityUploadIntent(
                    "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
                    "student-session", "11111111-1111-4111-8111-111111111111",
                    "research-branch", "4", new string('a', 64),
                    new string('b', 64), new string('c', 64));
                var outbox = new UniversitySyncOutbox(path);
                Assert.That(outbox.Enqueue(intent), Is.True);
                Assert.That(outbox.Enqueue(intent), Is.False,
                    "Exact retry must not mutate the durable intent.");
                var recovered = new UniversitySyncOutbox(path).Pending();
                Assert.That(recovered.Count, Is.EqualTo(1));
                Assert.That(recovered[0].NativeEventSequence, Is.EqualTo("4"));
                Assert.That(recovered[0].ScientificPackageSha256, Is.EqualTo(new string('b', 64)));
                Assert.Throws<InvalidDataException>(() => outbox.Enqueue(
                    new UniversityUploadIntent(intent.InstitutionId, intent.LocalSessionId,
                        intent.EventId, intent.NativeBranchId, intent.NativeEventSequence,
                        new string('d', 64), intent.ScientificPackageSha256,
                        intent.ArtifactSha256)));
                Assert.That(outbox.Pending().Count, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void U04_SecondProducerMustNotBypassExclusiveFileLease()
        {
            var root = Path.Combine(Path.GetTempPath(), "plv-u04-lease-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var path = Path.Combine(root, "pending.plvoutbox");
                var outbox = new UniversitySyncOutbox(path);
                var intent = new UniversityUploadIntent(
                    "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
                    "student-session", "11111111-1111-4111-8111-111111111111",
                    "research-branch", "4", new string('a', 64),
                    new string('b', 64), new string('c', 64));
                using (var lease = new FileStream(path + ".lock",
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.Throws<InvalidDataException>(() => outbox.Enqueue(intent));
                    Assert.That(outbox.Pending(), Is.Empty);
                }
                Assert.That(outbox.Enqueue(intent), Is.True);
                Assert.That(outbox.Pending().Count, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
