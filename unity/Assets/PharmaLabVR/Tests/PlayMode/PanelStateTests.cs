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
            var visibleButtons = presenter.GetComponentsInChildren<Button>()
                .Select(button => button.GetComponent<RectTransform>())
                .OrderByDescending(rect => rect.anchoredPosition.y).ToArray();
            for (var index = 1; index < visibleButtons.Length; index++)
            {
                var clearGap = visibleButtons[index - 1].anchoredPosition.y -
                    visibleButtons[index].anchoredPosition.y -
                    (visibleButtons[index - 1].rect.height + visibleButtons[index].rect.height) / 2f;
                Assert.That(clearGap, Is.GreaterThanOrEqualTo(5f),
                    "Adjacent world-panel buttons need a visible, hittable gap.");
            }
            var xrCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Single(camera => camera.name == "XR Camera");
            Assert.That(xrCamera.gameObject.activeInHierarchy, Is.True);
            var viewport = xrCamera.WorldToViewportPoint(canvas.transform.position);
            Assert.That(viewport.z, Is.GreaterThan(0f));
            Assert.That(viewport.x, Is.InRange(0.1f, 0.9f));
            Assert.That(viewport.y, Is.InRange(0.1f, 0.9f));
            Assert.That(Vector3.Dot(canvas.transform.right, xrCamera.transform.right),
                Is.GreaterThan(0.9f), "The spatial panel must read left-to-right from the XR camera.");
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
#if UNITY_EDITOR
                var renderPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../artifacts/test-results/vr-panel-render.ppm"));
                using (var writer = new BinaryWriter(File.Create(renderPath)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("P6\n512 512\n255\n"));
                    var colors = pixels.GetPixels32();
                    for (var y = 511; y >= 0; y--)
                        for (var x = 0; x < 512; x++)
                        {
                            var color = colors[y * 512 + x];
                            writer.Write(color.r);
                            writer.Write(color.g);
                            writer.Write(color.b);
                        }
                }
#endif
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
            var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
            var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
            sessions.Configure(driver, "playmode-vr-review-" + System.Guid.NewGuid().ToString("N"));
            var review = presenter.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Open review");
            Assert.That(review.gameObject.activeInHierarchy, Is.True);
            review.onClick.Invoke();
            Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Review));
            Assert.That(sessions.RecordedReview, Is.Not.Null);
            var reviewDetail = presenter.GetComponentsInChildren<Text>(true)
                .Single(label => label.name == "Panel detail");
            StringAssert.Contains("receiver", reviewDetail.text,
                "VR Review must show a recorded vessel, not just an observation count.");
            Assert.That(sessions.LastDurableSavePath, Is.Null,
                "Opening a recorded review must not silently overwrite a durable save.");
#if UNITY_EDITOR
            var reviewTarget = RenderTexture.GetTemporary(512, 512, 24);
            var originalTarget = xrCamera.targetTexture;
            var originalActive = RenderTexture.active;
            var reviewPixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            try
            {
                xrCamera.targetTexture = reviewTarget;
                xrCamera.Render();
                RenderTexture.active = reviewTarget;
                reviewPixels.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
                reviewPixels.Apply();
                var reviewPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../artifacts/test-results/vr-review-render.ppm"));
                using (var writer = new BinaryWriter(File.Create(reviewPath)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("P6\n512 512\n255\n"));
                    var colors = reviewPixels.GetPixels32();
                    for (var y = 511; y >= 0; y--)
                        for (var x = 0; x < 512; x++)
                        {
                            var color = colors[y * 512 + x];
                            writer.Write(color.r);
                            writer.Write(color.g);
                            writer.Write(color.b);
                        }
                }
            }
            finally
            {
                xrCamera.targetTexture = originalTarget;
                RenderTexture.active = originalActive;
                RenderTexture.ReleaseTemporary(reviewTarget);
                Object.DestroyImmediate(reviewPixels);
            }
