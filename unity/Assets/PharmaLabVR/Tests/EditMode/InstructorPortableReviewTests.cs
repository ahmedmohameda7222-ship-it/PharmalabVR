using System;
using System.IO;
using NUnit.Framework;
using PharmaLabVR.Session;

namespace PharmaLabVR.Tests.EditMode
{
    public sealed class InstructorPortableReviewTests
    {
        [Test]
        public void P23_02_InstructorPreviewsPortableNativeAndStudentEvidenceWithoutAttestation()
        {
            var folder = Path.Combine(Path.GetTempPath(), "plv-instructor-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                const string branch = "classroom-research-branch";
                const string native = "{\"schemaVersion\":1,\"branchId\":\"classroom-research-branch\"," +
                    "\"eventSequence\":\"7\",\"scientificPackage\":{\"identity\":\"research-fixture-db\"}," +
                    "\"observations\":[{\"vesselId\":\"receiver\",\"observableId\":\"pH\"," +
                    "\"asOfMaterialRevision\":\"2\",\"support\":\"Supported\"," +
                    "\"freshness\":\"Current\",\"computationState\":\"Ready\"," +
                    "\"modelId\":\"IPhreeqc-research\",\"databaseIdentity\":\"research-fixture-db\"," +
                    "\"maturity\":\"Research\",\"value\":6.2,\"valueAvailable\":true}]," +
                    "\"commandReceipts\":[]}";
                var notebook = new NotebookService();
                var journal = Path.Combine(folder, "learner.plv-notes");
                notebook.Append(journal, branch, "7", "receiver",
                    "Student explanation: I would repeat this research measurement.");
                var snapshot = notebook.ExportBranch(journal, branch, "7");
                var pack = Path.Combine(folder, "submitted.plvpack");
                PortableSessionArchive.Write(pack, native, snapshot, branch, "7");
                var preview = InstructorPortableReview.Open(pack);
                Assert.That(preview.NativeBranchId, Is.EqualTo(branch));
                Assert.That(preview.NativeEventSequence, Is.EqualTo("7"));
                Assert.That(preview.StudentAnnotations.Count, Is.EqualTo(1));
                Assert.That(preview.NativeObservations.Count, Is.EqualTo(1));
                Assert.That(preview.NativeObservations[0].DisplayValue, Is.EqualTo("6.20"));
                Assert.That(preview.Summary, Does.Contain("raw 6.2"));
                Assert.That(preview.Summary, Does.Contain("display 6.20"));
                Assert.That(preview.Summary, Does.Contain("NOT ATTESTED OR GRADED"));
                Assert.That(preview.CompactSummary, Does.Contain("NOT ATTESTED"));
                Assert.That(preview.CompactSummary, Does.Contain("Recorded receiver pH 6.20"));
                Assert.That(preview.CompactSummary.Length, Is.LessThan(300),
                    "XR recorded preview must stay inside its bounded detail area.");
                Assert.That(preview.Summary, Does.Contain("Student explanation"));
                Assert.That(preview.Summary, Does.Contain("checksum").IgnoreCase);
                Assert.That(preview.Summary, Does.Not.Contain("certified score"));
                File.AppendAllText(pack, "tampered");
                Assert.Throws<InvalidDataException>(() => InstructorPortableReview.Open(pack));
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
