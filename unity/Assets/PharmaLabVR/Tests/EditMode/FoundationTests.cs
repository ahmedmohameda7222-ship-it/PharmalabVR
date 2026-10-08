using NUnit.Framework;
using PharmaLabVR.Protocols;
using PharmaLabVR.Input;
using PharmaLabVR.Core;
using UnityEngine;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;

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
        public void J03_UnsupportedObservationNeverDisplaysAValue()
        {
            var root = new GameObject("measurement");
            var view = root.AddComponent<MeasurementView>();
            view.Present(MeasurementAvailability.Unsupported, "12.34", "No validated optical model");
            Assert.That(view.DisplayValue, Is.EqualTo("--"));
            Assert.That(view.Availability, Is.EqualTo(MeasurementAvailability.Unsupported));
            Object.DestroyImmediate(root);
        }
    }
}
