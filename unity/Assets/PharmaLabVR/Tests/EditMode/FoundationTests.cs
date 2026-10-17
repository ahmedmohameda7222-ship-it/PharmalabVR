using NUnit.Framework;
using PharmaLabVR.Protocols;
using PharmaLabVR.Input;
using PharmaLabVR.Core;
using UnityEngine;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;
using PharmaLabVR.Session;
using System;
using System.IO;
using Object = UnityEngine.Object;

namespace PharmaLabVR.Tests
{
    public sealed class FoundationTests
    {
        [Test]
        public void P11_01_ManagedCheckpointImportIsolatedFromOriginalNativeAuthority()
        {
            var root = Path.Combine(Path.GetTempPath(), "plv-native-import-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "live.ckpt");
            CoreSession original = null;
            try
            {
                original = new CoreSession("managed-recovery");
                var firstReceipt = original.SubmitOrdered("CreateVessel",
                    "{\"id\":\"source\",\"capacityM3\":0.00001}");
                StringAssert.Contains("\"accepted\":true", firstReceipt);
                original.WriteNativeCheckpoint(path);
                using (var recovered = CoreSession.ImportNativeCheckpoint(path))
                {
                    Assert.That(recovered.BranchId, Is.EqualTo(original.BranchId));
                    var beforeOriginal = original.ExportSession();
                    var restoredBefore = recovered.ExportSession();
                    StringAssert.Contains("\"id\":\"source\"", restoredBefore);
                    StringAssert.Contains("\"nextCommandSequence\":\"2\"", restoredBefore);
                    // Native import establishes SessionLoad Hold, even when the
                    // checkpoint bytes themselves were fully validated.
                    StringAssert.Contains("\"reason\":\"SessionLoad\"", restoredBefore);
                    var restoredReceipt = recovered.SubmitOrdered("CreateVessel",
                        "{\"id\":\"receiver\",\"capacityM3\":0.00001}");
                    StringAssert.Contains("\"code\":\"SessionHeld\"", restoredReceipt);
                    StringAssert.Contains("\"nextCommandSequence\":\"3\"",
                        recovered.ExportSession());
                    Assert.That(original.ExportSession(), Is.EqualTo(beforeOriginal),
                        "Recovered Native handle mutated the active original context.");
                }
                Assert.That(original.IsOpen, Is.True,
                    "Disposing the restored context must not retire the original.");
                Assert.Throws<ArgumentException>(() =>
                    CoreSession.ImportNativeCheckpoint("relative.ckpt"));
                Assert.Throws<ScientificPackageMismatchException>(() =>
                    CoreSession.ImportNativeCheckpoint(path, "different-installed-science.dat"));
            }
            finally
            {
                original?.Dispose();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void P09_02_RecordedObservationsNeedExplicitNativeNumericPresence()
        {
            // Existing Native exports lacking a finite 'value' must not acquire
            // a fictitious pH zero when JsonUtility defaults the double to 0.0.
            const string missing = @"{""schemaVersion"":1,""branchId"":""branch-1"",
                ""scientificPackage"":{""identity"":""fixture""},
                ""observations"":[{""vesselId"":""receiver"",""observableId"":""pH"",
                ""support"":""Supported"",""freshness"":""Current"",
                ""computationState"":""Ready"",""value"":0}]}";
            var missingReview = RecordedSessionReview.FromJson(missing);
            Assert.That(missingReview.Observations[0].DisplayValue, Is.EqualTo("--"),
                "Missing Native numeric presence must not render fabricated pH 0.");
            var present = missing.Replace(@"""value"":0", @"""valueAvailable"":true,""value"":0");
            var actualZero = RecordedSessionReview.FromJson(present);
            Assert.That(actualZero.Observations[0].DisplayValue, Is.EqualTo("0.00"),
                "A present finite zero and an omitted pH field must remain distinct.");
            var stale = present.Replace(@"""freshness"":""Current""", @"""freshness"":""Stale""");
            Assert.That(RecordedSessionReview.FromJson(stale).Observations[0].DisplayValue,
                Is.EqualTo("--"), "Stale recorded scientific pH must never be labeled current.");
        }

        [Test]
        public void P11_02_RecordedReviewHasOnlyValidatedNativeCommandReceipts()
        {
            const string json = @"{""schemaVersion"":1,""branchId"":""branch-test"",
                ""scientificPackage"":{""identity"":""minteq.v4.dat""},
                ""observations"":[],
                ""commandReceipts"":[
                  {""commandSequence"":""2"",
                   ""canonicalCommand"":{""schemaVersion"":1,""branchId"":""branch-test"",
                     ""commandSequence"":""2"",""type"":""PrepareStock""},
                   ""terminalOutcome"":{""schemaVersion"":1,""branchId"":""branch-test"",
                     ""commandSequence"":""2"",""type"":""CommandOutcome"",""accepted"":false,""code"":""Rejected""}},
                  {""commandSequence"":""1"",
                   ""canonicalCommand"":{""schemaVersion"":1,""branchId"":""branch-test"",
                     ""commandSequence"":""1"",""type"":""CreateVessel""},
                   ""terminalOutcome"":{""schemaVersion"":1,""branchId"":""branch-test"",
                     ""commandSequence"":""1"",""type"":""CommandOutcome"",""accepted"":true,""code"":""Accepted""}}]}";
            var history = RecordedSessionReview.FromJson(json);
            Assert.That(history.CommandReceipts.Count, Is.EqualTo(2));
            Assert.That(history.CommandReceipts[0].CommandSequence, Is.EqualTo("1"));
            Assert.That(history.CommandReceipts[1].CommandType, Is.EqualTo("PrepareStock"));
            Assert.That(history.CommandReceipts[1].Accepted, Is.False);
        }

        [Test]
        public void P11_03_RecordedRunComparisonNeverComputesDeltaForStaleOrDifferentModels()
        {
            const string source = @"{""schemaVersion"":1,""branchId"":""run-a"",
                ""scientificPackage"":{""identity"":""minteq-pinned""},
                ""observations"":[{""vesselId"":""receiver"",""observableId"":""pH"",
                    ""support"":""Supported"",""freshness"":""Current"",
                    ""computationState"":""Ready"",""modelId"":""IPhreeqc-research"",
                    ""databaseIdentity"":""minteq-pinned"",""valueAvailable"":true,""value"":7.1}]}";
            var current = RecordedSessionReview.FromJson(source);
            var differentBranch = source.Replace("run-a", "run-b").Replace("7.1", "6.7");
            var comparable = RecordedRunComparison.Compare(current,
                RecordedSessionReview.FromJson(differentBranch));
            Assert.That(comparable.Rows[0].DeltaAvailable, Is.True);
            Assert.That(comparable.Rows[0].Delta, Is.EqualTo(-0.4).Within(0.000001));
            var stale = RecordedRunComparison.Compare(current, RecordedSessionReview.FromJson(
                differentBranch.Replace("Current", "Stale")));
            Assert.That(stale.Rows[0].CandidateDisplay, Is.EqualTo("--"));
            Assert.That(stale.Rows[0].DeltaAvailable, Is.False);
            var otherModel = RecordedRunComparison.Compare(current, RecordedSessionReview.FromJson(
                differentBranch.Replace("minteq-pinned", "another-science-db")));
            Assert.That(otherModel.Rows[0].DeltaAvailable, Is.False);
            Assert.That(otherModel.Rows[0].CandidateDisplay, Is.EqualTo("6.70"));
        }

        [Test]
        public void P11_02_RecordedOnlyBackupReviewOpensWithoutScientificRecompute()
        {
            var saved = Path.Combine(Path.GetTempPath(),
                "plv-recorded-backup-" + Guid.NewGuid().ToString("N") + ".plv.json");
            const string recorded = @"{""schemaVersion"":1,""branchId"":""research-branch"",
                ""scientificPackage"":{""identity"":""unavailable-model""},
                ""observations"":[{""vesselId"":""receiver"",""observableId"":""pH"",
                    ""support"":""Unsupported"",""freshness"":""Absent"",
                    ""computationState"":""Pending""}]}";
            try
            {
                File.WriteAllText(saved + ".bak", recorded);
                var review = RecordedSessionReview.OpenRecoverable(saved);
                Assert.That(review.BranchId, Is.EqualTo("research-branch"));
                Assert.That(review.ModelIdentity, Is.EqualTo("unavailable-model"));
                Assert.That(review.Observations[0].DisplayValue, Is.EqualTo("--"));
                File.WriteAllText(saved, "{\"schemaVersion\":-1}");
                Assert.That(RecordedSessionReview.OpenRecoverable(saved).BranchId,
                    Is.EqualTo("research-branch"));
                File.WriteAllText(saved + ".bak", "{\"schemaVersion\":-1}");
                Assert.Throws<InvalidDataException>(() => RecordedSessionReview.OpenRecoverable(saved));
            }
            finally
            {
                if (File.Exists(saved)) File.Delete(saved);
                if (File.Exists(saved + ".bak")) File.Delete(saved + ".bak");
            }
        }

