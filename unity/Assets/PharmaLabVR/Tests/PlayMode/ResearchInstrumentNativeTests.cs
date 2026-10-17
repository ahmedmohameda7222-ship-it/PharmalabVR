using System;
using System.Linq;
using NUnit.Framework;
using PharmaLabVR.Core;
using PharmaLabVR.Tools;
using UnityEngine;

namespace PharmaLabVR.Tests.PlayMode
{
    // Requires the real Native plugin. Authored now; Unity execution deferred.
    public sealed class ResearchInstrumentNativeTests
    {
        [Test]
        public void P03_02_RealPipetteBottlePourAndCleaningConserveNativeMaterial()
        {
            var root = new GameObject("Research native instruments");
            using var session = new CoreSession("research-metered-test", "Desktop", "", "", "Bulk");
            try
            {
                void Accepted(string type, string payload, string revisions = "{}")
                {
                    var result = session.SubmitOrdered(type, payload, revisions);
                    StringAssert.Contains("\"accepted\":true", result, type);
                }
                Accepted("CreateVessel", "{\"id\":\"source\",\"capacityM3\":0.0001}");
                Accepted("CreateVessel", "{\"id\":\"receiver\",\"capacityM3\":0.0001}");
                Accepted("CreateVessel", "{\"id\":\"rinse-water\",\"capacityM3\":0.0001}");
                Accepted("CreateSink", "{\"id\":\"spill\"}");
                Accepted("CreateSink", "{\"id\":\"waste\"}");
                Accepted("PrepareStock", "{\"vesselId\":\"source\",\"stockKind\":\"SodiumChloride\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.000005}",
                    "{\"source\":\"0\"}");
                Accepted("PrepareStock", "{\"vesselId\":\"rinse-water\",\"stockKind\":\"Water\",\"concentrationMolPerL\":0.0,\"referenceVolumeM3\":0.000001}",
                    "{\"rinse-water\":\"0\"}");
                var before = JsonUtility.FromJson<LedgerSnapshot>(session.ReadSnapshot());
                var pipette = root.AddComponent<PipetteActuator>();
                var bottle = root.AddComponent<BottlePourActuator>();
                pipette.Configure(session);
                bottle.Configure(session);
                var poured = bottle.Pour(2e-8);
                Assert.That(poured.Accepted, Is.True, poured.Code);
                Assert.That(bottle.RepresentedFilmVolumeM3, Is.EqualTo(2e-8).Within(1e-12));
                var washed = bottle.Rinse(1e-7);
                Assert.That(washed.Accepted, Is.True, washed.Code);
                var aspirated = pipette.Aspirate(1.5e-7);
                Assert.That(aspirated.Accepted, Is.True, aspirated.Code);
                var dispensed = pipette.Dispense(5e-8);
                Assert.That(dispensed.Accepted, Is.True, dispensed.Code);
                Assert.That(pipette.RepresentedTipVolumeM3, Is.EqualTo(1e-7).Within(1e-12));
                var after = JsonUtility.FromJson<LedgerSnapshot>(session.ReadSnapshot());
                Assert.That(after.vessels.Single(v => v.id == "receiver").inventory.chlorideMol,
                    Is.GreaterThan(0), "Receiver must have real represented solute.");
                Assert.That(after.sinks.Single(s => s.id == "waste").inventory.chlorideMol,
                    Is.GreaterThan(0), "Film cleaning must transfer real chloride into Native waste.");
                Assert.That(after.materialLedger.solventWaterKg,
                    Is.EqualTo(before.materialLedger.solventWaterKg).Within(1e-12));
                Assert.That(after.materialLedger.chlorideMol,
                    Is.EqualTo(before.materialLedger.chlorideMol).Within(1e-12));
                Assert.That(after.materialLedger.researchAdditiveVolumeM3,
                    Is.EqualTo(before.materialLedger.researchAdditiveVolumeM3).Within(1e-12));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Serializable] private sealed class LedgerSnapshot
        {
            public Vessel[] vessels;
            public Sink[] sinks;
            public Material materialLedger;
        }
        [Serializable] private sealed class Vessel { public string id; public Material inventory; }
        [Serializable] private sealed class Sink { public string id; public Material inventory; }
        [Serializable] private sealed class Material
        {
            public double solventWaterKg;
            public double chlorideMol;
            public double researchAdditiveVolumeM3;
        }
    }
}
