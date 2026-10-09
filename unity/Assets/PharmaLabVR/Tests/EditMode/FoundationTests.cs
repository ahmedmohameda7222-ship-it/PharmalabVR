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
