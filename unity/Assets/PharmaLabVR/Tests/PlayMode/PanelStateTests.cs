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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PharmaLabVR.Input;
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
            var selectedDriver = roots.SelectMany(root =>
                root.GetComponentsInChildren<CoreDriver>(true)).Single();
            var inspectedStatus = presenter.GetComponentsInChildren<Text>(true)
                .Single(label => label.name == "Panel detail").text;
            Assert.That(selectedDriver.Measurement, Is.Not.Null,
                "The XR panel needs the actual Native-backed MeasurementView.");
            StringAssert.Contains(selectedDriver.Measurement.Availability.ToString(), inspectedStatus,
                "XR must show the scientific availability, not only a cached freshness tag.");
            if (selectedDriver.Measurement.Availability != MeasurementAvailability.Current)
                StringAssert.Contains("pH receiver: --", inspectedStatus,
                    "XR unavailable/failed/stale observations must not display a cached pH.");
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
            var csvButton = presenter.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Export results CSV");
            Assert.That(csvButton.gameObject.activeInHierarchy, Is.True);
            csvButton.onClick.Invoke();
            Assert.That(sessions.LastCsvExportPath, Is.Not.Null.And.Not.Empty,
                sessions.LastError);
            var csvBytes = File.ReadAllText(sessions.LastCsvExportPath);
            StringAssert.StartsWith("schemaVersion,recordKind,branchId,", csvBytes);
            StringAssert.Contains(driver.Session.BranchId, csvBytes);
            Assert.That(sessions.LastDurableSavePath, Is.Null);
            File.Delete(sessions.LastCsvExportPath);
            if (File.Exists(sessions.LastCsvExportPath + ".bak"))
                File.Delete(sessions.LastCsvExportPath + ".bak");
            // P11.03: XR controller UI follows Review -> Notebook and exports an
            // immutable Native snapshot + exact branch notebook, without Save.
            var notebook = presenter.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Open notebook");
            Assert.That(notebook.gameObject.activeInHierarchy, Is.True);
            notebook.onClick.Invoke();
            Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Notebook));
            var exportArchive = presenter.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Export archive");
            Assert.That(exportArchive.gameObject.activeInHierarchy, Is.True);
            var nativeBeforeArchive = driver.CurrentLabView.EventSequence;
            exportArchive.onClick.Invoke();
            Assert.That(sessions.LastPortableArchivePath, Is.Not.Null.And.Not.Empty,
                sessions.LastError);
            var exported = PortableSessionArchive.Read(sessions.LastPortableArchivePath);
            Assert.That(exported.BranchId, Is.EqualTo(driver.Session.BranchId));
            Assert.That(exported.EventSequence, Is.EqualTo(nativeBeforeArchive));
            Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(nativeBeforeArchive));
            Assert.That(sessions.LastDurableSavePath, Is.Null,
                "XR archive export must not misreport ordinary Native Save completion.");
            if (File.Exists(sessions.LastPortableArchivePath))
                File.Delete(sessions.LastPortableArchivePath);
            if (File.Exists(sessions.LastPortableArchivePath + ".bak"))
                File.Delete(sessions.LastPortableArchivePath + ".bak");
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

        [UnityTest]
        public IEnumerator P06_02_PausedReviewReturnResumesNativeBeforeReportingLab()
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
                Assert.That(panels.Request(PanelAction.Pause), Is.True);
                Assert.That(driver.IsNativePaused, Is.True);
                Assert.That(panels.Request(PanelAction.Review), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Review));
                var before = driver.CurrentLabView.EventSequence;
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(driver.IsNativePaused, Is.False,
                    "Returning to an operating Lab from Review must issue the native Continue.");
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(before),
                    "Panel navigation may not add native material events.");
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        // Authored for final owner's Unity campaign: this exercises real
        // Lab scene CoreDriver and panel routing, not the .NET test stub.
        [UnityTest]
        public IEnumerator P06_02_DirectRouterShowNeverBypassesRealNativePause()
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
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(panels.Request(PanelAction.Pause), Is.True);
                router.Show(ScreenId.Lab); // Intentionally bypass controller.
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause));
                Assert.That(driver.IsNativePaused, Is.True,
                    "Presentation cannot invent a Native Continue receipt.");

                router.Show(ScreenId.Settings);
                router.Show(ScreenId.Lab);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(panels.Request(PanelAction.Back), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause));
                Assert.That(driver.IsNativePaused, Is.True);
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(driver.IsNativePaused, Is.False);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab));

                router.Show(ScreenId.Settings);
                Assert.That(driver.PauseSession(), Is.True);
                Assert.That(router.ReturnFromSettings(), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause),
                    "A Native pause arriving during Settings must not revive Lab.");
                Assert.That(driver.IsNativePaused, Is.True);
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_SettingsBackRestoresNativePausedScreenWithoutContinuing()
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
                Assert.That(panels.Request(PanelAction.Pause), Is.True);
                Assert.That(panels.Request(PanelAction.Settings), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(panels.Request(PanelAction.Back), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause));
                Assert.That(driver.IsNativePaused, Is.True);
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(driver.IsNativePaused, Is.False);
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_VrSettingsControlsMutateRealSharedPanelPreferences()
        {
            var original = SetupPreferencesSelection.Current;
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
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                var presenter = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SpatialPanelPresenter>(true)).Single();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var actions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                Assert.That(actions.Request(PanelAction.Pause), Is.True);
                Assert.That(actions.Request(PanelAction.Settings), Is.True);
                var settingButtons = presenter.GetComponentsInChildren<Button>(true);
                Button Find(string name) => settingButtons.Single(button => button.name == name);
                Assert.That(Find("Settings choice A").gameObject.activeInHierarchy, Is.True);
                var initialPosture = router.Current.Setup.Posture;
                Find("Settings choice A").onClick.Invoke();
                Assert.That(router.Current.Setup.Posture, Is.Not.EqualTo(initialPosture));
                var initialHand = router.Current.Setup.DominantHand;
                Find("Settings choice B").onClick.Invoke();
                Assert.That(router.Current.Setup.DominantHand, Is.Not.EqualTo(initialHand));
                Find("Next settings").onClick.Invoke();
                var startingScale = router.Current.Setup.TextScale;
                Find("Settings choice C").onClick.Invoke();
                Assert.That(router.Current.Setup.TextScale, Is.GreaterThan(startingScale));
                Find("Next settings").onClick.Invoke();
                var startingContrast = router.Current.Setup.HighContrast;
                Find("Settings choice C").onClick.Invoke();
                Assert.That(router.Current.Setup.HighContrast, Is.Not.EqualTo(startingContrast));
                Find("Back").onClick.Invoke();
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause));
                Assert.That(router.Current.Setup.HighContrast, Is.Not.EqualTo(original.HighContrast));
            }
            finally
            {
                SetupPreferencesSelection.Select(original);
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    Object.DestroyImmediate(root);
                if (scene.isLoaded)
                    foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P10_01_EscapeFromDesktopLabRequestsActualNativePauseAndSharedPanel()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var input = roots.SelectMany(root => root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                input.SampleInputs(1_000_000_000UL);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause),
                    "Esc must dispatch through the panel route rather than disabling input.");
                Assert.That(driver.IsNativePaused, Is.True);
                Assert.That(input.AwaitingFocusContinue, Is.False,
                    "Esc is a routine native Pause, not an unrecoverable focus-loss hold.");
                Assert.That(roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single()
                    .Request(PanelAction.Lab), Is.True);
                Assert.That(driver.IsNativePaused, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator P07_03_VrSnapControlsHaveSeparateButtonAndLabelHitRegions()
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
                var presenter = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpatialPanelPresenter>(true)).Single();
                var buttons = presenter.GetComponentsInChildren<Button>(true);
                var left = buttons.Single(button => button.name == "Snap left 30")
                    .GetComponent<RectTransform>();
                var right = buttons.Single(button => button.name == "Snap right 30")
                    .GetComponent<RectTransform>();
                var leftLabel = left.GetComponentInChildren<Text>(true).rectTransform;
                var rightLabel = right.GetComponentInChildren<Text>(true).rectTransform;
                Assert.That(left.anchoredPosition.y, Is.EqualTo(right.anchoredPosition.y));
                Assert.That(left.anchoredPosition.x + left.sizeDelta.x * 0.5f,
                    Is.LessThan(right.anchoredPosition.x - right.sizeDelta.x * 0.5f),
                    "Tracked-ray controls cannot share an overlapping pointer hit rectangle.");
                Assert.That(leftLabel.sizeDelta.x, Is.LessThanOrEqualTo(left.sizeDelta.x));
                Assert.That(rightLabel.sizeDelta.x, Is.LessThanOrEqualTo(right.sizeDelta.x));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }


        [UnityTest]
        public IEnumerator P07_03_CrossModeNativeImportRetainsOriginalSessionOnFailure()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            CoreSession imported = null;
            try
            {
                var driver = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var original = driver.Session;
                var exportedDesktop = original.ExportSession();
                Assert.That(driver.BeginModeChange(ApplicationMode.VirtualReality), Is.True);
                var beforeRevision = driver.CurrentLabView.EventSequence;
                imported = CoreSession.ImportSession(exportedDesktop, driver.ScientificDatabasePath);
                Assert.That(imported.IsOpen, Is.True);
                Assert.Throws<System.InvalidOperationException>(() => driver.ReplaceSession(imported));
                Assert.That(driver.Session, Is.SameAs(original),
                    "Denied cross-mode replacement must retain the original Native handle.");
                Assert.That(original.IsOpen, Is.True);
                Assert.That(imported.IsOpen, Is.True,
                    "Failed replacement must not silently dispose a caller-owned candidate.");
                Assert.That(driver.CurrentMode, Is.EqualTo("VR"));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(beforeRevision),
                    "Denied load must not mutate material revision.");
                Assert.That(driver.IsTimeHeld, Is.True);
            }
            finally
            {
                imported?.Dispose();
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P07_03_DesktopNativeImportEntersSessionLoadHoldWithRevisionPreserved()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            CoreSession imported = null;
            try
            {
                var driver = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
                var original = driver.Session;
                var revision = driver.CurrentLabView.EventSequence;
                imported = CoreSession.ImportSession(original.ExportSession(), driver.ScientificDatabasePath);
                driver.ReplaceSession(imported);
                imported = null;
                Assert.That(original.IsOpen, Is.False);
                Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(driver.NativeRecoveryReady, Is.False);
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(revision));
                Assert.That(driver.IsNativePaused, Is.True);
            }
            finally
            {
                imported?.Dispose();
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P07_03_VrPauseLoadSavedSessionEntersNativeSessionLoadHoldWithoutSavingAgain()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var createdSave = (string)null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var session = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var actions = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var dock = roots.SelectMany(root => root.GetComponentsInChildren<PharmaLabVR.Tools.ToolMount>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                session.Configure(driver, "p07-vr-load-" + System.Guid.NewGuid().ToString("N"));
                Assert.That(driver.BeginModeChange(ApplicationMode.VirtualReality), Is.True);
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(dock.TryMount(), Is.True);
                var revision = driver.CurrentLabView.EventSequence;
                Assert.That(session.Save(), Is.True, session.LastError);
                createdSave = session.LastDurableSavePath;
                var replaced = driver.Session;
                Assert.That(actions.Request(PanelAction.Load), Is.True, session.LastError);
                Assert.That(replaced.IsOpen, Is.False);
                Assert.That(driver.Session, Is.Not.SameAs(replaced));
                Assert.That(driver.CurrentMode, Is.EqualTo("VR"));
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(driver.NativeRecoveryReady, Is.False);
                Assert.That(dock.IsMounted, Is.True);
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(revision),
                    "Explicit Load must not create material or observation revisions.");
                Assert.That(session.RecordedReview, Is.Null,
                    "Load cannot implicitly open or recompute Review.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(createdSave) && File.Exists(createdSave))
                    File.Delete(createdSave);
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_DesktopPauseLoadSavedSessionIsExplicitAndNativeBacked()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            string savedPath = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var session = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var actions = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
                session.Configure(driver, "p06-desktop-load-" + System.Guid.NewGuid().ToString("N"));
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab));
                Assert.That(router.Current.SupportedActions, Does.Not.Contain("Load"));
                Assert.That(actions.Request(PanelAction.Load), Is.False,
                    "A running Desktop laboratory must not replace its Native session.");
                Assert.That(actions.Request(PanelAction.Pause), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause));
                Assert.That(router.Current.SupportedActions, Does.Contain("Load"));
                Assert.That(actions.Request(PanelAction.Load), Is.False,
                    "A missing save must not replace the Native session.");
                Assert.That(session.Save(), Is.True, session.LastError);
                savedPath = session.LastDurableSavePath;
                var previous = driver.Session;
                var revision = driver.CurrentLabView.EventSequence;
                Assert.That(actions.Request(PanelAction.Load), Is.True, session.LastError);
                Assert.That(previous.IsOpen, Is.False);
                Assert.That(driver.Session, Is.Not.SameAs(previous));
                Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(revision));
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(driver.NativeRecoveryReady, Is.False);
                Assert.That(session.RecordedReview, Is.Null);

                // Repeat an explicit Save to establish the confirmed .bak.
                // If an interrupted write loses the primary, the Pause/Hold
                // Load action must still admit the surviving Native backup.
                Assert.That(session.Save(), Is.True, session.LastError);
                Assert.That(session.Save(), Is.True, session.LastError);
                Assert.That(File.Exists(savedPath + ".bak"), Is.True);
                File.Delete(savedPath);
                Assert.That(session.CanLoadSavedSession, Is.True,
                    "A confirmed backup alone must remain discoverable.");
                var nextPrevious = driver.Session;
                Assert.That(actions.Request(PanelAction.Load), Is.True, session.LastError);
                Assert.That(nextPrevious.IsOpen, Is.False);
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(revision));
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(session.RecordedReview, Is.Null);
            }
            finally
            {
                if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath + ".bak"))
                    File.Delete(savedPath + ".bak");
                if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
                    File.Delete(savedPath);
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P11_02_DesktopNotebookRecordsNativeEventWithoutMaterialRevision()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            string notesPath = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var session = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var actions = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var name = "p11-note-" + System.Guid.NewGuid().ToString("N");
                session.Configure(driver, name);
                notesPath = Path.Combine(Application.persistentDataPath, "sessions", name + ".plv-notes");
                var revision = driver.CurrentLabView.EventSequence;
                var original = driver.Session;
                Assert.That(actions.Request(PanelAction.AddNotebookNote, "Not yet in notebook"), Is.False);
                Assert.That(actions.Request(PanelAction.Notebook), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Notebook));
                Assert.That(actions.Request(PanelAction.AddNotebookNote,
                    "Prediction: prepare the receiver without changing the chemistry."), Is.True, session.LastError);
                Assert.That(File.Exists(notesPath), Is.True);
                var entries = session.ReadNotebookEntries();
                Assert.That(entries.Count, Is.EqualTo(1));
                Assert.That(entries[0].EventSequence, Is.EqualTo(revision));
                Assert.That(entries[0].BranchId, Is.EqualTo(driver.Session.BranchId));
                Assert.That(actions.Request(PanelAction.ExportPortable), Is.True, session.LastError);
                Assert.That(session.LastPortableArchivePath, Is.Not.Null.And.Not.Empty);
                var archive = PortableSessionArchive.Read(session.LastPortableArchivePath);
                Assert.That(archive.BranchId, Is.EqualTo(driver.Session.BranchId));
                Assert.That(archive.EventSequence, Is.EqualTo(revision));
                Assert.That(archive.NotebookSnapshot, Does.Contain("PLV-NOTEBOOK-V1"));
                Assert.That(archive.NativeJson, Does.Contain("\"branchId\""));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(revision));
                Assert.That(session.LastDurableSavePath, Is.Null,
                    "Portable archive export must not silently mark Native Save completed.");
                Assert.That(actions.Request(PanelAction.AddNotebookNote, "\u0639"), Is.False,
                    "English-only notebook should not accept Arabic-script entries.");
                var rejectedMessage = session.LastError;
                Assert.That(rejectedMessage, Is.Not.Null.And.Not.Empty);
                Assert.That(session.ReadNotebookEntries().Count, Is.EqualTo(1));
                Assert.That(session.LastError, Is.EqualTo(rejectedMessage),
                    "Reading the notebook hid a failed write before UI could present it.");
                Assert.That(driver.Session, Is.SameAs(original));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(revision));
                Assert.That(session.LastDurableSavePath, Is.Null);
                Assert.That(actions.Request(PanelAction.Lab), Is.True);
                // P11.03 two-run recorded comparison uses the real Native
                // durable checkpoint and current, unmutated Native Review.
                Assert.That(session.Save(), Is.True, session.LastError);
                Assert.That(actions.Request(PanelAction.Review), Is.True, session.LastError);
                Assert.That(actions.Request(PanelAction.CompareSaved), Is.True, session.LastError);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Compare));
                Assert.That(session.CurrentComparison, Is.Not.Null);
                Assert.That(session.CurrentComparison.BaselineBranchId, Is.EqualTo(original.BranchId));
                Assert.That(session.CurrentComparison.CandidateBranchId, Is.EqualTo(original.BranchId));
                Assert.That(driver.Session, Is.SameAs(original));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(revision));
                Assert.That(actions.Request(PanelAction.Review), Is.True);
                Assert.That(actions.Request(PanelAction.Lab), Is.True);
            }
            finally
            {
                if (notesPath != null && File.Exists(notesPath)) File.Delete(notesPath);
                if (notesPath != null && File.Exists(notesPath + ".bak")) File.Delete(notesPath + ".bak");
                if (notesPath != null)
                {
                    var archivePath = Path.Combine(Path.GetDirectoryName(notesPath),
                        Path.GetFileNameWithoutExtension(notesPath) + ".plvpack");
                    if (File.Exists(archivePath)) File.Delete(archivePath);
                    if (File.Exists(archivePath + ".bak")) File.Delete(archivePath + ".bak");
                    var checkpoint = Path.Combine(Path.GetDirectoryName(notesPath),
                        Path.GetFileNameWithoutExtension(notesPath) + ".plv.json");
                    if (File.Exists(checkpoint)) File.Delete(checkpoint);
                    if (File.Exists(checkpoint + ".bak")) File.Delete(checkpoint + ".bak");
                }
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

    }
}