        [Test]
        public void P11_01_ExplicitNativeCheckpointPersistsRealNativeExportWithoutChemistryMutation()
        {
            var dir = Path.Combine(Path.GetTempPath(),
                "plv-native-ckpt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var pinned = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
                using var core = new CoreSession("native-checkpoint-test", "Desktop",
                    pinned, ScientificPackage.DatabaseSha256);
                Assert.That(core.SubmitOrdered("CreateVessel",
                    "{\"id\":\"receiver\",\"capacityM3\":0.00001}"),
                    Does.Contain("\"accepted\":true"));
                var exportBefore = core.ExportSession();
                var destination = Path.Combine(dir, "native.ckpt");
                core.WriteNativeCheckpoint(destination);
                Assert.That(File.Exists(destination + ".slot0"), Is.True);
                StringAssert.Contains(exportBefore, File.ReadAllText(destination + ".slot0"));
                Assert.That(core.ExportSession(), Is.EqualTo(exportBefore),
                    "Explicit checkpoint must not advance Native event or material revision.");
                core.WriteNativeCheckpoint(destination);
                Assert.That(File.Exists(destination + ".slot1"), Is.True);
                Assert.That(core.ExportSession(), Is.EqualTo(exportBefore));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void P11_01_AutomaticNativeCheckpointPersistsDirtySessionWithoutExplicitSave()
        {
            var directory = Path.Combine(Path.GetTempPath(),
                "plv-auto-native-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var pinned = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
                var path = Path.Combine(directory, "auto.ckpt");
                using var core = new CoreSession("native-auto-test", "Desktop",
                    pinned, ScientificPackage.DatabaseSha256, "Precision", path);
                Assert.That(core.SubmitOrdered("CreateVessel",
                    "{\"id\":\"receiver\",\"capacityM3\":0.00001}"),
                    Does.Contain("\"accepted\":true"));
                core.Step(0.0, 1_000_000_000UL);
                Assert.That(File.Exists(path + ".slot0"), Is.False);
                core.Step(0.0, 30_000_000_000UL);
                Assert.That(File.Exists(path + ".slot0"), Is.True,
                    "Native did not persist its first-dirty wall deadline.");
                Assert.That(File.ReadAllText(path + ".slot0"),
                    Does.Contain(core.ExportSession()));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void P11_01_StorageHoldIdentifiesCommittedNativeTickAfterFailedAutoCheckpoint()
        {
            var directory = Path.Combine(Path.GetTempPath(),
                "plv-storage-hold-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var pinned = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
                var path = Path.Combine(directory, "auto.ckpt");
                using var core = new CoreSession("native-storage-test", "Desktop",
                    pinned, ScientificPackage.DatabaseSha256, "Precision", path);
                core.WriteNativeCheckpoint(path);
                var previous = File.ReadAllText(path + ".slot0");
                Assert.That(core.SubmitOrdered("CreateVessel",
                    "{\"id\":\"receiver\",\"capacityM3\":0.00001}"),
                    Does.Contain("\"accepted\":true"));
                Directory.CreateDirectory(path + ".slot1.tmp");
                for (ulong tick = 1; tick <= 1499UL; ++tick)
                    core.Step(0.02, tick * 1_000_000UL);
                // The 1500th tick crossed the 30s simulation threshold; the
                // storage failure must not misreport a committed tick as rejected.
                core.Step(0.021, 1_500_000_000UL);
                var committed = core.ReadSnapshot();
                StringAssert.Contains("\"reason\":\"Storage\"", committed);
                StringAssert.Contains("\"active\":true", committed);
                Assert.That(File.ReadAllText(path + ".slot0"), Is.EqualTo(previous),
                    "Failed Native staging overwrote the last confirmed checkpoint.");
                Assert.Throws<InvalidOperationException>(() =>
                    core.Step(0.02, 1_501_000_000UL));
                Assert.That(core.ReadSnapshot(), Is.EqualTo(committed),
                    "A failed pre-tick durability retry must not advance simulation.");
                Directory.Delete(path + ".slot1.tmp");
                core.Step(0.0, 1_502_000_000UL);
                StringAssert.Contains("\"reason\":\"Storage\"", core.ReadSnapshot());
                Assert.That(File.Exists(path + ".slot1"), Is.True,
                    "Storage recovery did not durably retry its real Native snapshot.");
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void P11_01_ImportedNativeSessionCanResumeAutomaticCheckpointPersistence()
        {
            var directory = Path.Combine(Path.GetTempPath(),
                "plv-native-resume-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var pinned = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
                string originalExport;
                using (var original = new CoreSession("native-resume-test", "Desktop",
                    pinned, ScientificPackage.DatabaseSha256))
                {
                    Assert.That(original.SubmitOrdered("CreateVessel",
                        "{\"id\":\"receiver\",\"capacityM3\":0.00001}"),
                        Does.Contain("\"accepted\":true"));
                    originalExport = original.ExportSession();
                }
                using var restored = CoreSession.ImportSession(originalExport, pinned);
                var checkpoint = Path.Combine(directory, "resumed.ckpt");
                restored.ConfigureAutomaticCheckpoint(checkpoint);
                restored.Step(0.0, 30_000_000_000UL);
                Assert.That(File.Exists(checkpoint + ".slot0"), Is.True);
                Assert.That(File.ReadAllText(checkpoint + ".slot0"),
                    Does.Contain("\"branchId\":\"native-resume-test\""));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void G01_IdenticalWorldGeometryProducesIdenticalLabSample()
        {
            var lab = new GameObject("lab"); var tool = new GameObject("tool");
            tool.transform.position = new Vector3(0.2f, 0.8f, 0.5f);
            var a = LabGeometryAdapter.BuildSample("tool", 1, 1, lab.transform, tool.transform, 0.5f, "profile", true);
            var b = LabGeometryAdapter.BuildSample("tool", 2, 2, lab.transform, tool.transform, 0.5f, "profile", true);
            Assert.That(a.positionMetres, Is.EqualTo(b.positionMetres));
            Object.DestroyImmediate(tool); Object.DestroyImmediate(lab);
        }

        [Test]
        public void G02_CaptureUsesDownwardOutletAndReceiverApertureInsteadOfToolRootDistance()
        {
            var receiver = new GameObject("receiver");
            var target = receiver.AddComponent<LabCaptureTarget>();
            target.Configure("receiver", 0.05f);
            var tool = new GameObject("burette");
            var outletExit = new GameObject("OutletExit");
            outletExit.transform.SetParent(tool.transform, false);
            outletExit.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            tool.transform.position = receiver.transform.position;
            Assert.That(target.Estimate(tool.transform).Length, Is.Zero, "Tool root inside receiver is not a valid outlet.");
            tool.transform.position = new Vector3(0f, 0.10f, 0f);
            Assert.That(target.Estimate(tool.transform).Length, Is.EqualTo(1));
            tool.transform.position = new Vector3(0.10f, 0.10f, 0f);
            Assert.That(target.Estimate(tool.transform).Length, Is.Zero, "Outlet misses the aperture.");
            tool.transform.position = new Vector3(0f, 0.10f, 0f);
            tool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Assert.That(target.Estimate(tool.transform).Length, Is.Zero, "Sideways outlet cannot land in aperture.");
            Object.DestroyImmediate(tool);
            Object.DestroyImmediate(receiver);
        }

        [Test]
        public void P03_03_CaptureFollowsTheToolOutletInsteadOfAnEmbeddedOffset()
        {
            var receiver = new GameObject("receiver");
            var target = receiver.AddComponent<LabCaptureTarget>();
            target.Configure("receiver", 0.05f);
            var tool = new GameObject("burette");
            tool.transform.position = new Vector3(0f, 0.10f, 0f);
            var outlet = new GameObject("OutletExit");
            outlet.transform.SetParent(tool.transform, false);
            outlet.transform.localPosition = new Vector3(0.10f, -0.02f, 0f);
            Assert.That(target.Estimate(tool.transform).Length, Is.Zero, "An outlet outside the aperture must spill.");
            outlet.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            Assert.That(target.Estimate(tool.transform).Length, Is.EqualTo(1));
            Object.DestroyImmediate(tool);
            Object.DestroyImmediate(receiver);
        }

        [Test]
        public void P03_03_MovingReceiverRejectsCaptureWithoutModifyingToolOrInventory()
        {
            var receiver = new GameObject("moving-receiver");
            var tool = new GameObject("burette-geometry");
            try
            {
                var aperture = receiver.AddComponent<LabCaptureTarget>();
                aperture.Configure("receiver", 0.05f);
                var outlet = new GameObject("OutletExit");
                outlet.transform.SetParent(tool.transform, false);
                outlet.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                tool.transform.position = new Vector3(0f, 0.1f, 0f);
                Assert.That(aperture.Estimate(tool.transform, Vector3.zero).Length, Is.EqualTo(1));
                Assert.That(aperture.Estimate(tool.transform, new Vector3(0.04f, 0f, 0f)).Length,
                    Is.Zero, "Fast receiver cannot inherit a stationary aperture hit.");
                Assert.That(aperture.Estimate(tool.transform, new Vector3(float.NaN, 0f, 0f)).Length,
                    Is.Zero, "Invalid tracking velocity must never admit a capture.");
                Assert.That(aperture.Estimate(tool.transform, Vector3.zero).Length, Is.EqualTo(1),
                    "Rejected motion must not permanently poison the stationary receiver.");
            }
            finally
            {
                Object.DestroyImmediate(tool);
                Object.DestroyImmediate(receiver);
            }
        }

        [Test]
        public void P03_03_OverlappingPhysicalReceiversNeverAwardFirstColliderMaterial()
        {
            var first = new GameObject("first-receiver");
            var second = new GameObject("second-receiver");
            var tool = new GameObject("outlet-tool");
            try
            {
                var a = first.AddComponent<LabCaptureTarget>();
                a.Configure("receiver", 0.05f);
                var b = second.AddComponent<LabCaptureTarget>();
                b.Configure("other-receiver", 0.05f);
                var outlet = new GameObject("OutletExit");
                outlet.transform.SetParent(tool.transform, false);
                outlet.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                tool.transform.position = new Vector3(0f, 0.10f, 0f);
                // Moving the second receiver away makes the only real target
                // admissible; restoring it must refuse both candidates.
                second.transform.position = new Vector3(0.5f, 0f, 0f);
                var single = LabCaptureTarget.EstimateExclusive(tool.transform, a);
                Assert.That(single.Length, Is.EqualTo(1));
                Assert.That(single[0].destinationInventoryId, Is.EqualTo("receiver"));
                second.transform.position = Vector3.zero;
                Assert.That(LabCaptureTarget.EstimateExclusive(tool.transform, a).Length,
                    Is.Zero, "Overlapping receiver rims must be rejected, never first-wins.");
                b.Configure("receiver", 0.05f);
                Assert.That(LabCaptureTarget.EstimateExclusive(tool.transform, a).Length,
                    Is.Zero, "Two physical rims remain ambiguous even with same inventory ID.");
                second.SetActive(false);
                Assert.That(LabCaptureTarget.EstimateExclusive(tool.transform, a).Length,
                    Is.EqualTo(1), "An inactive candidate cannot shadow an active receiver.");
            }
            finally
            {
                Object.DestroyImmediate(tool);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void Q02_ResearchOrUnsupportedObservationCannotEnableCertifiedScore()
        {
            var protocol = new ProtocolDefinition { certifiedScoring = true, requiredObservables = new[] { "pH", "indicatorColor" } };
            var capabilities = new[] { new ObservationCapability("pH", "Validated", "Ready", "Current", "Published"), new ObservationCapability("indicatorColor", "Unsupported", "Ready", "Absent", "Research") };
            Assert.That(CapabilityGate.EligibleForCertifiedScore(protocol, capabilities), Is.False);
        }

        [Test]
        public void D06_LocalizationPreservesPunctuationAndReportsRtl()
        {
            var localization = new LocalizationService();
            localization.Load("ar", new TextAsset("{\"status\":\"أغلق الأداة، ثم تابع: الآن.\",\"escaped\":\"سطر\\nثانٍ\"}"));
            Assert.That(localization.IsRightToLeft, Is.True);
            Assert.That(localization.Get("status"), Is.EqualTo("أغلق الأداة، ثم تابع: الآن."));
            Assert.That(localization.Get("escaped"), Is.EqualTo("سطر\nثانٍ"));
            Assert.That(localization.Get("missing"), Is.EqualTo("[missing]"));
        }

        [Test]
        public void B01_ModeCoordinatorNeverLeavesBothAdaptersActive()
        {
            var root = new GameObject("modes");
            var desktop = new GameObject("desktop");
            var xr = new GameObject("xr");
            var coordinator = root.AddComponent<ModeCoordinator>();
            coordinator.Configure(desktop, xr);
            Assert.That(desktop.activeSelf, Is.True);
            Assert.That(xr.activeSelf, Is.False);
            coordinator.Select(ApplicationMode.VirtualReality);
            Assert.That(desktop.activeSelf, Is.False);
            Assert.That(xr.activeSelf, Is.True);
            Object.DestroyImmediate(root); Object.DestroyImmediate(desktop); Object.DestroyImmediate(xr);
        }

        [Test]
        public void G03_InputBatchUsesInvariantNativeSchema()
        {
            var sample = new LabInputSample { toolId = "tool\"1", sampleSequence = 2, captureMonotonicNs = 3,
                positionMetres = new Vector3(1.5f, 2f, 3f), rotation = Quaternion.identity,
                trackingValid = true, actuator01 = 0.25f, coordinateFrame = "lab", geometryProfileHash = "profile",
                profileRevision = 4, toolRevision = 5,
                captureFractions = new[] { new LabCaptureEstimate { destinationInventoryId = "receiver", fraction = 0.75f } } };
            var json = NativeJsonCodec.EncodeInputBatch(new[] { sample });
            StringAssert.StartsWith("[", json);
            StringAssert.Contains("\"toolId\":\"tool\\\"1\"", json);
            StringAssert.Contains("\"positionMetres\":[1.5,2,3]", json);
            StringAssert.Contains("\"sampleSequence\":\"2\"", json);
            StringAssert.Contains("\"coordinateFrame\":\"lab\"", json);
            StringAssert.Contains("\"profileRevision\":\"4\"", json);
            StringAssert.Contains("\"destinationInventoryId\":\"receiver\"", json);
        }

        [Test]
        public void Q01_Q03_ProtocolTracksCommittedVolumesWithoutMutatingCoreOrResettingHistory()
        {
            var definition = new ProtocolDefinition { schemaVersion = 1, maturity = "Research", certifiedScoring = false,
                steps = new[] { "Prepare", "Titrate" }, tracks = new[] { "cumulativeDeliveredVolumeM3", "stageDeliveredVolumeM3" } };
            var runner = new ProtocolRunner(definition);
            runner.RecordCommittedDelivery(2e-6);
            runner.RecordRefill();
            runner.RecordCommittedDelivery(3e-6);
            Assert.That(runner.CumulativeDeliveredVolumeM3, Is.EqualTo(5e-6).Within(1e-15));
            Assert.That(runner.StageDeliveredVolumeM3, Is.EqualTo(3e-6).Within(1e-15));
            Assert.That(runner.AdvanceInstruction(), Is.True);
            Assert.That(runner.AdvanceInstruction(), Is.False);
        }

        [Test]
        public void V01_LiquidVisualIsDerivedFromCommittedVolumeAndAnchoredAtTheBottom()
        {
            var root = new GameObject("vessel");
            var visual = new GameObject("liquid");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one;
            var presenter = root.AddComponent<LiquidPresenter>();
            presenter.Configure(visual.transform, 1e-5, 0.2f, -0.1f);
            presenter.SetCommittedVolume(5e-6);
            Assert.That(presenter.Fill01, Is.EqualTo(0.5f).Within(1e-6));
            Assert.That(visual.transform.localScale.y, Is.EqualTo(0.1f).Within(1e-6));
            Assert.That(visual.transform.localPosition.y, Is.EqualTo(-0.05f).Within(1e-6));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void V01_CylinderLiquidRenderedBoundsMatchFullHalfAndEmptyVolume()
        {
            var root = new GameObject("vessel");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.transform.SetParent(root.transform, false);
            var presenter = root.AddComponent<LiquidPresenter>();
            presenter.Configure(visual.transform, 1e-5, 0.2f, -0.1f);

            presenter.SetCommittedVolume(1e-5);
            Assert.That(visual.GetComponent<Renderer>().bounds.size.y, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(visual.GetComponent<Renderer>().bounds.min.y, Is.EqualTo(-0.1f).Within(1e-5f));
            presenter.SetCommittedVolume(5e-6);
            Assert.That(visual.GetComponent<Renderer>().bounds.size.y, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(visual.GetComponent<Renderer>().bounds.min.y, Is.EqualTo(-0.1f).Within(1e-5f));
            presenter.SetCommittedVolume(0);
            Assert.That(visual.activeSelf, Is.False);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void J03_UnsupportedObservationNeverDisplaysAValue()
        {
            var root = new GameObject("measurement");
            var view = root.AddComponent<MeasurementView>();
            view.Present(MeasurementAvailability.Unsupported, "12.34", "No validated optical model");
            Assert.That(view.DisplayValue, Is.EqualTo("--"));
            Assert.That(view.Availability, Is.EqualTo(MeasurementAvailability.Unsupported));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void R03_CorruptLatestSaveRecoversLastConfirmedBackupWithoutReadingPartialTemporaryFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "plv-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var package = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
                using var session = new CoreSession("save-fault", "Desktop", package, ScientificPackage.DatabaseSha256);
                var saves = new SaveService();
                var path = saves.Save(session, directory, "student");
                Assert.That(session.SubmitOrdered("CreateVessel", "{\"id\":\"later\",\"capacityM3\":0.00001}"),
                    Does.Contain("\"accepted\":true"));
                saves.Save(session, directory, "student");
                Assert.That(File.Exists(path + ".bak"), Is.True);
                File.WriteAllText(path + ".tmp", "partial crash tail");
                File.WriteAllText(path, "corrupt primary");
                using var recovered = saves.Load(path, package);
                StringAssert.DoesNotContain("\"id\":\"later\"", recovered.ReadSnapshot());
                File.WriteAllText(path + ".bak", "corrupt backup");
                Assert.Throws<InvalidDataException>(() => saves.Load(path, package));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void R04_FailedTemporaryWritePreservesTheLastConfirmedSave()
        {
            var directory = Path.Combine(Path.GetTempPath(), "plv-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var package = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
                using var session = new CoreSession("write-fault", "Desktop", package, ScientificPackage.DatabaseSha256);
                var saves = new SaveService();
                var path = saves.Save(session, directory, "student");
                var confirmed = File.ReadAllBytes(path);
                Directory.CreateDirectory(path + ".tmp");
                Assert.Catch<UnauthorizedAccessException>(() => saves.Save(session, directory, "student"));
                CollectionAssert.AreEqual(confirmed, File.ReadAllBytes(path));
                using var recovered = saves.Load(path, package);
                Assert.That(recovered.IsOpen, Is.True);
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
