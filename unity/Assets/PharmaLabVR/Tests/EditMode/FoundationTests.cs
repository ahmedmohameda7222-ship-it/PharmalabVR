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
