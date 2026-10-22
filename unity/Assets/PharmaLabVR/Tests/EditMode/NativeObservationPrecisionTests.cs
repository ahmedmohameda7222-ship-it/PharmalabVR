using System;
using NUnit.Framework;
using PharmaLabVR.Lab;
using PharmaLabVR.Protocols;
using PharmaLabVR.UI;

namespace PharmaLabVR.Tests.EditMode
{
    public sealed class NativeObservationPrecisionTests
    {
        [Test]
        public void P12_02_RealUnitySnapshotParserRetainsNativePrecisionWhenDisplayRoundsToSeven()
        {
            // Fixture is a JSON/DTO contract, not synthesized chemistry.
            // It exercises Unity JsonUtility's actual double parsing path.
            const string snapshot =
                @"{""eventSequence"":""7"",""vessels"":[{""id"":""receiver""," +
                @"""materialRevision"":""3"",""capacityM3"":0.00005," +
                @"""inventory"":{""researchAdditiveVolumeM3"":0.00000001}}]," +
                @"""observations"":[{""vesselId"":""receiver"",""observableId"":""pH""," +
                @"""modelId"":""IPhreeqc-research"",""databaseIdentity"":""sha256:test-db""," +
                @"""support"":""Supported"",""freshness"":""Current""," +
                @"""computationState"":""Ready"",""maturity"":""Research""," +
                @"""asOfMaterialRevision"":""3"",""valueAvailable"":true,""value"":6.999}]}";
            var below = LabViewModel.FromNativeSnapshot(snapshot);
            Assert.That(below.Observations[0].ValueAvailable, Is.True);
            Assert.That(below.Observations[0].ComputationState, Is.EqualTo("Ready"));
            Assert.That(below.Observations[0].Value, Is.EqualTo(6.999).Within(1e-10));
            Assert.That(NativeMeasurementNote.Build(below, "receiver",
                MeasurementAvailability.Current, "7.00"), Does.Contain("raw Native pH 6.999"));
            Assert.That(ResearchLessonJourney.CompareRecordedPrediction("below-7",
                below, "receiver", MeasurementAvailability.Current, "7.00"),
                Does.Contain("criterion matched"));

            var above = LabViewModel.FromNativeSnapshot(snapshot.Replace(
                @"""value"":6.999", @"""value"":7.001"));
            Assert.That(above.Observations[0].Value, Is.EqualTo(7.001).Within(1e-10));
            Assert.That(ResearchLessonJourney.CompareRecordedPrediction("at-least-7",
                above, "receiver", MeasurementAvailability.Current, "7.00"),
                Does.Contain("criterion matched"));
            Assert.Throws<InvalidOperationException>(() => NativeMeasurementNote.Build(
                below, "receiver", MeasurementAvailability.Current, "6.20"));
            var missing = LabViewModel.FromNativeSnapshot(snapshot.Replace(
                @"""valueAvailable"":true", @"""valueAvailable"":false"));
            Assert.Throws<InvalidOperationException>(() => NativeMeasurementNote.Build(
                missing, "receiver", MeasurementAvailability.Current, "7.00"));
        }
    }
}