#endif
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
                Assert.That(panels.Request(PanelAction.Review), Is.True, sessions.LastError);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Review));
                Assert.That(panels.Request(PanelAction.Lab), Is.False,
                    "A held session must not report a successful return to the operating Lab.");
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Review));
                File.Delete(sessions.LastDurableSavePath);
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_LiveLabReviewCapturesRecordedStateWithoutImplicitSaveOrNativeRevision()
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
                sessions.Configure(driver, "playmode-review-" + System.Guid.NewGuid().ToString("N"));
                var before = driver.CurrentLabView.EventSequence;
                Assert.That(panels.Request(PanelAction.Review), Is.True, sessions.LastError);
                Assert.That(sessions.LastDurableSavePath, Is.Null);
                Assert.That(router.Current.SaveState, Is.EqualTo("Unsaved"));
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Review));
                Assert.That(sessions.RecordedReview, Is.Not.Null);
                Assert.That(sessions.RecordedReview.ModelIdentity, Is.Not.Empty);
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(before));
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator P06_02_SetupPreferencesSurviveBackNavigationAndNativeLabSceneLoad()
        {
            var previous = SetupPreferencesSelection.Current;
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Boot.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Boot", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var router = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var panels = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                Assert.That(panels.Request(PanelAction.EnterDesktop), Is.True);
                var selected = new LabSetupPreferences("Seated", "Left", 0.74f, 1.2f,
                    captionsEnabled: true, reducedMotion: true, highContrast: true);
                router.UpdateSetupPreferences(selected);
                Assert.That(router.Current.Setup.Posture, Is.EqualTo("Seated"));
                Assert.That(router.Current.Setup.DominantHand, Is.EqualTo("Left"));
                Assert.That(router.Current.Setup.BenchHeightMeters, Is.EqualTo(0.74f).Within(0.0001f));
                Assert.That(router.Current.Setup.TextScale, Is.EqualTo(1.2f).Within(0.0001f));
                Assert.That(router.Current.Setup.CaptionsEnabled, Is.True);
                Assert.That(router.Current.Setup.ReducedMotion, Is.True);
                Assert.That(router.Current.Setup.HighContrast, Is.True);
                Assert.That(panels.Request(PanelAction.Home), Is.True);
                Assert.That(panels.Request(PanelAction.EnterDesktop), Is.True);
                Assert.That(router.Current.Setup, Is.SameAs(selected));
                Assert.That(panels.Request(PanelAction.ConfirmSetup), Is.True);
                yield return null;
                var labRoots = SceneManager.GetActiveScene().GetRootGameObjects();
                var labRouter = labRoots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var driver = labRoots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(labRouter.Current.Setup, Is.SameAs(selected));
                Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
                Assert.That(labRouter.Current.SaveState, Is.EqualTo("Unsaved"));
            }
            finally
            {
                SetupPreferencesSelection.Select(previous);
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    Object.DestroyImmediate(root);
                if (scene.isLoaded)
                    foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_LeftHandAndTextScaleAffectTheActualVrWorldPanel()
        {
            var previous = SetupPreferencesSelection.Current;
            SetupPreferencesSelection.Select(new LabSetupPreferences(
                "Standing", "Left", 1.00f, 1.2f, captionsEnabled: true,
                reducedMotion: true, highContrast: true));
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Boot.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Boot", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var panels = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                Assert.That(panels.Request(PanelAction.EnterVR), Is.True);
                Assert.That(panels.Request(PanelAction.ConfirmSetup), Is.True);
                yield return null;
                var labRoots = SceneManager.GetActiveScene().GetRootGameObjects();
                var presenter = labRoots.SelectMany(root =>
                    root.GetComponentsInChildren<SpatialPanelPresenter>(true)).Single();
                var camera = labRoots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .Single(item => item.name == "XR Camera");
                var canvas = presenter.GetComponentInChildren<Canvas>(true);
                Assert.That(canvas.gameObject.activeInHierarchy, Is.True);
                Assert.That(Vector3.Dot(canvas.transform.position - camera.transform.position,
                    camera.transform.right), Is.LessThan(0f),
                    "Left-hand setup must move the interactive world panel left of the head.");
                var title = presenter.GetComponentsInChildren<Text>(true)
                    .Single(item => item.name == "Panel title");
                Assert.That(title.fontSize, Is.EqualTo(31),
                    "Text scale must modify the rendered world-panel typography.");
                var router = labRoots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(router.Current.Setup.BenchHeightMeters, Is.EqualTo(1.00f).Within(0.0001f));
            }
            finally
            {
                SetupPreferencesSelection.Select(previous);
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    Object.DestroyImmediate(root);
                if (scene.isLoaded)
                    foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            }
            yield return null;
        }

    }
}
