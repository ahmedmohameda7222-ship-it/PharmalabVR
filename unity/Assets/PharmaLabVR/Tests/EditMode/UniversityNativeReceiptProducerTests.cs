using System;
using System.IO;
using NUnit.Framework;
using PharmaLabVR.Core;
using PharmaLabVR.University;
using UnityEngine;

namespace PharmaLabVR.Tests.EditMode
{
    // Requires the REAL Unity Native plugin and pinned IPhreeqc data.
    // Authored only; execute in the final local Unity 6000.3.25f1 campaign.
    public sealed class UniversityNativeReceiptProducerTests
    {
        [Test]
        public void U04_RealNativeCommandExportBecomesImmutablePendingIntentOnly()
        {
            var path = Path.Combine(Path.GetTempPath(),
                "plv-u04-core-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            try
            {
                var pinnedPath = ScientificPackage.PrepareLocal(
                    Application.streamingAssetsPath);
                var package = File.ReadAllBytes(pinnedPath);
                using (var native = new CoreSession("u04-native-source-1", "Desktop",
                    pinnedPath, ScientificPackage.DatabaseSha256))
                {
                    var outcome = native.SubmitOrdered("Pause");
                    var unchangedExport = native.ExportSession();
                    var queue = new UniversitySyncOutbox(
                        Path.Combine(path, "pending.plvoutbox"));
                    const string tenant = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
                    const string eventId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
                    Assert.That(UniversityNativeReceiptProducer.CaptureCompletedCommand(
                        queue, native, tenant, "local-lab-1", eventId, outcome, package),
                        Is.True);
                    Assert.That(queue.Pending().Count, Is.EqualTo(1));
                    Assert.That(queue.Pending()[0].NativeBranchId,
                        Is.EqualTo(native.BranchId));
                    Assert.That(queue.Pending()[0].NativeEventSequence,
                        Is.EqualTo("1"));
                    Assert.That(native.ExportSession(), Is.EqualTo(unchangedExport),
                        "Local evidence capture must never modify Native science.");
                    Assert.That(UniversityNativeReceiptProducer.CaptureCompletedCommand(
                        queue, native, tenant, "local-lab-1", eventId, outcome, package),
                        Is.False, "An exact replay is only a local immutable retry.");

                    var forgedOutcome = outcome.Replace(
                        "\"branchId\":\"u04-native-source-1\"",
                        "\"branchId\":\"another-branch\"");
                    Assert.That(forgedOutcome, Is.Not.EqualTo(outcome),
                        "Test must alter the actual Native outcome identity.");
                    Assert.Throws<InvalidDataException>(() =>
                        UniversityNativeReceiptProducer.CaptureCompletedCommand(
                            queue, native, tenant, "local-lab-1",
                            "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
                            forgedOutcome, package));
                    Assert.That(queue.Pending().Count, Is.EqualTo(1),
                        "Rejected Native identity must never change pending records.");
                }
            }
            finally
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
