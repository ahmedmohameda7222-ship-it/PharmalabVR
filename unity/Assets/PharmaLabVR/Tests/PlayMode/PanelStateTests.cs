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
        public IEnumerator M2_XRLongResearchReadoutsStayWithinScrollableViewport()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab",
                new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var presenter = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpatialPanelPresenter>(true))
                    .Single();
                var scroller = presenter.GetComponentInChildren<ScrollRect>(true);
                Assert.That(scroller, Is.Not.Null,
                    "XR scientific and instructor detail must be scrollable.");
                Assert.That(scroller.horizontal, Is.False);
                Assert.That(scroller.vertical, Is.True);
                Assert.That(scroller.viewport, Is.Not.Null);
                Assert.That(scroller.viewport.GetComponent<RectMask2D>(), Is.Not.Null,
                    "Long explanation text must be clipped above the working ray targets.");
                Assert.That(scroller.content, Is.Not.Null);
                var message = scroller.content.GetComponent<Text>();
                Assert.That(message, Is.Not.Null);
                Assert.That(message.name, Is.EqualTo("Panel detail"));
                Assert.That(message.raycastTarget, Is.False);
                Assert.That(message.GetComponent<ContentSizeFitter>().verticalFit,
                    Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
                var viewportRect = scroller.GetComponent<RectTransform>();
                Assert.That(viewportRect.anchoredPosition.y - viewportRect.sizeDelta.y / 2f,
                    Is.GreaterThan(53f),
                    "Readout scroller must end above the first instrument hit rectangle.");
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        [UnityTest]
        public IEnumerator M2_XRScientificTextMustNotStealRayClicksFromInstrumentButtons()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab",
                new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var panel = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpatialPanelPresenter>(true))
                    .Single();
                var content = panel.GetComponentsInChildren<Text>(true);
                Assert.That(content.Length, Is.GreaterThan(6));
                Assert.That(content.All(text => !text.raycastTarget), Is.True,
                    "Readouts and button labels cannot intercept direct XR UI raycasts.");
                var clicks = panel.GetComponentsInChildren<Button>(true);
                Assert.That(clicks.Length, Is.GreaterThan(8));
                Assert.That(clicks.All(button =>
                    button.GetComponent<Image>() != null &&
                    button.GetComponent<Image>().raycastTarget), Is.True,
                    "Only the actual Button Image should own each XR interaction.");
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        [UnityTest]
        public IEnumerator M2_XRResearchInstrumentPagesShowDistinctVisibleActions()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab",
                new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var panel = roots.SelectMany(root => root.GetComponentsInChildren<SpatialPanelPresenter>(true)).Single();
                var buttons = panel.GetComponentsInChildren<Button>(true)
                    .ToDictionary(button => button.name);
                Assert.That(buttons["Research instruments"].gameObject.activeSelf, Is.True);
                buttons["Research instruments"].onClick.Invoke();
                Assert.That(buttons["Stock setup"].gameObject.activeSelf, Is.True);
                buttons["Stock setup"].onClick.Invoke();
                Assert.That(buttons["More tools and lessons"].gameObject.activeSelf, Is.True);
                Assert.That(buttons["Choose concentration"].gameObject.activeSelf, Is.True);
                Assert.That(buttons["Settings"].gameObject.activeSelf, Is.False,
                    "Initial stock setup settings should not overlap a science control.");
                buttons["More tools and lessons"].onClick.Invoke();
                Assert.That(buttons["Prepare reserve stock"].gameObject.activeSelf, Is.True);
                Assert.That(buttons["Strong acid lesson"].gameObject.activeSelf, Is.True);
                Assert.That(buttons["Mixed acid lesson"].gameObject.activeSelf, Is.True);
                var shown = buttons.Values.Where(button => button.gameObject.activeSelf).ToArray();
                var rows = shown.Select(button =>
                    button.GetComponent<RectTransform>().anchoredPosition.y).ToArray();
                Assert.That(rows.Distinct().Count(), Is.EqualTo(rows.Length),
                    "All visible XR controls need separate ray hit targets.");
                buttons["Strong acid lesson"].onClick.Invoke();
                Assert.That(buttons["Predict pH below 7"].gameObject.activeSelf, Is.True);
                Assert.That(buttons["Predict pH at least 7"].gameObject.activeSelf, Is.True);
                Assert.That(buttons["More tools and lessons"].gameObject.activeSelf, Is.False);
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var actions = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                Assert.That(actions.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True, "Native first-use stock preparation is required for notebook access.");
                Assert.That(buttons["Open notebook"].gameObject.activeSelf, Is.True,
                    "Ready student lesson panel must expose the real Notebook.");
                buttons["Open notebook"].onClick.Invoke();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Notebook));
                Assert.That(driver.Session, Is.Not.Null);
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        [UnityTest]
        public IEnumerator M1_FirstNativeSetupLetsStudentChooseLessonAndPrepareStock()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab",
                new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var panel = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.SourceNeedsPreparation, Is.True);
                Assert.That(driver.IsNativePaused, Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab),
                    "Initial Native stock setup must be accessible, not hidden behind Pause.");
                Assert.That(router.Current.SupportedActions, Does.Contain("PrepareResearchStock"));
                Assert.That(router.Current.SupportedActions, Does.Not.Contain("FillBurette"));
                Assert.That(panel.Request(PanelAction.SelectResearchLesson,
                    "strong-acid-titration-research"), Is.True);
                Assert.That(panel.HasResearchLesson, Is.True);
                Assert.That(panel.Request(PanelAction.PrepareResearchStock, "hcl", 0.05), Is.True);
                Assert.That(driver.IsNativePaused, Is.False);
                Assert.That(driver.SourceNeedsPreparation, Is.False);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab));
                Assert.That(driver.CurrentLabView.Vessels.Single(item => item.Id == "source")
                    .CommittedVolumeM3, Is.GreaterThan(0d));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
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

                // Pause independently of PanelAction.Pause, then allow a
                // Native-view refresh to reconcile screen before any live UI.
                Assert.That(driver.PauseSession(), Is.True);
                router.Configure(driver);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause));
                Assert.That(driver.IsNativePaused, Is.True);
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(driver.IsNativePaused, Is.False);

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

        // Authored only: requires real Unity Editor and pinned Native IPhreeqc.
        // This covers the SAME CoreDriver with a newly imported CoreSession,
        // not the replacement-MonoBehaviour test or the headless test stub.
        [UnityTest]
        public IEnumerator P06_02_NativeSessionImportClearsStaleSettingsAndSavedPanel()
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
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
                var original = driver.Session;
                Assert.That(router.Current, Is.Not.Null);
                router.Show(ScreenId.Inspect, "receiver");
                Assert.That(router.Current.SelectedEntityId, Is.EqualTo("receiver"));
                router.MarkSaved(); // Presentation fixture, NOT a disk Save.
                Assert.That(router.Current.SaveState, Is.EqualTo("Saved"));
                router.Show(ScreenId.Settings);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Settings));

                imported = CoreSession.ImportSession(original.ExportSession(),
                    driver.ScientificDatabasePath);
                var sawNativeRestoreSignal = false;
                var prematureContinue = false;
                var publishedBeforeRestoreView = false;
                System.Action captureBeforePublish = () =>
                {
                    sawNativeRestoreSignal = true;
                    prematureContinue = router.Current.SupportedActions.Contains("Continue");
                    publishedBeforeRestoreView = driver.NativePublishedRestorationEpoch == driver.NativeRestorationEpoch;
                };
                driver.NativeSessionRestored += captureBeforePublish;
                try { driver.ReplaceSession(imported); }
                finally { driver.NativeSessionRestored -= captureBeforePublish; }
                imported = null;
                Assert.That(sawNativeRestoreSignal, Is.True);
                Assert.That(prematureContinue, Is.False,
                    "NativeSessionRestored is before PublishSnapshot; stale recovery cannot offer Continue.");
                Assert.That(publishedBeforeRestoreView, Is.False,
                    "Restore event cannot claim a replacement Native snapshot was already published.");
                Assert.That(driver.NativePublishedRestorationEpoch,
                    Is.EqualTo(driver.NativeRestorationEpoch),
                    "A successful imported Native snapshot must advance the local published epoch.");
                Assert.That(driver.Session, Is.Not.SameAs(original));
                Assert.That(original.IsOpen, Is.False);
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(driver.NativeRecoveryReady, Is.False);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
                Assert.That(router.Current.SaveState, Is.EqualTo("Unsaved"));
                Assert.That(router.Current.Dirty, Is.True);
                Assert.That(router.Current.SelectedEntityId, Is.EqualTo("source"));
                Assert.That(router.ReturnFromSettings(), Is.False,
                    "Imported Native session must not reopen the old Settings history.");
                Assert.That(router.Current.SupportedActions, Does.Not.Contain("PipetteDispense"),
                    "Native SessionLoad Hold must not expose live chemical tools.");
            }
            finally
            {
                imported?.Dispose();
                foreach (var root in scene.GetRootGameObjects())
                    Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        // Deferred Unity PlayMode campaign: the panel is intentionally
        // disabled during real Native import. The restoration event cannot
        // arrive through its detached listener; the Native epoch must still
        // invalidate stale presentation on reactivation.
        [UnityTest]
        public IEnumerator P06_02_DisabledPanelReactivatesIntoNativeSessionLoadHold()
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
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
                var original = driver.Session;
                var previousEpoch = driver.NativeRestorationEpoch;
                router.Show(ScreenId.Inspect, "receiver");
                router.MarkSaved(); // UI fixture only; no file saved.
                router.Show(ScreenId.Settings);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Settings));
                router.enabled = false;
                imported = CoreSession.ImportSession(original.ExportSession(),
                    driver.ScientificDatabasePath);
                driver.ReplaceSession(imported);
                imported = null;
                Assert.That(driver.NativeRestorationEpoch, Is.EqualTo(previousEpoch + 1UL));
                Assert.That(driver.NativePublishedRestorationEpoch,
                    Is.EqualTo(driver.NativeRestorationEpoch),
                    "Disabled panel must reattach to the already published imported snapshot.");
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                router.enabled = true;
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
                Assert.That(router.Current.SaveState, Is.EqualTo("Unsaved"));
                Assert.That(router.Current.SelectedEntityId, Is.EqualTo("source"));
                Assert.That(router.Current.Dirty, Is.True);
                Assert.That(router.ReturnFromSettings(), Is.False);
                Assert.That(router.Current.SupportedActions,
                    Does.Not.Contain("PipetteDispense"));
                Assert.That(original.IsOpen, Is.False);
            }
            finally
            {
                imported?.Dispose();
                foreach (var root in scene.GetRootGameObjects())
                    Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        // NativeSessionRestored is synchronous. The router sees it, then
        // temporarily becomes inactive before the replacement snapshot
        // is published. OnEnable must use the published restoration epoch
        // rather than waiting forever for a LabViewChanged it missed.
        [UnityTest]
        public IEnumerator P06_02_DisabledAfterRestoreSignalRecoversPublishedNativeSnapshot()
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
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var previous = driver.Session;
                var originalEpoch = driver.NativeRestorationEpoch;
                var unpublishedGate = typeof(ScreenRouter).GetField(
                    "awaitingRestoredNativeSnapshot",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(unpublishedGate, Is.Not.Null,
                    "The presentation-only Native publication gate was not available.");
                imported = CoreSession.ImportSession(previous.ExportSession(),
                    driver.ScientificDatabasePath);
                var restoreSignalObserved = false;
                System.Action disableBetweenRestoreAndPublication = () =>
                {
                    restoreSignalObserved =
                        router.Current.ScreenId == ScreenId.Hold &&
                        router.Current.SaveState == "Unsaved" &&
                        (bool)unpublishedGate.GetValue(router);
                    router.enabled = false;
                };
                driver.NativeSessionRestored += disableBetweenRestoreAndPublication;
                try { driver.ReplaceSession(imported); }
                finally { driver.NativeSessionRestored -= disableBetweenRestoreAndPublication; }
                imported = null;
                Assert.That(restoreSignalObserved, Is.True,
                    "The restore notification must close UI access before snapshot publication.");
                Assert.That(router.enabled, Is.False);
                Assert.That(driver.NativeRestorationEpoch, Is.EqualTo(originalEpoch + 1UL));
                Assert.That(driver.NativePublishedRestorationEpoch,
                    Is.EqualTo(driver.NativeRestorationEpoch));
                Assert.That((bool)unpublishedGate.GetValue(router), Is.True,
                    "The detached panel should still be pending its missed publication.");
                router.enabled = true;
                Assert.That((bool)unpublishedGate.GetValue(router), Is.False,
                    "A published replacement snapshot must clear pending state on reactivation.");
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
                Assert.That(router.Current.SaveState, Is.EqualTo("Unsaved"));
                Assert.That(router.Current.Dirty, Is.True);
                Assert.That(router.Current.SupportedActions, Does.Not.Contain("PipetteDispense"));
                Assert.That(previous.IsOpen, Is.False);
            }
            finally
            {
                imported?.Dispose();
                foreach (var root in scene.GetRootGameObjects())
                    Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        // Authored for owner's final Unity campaign with a real Native
        // CoreSession and existing SaveService. No Unity execution in CI.
        [UnityTest]
        public IEnumerator P06_02_RestartCheckpointInvalidatesRecordedReviewAndComparison()
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
                var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                sessions.Configure(driver, "p06-restore-cache-" + System.Guid.NewGuid().ToString("N"));
                Assert.That(sessions.CreateCheckpoint(), Is.True, sessions.LastError);
                Assert.That(sessions.Save(), Is.True, sessions.LastError);
                savedPath = sessions.LastDurableSavePath;
                Assert.That(sessions.OpenReview(), Is.True, sessions.LastError);
                router.Show(ScreenId.Review);
                Assert.That(sessions.OpenSavedComparison(), Is.True, sessions.LastError);
                Assert.That(sessions.RecordedReview, Is.Not.Null);
                Assert.That(sessions.CurrentComparison, Is.Not.Null);
                var beforeEpoch = driver.NativeRestorationEpoch;
                Assert.That(sessions.RestartCheckpoint(), Is.True, sessions.LastError);
                Assert.That(driver.NativeRestorationEpoch, Is.EqualTo(beforeEpoch + 1UL));
                Assert.That(sessions.RecordedReview, Is.Null,
                    "Restarted checkpoint must never display the previous Native recorded review.");
                Assert.That(sessions.CurrentComparison, Is.Null,
                    "Restarted checkpoint must never display an obsolete saved-run comparison.");
                Assert.That(sessions.ComparisonSummary, Is.EqualTo("No recorded run comparison is open."));
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
                Assert.That(router.Current.SaveState, Is.EqualTo("Unsaved"));
                Assert.That(driver.IsTimeHeld, Is.True);
            }
            finally
            {
                if (savedPath != null && File.Exists(savedPath)) File.Delete(savedPath);
                if (savedPath != null && File.Exists(savedPath + ".bak")) File.Delete(savedPath + ".bak");
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_02_DisabledSessionControllerCannotExposePreImportReviewCache()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            CoreSession imported = null;
            string savedPath = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                sessions.Configure(driver, "p06-disabled-review-" + System.Guid.NewGuid().ToString("N"));
                Assert.That(sessions.Save(), Is.True, sessions.LastError);
                savedPath = sessions.LastDurableSavePath;
                Assert.That(sessions.OpenReview(), Is.True, sessions.LastError);
                router.Show(ScreenId.Review);
                Assert.That(sessions.OpenSavedComparison(), Is.True, sessions.LastError);
                var oldNative = driver.Session;
                var epoch = driver.NativeRestorationEpoch;
                sessions.enabled = false;
                imported = CoreSession.ImportSession(oldNative.ExportSession(),
                    driver.ScientificDatabasePath);
                driver.ReplaceSession(imported);
                imported = null;
                Assert.That(driver.NativeRestorationEpoch, Is.EqualTo(epoch + 1UL));
                Assert.That(sessions.RecordedReview, Is.Null,
                    "Reading the disabled controller must invalidate a missed restore epoch.");
                Assert.That(sessions.CurrentComparison, Is.Null);
                sessions.enabled = true;
                Assert.That(sessions.RecordedReview, Is.Null);
                Assert.That(sessions.CurrentComparison, Is.Null);
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
            }
            finally
            {
                imported?.Dispose();
                if (savedPath != null && File.Exists(savedPath)) File.Delete(savedPath);
                if (savedPath != null && File.Exists(savedPath + ".bak")) File.Delete(savedPath + ".bak");
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        // A saved baseline is durable evidence, separate from the
        // currently observed Native Review. A replacement save or changed
        // save-file context invalidates a cached comparison without editing
        // or recomputing any scientific observations.
        [UnityTest]
        public IEnumerator P06_02_NewDurableSaveInvalidatesOnlyPriorRecordedComparison()
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
                var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var uniqueName = "p06-compare-save-" + System.Guid.NewGuid().ToString("N");
                sessions.Configure(driver, uniqueName);
                var beforeRevision = driver.CurrentLabView.EventSequence;
                Assert.That(sessions.Save(), Is.True, sessions.LastError);
                savedPath = sessions.LastDurableSavePath;
                Assert.That(sessions.OpenReview(), Is.True, sessions.LastError);
                router.Show(ScreenId.Review);
                Assert.That(sessions.OpenSavedComparison(), Is.True, sessions.LastError);
                var activeReview = sessions.RecordedReview;
                Assert.That(activeReview, Is.Not.Null);
                Assert.That(sessions.CurrentComparison, Is.Not.Null);

                // A second durable Save replaces this baseline even if its
                // Native chemistry revision happens to be identical.
                Assert.That(sessions.Save(), Is.True, sessions.LastError);
                Assert.That(sessions.RecordedReview, Is.SameAs(activeReview));
                Assert.That(sessions.CurrentComparison, Is.Null);
                Assert.That(sessions.ComparisonSummary, Is.EqualTo("No recorded run comparison is open."));
                Assert.That(sessions.OpenSavedComparison(), Is.True, sessions.LastError);
                Assert.That(sessions.CurrentComparison, Is.Not.Null);

                // Switching saved-run identity on the same Native driver
                // must not keep comparing against a previous course file.
                sessions.Configure(driver, "p06-new-baseline-" + System.Guid.NewGuid().ToString("N"));
                Assert.That(sessions.RecordedReview, Is.SameAs(activeReview));
                Assert.That(sessions.CurrentComparison, Is.Null);
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(beforeRevision));
            }
            finally
            {
                if (savedPath != null && File.Exists(savedPath)) File.Delete(savedPath);
                if (savedPath != null && File.Exists(savedPath + ".bak")) File.Delete(savedPath + ".bak");
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
                var roots = scene.GetRootGameObjects();
                var desktop = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                var xr = roots.SelectMany(root =>
                    root.GetComponentsInChildren<XRInputAdapter>(true)).Single();
                var pipette = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopGrabbable>(true))
                    .Single(tool => tool.ToolId == "research-pipette-visual");
                var pipetteBody = pipette.GetComponent<Rigidbody>();
                var originallyKinematic = pipetteBody.isKinematic;
                var beginHold = typeof(DesktopInputAdapter).GetMethod("BeginHold",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(beginHold, Is.Not.Null);
                beginHold.Invoke(desktop, new object[] { pipette.transform });
                Assert.That(desktop.HasHeldTool, Is.True);
                var original = driver.Session;
                var revision = driver.CurrentLabView.EventSequence;
                Assert.That(driver.SelectMeasurementVessel("receiver"), Is.True);
                Assert.That(driver.SelectedMeasurementVesselId, Is.EqualTo("receiver"));
                imported = CoreSession.ImportSession(original.ExportSession(), driver.ScientificDatabasePath);
                driver.ReplaceSession(imported);
                imported = null;
                Assert.That(original.IsOpen, Is.False);
                Assert.That(driver.CurrentMode, Is.EqualTo("Desktop"));
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(driver.NativeRecoveryReady, Is.False);
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(revision));
                Assert.That(driver.SelectedMeasurementVesselId, Is.EqualTo("source"),
                    "Restored source screen must not retain the previous receiver's pH selection.");
                Assert.That(desktop.HasHeldTool, Is.False,
                    "Restoration must release the old Desktop hand-to-tool owner.");
                Assert.That(pipetteBody.isKinematic, Is.EqualTo(originallyKinematic),
                    "Restoring ownership must preserve the authored physical Rigidbody policy.");
                Assert.That(desktop.HasFreshPhysicalGestureSinceRestore, Is.False,
                    "An old Desktop press cannot be reused after Native import.");
                Assert.That(xr.HasFreshGripSinceRestore, Is.False,
                    "An old XR grip/trigger cannot be reused after Native import.");
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
                var desktop = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                var xr = roots.SelectMany(root =>
                    root.GetComponentsInChildren<XRInputAdapter>(true)).Single();
                Assert.That(desktop.HasFreshPhysicalGestureSinceRestore, Is.False);
                Assert.That(xr.HasFreshGripSinceRestore, Is.False);
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
            string exportedPackage = null;
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
                exportedPackage = session.LastPortableArchivePath;
                Assert.That(OfflineArchivePicker.ValidName(Path.GetFileName(exportedPackage)),
                    Is.True, "Student export must be discoverable in the offline package chooser.");
                var archive = PortableSessionArchive.Read(exportedPackage);
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
                    if (!string.IsNullOrEmpty(exportedPackage) && File.Exists(exportedPackage))
                        File.Delete(exportedPackage);
                    if (!string.IsNullOrEmpty(exportedPackage) && File.Exists(exportedPackage + ".bak"))
                        File.Delete(exportedPackage + ".bak");
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
