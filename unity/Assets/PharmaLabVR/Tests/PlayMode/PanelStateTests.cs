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
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace PharmaLabVR.Tests.PlayMode
{
    public sealed class PanelStateTests
    {
        [UnityTest]
        public IEnumerator P06_02_BootModeChoiceRoutesThroughSharedSetupState()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Boot.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Boot", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var panels = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Home));
                Assert.That(panels.Request(PanelAction.EnterDesktop), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Setup));
                Assert.That(router.Current.Mode, Is.EqualTo("Desktop"));
                Assert.That(panels.Request(PanelAction.Home), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Home));
                Assert.That(panels.Request(PanelAction.EnterVR), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Setup));
                Assert.That(router.Current.Mode, Is.EqualTo("VR"));
                Assert.That(router.Current.SupportedActions, Does.Contain("ConfirmSetup"));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_SetupConfirmLoadsNativeBackedLabInSelectedMode()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Boot.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Boot", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var panels = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
            Assert.That(panels.Request(PanelAction.EnterDesktop), Is.True);
            Assert.That(panels.Request(PanelAction.ConfirmSetup), Is.True);
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Lab"));
            var labRoots = SceneManager.GetActiveScene().GetRootGameObjects();
            var driver = labRoots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
            Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
            Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
            Assert.That(driver.CurrentLabView, Is.Not.Null);
            foreach (var root in labRoots) Object.DestroyImmediate(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_VrLabHasOneActiveInteractionManager()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Boot.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Boot", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var panels = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
            Assert.That(panels.Request(PanelAction.EnterVR), Is.True);
            Assert.That(panels.Request(PanelAction.ConfirmSetup), Is.True);
            yield return null;
            var managers = Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None);
            Assert.That(managers.Length, Is.EqualTo(1),
                "A second auto-created manager can isolate the body from the controller interactors.");
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_VrPanelRoutesInspectThroughSharedNativeState()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Boot.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Boot", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var panels = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
            Assert.That(panels.Request(PanelAction.EnterVR), Is.True);
            Assert.That(panels.Request(PanelAction.ConfirmSetup), Is.True);
            yield return null;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var presenter = roots.SelectMany(root => root.GetComponentsInChildren<SpatialPanelPresenter>(true)).Single();
            var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
            var canvas = presenter.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(router.Current.Mode, Is.EqualTo("VR"));
            Assert.That(canvas.gameObject.activeInHierarchy, Is.True);
            var xrCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Single(camera => camera.name == "XR Camera");
            Assert.That(xrCamera.gameObject.activeInHierarchy, Is.True);
            var viewport = xrCamera.WorldToViewportPoint(canvas.transform.position);
            Assert.That(viewport.z, Is.GreaterThan(0f));
            Assert.That(viewport.x, Is.InRange(0.1f, 0.9f));
            Assert.That(viewport.y, Is.InRange(0.1f, 0.9f));
            var renderTarget = RenderTexture.GetTemporary(512, 512, 24);
            var oldTarget = xrCamera.targetTexture;
            var oldActive = RenderTexture.active;
            var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            try
            {
                xrCamera.targetTexture = renderTarget;
                xrCamera.Render();
                RenderTexture.active = renderTarget;
                pixels.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
                pixels.Apply();
                var sample = pixels.GetPixel(Mathf.RoundToInt(viewport.x * 511f),
                    Mathf.RoundToInt(viewport.y * 511f));
                Assert.That(sample.r, Is.GreaterThan(0.70f),
                    "The world panel must paint an opaque pale surface inside the XR camera view: " + sample);
            }
            finally
            {
                xrCamera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(renderTarget);
                Object.DestroyImmediate(pixels);
            }
            Assert.That(presenter.GetComponentInChildren<TrackedDeviceGraphicRaycaster>(true), Is.Not.Null);
            var eventSystem = roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
            Assert.That(eventSystem.GetComponents<BaseInputModule>().OfType<XRUIInputModule>().Count(), Is.EqualTo(1));
            var rays = roots.SelectMany(root => root.GetComponentsInChildren<XRRayInteractor>(true)).ToArray();
            Assert.That(rays.Length, Is.EqualTo(2));
            foreach (var ray in rays)
            {
                Assert.That(ray.enableUIInteraction, Is.True);
                Assert.That(ray.uiPressInput.inputSourceMode, Is.EqualTo(XRInputButtonReader.InputSourceMode.InputAction));
                StringAssert.Contains("triggerPressed", ray.uiPressInput.inputActionPerformed.bindings[0].path);
#pragma warning disable CS0618
                Assert.That(ray.inputCompatibilityMode,
                    Is.EqualTo(XRBaseInputInteractor.InputCompatibilityMode.ForceInputReaders));
#pragma warning restore CS0618
            }
            var inspect = presenter.GetComponentsInChildren<Button>(true).Single(button => button.name == "Inspect receiver");
            inspect.onClick.Invoke();
            Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Inspect));
            Assert.That(router.Current.SelectedEntityId, Is.EqualTo("receiver"));
            foreach (var root in roots) Object.DestroyImmediate(root);
            yield return null;
        }

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
