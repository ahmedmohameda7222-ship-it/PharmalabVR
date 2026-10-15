using System.Linq;
using NUnit.Framework;
using PharmaLabVR.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PharmaLabVR.Tests
{
    // Requires BuildPipelineEntry.AllAssets before the deferred Editor campaign.
    // These tests are authored only: NOT_RUN at the source SHA.
    public sealed class ResearchInstrumentAssetTests
    {
        [Test]
        public void P03_02_ResearchBottleAndPipetteAreDistinctPersistentAssets()
        {
            var pipette = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchPipette.prefab");
            var bottle = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PharmaLabVR/Prefabs/Tools/ResearchStockBottle.prefab");
            Assert.That(pipette, Is.Not.Null, "AllAssets must generate a separate pipette.");
            Assert.That(bottle, Is.Not.Null, "AllAssets must generate a separate stock bottle.");
            Assert.That(pipette.GetComponent<PipetteActuator>(), Is.Not.Null);
            Assert.That(bottle.GetComponent<BottlePourActuator>(), Is.Not.Null);
            Assert.That(pipette.GetComponent<ValveActuator>(), Is.Null,
                "Research pipette must not masquerade as burette valve.");
            Assert.That(bottle.GetComponent<ValveActuator>(), Is.Null,
                "Stock bottle measured pour cannot be actuated with the burette valve.");
            Assert.That(pipette.transform.Find("GraduatedGlassTube"), Is.Not.Null);
            Assert.That(pipette.transform.Find("MeteredOutletTip"), Is.Not.Null);
            Assert.That(bottle.transform.Find("StockBottleBody"), Is.Not.Null);
            Assert.That(bottle.transform.Find("100mL_Research_NotCalibrated"), Is.Not.Null);
            Assert.That(pipette.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(20));
            Assert.That(bottle.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void P03_02_GeneratedLabPlacesOneOfEachResearchInstrumentWithoutReplacingBurette()
        {
            var scene = EditorSceneManager.OpenScene("Assets/PharmaLabVR/Scenes/Lab.unity",
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var pipettes = roots.SelectMany(root => root.GetComponentsInChildren<PipetteActuator>(true)).ToArray();
                var bottles = roots.SelectMany(root => root.GetComponentsInChildren<BottlePourActuator>(true)).ToArray();
                Assert.That(pipettes.Length, Is.EqualTo(1));
                Assert.That(bottles.Length, Is.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Any(item => item.name == "DesktopResearchTool"), Is.True,
                    "Existing 50 mL burette must retain its own scene route.");
                Assert.That(pipettes[0].transform.position, Is.Not.EqualTo(bottles[0].transform.position));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
