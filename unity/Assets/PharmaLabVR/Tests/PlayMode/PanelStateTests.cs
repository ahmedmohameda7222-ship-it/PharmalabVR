using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using PharmaLabVR.Core;
using PharmaLabVR.Session;
using PharmaLabVR.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace PharmaLabVR.Tests.PlayMode
{
    public sealed class PanelStateTests
    {
        [UnityTest]
        public IEnumerator P06_02_ColdSessionFirstFrameEstablishesClockBaseline()
        {
            var root = new GameObject("ColdSessionClockBaseline");
            try
            {
                var driver = root.AddComponent<CoreDriver>();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Thread.Sleep(120);
                yield return null;
                Assert.That(driver.IsTimeHeld, Is.False, driver.Session.ReadSnapshot());
                Thread.Sleep(120);
                yield return null;
                Assert.That(driver.IsTimeHeld, Is.False,
                    "A paused setup session has no transport backlog to catch up.");
                Assert.That(driver.ContinueSession(), Is.True);
                yield return null;
                Thread.Sleep(120);
                yield return null;
                Assert.That(driver.IsTimeHeld, Is.True,
                    "A later unpaused transport backlog must still enter TimeDiscontinuity hold.");
                StringAssert.Contains("\"reason\":\"TimeDiscontinuity\"", driver.Session.ReadSnapshot());
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_LiveLabRoutesNativeHoldWithoutOfferingAFalseContinue()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var panels = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                sessions.Configure(driver, "playmode-panel-" + System.Guid.NewGuid().ToString("N"));
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab));
                Assert.That(panels.Request(PanelAction.Inspect, "receiver"), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Inspect));
                Assert.That(router.Current.SelectedEntityId, Is.EqualTo("receiver"));
                Assert.That(router.Current.NativeRevision, Is.EqualTo(driver.CurrentLabView.EventSequence));
                var receiverObservation = driver.CurrentLabView.Observations.Single(item =>
                    item.VesselId == "receiver" && item.ObservableId == "pH");
                Assert.That(router.Current.ModelIdentity, Is.EqualTo(receiverObservation.ModelId));
                Assert.That(router.Current.ObservationFreshness, Is.EqualTo(receiverObservation.Freshness));
                Assert.That(panels.Request(PanelAction.Save), Is.True, sessions.LastError);
                Assert.That(router.Current.SaveState, Is.EqualTo("Saved"));
                Assert.That(router.Current.Dirty, Is.False);
                Assert.That(File.Exists(sessions.LastDurableSavePath), Is.True);
                File.Delete(sessions.LastDurableSavePath);
                Assert.That(panels.Request(PanelAction.Pause), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause));
                StringAssert.Contains("\"paused\":true", driver.Session.ReadSnapshot());
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                StringAssert.Contains("\"paused\":false", driver.Session.ReadSnapshot());
                Assert.That(driver.BeginInputHold("FocusLoss"), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
                Assert.That(router.Current.HoldReason, Is.EqualTo("FocusLoss"));
                Assert.That(router.Current.SupportedActions, Does.Not.Contain("Continue"));
                Assert.That(panels.Request(PanelAction.Measure, "receiver"), Is.False);
                Assert.That(panels.Request(PanelAction.Continue), Is.False);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
    }
}
