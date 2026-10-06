using NUnit.Framework;
using PharmaLabVR.Protocols;
using PharmaLabVR.Input;
using PharmaLabVR.UI;
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
    }
}
