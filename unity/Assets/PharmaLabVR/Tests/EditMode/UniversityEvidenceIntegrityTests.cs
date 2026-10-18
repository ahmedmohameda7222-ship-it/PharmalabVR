using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using PharmaLabVR.University;

namespace PharmaLabVR.Tests.EditMode
{
    // Authored for final Unity campaign; source-linked .NET8 runs in CI.
    public sealed class UniversityEvidenceIntegrityTests
    {
        [Test]
        public void U04_ExportBytesMustMatchEveryDeclaredDigestBeforePendingQueue()
        {
            var root = Path.Combine(Path.GetTempPath(), "plv-u04-bytes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var receipt = Encoding.UTF8.GetBytes("native-receipt");
                var package = Encoding.UTF8.GetBytes("science-package");
                var export = Encoding.UTF8.GetBytes("native-export");
                string Hash(byte[] bytes)
                {
                    using (var sha = SHA256.Create())
                        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "")
                            .ToLowerInvariant();
                }
                var intent = new UniversityUploadIntent(
                    "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "local-session",
                    "11111111-1111-4111-8111-111111111111", "native-branch",
                    "3", Hash(receipt), Hash(package), Hash(export));
                var outbox = new UniversitySyncOutbox(Path.Combine(root, "events.plvoutbox"));
                Assert.That(outbox.EnqueueVerifiedEvidence(intent, receipt, package, export),
                    Is.True);
                Assert.Throws<InvalidDataException>(() => outbox.EnqueueVerifiedEvidence(
                    intent, receipt, package, Encoding.UTF8.GetBytes("tampered")));
                Assert.That(outbox.Pending().Count, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
