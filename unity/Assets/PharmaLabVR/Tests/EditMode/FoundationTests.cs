using NUnit.Framework;
using PharmaLabVR.Protocols;
using PharmaLabVR.Input;
using PharmaLabVR.UI;
using PharmaLabVR.Core;
using UnityEngine;

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
                trackingValid = true, actuator01 = 0.25f, geometryProfileHash = "profile" };
            var json = NativeJsonCodec.EncodeInputBatch(new[] { sample });
            StringAssert.StartsWith("[", json);
            StringAssert.Contains("\"toolId\":\"tool\\\"1\"", json);
            StringAssert.Contains("\"positionMetres\":[1.5,2,3]", json);
            StringAssert.Contains("\"sampleSequence\":\"2\"", json);
        }
    }
}
