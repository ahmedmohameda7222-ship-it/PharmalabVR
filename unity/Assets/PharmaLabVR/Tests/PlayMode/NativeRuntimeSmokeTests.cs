using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;
using PharmaLabVR.Session;
using PharmaLabVR.Lab;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace PharmaLabVR.Tests.PlayMode
{
    public sealed class NativeRuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator M3_SessionLoadNeverChoosesContradictoryLearnerPrediction()
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
            string save = null;
            string notes = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var sessions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SessionController>(true)).Single();
                var panels = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var id = "ambiguous-" + System.Guid.NewGuid().ToString("N");
                sessions.Configure(driver, id);
                notes = Path.Combine(Application.persistentDataPath,
                    "sessions", id + ".plv-notes");
                Assert.That(panels.Request(PanelAction.SelectResearchLesson,
                    "strong-acid-titration-research"), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.RecordResearchPrediction,
                    "below-7"), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.PrepareResearchStock,
                    "hcl", 0.05), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Notebook), Is.True);
                Assert.That(panels.Request(PanelAction.AddNotebookNote,
                    "Student hypothesis for strong-acid-titration-research: receiver pH predicted at least 7. " +
                    "This is a learner prediction, not a Native result or certified endpoint."),
                    Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                var branch = driver.Session.BranchId;
                var eventSequence = driver.CurrentLabView.EventSequence;
                Assert.That(panels.Request(PanelAction.Save), Is.True, sessions.LastError);
                save = sessions.LastDurableSavePath;
                Assert.That(panels.Request(PanelAction.Pause), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Load), Is.True, sessions.LastError);
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(driver.Session.BranchId, Is.EqualTo(branch));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(eventSequence));
                Assert.That(sessions.HasResearchLesson, Is.False,
                    "Contradictory same-lesson hypotheses cannot restore guidance by list order.");
                Assert.That(sessions.LastError, Does.Contain("ambiguous"));
                Assert.That(sessions.ReadNotebookEntries().Count, Is.EqualTo(2),
                    "Ambiguous student Notebook evidence must remain available for review.");
            }
            finally
            {
                foreach (var file in new[] {
                    save, save == null ? null : save + ".bak",
                    notes, notes == null ? null : notes + ".bak" })
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                        File.Delete(file);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator M3_XRPortableStudyPageExportsRealLocalNativeBranchFromPause()
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
            string exported = null;
            string secondExport = null;
            string notesPath = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var sessions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SessionController>(true)).Single();
                var panels = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var spatial = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SpatialPanelPresenter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var id = "xr-portable-" + System.Guid.NewGuid().ToString("N");
                sessions.Configure(driver, id);
                notesPath = Path.Combine(Application.persistentDataPath,
                    "sessions", id + ".plv-notes");
                Assert.That(panels.Request(PanelAction.PrepareResearchStock,
                    "hcl", 0.05), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Notebook), Is.True);
                Assert.That(panels.Request(PanelAction.AddNotebookNote,
                    "I will recheck the Native receiver pH before deciding."),
                    Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                var recordedRevision = driver.CurrentLabView.EventSequence;
                var recordedBranch = driver.Session.BranchId;
                Assert.That(panels.Request(PanelAction.Pause), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause));

                var field = typeof(SpatialPanelPresenter).GetField("buttons",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                Assert.That(field, Is.Not.Null);
                var buttons = (System.Collections.Generic.Dictionary<
                    string, UnityEngine.UI.Button>)field.GetValue(spatial);
                Assert.That(buttons["Portable study restore"].gameObject.activeSelf, Is.True);
                buttons["Portable study restore"].onClick.Invoke();
                Assert.That(buttons["Export portable study"].gameObject.activeSelf, Is.True,
                    "The XR Pause package page must expose local Native+Notebook export.");
                buttons["Export portable study"].onClick.Invoke();
                exported = sessions.LastPortableArchivePath;
                Assert.That(exported, Is.Not.Null.And.Not.Empty, sessions.LastError);
                Assert.That(File.Exists(exported), Is.True);
                var data = PortableSessionArchive.Read(exported);
                Assert.That(data.BranchId, Is.EqualTo(recordedBranch));
                Assert.That(data.EventSequence, Is.EqualTo(recordedRevision));
                Assert.That(data.NotebookSnapshot, Does.Contain(
                    "I will recheck the Native receiver pH before deciding."));
                Assert.That(panels.LastPortableArchiveFileName,
                    Is.EqualTo(Path.GetFileName(exported)));
                var firstArchiveBytes = File.ReadAllBytes(exported);
                buttons["Export portable study"].onClick.Invoke();
                secondExport = sessions.LastPortableArchivePath;
                Assert.That(secondExport, Is.Not.Null.And.Not.EqualTo(exported),
                    "Second export must preserve the first student research artifact.");
                Assert.That(File.Exists(secondExport), Is.True);
                Assert.That(File.ReadAllBytes(exported), Is.EqualTo(firstArchiveBytes));
                Assert.That(PortableSessionArchive.Read(secondExport).BranchId,
                    Is.EqualTo(recordedBranch));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(recordedRevision),
                    "XR package export cannot issue an unauthorized material command.");
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause),
                    "Export should keep Native Pause until explicit student navigation.");
            }
            finally
            {
                foreach (var candidate in new[] {
                    exported, exported == null ? null : exported + ".bak",
                    secondExport, secondExport == null ? null : secondExport + ".bak",
                    notesPath, notesPath == null ? null : notesPath + ".bak" })
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                        File.Delete(candidate);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator M2_DesktopNotebookAndReviewSampleNeutralWithoutNewNativeMaterial()
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
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var panels = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var desktop = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(panels.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True);
                var nativeRevision = driver.CurrentLabView.EventSequence;
                Assert.That(ResearchLabScreenAdmission.MayActuate(
                    router.Current.ScreenId.ToString(), true), Is.True);
                Assert.That(panels.Request(PanelAction.Notebook), Is.True);
                Assert.That(ResearchLabScreenAdmission.MayActuate(
                    router.Current.ScreenId.ToString(), true), Is.False);
                var notebookSample = desktop.SampleInputs(
                    (ulong)(Time.realtimeSinceStartupAsDouble * 1000000000.0));
                Assert.That(notebookSample.Length, Is.EqualTo(1));
                Assert.That(notebookSample[0].actuator01, Is.EqualTo(0f));
                Assert.That(desktop.HasFreshLabScreenGesture, Is.False,
                    "Notebook must disown prior held mouse/G input until release.");
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(nativeRevision));
                Assert.That(panels.Request(PanelAction.Review), Is.True);
                var reviewSample = desktop.SampleInputs(
                    (ulong)(Time.realtimeSinceStartupAsDouble * 1000000000.0));
                Assert.That(reviewSample.Length, Is.EqualTo(1));
                Assert.That(reviewSample[0].actuator01, Is.EqualTo(0f));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(nativeRevision),
                    "Recorded Review must not actuate Native lab material in the background.");
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(desktop.HasFreshLabScreenGesture, Is.False,
                    "A Notebook-to-Lab navigation must not itself re-arm a prior material press.");
                var resumed = desktop.SampleInputs(
                    (ulong)(Time.realtimeSinceStartupAsDouble * 1000000000.0));
                Assert.That(resumed.Length, Is.EqualTo(1));
                Assert.That(resumed[0].actuator01, Is.EqualTo(0f));
                Assert.That(desktop.HasFreshLabScreenGesture, Is.False,
                    "Native-sample-only reconciliation must not fabricate a fresh key release.");
                Assert.That(ResearchLabScreenAdmission.MayActuate(
                    router.Current.ScreenId.ToString(), true), Is.True);
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        // Simulates actual authored XR UI button events against the live
        // Native-backed student Notebook; tracked-ray hardware still requires
        // the final local Player campaign.
        [UnityTest]
        public IEnumerator M3_XRStudentQuickReflectionAddsOnlyChosenBranchLinkedNotebookText()
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
            string noteFile = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var session = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SessionController>(true)).Single();
                var panel = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var spatial = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SpatialPanelPresenter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var name = "xr-student-notes-" + System.Guid.NewGuid().ToString("N");
                session.Configure(driver, name);
                noteFile = Path.Combine(Application.persistentDataPath,
                    "sessions", name + ".plv-notes");
                Assert.That(panel.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True, session.LastError);
                var field = typeof(SpatialPanelPresenter).GetField("buttons",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                Assert.That(field, Is.Not.Null);
                var buttons = (System.Collections.Generic.Dictionary<
                    string, UnityEngine.UI.Button>)field.GetValue(spatial);
                Assert.That(buttons["Open notebook"].gameObject.activeSelf, Is.True,
                    "The normal XR Lab screen must expose its Notebook without lesson-page detour.");
                var notebookRect = buttons["Open notebook"].GetComponent<RectTransform>();
                var saveRect = buttons["Save session"].GetComponent<RectTransform>();
                Assert.That(notebookRect.sizeDelta.x, Is.LessThanOrEqualTo(176f));
                Assert.That(saveRect.sizeDelta.x, Is.LessThanOrEqualTo(176f));
                Assert.That(Mathf.Abs(notebookRect.anchoredPosition.x -
                    saveRect.anchoredPosition.x), Is.GreaterThanOrEqualTo(180f),
                    "Save and Notebook must be separate XR raycast hit targets.");
                buttons["Open notebook"].onClick.Invoke();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Notebook));
                Assert.That(buttons["Quick notes"].gameObject.activeSelf, Is.True);
                var nativeRevision = driver.CurrentLabView.EventSequence;
                buttons["Quick notes"].onClick.Invoke();
                Assert.That(buttons["Note: request another pH"].gameObject.activeSelf,
                    Is.True, "XR reflection must be on its own non-overlapping page.");
                buttons["Note: request another pH"].onClick.Invoke();
                Assert.That(session.ReadNotebookEntries().Count, Is.EqualTo(1));
                var record = session.ReadNotebookEntries().Single();
                Assert.That(record.Text, Is.EqualTo(
                    "Student selected reflection: I want another Native pH measurement before reaching a conclusion."));
                Assert.That(record.BranchId, Is.EqualTo(driver.Session.BranchId));
                Assert.That(record.EventSequence, Is.EqualTo(nativeRevision));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(nativeRevision),
                    "XR Notebook selection cannot invent an authoritative Native science event.");
                Assert.That(buttons["Quick notes"].gameObject.activeSelf, Is.True,
                    "After writing, the XR page must return to the normal Notebook.");
                Assert.That(File.Exists(noteFile), Is.True);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(noteFile))
                    foreach (var candidate in new[] { noteFile, noteFile + ".bak",
                        noteFile + ".lock" })
                        if (File.Exists(candidate)) File.Delete(candidate);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        // Actual reserve-stock solvent comes from Native inventory. Both
        // physically distinct bottle apertures must match the currently
        // registered Native pipette source, never arbitrary proximity.
        [UnityTest]
        public IEnumerator M2_PreparedReserveStockIsPhysicalPipetteIntakeWithoutInventedMaterial()
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
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var actions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var sessions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SessionController>(true)).Single();
                var pipette = roots.SelectMany(root =>
                    root.GetComponentsInChildren<PipetteActuator>(true)).Single();
                var targets = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ResearchStockAspirateTarget>(true)).ToArray();
                var source = targets.Single(target => target.NativeInventoryId == "source");
                var reserve = targets.Single(target => target.NativeInventoryId == "reserve-stock");
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(reserve.IsImmersed(pipette.transform), Is.False,
                    "An unprepared reserve is not an aspiratable liquid.");
                Assert.That(actions.Request(PanelAction.PrepareReserveStock,
                    "acetic-acid", 0.05), Is.True);
                Assert.That(actions.Request(PanelAction.PrepareResearchStock,
                    "hcl", 0.05), Is.True);
                var desktop = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                var hold = typeof(DesktopInputAdapter).GetMethod("BeginHold",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(hold, Is.Not.Null);
                hold.Invoke(desktop, new object[] { pipette.transform });
                var before = driver.CurrentLabView.Vessels.Single(v =>
                    v.Id == "reserve-stock").CommittedVolumeM3;
                Assert.That(before, Is.GreaterThan(0d));
                Assert.That(source.NativeSourceMatches(driver.Session, pipette), Is.True);
                Assert.That(reserve.NativeSourceMatches(driver.Session, pipette), Is.False);
                var beforeTip = actions.PipetteTipVolumeM3;
                var beforeSwitchRevision = driver.CurrentLabView.EventSequence;
                Assert.That(sessions.TryPhysicalSelectPipetteSource(pipette.transform, reserve),
                    Is.False, "Unimmersed pipette cannot pick a new Native source.");
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(beforeSwitchRevision),
                    "Rejected physical source-selection cannot create a Native event.");

                var liquid = reserve.transform.Find("NativeBottleLiquid");
                var tip = pipette.transform.Find("OutletExit");
                var surface = liquid.TransformPoint(new Vector3(0f,
                    liquid.GetComponent<MeshFilter>().sharedMesh.bounds.max.y, 0f));
                pipette.transform.rotation = reserve.transform.rotation;
                pipette.transform.position +=
                    surface - tip.position - reserve.transform.up * .006f;
                Assert.That(reserve.IsImmersed(pipette.transform), Is.True);
                Assert.That(sessions.TryPhysicalSelectPipetteSource(pipette.transform, reserve),
                    Is.True, sessions.LastError);
                Assert.That(driver.CurrentLabView.EventSequence, Is.Not.EqualTo(beforeSwitchRevision),
                    "The new physical source must be selected by a real Native command.");
                Assert.That(sessions.LastInstrumentReceipt, Does.Contain("Native accepted"));
                Assert.That(source.NativeSourceMatches(driver.Session, pipette), Is.False,
                    "After switching, physical source bottle cannot authorize reserve aspiration.");
                Assert.That(reserve.NativeSourceMatches(driver.Session, pipette), Is.True);
                Assert.That(driver.CurrentLabView.Vessels.Single(v =>
                    v.Id == "reserve-stock").CommittedVolumeM3,
                    Is.EqualTo(before).Within(1e-11),
                    "Switching stock must not silently aspirate any liquid.");
                Assert.That(actions.PipetteTipVolumeM3, Is.EqualTo(beforeTip).Within(1e-12));
                Assert.That(sessions.TryPhysicalSelectPipetteSource(pipette.transform, reserve),
                    Is.False, "Re-pressing against the already selected stock cannot switch again.");
                Assert.That(actions.Request(PanelAction.PipetteAspirate),
                    Is.True, actions.LastError);
                Assert.That(driver.CurrentLabView.Vessels.Single(v =>
                    v.Id == "reserve-stock").CommittedVolumeM3, Is.LessThan(before));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        [UnityTest]
        public IEnumerator M2_PhysicalSourceBottlePrimesUpperBuretteViaNativeMaterialOnly()
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
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var sessions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SessionController>(true)).Single();
                var actions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var adapter = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                var inlet = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ResearchBuretteInletTarget>(true)).Single();
                var bottle = roots.SelectMany(root =>
                    root.GetComponentsInChildren<BottlePourActuator>(true)).Single().transform;
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(actions.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True, sessions.LastError);
                var beginHold = typeof(DesktopInputAdapter).GetMethod("BeginHold",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                Assert.That(beginHold, Is.Not.Null);
                beginHold.Invoke(adapter, new object[] { bottle });
                var before = driver.CurrentLabView.Vessels.Single(v =>
                    v.Id == "burette").CommittedVolumeM3;
                var stockBefore = driver.CurrentLabView.Vessels.Single(v =>
                    v.Id == "source").CommittedVolumeM3;
                var nativeEvent = driver.CurrentLabView.EventSequence;
                Assert.That(sessions.TryPhysicalFillBuretteFromBottle(bottle, inlet),
                    Is.False, "A parked but unaligned bottle cannot prime the Native burette.");
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(nativeEvent));
                bottle.rotation = Quaternion.Euler(180f, 0f, 0f);
                var outlet = bottle.Find("OutletExit");
                var marker = inlet.transform.Find("InletEntry");
                Assert.That(outlet, Is.Not.Null);
                Assert.That(marker, Is.Not.Null);
                bottle.position += marker.position + inlet.transform.up * .075f -
                    outlet.position;
                Assert.That(inlet.AdmitsBottle(bottle, true, driver.Session), Is.True,
                    "Native-closed burette and real aligned upper inlet must admit the bottle.");
                Assert.That(actions.Request(PanelAction.Review), Is.True);
                Assert.That(sessions.TryPhysicalFillBuretteFromBottle(bottle, inlet),
                    Is.False, "Review cannot move Native liquid even with physically aligned equipment.");
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(nativeEvent));
                Assert.That(actions.Request(PanelAction.Lab), Is.True);
                Assert.That(sessions.TryPhysicalFillBuretteFromBottle(bottle, inlet),
                    Is.True, sessions.LastError);
                Assert.That(driver.CurrentLabView.Vessels.Single(v =>
                    v.Id == "burette").CommittedVolumeM3,
                    Is.EqualTo(before + 5e-8).Within(1e-11),
                    "Only Native may update the burette volume.");
                Assert.That(driver.CurrentLabView.Vessels.Single(v =>
                    v.Id == "source").CommittedVolumeM3,
                    Is.EqualTo(stockBefore - 5e-8).Within(1e-11),
                    "Physical prime must debit real Native stock.");
                Assert.That(driver.CurrentLabView.EventSequence, Is.Not.EqualTo(nativeEvent));
                Assert.That(sessions.LastInstrumentReceipt, Does.Contain("Native accepted"));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        // A student imports a real offline study pack into a new isolated
        // local save/Notebook. The old Native session is replaced only after
        // the archive, model and both durable files have been admitted.
        [UnityTest]
        public IEnumerator M3_StudentRestoresPortableNativeExperimentAndLocalNotesWithoutOverwritingOldRun()
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
            string originalNotes = null;
            string packed = null;
            string installed = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var sessions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<SessionController>(true)).Single();
                var actions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var name = "portable-student-" + System.Guid.NewGuid().ToString("N");
                sessions.Configure(driver, name);
                originalNotes = Path.Combine(Application.persistentDataPath,
                    "sessions", name + ".plv-notes");
                Assert.That(actions.Request(PanelAction.SelectResearchLesson,
                    "strong-acid-titration-research"), Is.True, sessions.LastError);
                Assert.That(actions.Request(PanelAction.RecordResearchPrediction,
                    "below-7"), Is.True, sessions.LastError);
                Assert.That(actions.Request(PanelAction.PrepareResearchStock,
                    "hcl", 0.05), Is.True, sessions.LastError);
                Assert.That(actions.Request(PanelAction.BottlePour), Is.True, sessions.LastError);
                var source = driver.Session;
                var branch = source.BranchId;
                var atEvent = driver.CurrentLabView.EventSequence;
                var originalNotesBytes = File.ReadAllBytes(originalNotes);
                Assert.That(actions.Request(PanelAction.Pause), Is.True);
                Assert.That(actions.Request(PanelAction.ExportPortable), Is.True,
                    sessions.LastError);
                packed = sessions.LastPortableArchivePath;
                Assert.That(File.Exists(packed), Is.True);
                Assert.That(sessions.CanChooseLocalPortableArchive, Is.True);
                var expectedName = Path.GetFileName(packed);
                for (var i = 0; i < 64 &&
                     sessions.SelectedArchiveFileName != expectedName; i++)
                    Assert.That(actions.Request(PanelAction.ChoosePortableArchive),
                        Is.True, sessions.LastError);
                Assert.That(sessions.SelectedArchiveFileName, Is.EqualTo(expectedName));
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Pause),
                    "Choosing the local study pack cannot navigate away from the only legal restore screen.");
                Assert.That(sessions.CanRestoreLocalPortableSession, Is.True);
                // A corrupted package cannot replace the existing Native
                // session or alter the current branch's durable journal.
                var savedPackBytes = File.ReadAllBytes(packed);
                File.AppendAllText(packed, "tampered");
                Assert.That(actions.Request(PanelAction.RestorePortable), Is.False);
                Assert.That(driver.Session, Is.SameAs(source));
                Assert.That(source.IsOpen, Is.True);
                Assert.That(File.ReadAllBytes(originalNotes),
                    Is.EqualTo(originalNotesBytes));
                File.WriteAllBytes(packed, savedPackBytes);
                Assert.That(actions.Request(PanelAction.RestorePortable),
                    Is.True, sessions.LastError);
                installed = sessions.LastDurableSavePath;
                Assert.That(source.IsOpen, Is.False);
                Assert.That(driver.Session, Is.Not.SameAs(source));
                Assert.That(driver.Session.BranchId, Is.EqualTo(branch));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(atEvent));
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
                Assert.That(File.Exists(installed), Is.True);
                Assert.That(installed, Is.Not.EqualTo(Path.Combine(
                    Application.persistentDataPath, "sessions", name + ".plv.json")));
                Assert.That(File.ReadAllBytes(originalNotes),
                    Is.EqualTo(originalNotesBytes),
                    "Portable student restore overwrote existing learner annotations.");
                Assert.That(sessions.ReadNotebookEntries().Count, Is.EqualTo(1),
                    "Restored Notebook must contain only the exported Native branch/event evidence.");
                Assert.That(sessions.ResearchPredictionSummary, Does.Contain("below 7"));
                Assert.That(sessions.LastPortableImportSummary,
                    Does.Contain("not identity").IgnoreCase);
                Assert.That(sessions.RecordedReview, Is.Null);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(packed))
                    foreach (var name in new[] { packed, packed + ".bak", packed + ".tmp" })
                        if (File.Exists(name)) File.Delete(name);
                if (!string.IsNullOrWhiteSpace(originalNotes))
                    foreach (var name in new[] { originalNotes, originalNotes + ".bak",
                        originalNotes + ".lock" })
                        if (File.Exists(name)) File.Delete(name);
                if (!string.IsNullOrWhiteSpace(installed))
                    foreach (var name in new[] { installed, installed + ".bak",
                        installed.Replace(".plv.json", ".plv-notes"),
                        installed.Replace(".plv.json", ".plv-notes.bak") })
                        if (File.Exists(name)) File.Delete(name);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        // Native-backed laboratory geometry plus stock->tip->receiver material.
        // Actual keyboard and tracked-controller presses remain final Player tests.
        [UnityTest]
        public IEnumerator M2_ImmersedPipetteUsesNativeStockAndDeliversToRealReceiver()
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
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var panels = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var source = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ResearchStockAspirateTarget>(true))
                    .Single(item => item.NativeInventoryId == "source");
                var pipette = roots.SelectMany(root =>
                    root.GetComponentsInChildren<PipetteActuator>(true)).Single().transform;
                var receiver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabCaptureTarget>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(source.IsImmersed(pipette), Is.False,
                    "A parked pipette is not inside source liquid.");
                Assert.That(panels.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True, "Prepare real Native stock first.");
                var fill = source.transform.Find("NativeBottleLiquid");
                var tip = pipette.Find("OutletExit");
                var surface = fill.TransformPoint(new Vector3(
                    0f, fill.GetComponent<MeshFilter>().sharedMesh.bounds.max.y, 0f));
                pipette.rotation = source.transform.rotation;
                pipette.position += surface - tip.position - source.transform.up * 0.006f;
                Assert.That(source.IsImmersed(pipette), Is.True,
                    "Pipette tip must physically reach Native-rendered stock liquid.");
                Assert.That(source.NativeSourceMatches(driver.Session,
                    pipette.GetComponent<PipetteActuator>()), Is.True,
                    "First placed pipette must initially draw from represented source stock.");
                for (var i = 0; i < 3; i++)
                    Assert.That(panels.Request(PanelAction.PipetteAspirate), Is.True,
                        "Three 0.05 mL Native aspirates are needed for the retained-tip budget.");
                Assert.That(panels.PipetteTipVolumeM3, Is.EqualTo(1.5e-7).Within(1e-10));
                var outlet = pipette.Find("OutletExit");
                var aperture = receiver.transform.position + receiver.transform.up * 0.04f;
                pipette.position += aperture + receiver.transform.up * 0.06f - outlet.position;
                Assert.That(source.IsImmersed(pipette), Is.False);
                Assert.That(receiver.Estimate(pipette, Vector3.zero).Length, Is.EqualTo(1));
                Assert.That(panels.Request(PanelAction.PipetteDispense), Is.True,
                    "An aligned, sufficiently filled Native pipette must deliver real liquid.");
                Assert.That(panels.PipetteTipVolumeM3, Is.EqualTo(1e-7).Within(1e-10));
                Assert.That(driver.CurrentLabView.Vessels.Single(v => v.Id == "receiver")
                    .CommittedVolumeM3, Is.GreaterThan(0.0));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        // Authored for the final local Unity campaign, NOT_RUN here.
        // Direct physical receiver selection enters the same shared Native
        // measurement panel as Desktop/XR UI; no fabricated pH is asserted.
        [UnityTest]
        public IEnumerator M2_DesktopReceiverClickSelectsNativeMeasurementOnlyWhenLabIsActive()
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
                var driver = roots.SelectMany(root =>
                    root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var adapter = roots.SelectMany(root =>
                    root.GetComponentsInChildren<DesktopInputAdapter>(true)).Single();
                var target = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabCaptureTarget>(true)).Single();
                var actions = roots.SelectMany(root =>
                    root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var router = roots.SelectMany(root =>
                    root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(target.InventoryId, Is.EqualTo("receiver"));
                Assert.That(target.GetComponent<Collider>(), Is.Not.Null,
                    "Receiver needs a physical collider for mouse selection.");
                var before = driver.CurrentLabView.EventSequence;
                Assert.That(adapter.TryInspectReceiver(target), Is.False,
                    "Initial Native stock-setup Pause cannot admit a measurement click.");
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(before),
                    "A refused click must not mutate Native event history.");
                Assert.That(actions.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True, "Prepare supported Native stock before inspecting.");
                Assert.That(adapter.TryInspectReceiver(target), Is.True,
                    adapter.LastPhysicalInspectionFeedback);
                Assert.That(driver.SelectedMeasurementVesselId, Is.EqualTo("receiver"));
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Measure));
                Assert.That(driver.Measurement, Is.Not.Null);
                Assert.That(adapter.TryInspectReceiver(null), Is.False,
                    "A click without the real configured receiver cannot select science.");
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        // Verify actual authored outlet geometry and committed Native transfer
        // together. Headless policy tests cannot establish this Unity scene path.
        [UnityTest]
        public IEnumerator M2_XRPhysicalDeliveryFailureIsVisibleWithoutPanelNavigation()
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
                var xrInput = roots.SelectMany(root => root.GetComponentsInChildren<XRInputAdapter>(true)).Single();
                var panel = roots.SelectMany(root => root.GetComponentsInChildren<SpatialPanelPresenter>(true)).Single();
                var status = panel.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                    .Single(item => item.name == "Panel detail");
                // The adaptation layer's feedback is a view-only explanation.
                // Geometry rejection never issues a fake Native material event.
                var field = typeof(XRInputAdapter).GetField(
                    "<LastPhysicalDeliveryFeedback>k__BackingField",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                Assert.That(field, Is.Not.Null);
                var router = driver.transform.root.GetComponentInChildren<ScreenRouter>(true);
                router.Show(ScreenId.Lab);
                // This source test runs the Desktop scene by default. Present
                // the same authored world panel for its explicit VR update,
                // then inject a new adapter result WITHOUT navigating again.
                var panelRootField = typeof(SpatialPanelPresenter).GetField("panelRoot",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                Assert.That(panelRootField, Is.Not.Null);
                var worldPanel = (GameObject)panelRootField.GetValue(panel);
                Assert.That(worldPanel, Is.Not.Null);
                worldPanel.SetActive(true);
                field.SetValue(xrInput, "Align the direct-gripped instrument outlet over the stationary receiver.");
                panel.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                Assert.That(status.text, Does.Contain("Physical delivery:"));
                Assert.That(status.text, Does.Contain("Align the direct-gripped instrument"));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        [UnityTest]
        public IEnumerator M2_ReceiverAlignedStockBottleHasActualNativeDeliveryPath()
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
                var panels = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var bottle = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "ResearchStockBottle");
                var receiver = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "ReceiverVessel");
                var target = receiver.GetComponent<LabCaptureTarget>();
                var outlet = bottle.Find("OutletExit");
                Assert.That(outlet, Is.Not.Null);
                Assert.That(target, Is.Not.Null);
                Assert.That(target.Estimate(bottle, Vector3.zero), Is.Empty,
                    "Bottle initially parked away from receiver must have no capture.");
                bottle.rotation = Quaternion.Euler(180f, 0f, 0f);
                var aperture = receiver.position + receiver.up * 0.04f;
                bottle.position += aperture + receiver.up * 0.06f - outlet.position;
                var capture = target.Estimate(bottle, Vector3.zero);
                Assert.That(capture.Length, Is.EqualTo(1),
                    "Inverted, positioned physical bottle must intersect the authored receiver aperture.");
                Assert.That(capture[0].destinationInventoryId, Is.EqualTo("receiver"));
                Assert.That(capture[0].fraction, Is.EqualTo(1f));
                Assert.That(ResearchAuxiliaryDelivery.Resolve(
                    bottle.GetComponent<DesktopGrabbable>().ToolId, true, true,
                    capture.Length == 1, true),
                    Is.EqualTo(ResearchAuxiliaryDeliveryIntent.BottlePour));
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(panels.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True, "Native source preparation is required.");
                var before = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                var start = before.vessels.Single(item => item.id == "receiver")
                    .inventory.researchAdditiveVolumeM3;
                Assert.That(panels.Request(PanelAction.BottlePour), Is.True,
                    "Physical intent must use same shared Native command as UI.");
                var after = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                Assert.That(after.vessels.Single(item => item.id == "receiver")
                    .inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(start + 1e-8).Within(1e-10));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        // Source test only. Real Editor/Player execution remains the final
        // local Codex verification gate; no simulated pH or note fixture.
        // This is a real local .plvpack journey, not a cloud teacher grant.
        // Unity runtime execution remains reserved for the final local gate.
        [UnityTest]
        public IEnumerator M4_ReadOnlyStudentArchiveReviewCannotMutateNativeOrCrossFilesystem()
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
            string notes = null;
            string portableExport = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var panels = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var name = "teacher-" + System.Guid.NewGuid().ToString("N");
                sessions.Configure(driver, name);
                notes = Path.Combine(Application.persistentDataPath, "sessions", name + ".plv-notes");
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(panels.Request(PanelAction.SelectResearchLesson,
                    "strong-acid-titration-research"), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.RecordResearchPrediction, "below-7"),
                    Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Notebook), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.AddNotebookNote,
                    "I would repeat the experiment to check the research observation."),
                    Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.ExportPortable), Is.True, sessions.LastError);
                portableExport = sessions.LastPortableArchivePath;
                var portable = portableExport;
                Assert.That(File.Exists(portable), Is.True);
                Assert.That(panels.Request(PanelAction.Save), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Review), Is.True, sessions.LastError);
                var session = driver.Session;
                var eventSequence = driver.CurrentLabView.EventSequence;
                Assert.That(panels.Request(PanelAction.ChoosePortableArchive),
                    Is.True, sessions.LastError);
                Assert.That(sessions.SelectedArchiveFileName,
                    Is.EqualTo(Path.GetFileName(portable)));
                Assert.That(panels.Request(PanelAction.PreviewPortableArchive),
                    Is.True, sessions.LastError);
                var teaching = sessions.LastInstructorPortableReview;
                Assert.That(teaching, Is.Not.Null);
                Assert.That(teaching.NativeBranchId, Is.EqualTo(session.BranchId));
                Assert.That(teaching.StudentAnnotations.Count, Is.EqualTo(2));
                Assert.That(teaching.Summary, Does.Contain("NOT ATTESTED OR GRADED"));
                Assert.That(sessions.InstructorPortableCompactSummary,
                    Does.Contain("Student hypothesis"));
                Assert.That(panels.Request(PanelAction.NextInstructorAnnotation),
                    Is.True, sessions.LastError);
                Assert.That(sessions.InstructorPortableCompactSummary,
                    Does.Contain("repeat the experiment"));
                Assert.That(panels.Request(PanelAction.NextInstructorAnnotation),
                    Is.True, sessions.LastError);
                Assert.That(sessions.InstructorPortableCompactSummary,
                    Does.Contain("Student hypothesis"));
                Assert.That(teaching.Summary, Does.Contain("Student hypothesis"));
                Assert.That(driver.Session, Is.SameAs(session));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(eventSequence));
                Assert.That(panels.Request(PanelAction.PreviewPortableArchive,
                    "../outside.plvpack"), Is.False, "Imported filename traversal must be rejected.");
                Assert.That(sessions.LastInstructorPortableReview, Is.Null,
                    "Failed import must not leave apparently verified teaching evidence.");
                Assert.That(driver.Session, Is.SameAs(session));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(eventSequence));
            }
            finally
            {
                if (notes != null)
                {
                    var folder = Path.GetDirectoryName(notes);
                    var basePath = Path.Combine(folder,
                        Path.GetFileName(notes).Replace(".plv-notes", ""));
                    foreach (var extension in new[] {
                        ".plv-notes", ".plv.json",
                        ".plv-notes.bak", ".plv.json.bak",
                        ".plv-notes.tmp", ".plv.json.tmp" })
                    {
                        var target = basePath + extension;
                        if (File.Exists(target)) File.Delete(target);
                    }
                }
                if (!string.IsNullOrEmpty(portableExport))
                {
                    if (File.Exists(portableExport)) File.Delete(portableExport);
                    if (File.Exists(portableExport + ".bak"))
                        File.Delete(portableExport + ".bak");
                }
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator M3_ResearchLessonTransfersAndRecordsActualNativePHIntoOfflineNotebook()
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
            string notePath = null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var panels = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true)).Single();
                var router = roots.SelectMany(root => root.GetComponentsInChildren<ScreenRouter>(true)).Single();
                var name = "measurement-" + System.Guid.NewGuid().ToString("N");
                sessions.Configure(driver, name);
                notePath = Path.Combine(Application.persistentDataPath, "sessions", name + ".plv-notes");
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(panels.Request(PanelAction.SelectResearchLesson,
                    "strong-acid-titration-research"), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.RecordResearchPrediction, "below-7"),
                    Is.True, sessions.LastError);
                Assert.That(sessions.ReadNotebookEntries().Count, Is.EqualTo(1));
                Assert.That(sessions.ReadNotebookEntries()[0].Text, Does.Contain("Student hypothesis"));
                Assert.That(panels.Request(PanelAction.SelectResearchLesson,
                    "strong-acid-titration-research"), Is.True, sessions.LastError);
                Assert.That(sessions.CanRecordResearchPrediction, Is.False,
                    "Re-selecting same lesson must reuse the durable original hypothesis.");
                Assert.That(sessions.ResearchPredictionSummary, Does.Contain("below 7"));
                Assert.That(panels.Request(PanelAction.RecordResearchPrediction, "at-least-7"),
                    Is.False, "A second prediction must not overwrite the original hypothesis.");
                Assert.That(panels.Request(PanelAction.PrepareResearchStock, "hcl", 0.05),
                    Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Measure, "receiver"), Is.True);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Measure));
                Assert.That(sessions.CanRecordCurrentNativePH, Is.False,
                    "An empty receiver has no recordable Native pH.");
                Assert.That(panels.Request(PanelAction.RecordCurrentPH), Is.False);
                Assert.That(sessions.ReadNotebookEntries().Count, Is.EqualTo(1),
                    "A rejected empty-receiver measurement must not alter the prediction note.");
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(panels.Request(PanelAction.BottlePour), Is.True, sessions.LastError);
                Assert.That(panels.Request(PanelAction.Measure, "receiver"), Is.True);
                for (var i = 0; i < 200 && !sessions.CanRecordCurrentNativePH; ++i)
                    yield return new WaitForSecondsRealtime(0.01f);
                Assert.That(sessions.CanRecordCurrentNativePH, Is.True,
                    driver.Session.ReadSnapshot());
                var revision = driver.CurrentLabView.Vessels.Single(v => v.Id == "receiver").MaterialRevision;
                var eventSequence = driver.CurrentLabView.EventSequence;
                Assert.That(panels.Request(PanelAction.RecordCurrentPH), Is.True, sessions.LastError);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Notebook));
                var entries = sessions.ReadNotebookEntries();
                Assert.That(entries.Count, Is.EqualTo(2));
                Assert.That(entries[0].Text, Does.Contain("Student hypothesis"));
                Assert.That(entries[0].Text, Does.Contain("below 7"));
                Assert.That(entries[1].VesselId, Is.EqualTo("receiver"));
                Assert.That(entries[1].EventSequence, Is.EqualTo(eventSequence));
                Assert.That(entries[1].Text, Does.Contain("material revision " + revision));
                Assert.That(entries[1].Text, Does.Contain("Native pH"));
                Assert.That(entries[1].Text, Does.Contain("database "));
                Assert.That(entries[1].Text, Does.Contain("Research observation only"));
                Assert.That(entries[1].Text, Does.Contain("Student prediction criterion"));
                Assert.That(entries[1].Text, Does.Contain("Research reflection only"));
                Assert.That(sessions.NotebookSummary, Does.Contain("Local recorded Native pH by event"));
                Assert.That(sessions.NotebookSummary, Does.Contain("event " + eventSequence));
                Assert.That(sessions.NotebookSummary, Does.Contain("not a certified titration curve"));
                Assert.That(panels.Request(PanelAction.RecordCurrentPH), Is.False,
                    "Notebook navigation cannot duplicate a measurement action.");
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(panels.Request(PanelAction.Measure, "receiver"), Is.True);
                Assert.That(sessions.CanRecordCurrentNativePH, Is.False,
                    "Re-entering measurement at the same Native event cannot record a duplicate pH.");
                Assert.That(panels.Request(PanelAction.RecordCurrentPH), Is.False,
                    "Repeat measurement action should be denied without a new Native event.");
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(panels.Request(PanelAction.Notebook), Is.True);
                Assert.That(sessions.ReadNotebookEntries().Count, Is.EqualTo(2));
                Assert.That(sessions.CanRecordResearchExplanation, Is.True,
                    "Student explanation requires recorded, Current Native pH.");
                Assert.That(panels.Request(PanelAction.RecordResearchExplanation,
                    "model-limitations"), Is.True, sessions.LastError);
                Assert.That(sessions.CanRecordResearchExplanation, Is.False,
                    "A second explanation for the same Native event must be refused.");
                Assert.That(panels.Request(PanelAction.RecordResearchExplanation,
                    "composition-hypothesis"), Is.False);
                var explainedNotes = sessions.ReadNotebookEntries();
                Assert.That(explainedNotes.Count, Is.EqualTo(3));
                Assert.That(explainedNotes[2].Text, Does.Contain("Student explanation for"));
                Assert.That(explainedNotes[2].Text, Does.Contain("not Native scientific evidence"));
                Assert.That(sessions.ResearchLessonSummary, Does.Contain("Recorded: Native pH"));
                Assert.That(sessions.ResearchLessonSummary, Does.Contain("Recorded: learner reflection"));
                Assert.That(sessions.ResearchLessonSummary, Does.Contain("No certified grade"));
                Assert.That(router.Current.SupportedActions, Does.Contain("Save"));
                Assert.That(router.Current.SupportedActions, Does.Contain("Review"));
                Assert.That(panels.Request(PanelAction.Save), Is.True, sessions.LastError);
                Assert.That(File.Exists(sessions.LastDurableSavePath), Is.True);
                Assert.That(panels.Request(PanelAction.Review), Is.True, sessions.LastError);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Review));
                Assert.That(sessions.RecordedReview, Is.Not.Null,
                    "Saved notebook observation must be reachable from recorded review.");

                // Full offline learner checkpoint recovery: a *later* note on
                // the same branch must be excluded when the saved Native event
                // is loaded again. The authored lesson/prediction should return
                // without inventing a fresh hypothesis or redoing chemistry.
                var savedEvent = driver.CurrentLabView.EventSequence;
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(panels.Request(PanelAction.BottlePour), Is.True, sessions.LastError);
                Assert.That(driver.CurrentLabView.EventSequence, Is.Not.EqualTo(savedEvent));
                Assert.That(panels.Request(PanelAction.Notebook), Is.True);
                Assert.That(panels.Request(PanelAction.AddNotebookNote,
                    "Later student note beyond the restored checkpoint."), Is.True,
                    sessions.LastError);
                Assert.That(sessions.ReadNotebookEntries().Count, Is.EqualTo(4));
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(panels.Request(PanelAction.Pause), Is.True);
                Assert.That(panels.Request(PanelAction.Load), Is.True, sessions.LastError);
                Assert.That(driver.IsTimeHeld, Is.True);
                Assert.That(driver.HoldReason, Is.EqualTo("SessionLoad"));
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Hold));
                Assert.That(driver.CurrentLabView.EventSequence, Is.EqualTo(savedEvent));
                Assert.That(sessions.HasResearchLesson, Is.True,
                    "Native restoration must recover the selected durable student lesson.");
                Assert.That(sessions.ResearchPredictionSummary, Does.Contain("below 7"),
                    "Restored lesson must use the original student hypothesis.");
                Assert.That(sessions.ReadNotebookEntries().Count, Is.EqualTo(3),
                    "Future-event annotations cannot be credited after a saved checkpoint load.");
                Assert.That(sessions.ResearchLessonSummary, Does.Not.Contain(
                    "Later student note beyond the restored checkpoint."));
                Assert.That(sessions.RecordedReview, Is.Null,
                    "SessionLoad must not leak a prior recorded review.");
                for (var i = 0; i < 200 && !driver.NativeRecoveryReady; i++)
                    yield return new WaitForSecondsRealtime(0.01f);
                Assert.That(driver.NativeRecoveryReady, Is.True,
                    "Native SessionLoad must publish fresh neutral recovery before Continue.");
                Assert.That(panels.Request(PanelAction.Continue), Is.True, sessions.LastError);
                Assert.That(driver.IsTimeHeld, Is.False);
                Assert.That(panels.Request(PanelAction.Lab), Is.True);
                Assert.That(sessions.HasResearchLesson, Is.True,
                    "A recovered Research lesson must survive the explicit Hold release.");
                Assert.That(panels.Request(PanelAction.Notebook), Is.True);
                Assert.That(sessions.ReadNotebookEntries().Count, Is.EqualTo(3));
                Assert.That(panels.Request(PanelAction.Review), Is.True);
                Assert.That(sessions.LearningReviewSummary,
                    Does.Not.Contain("Later student note beyond the restored checkpoint"));
            }
            finally
            {
                if (notePath != null)
                {
                    foreach (var file in new[] {
                        notePath, notePath + ".bak", notePath + ".tmp",
                        notePath.Replace(".plv-notes", ".plv.json"),
                        notePath.Replace(".plv-notes", ".plv.json.bak"),
                        notePath.Replace(".plv-notes", ".plv.json.tmp") })
                        if (File.Exists(file)) File.Delete(file);
                }
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator M1_StockBottleRendersActualNativeSourceWithoutInventingBuretteFill()
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
                var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var bottle = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "ResearchStockBottle")
                    .GetComponent<NativeMeteredInstrumentVisual>();
                var burette = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "DesktopResearchTool")
                    .GetComponent<LiquidPresenter>();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(bottle, Is.Not.Null);
                Assert.That(burette, Is.Not.Null);
                Assert.That(bottle.DisplayedVolumeM3, Is.Zero);
                Assert.That(sessions.SelectStock("nacl", 0.05), Is.True, sessions.LastError);
                yield return null;
                Assert.That(bottle.DisplayedVolumeM3, Is.EqualTo(2.5e-5).Within(1e-9),
                    "The authored bottle fill must reflect committed Native source inventory.");
                Assert.That(burette.CommittedVolumeM3, Is.Zero,
                    "Preparing stock is not proof of filling the physical burette.");
                var receiver = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "ReceiverVessel")
                    .GetComponent<LiquidPresenter>();
                var before = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                var initialSource = before.vessels.Single(item => item.id == "source")
                    .inventory.researchAdditiveVolumeM3;
                var initialReceiver = before.vessels.Single(item => item.id == "receiver")
                    .inventory.researchAdditiveVolumeM3;
                var routed = roots.SelectMany(root => root.GetComponentsInChildren<LabPanelController>(true))
                    .Single();
                Assert.That(routed.Request(PanelAction.BottlePour), Is.True, sessions.LastError);
                Assert.That(sessions.LastInstrumentReceipt, Does.Contain("Bottle pour 0.01 ml"));
                Assert.That(sessions.LastInstrumentReceipt, Does.Contain("Native accepted command #"));
                var after = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                Assert.That(after.vessels.Single(item => item.id == "source")
                    .inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(initialSource - 1e-8).Within(1e-10));
                Assert.That(after.vessels.Single(item => item.id == "receiver")
                    .inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(initialReceiver + 1e-8).Within(1e-10));
                Assert.That(receiver.CommittedVolumeM3,
                    Is.EqualTo(initialReceiver + 1e-8).Within(1e-10),
                    "The visible receiver must display the post-receipt Native material.");
                Assert.That(bottle.DisplayedVolumeM3,
                    Is.EqualTo(initialSource - 1e-8).Within(1e-10));
                Assert.That(burette.CommittedVolumeM3, Is.Zero);
                Assert.That(driver.PauseSession(), Is.True);
                Assert.That(sessions.TryBottleMeasuredPour(), Is.False,
                    "A held/paused Native session must not accept a fresh measured pour.");
                Assert.That(sessions.LastInstrumentReceipt, Is.Null,
                    "Rejected operations must not leave a misleading accepted receipt.");
                Assert.That(sessions.LastError, Is.Not.Empty);
                var held = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                Assert.That(held.vessels.Single(item => item.id == "receiver")
                    .inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(initialReceiver + 1e-8).Within(1e-10));
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        [UnityTest]
        public IEnumerator M2_NativeBuretteFillDebitsStockAndUpdatesVisibleGraduatedTool()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).Single();
                var sessions = roots.SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).Single();
                var burette = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(child => child.name == "DesktopResearchTool")
                    .GetComponent<LiquidPresenter>();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(sessions.CanFillBurette, Is.False);
                Assert.That(sessions.SelectStock("hcl", 0.05), Is.True, sessions.LastError);
                var before = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                var stockBefore = before.vessels.Single(v => v.id == "source").inventory.researchAdditiveVolumeM3;
                Assert.That(before.vessels.Single(v => v.id == "burette").inventory.researchAdditiveVolumeM3, Is.Zero);
                Assert.That(sessions.CanFillBurette, Is.True);
                Assert.That(sessions.TryFillBuretteFromBottle(), Is.True, sessions.LastError);
                var after = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                Assert.That(after.vessels.Single(v => v.id == "source").inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(stockBefore - 5e-8).Within(1e-10));
                Assert.That(after.vessels.Single(v => v.id == "burette").inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(5e-8).Within(1e-10));
                Assert.That(burette.CommittedVolumeM3, Is.EqualTo(5e-8).Within(1e-10));
                Assert.That(sessions.LastInstrumentReceipt, Does.Contain("Burette prime 0.05 ml"));
                Assert.That(driver.PauseSession(), Is.True);
                Assert.That(sessions.TryFillBuretteFromBottle(), Is.False);
                Assert.That(sessions.LastInstrumentReceipt, Is.Null);
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
        }

        [Test]
        public void P05_03_ComputeHoldCannotOfferContinueWithoutNativeRecovery()
        {
            var root = new GameObject("ComputeHoldReadiness");
            var panel = root.AddComponent<HoldPanel>();
            panel.Present(true, true, true, "Compute");
            Assert.That(panel.CanContinue, Is.False,
                "A neutral controller alone cannot clear an unresolved scientific hold.");
            var requests = 0;
            panel.ContinueRequested += () => requests++;
            panel.RequestContinue();
            Assert.That(requests, Is.Zero);
            panel.Present(true, true, true, "Compute", nativeRecoveryReady: true);
            Assert.That(panel.CanContinue, Is.True);
            panel.RequestContinue();
            Assert.That(requests, Is.EqualTo(1));
            Object.DestroyImmediate(root);
        }

        [System.Serializable] private sealed class StockInventory
        {
            public double solventWaterKg;
            public double sodiumMol;
            public double chlorideMol;
            public double acetateMol;
            public double researchAdditiveVolumeM3;
            public string preparationId;
            public string provenanceId;
        }
        [System.Serializable] private sealed class StockVessel
        {
            public string id;
            public StockInventory inventory;
        }
        [System.Serializable] private sealed class StockTool
        {
            public StockInventory tipInventory;
            public StockInventory inFlightInventory;
        }
        [System.Serializable] private sealed class StockState
        {
            public bool paused;
            public string eventSequence;
            public StockVessel[] vessels;
            public StockVessel[] sinks;
            public StockTool[] tools;
            public StockInventory materialLedger;
            public StockObservation[] observations;
        }
        [System.Serializable] private sealed class StockObservation
        {
            public string vesselId;
            public string freshness;
        }

        [UnityTest]
        public IEnumerator P06_02_XrSharedPanelSelectionPreparesExactNativeMolarityAndWaterReserve()
        {
            // This is a real Native session and panel, not the headless routing
            // stub. Execute in the deferred Unity 6000.3.25f1 campaign.
            var root = new GameObject("Research XR molarity route");
            var driver = root.AddComponent<CoreDriver>();
            var sessions = root.AddComponent<SessionController>();
            sessions.Configure(driver);
            try
            {
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var panel = root.GetComponent<LabPanelController>();
                Assert.That(panel, Is.Not.Null);
                Assert.That(driver.SourceNeedsPreparation, Is.True);
                Assert.That(panel.Request(PanelAction.PrepareReserveStock, "water", 0.0), Is.True,
                    sessions.LastError);
                var concentration = ResearchConcentrationSelection.Resolve(driver.ResearchConcentrations,
                    1, "hcl");
                Assert.That(concentration, Is.EqualTo(0.05d));
                Assert.That(panel.Request(PanelAction.PrepareResearchStock, "hcl", concentration), Is.True,
                    sessions.LastError);
                var snapshot = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                var source = snapshot.vessels.Single(item => item.id == "source").inventory;
                var reserve = snapshot.vessels.Single(item => item.id == "reserve-stock").inventory;
                Assert.That(source.chlorideMol, Is.EqualTo(0.05d * 0.025d).Within(1e-9),
                    "The VR-selected molarity must reach actual Native material pools.");
                Assert.That(source.sodiumMol, Is.EqualTo(0d).Within(1e-12));
                Assert.That(reserve.chlorideMol, Is.EqualTo(0d).Within(1e-12),
                    "A water reserve cannot inherit the selected acid concentration.");
                Assert.That(reserve.researchAdditiveVolumeM3, Is.GreaterThan(0d));
                Assert.That(snapshot.paused, Is.False,
                    "Completing deliberate stock setup should open the running Native Lab.");
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
            for (var attempt = 0; attempt < 200 && CoreSession.PendingRetirements > 0; ++attempt)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(CoreSession.PendingRetirements, Is.Zero);
        }

        private sealed class LivePourAdapter : MonoBehaviour, ILabInputAdapter
        {
            public Transform Tool;
            public Transform Frame;
            public LabCaptureTarget Target;
            public float Actuator;
            private ulong sequence;
            private bool enabledInput = true;

            public LabInputSample[] SampleInputs(ulong nowNs) => !enabledInput ? System.Array.Empty<LabInputSample>() :
                new[] { LabGeometryAdapter.BuildSample("research-tool", ++sequence, nowNs, Frame, Tool,
                    Actuator, "burette-50ml-research-v1", true, Target.Estimate(Tool)) };
            public void ResetBaselines() => Actuator = 0f;
            public void AdvanceSequence(string toolId, ulong watermark)
            {
                if (toolId == "research-tool" && sequence < watermark) sequence = watermark;
            }
            public void SetEnabled(bool value) => enabledInput = value;
        }

        [UnityTest]
        public IEnumerator P02_01_SelectedStocksPrepareNativePoolsFromAnEmptyLab()
        {
            var stockFactors = new[] {
                (id: "water", sodium: 0d, chloride: 0d, acetate: 0d),
                (id: "hcl", sodium: 0d, chloride: 1d, acetate: 0d),
                (id: "naoh", sodium: 1d, chloride: 0d, acetate: 0d),
                (id: "nacl", sodium: 1d, chloride: 1d, acetate: 0d),
                (id: "acetic-acid", sodium: 0d, chloride: 0d, acetate: 1d),
                (id: "sodium-acetate", sodium: 1d, chloride: 0d, acetate: 1d)
            };
            foreach (var (id, sodiumFactor, chlorideFactor, acetateFactor) in stockFactors)
            foreach (var concentration in id == "water" ? new[] { 0d } : new[] { 0.01d, 0.05d, 0.1d })
            {
                var sodium = sodiumFactor * concentration * 0.025d;
                var chloride = chlorideFactor * concentration * 0.025d;
                var acetate = acetateFactor * concentration * 0.025d;
                var root = new GameObject("StockSelection-" + id);
                var driver = root.AddComponent<CoreDriver>();
                var controller = root.AddComponent<SessionController>();
                controller.Configure(driver);
                try
                {
                var before = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                Assert.That(before.paused, Is.True, id);
                Assert.That(before.vessels.Single(v => v.id == "source").inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(0d), id);
                Assert.That(before.vessels.Single(v => v.id == "receiver").inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(0d), id);
                Assert.That(controller.SelectStock(id, concentration), Is.True, controller.LastError);
                var after = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                var prepared = after.vessels.Single(v => v.id == "source").inventory;
                Assert.That(after.paused, Is.False, id);
                Assert.That(prepared.researchAdditiveVolumeM3, Is.EqualTo(2.5e-5).Within(1e-12), id);
                Assert.That(prepared.solventWaterKg, Is.EqualTo(0.025).Within(1e-9), id);
                Assert.That(prepared.sodiumMol, Is.EqualTo(sodium).Within(1e-9), id);
                Assert.That(prepared.chlorideMol, Is.EqualTo(chloride).Within(1e-9), id);
                Assert.That(prepared.acetateMol, Is.EqualTo(acetate).Within(1e-9), id);
                Assert.That(prepared.preparationId, Is.EqualTo("source"), id);
                Assert.That(prepared.provenanceId, Is.EqualTo("aqueous-six-research-v1"), id);
                }
                finally { Object.DestroyImmediate(root); }
                yield return null;
                for (var attempt = 0; attempt < 200 && CoreSession.PendingRetirements > 0; attempt++)
                    yield return new WaitForSecondsRealtime(0.01f);
                Assert.That(CoreSession.PendingRetirements, Is.Zero, "The previous real solver context did not retire.");
            }
        }

        [UnityTest]
        public IEnumerator P02_02_TouchedRevisionFacadeRejectsStaleAndIgnoresUnrelatedVessel()
        {
            var root = new GameObject("CommandFacadeIntegration");
            var driver = root.AddComponent<CoreDriver>();
            try
            {
            var commands = new LabCommandService(driver.Session);
            var sourceAtZero = commands.CaptureTouchedRevisions("source");
            Assert.That(driver.Session.SubmitOrdered("CreateVessel",
                "{\"id\":\"unrelated\",\"capacityM3\":0.00005}"), Does.Contain("\"accepted\":true"));
            var unrelatedAtZero = commands.CaptureTouchedRevisions("unrelated");
            Assert.That(commands.Submit("PrepareStock",
                "{\"vesselId\":\"unrelated\",\"stockKind\":\"Water\",\"concentrationMolPerL\":0.0,\"referenceVolumeM3\":0.000001}",
                unrelatedAtZero).Accepted, Is.True);
            var sourcePayload = "{\"vesselId\":\"source\",\"stockKind\":\"SodiumChloride\",\"concentrationMolPerL\":0.05,\"referenceVolumeM3\":0.000025}";
            var committed = commands.Submit("PrepareStock", sourcePayload, sourceAtZero);
            Assert.That(committed.Accepted, Is.True,
                "An unrelated vessel revision must not invalidate the source intent.");
            var identity = "{\"schemaVersion\":1,\"branchId\":\"local-session\",\"commandSequence\":\"" +
                committed.CommandSequence + "\",\"type\":\"PrepareStock\",\"expectedMaterialRevisions\":{\"source\":\"0\"},\"payload\":" + sourcePayload + "}";
            driver.Session.Submit(identity);
            Assert.That(driver.Session.PollEvents().Any(e => e.Contains("\"commandSequence\":\"" + committed.CommandSequence + "\"") &&
                e.Contains("\"code\":\"Accepted\"")), Is.True, "Identical command identity should replay the accepted receipt.");
            driver.Session.Submit(identity.Replace("\"concentrationMolPerL\":0.05", "\"concentrationMolPerL\":0.1"));
            Assert.That(driver.Session.PollEvents().Any(e => e.Contains("\"commandSequence\":\"" + committed.CommandSequence + "\"") &&
                e.Contains("\"code\":\"CommandIdentityConflict\"")), Is.True);
            var rejected = commands.Submit("PrepareStock", sourcePayload, sourceAtZero);
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Code, Is.EqualTo("StaleRevision"));
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator B02_PlayerLifecycleBootstrapsAndDisposesTheNativeAuthority()
        {
            var root = new GameObject("NativeRuntimeSmoke");
            var driver = root.AddComponent<CoreDriver>();
            yield return null;

            Assert.That(driver.enabled, Is.True);
            Assert.That(driver.Session, Is.Not.Null);
            Assert.That(driver.Session.IsOpen, Is.True);
            var snapshot = driver.Session.ReadSnapshot();
            StringAssert.Contains("\"id\":\"source\"", snapshot);
            StringAssert.Contains("\"id\":\"receiver\"", snapshot);
            StringAssert.Contains("\"id\":\"research-tool\"", snapshot);

            Object.Destroy(root);
            yield return null;
            Assert.That(driver == null || driver.Session == null, Is.True);
        }

        [UnityTest]
        public IEnumerator P06_01_ReenabledLabDriverKeepsOneSessionViewAndSubscription()
        {
            var root = new GameObject("ReenabledLabDriver");
            try
            {
                var driver = root.AddComponent<CoreDriver>();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var originalSession = driver.Session;
                Assert.That(root.GetComponents<MeasurementView>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponents<NativeLabVisuals>().Length, Is.EqualTo(1));
                root.SetActive(false);
                yield return null;
                Assert.That(originalSession.IsOpen, Is.False);
                Assert.That(driver.CurrentLabView, Is.Null);
                root.SetActive(true);
                yield return null;
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.Session, Is.Not.SameAs(originalSession));
                Assert.That(root.GetComponents<MeasurementView>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponents<NativeLabVisuals>().Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_01_LabSceneReactivationDoesNotDuplicateLiquidViewsOrRecorder()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
                var tool = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .First(item => item.name == "DesktopResearchTool");
                var receiver = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .First(item => item.name == "ReceiverVessel");
                var recorder = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .First(item => item.name == "PerformanceRecorder");
                var initialView = driver.CurrentLabView;
                Assert.That(initialView, Is.Not.Null);
                Assert.That(initialView.Vessels.Any(item => item.Id == "source"), Is.True);
                Assert.That(tool.GetComponents<LiquidPresenter>().Length, Is.EqualTo(1));
                Assert.That(receiver.GetComponents<LiquidPresenter>().Length, Is.EqualTo(1));
                Assert.That(recorder.GetComponents<PlayerPerformanceRecorder>().Length, Is.EqualTo(1));
                driver.enabled = false;
                yield return null;
                Assert.That(driver.CurrentLabView, Is.Null);
                driver.enabled = true;
                yield return null;
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.CurrentLabView, Is.Not.SameAs(initialView));
                Assert.That(driver.GetComponents<NativeLabVisuals>().Length, Is.EqualTo(1));
                Assert.That(tool.GetComponents<LiquidPresenter>().Length, Is.EqualTo(1));
                Assert.That(receiver.GetComponents<LiquidPresenter>().Length, Is.EqualTo(1));
                Assert.That(recorder.GetComponents<PlayerPerformanceRecorder>().Length, Is.EqualTo(1));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_01_MissingScientificPackageDisablesLiveLabSession()
        {
            var root = new GameObject("MissingScientificPackage");
            root.SetActive(false);
            try
            {
                var driver = root.AddComponent<CoreDriver>();
                driver.ConfigurePackagePathResolver(() => throw new ScientificPackageMismatchException("Pinned package is missing."));
                LogAssert.Expect(LogType.Error, "Native scientific core unavailable: Pinned package is missing.");
                root.SetActive(true);
                yield return null;
                Assert.That(driver.enabled, Is.False);
                Assert.That(driver.Session, Is.Null);
                Assert.That(driver.StartupError, Does.Contain("Pinned package is missing"));
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator O06_CoreDriverPresentsLiveSolverStatusThroughMeasurementView()
        {
            var root = new GameObject("NativeMeasurementIntegration");
            try
            {
                var driver = root.AddComponent<CoreDriver>();
                Assert.That(driver.TryPrepareSourceStock("hcl", 0.05), Is.True);
                Assert.That(driver.Session.SubmitOrdered("CreateVessel", "{\"id\":\"empty-control\",\"capacityM3\":0.000002}"),
                    Does.Contain("\"accepted\":true"));
                for (var attempt = 0; attempt < 200 && (driver.Measurement == null ||
                    driver.Measurement.Availability != MeasurementAvailability.Current); attempt++)
                    yield return new WaitForSecondsRealtime(0.01f);
                Assert.That(driver.Measurement, Is.Not.Null);
                Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                    driver.Session.ReadSnapshot());
                Assert.That(float.TryParse(driver.Measurement.DisplayValue,
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                    out var value), Is.True);
                Assert.That(value, Is.InRange(0f, 14f));
                Assert.That(driver.Measurement.Explanation, Does.Contain("Research"));
                Assert.That(driver.SelectMeasurementVessel("empty-control"), Is.True);
                Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Unsupported));
                Assert.That(driver.Measurement.DisplayValue, Is.EqualTo("--"));
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
        }

        [Test]
        public void B02_VrSessionIdentityStartsInNativeVrMode()
        {
            using var session = new CoreSession("playmode-vr", "VR");
            StringAssert.Contains("\"mode\":\"VR\"", session.ReadSnapshot());
        }

        [UnityTest]
        public IEnumerator B01_DesktopAdapterInputIsAcceptedByNativeAuthority()
        {
            var root = new GameObject("DesktopNativeIntegration");
            root.SetActive(false);
            var tool = new GameObject("ResearchTool");
            tool.transform.SetParent(root.transform, false);
            tool.AddComponent<DesktopGrabbable>();
            var adapter = root.AddComponent<DesktopInputAdapter>();
            adapter.Configure(root.transform, null, null, tool.transform, null, "burette-50ml-research-v1");
            var driver = root.AddComponent<CoreDriver>();
            driver.ConfigureInputs(adapter);

            root.SetActive(true);
            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(driver.enabled, Is.True);
            Assert.That(driver.Session, Is.Not.Null);
            StringAssert.Contains("\"research-tool\"", driver.Session.ReadSnapshot());
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator H02_LoadedSessionKeepsNativeInputWatermarkAcrossAdapterReset()
        {
            var root = new GameObject("LoadedNativeSession");
            root.SetActive(false);
            var tool = new GameObject("ResearchTool");
            tool.transform.SetParent(root.transform, false);
            tool.AddComponent<DesktopGrabbable>();
            var adapter = root.AddComponent<DesktopInputAdapter>();
            adapter.Configure(root.transform, null, null, tool.transform, null, "burette-50ml-research-v1");
            var driver = root.AddComponent<CoreDriver>();
            driver.ConfigureInputs(adapter);
            root.SetActive(true);
            yield return null;
            var first = adapter.SampleInputs(1000000000)[0];
            first.sampleSequence = 50;
            driver.Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(new[] { first }));
            var imported = CoreSession.ImportSession(driver.Session.ExportSession());
            driver.ReplaceSession(imported);
            adapter.ResetBaselines();
            var next = adapter.SampleInputs(1020000000)[0];
            Assert.That(next.sampleSequence, Is.GreaterThan(50UL));
            Assert.DoesNotThrow(() => driver.Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(new[] { next })));
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator S01_PackagedDatabaseProducesLiveProductObservation()
        {
            var databasePath = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
            using var session = new CoreSession("player-science", "Desktop", databasePath, ScientificPackage.DatabaseSha256);
            Assert.That(session.SubmitOrdered("CreateVessel", "{\"id\":\"acid\",\"capacityM3\":0.00002}"), Does.Contain("\"accepted\":true"));
            Assert.That(session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"acid\",\"stockKind\":\"HydrochloricAcid\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.00001}",
                "{\"acid\":\"0\"}"), Does.Contain("\"accepted\":true"));
            var current = false;
            for (var attempt = 0; attempt < 200 && !current; attempt++)
            {
                session.Step(0.0, 1000000000);
                var snapshot = session.ReadSnapshot();
                current = snapshot.Contains("\"vesselId\":\"acid\"") &&
                          snapshot.Contains("\"freshness\":\"Current\"") &&
                          snapshot.Contains("\"databaseIdentity\":\"" + ScientificPackage.DatabaseSha256 + "\"");
                if (!current) yield return null;
            }
            Assert.That(current, Is.True, "Packaged database did not produce an eligible current pH observation.");
        }

        [Test]
        public void S01_LoadRequiresTheCurrentPinnedScientificPackage()
        {
            var databasePath = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
            using var session = new CoreSession("package-load", "Desktop", databasePath, ScientificPackage.DatabaseSha256);
            var exported = session.ExportSession();
            Assert.Throws<ScientificPackageMismatchException>(() => CoreSession.ImportSession(exported, databasePath + ".wrong"));
            using var restored = CoreSession.ImportSession(exported, databasePath);
            Assert.That(restored.IsOpen, Is.True);
        }

        [Test]
        public void P04_02_ColdStartAndImportRejectMismatchedActualPackageBytes()
        {
            var databasePath = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
            var corrupted = Path.Combine(Application.temporaryCachePath, "p04-02-corrupt-minteq.dat");
            var bytes = File.ReadAllBytes(databasePath);
            bytes[bytes.Length / 2] ^= 0x01;
            File.WriteAllBytes(corrupted, bytes);
            try
            {
                Assert.Throws<ScientificPackageMismatchException>(() =>
                {
                    using var rejected = new CoreSession("wrong-bytes", "Desktop", corrupted,
                        ScientificPackage.DatabaseSha256);
                });
            }
            finally { File.Delete(corrupted); }
            using var session = new CoreSession("package-identity", "Desktop", databasePath,
                ScientificPackage.DatabaseSha256);
            var wrongIdentity = session.ExportSession().Replace(ScientificPackage.DatabaseSha256,
                new string('0', ScientificPackage.DatabaseSha256.Length));
            Assert.Throws<ScientificPackageMismatchException>(() =>
            {
                using var rejected = CoreSession.ImportSession(wrongIdentity);
            });
        }

        [UnityTest]
        public IEnumerator P04_02_MismatchedSaveOpensRecordedOnlyObservationWithoutReplacingLiveSession()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).First();
            Assert.That(driver.TryPrepareSourceStock("hcl", 0.1), Is.True);
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            var actual = driver.Session.ExportSession();
            var mismatched = actual.Replace(ScientificPackage.DatabaseSha256,
                new string('0', ScientificPackage.DatabaseSha256.Length));
            var path = Path.Combine(Application.temporaryCachePath, "p04-02-recorded-only.plv.json");
            File.WriteAllText(path, mismatched);
            File.WriteAllText(path + ".bak", actual);
            try
            {
                Assert.Throws<ScientificPackageMismatchException>(() =>
                    new SaveService().Load(path, driver.ScientificDatabasePath),
                    "A compatible backup must not silently replace a present save with a different model.");
                Assert.That(controller.ContinueFrom(path), Is.False);
                Assert.That(controller.RecordedReview, Is.Not.Null);
                Assert.That(controller.RecordedReview.Observations.Single(item => item.vesselId == "source").DisplayValue,
                    Is.EqualTo(driver.Measurement.DisplayValue));
                Assert.That(controller.LastError, Does.Contain("saved observations only"));
                Assert.That(driver.Session.IsOpen, Is.True);
                Assert.That(driver.Session.ReadSnapshot(), Does.Contain("\"source\""));
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".bak");
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator X03_InjectedHmdPoseMovesGeneratedXrCamera()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var hmd = InputSystem.AddDevice<XRHMD>();
            yield return null;
            var xrRig = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "XRPlayerRig")?.gameObject;
            Assert.That(xrRig, Is.Not.Null);
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ModeCoordinator>(true))
                .First().Select(ApplicationMode.VirtualReality);
            var camera = xrRig.GetComponentInChildren<Camera>(true);
            Assert.That(camera, Is.Not.Null);
            InputSystem.QueueDeltaStateEvent(hmd.trackingState, 3);
            InputSystem.QueueDeltaStateEvent(hmd.devicePosition, new Vector3(0.15f, 1.6f, 0.25f));
            InputSystem.QueueDeltaStateEvent(hmd.deviceRotation, Quaternion.Euler(0f, 35f, 0f));
            InputSystem.Update();
            yield return null;
            Assert.That(camera.transform.localPosition.x, Is.EqualTo(0.15f).Within(0.03f));
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(1.6f).Within(0.03f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.Euler(0f, 35f, 0f)), Is.LessThan(3f));
            InputSystem.RemoveDevice(hmd);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator P02_03_SceneAdapterPourRendersNativeReceiverAndRetainsToolMaterial()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
            var children = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var driver = children.Select(item => item.GetComponent<CoreDriver>()).First(item => item != null);
            var tool = children.First(item => item.name == "DesktopResearchTool");
            var receiver = children.First(item => item.name == "ReceiverVessel");
            var pendingDrop = tool.GetComponent<DropPresenter>();
            Assert.That(pendingDrop, Is.Not.Null);
            var target = receiver.GetComponent<LabCaptureTarget>();
            var liquid = receiver.GetComponent<LiquidPresenter>();
            Assert.That(target, Is.Not.Null);
            Assert.That(liquid, Is.Not.Null);
            Assert.That(target.Estimate(tool).Length, Is.EqualTo(1), "The placed scene outlet must clear the receiver rim.");
            var input = new GameObject("LivePourActionInjection");
            SceneManager.MoveGameObjectToScene(input, scene);
            var adapter = input.AddComponent<LivePourAdapter>();
            adapter.Tool = tool;
            adapter.Frame = tool.parent;
            adapter.Target = target;
            driver.ConfigureInputs(adapter);
            Assert.That(driver.TryPrepareSourceStock("nacl", 0.1), Is.True);
            for (var attempt = 0; driver.IsTimeHeld && attempt < 100; attempt++)
            {
                driver.ResumeAfterTimeHold();
                yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.That(driver.IsTimeHeld, Is.False);
            Assert.That(driver.Session.SubmitOrdered("CreateVessel", "{\"id\":\"unrelated\",\"capacityM3\":0.000002}"),
                Does.Contain("\"accepted\":true"));
            Assert.That(driver.Session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"unrelated\",\"stockKind\":\"HydrochloricAcid\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.000001}",
                "{\"unrelated\":\"0\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                "The first pour needs an eligible science budget boundary.");
            var unrelatedCurrent = false;
            for (var attempt = 0; attempt < 200 && !unrelatedCurrent; attempt++)
            {
                var state = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                unrelatedCurrent = state.observations != null && state.observations.Any(item =>
                    item.vesselId == "unrelated" && item.freshness == "Current");
                if (!unrelatedCurrent) yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.That(unrelatedCurrent, Is.True, driver.Session.ReadSnapshot());
            tool.position = receiver.position + Vector3.up * 0.10f;
            Assert.That(target.Estimate(tool).Length, Is.EqualTo(1), "The scene outlet must aim into its real capture aperture.");
            Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            adapter.Actuator = 1f;
            for (var attempt = 0; attempt < 100 && liquid.CommittedVolumeM3 <= 0d; attempt++)
            {
                if (driver.IsTimeHeld)
                {
                    adapter.Actuator = 0f;
                    for (var recovery = 0; driver.IsTimeHeld && recovery < 100; recovery++)
                    {
                        driver.ResumeAfterTimeHold();
                        yield return new WaitForSecondsRealtime(0.01f);
                    }
                    Assert.That(driver.IsTimeHeld, Is.False, driver.Session.ReadSnapshot());
                    adapter.Actuator = 1f;
                }
                yield return new WaitForSecondsRealtime(0.02f);
            }
            Assert.That(liquid.CommittedVolumeM3, Is.GreaterThan(0d), driver.Session.ReadSnapshot());
            var flowing = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
            Assert.That(flowing.tools[0].tipInventory.researchAdditiveVolumeM3, Is.GreaterThan(0d));
            Assert.That(pendingDrop.CommittedVolumeM3, Is.GreaterThan(0d));
            Assert.That(pendingDrop.ActiveParcelId, Is.Not.Empty);
            Assert.That(tool.Find("NativePendingDrop").gameObject.activeSelf, Is.True);
#if UNITY_EDITOR
            var camera = children.Select(item => item.GetComponent<Camera>())
                .First(item => item != null && item.name == "DesktopCamera");
            var renderTarget = RenderTexture.GetTemporary(960, 600, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var image = new Texture2D(960, 600, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = renderTarget;
                camera.Render();
                RenderTexture.active = renderTarget;
                image.ReadPixels(new Rect(0f, 0f, 960f, 600f), 0, 0);
                image.Apply();
                var evidenceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../artifacts/evidence-p03-lab"));
                Directory.CreateDirectory(evidenceDirectory);
                using (var writer = new BinaryWriter(File.Create(Path.Combine(evidenceDirectory,
                    "lab-pending-drop-editor.ppm"))))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("P6\n960 600\n255\n"));
                    var colors = image.GetPixels32();
                    for (var y = 599; y >= 0; y--)
                        for (var x = 0; x < 960; x++)
                        {
                            var color = colors[y * 960 + x];
                            writer.Write(color.r);
                            writer.Write(color.g);
                            writer.Write(color.b);
                        }
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTarget);
                Object.DestroyImmediate(image);
            }
#endif
            Assert.That(flowing.materialLedger.researchAdditiveVolumeM3, Is.EqualTo(26e-6).Within(1e-12));
            adapter.Actuator = 0f;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                if (driver.IsTimeHeld) driver.ResumeAfterTimeHold();
                yield return new WaitForSecondsRealtime(0.02f);
                var latest = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                if (latest.tools[0].inFlightInventory.researchAdditiveVolumeM3 == 0d) break;
            }
            var landed = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
            Assert.That(landed.tools[0].inFlightInventory.researchAdditiveVolumeM3, Is.Zero);
            Assert.That(pendingDrop.CommittedVolumeM3, Is.Zero);
            Assert.That(pendingDrop.ActiveParcelId, Is.Empty);
            Assert.That(tool.Find("NativePendingDrop").gameObject.activeSelf, Is.False);
            Assert.That(landed.tools[0].tipInventory.researchAdditiveVolumeM3, Is.GreaterThan(0d));
            Assert.That(landed.materialLedger.researchAdditiveVolumeM3, Is.EqualTo(26e-6).Within(1e-12));
            Assert.That(landed.observations.Any(item => item.vesselId == "unrelated" && item.freshness == "Current"),
                Is.True, "A continuous pour must not stale an unrelated vessel's real observation.");
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P04_01_SceneCheckpointRestoresScientificMeasurementGeneration()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            try
            {
            Assert.That(driver.Session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"source\",\"stockKind\":\"HydrochloricAcid\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.00000004}",
                "{\"source\":\"0\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            var acidValue = driver.Measurement.DisplayValue;
            if (driver.IsTimeHeld) Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            Assert.That(driver.Session.SubmitOrdered("CreateVessel", "{\"id\":\"base\",\"capacityM3\":0.00005}"),
                Does.Contain("\"accepted\":true"));
            Assert.That(driver.Session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"base\",\"stockKind\":\"SodiumHydroxide\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.00000009}",
                "{\"base\":\"0\"}"), Does.Contain("\"accepted\":true"));
            var baseCurrent = false;
            for (var attempt = 0; attempt < 200 && !baseCurrent; attempt++)
            {
                var state = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                baseCurrent = state.observations != null && state.observations.Any(item =>
                    item.vesselId == "base" && item.freshness == "Current");
                if (!baseCurrent) yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.That(baseCurrent, Is.True, driver.Session.ReadSnapshot());
            Assert.That(driver.CreateCheckpoint("acid-state"), Is.True);
            if (driver.IsTimeHeld) Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"base\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00000004},\"captureFractions\":[{\"destinationInventoryId\":\"source\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"base\":\"1\",\"source\":\"1\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 && driver.Measurement.DisplayValue == acidValue; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                driver.Session.ReadSnapshot());
            Assert.That(driver.Measurement.DisplayValue, Is.Not.EqualTo(acidValue));
            Assert.That(driver.RestartCheckpoint("acid-state"), Is.True);
            Assert.That(driver.Measurement.DisplayValue, Is.EqualTo("--"),
                "A newer-state pH must disappear as soon as the checkpoint is restored.");
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                driver.Session.ReadSnapshot());
            Assert.That(driver.Measurement.DisplayValue, Is.EqualTo(acidValue));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P04_02_ReceiverInspectionUsesItsOwnLiveObservation()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            try
            {
            Assert.That(driver.TryPrepareSourceStock("hcl", 0.1), Is.True);
            Assert.That(driver.Session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"receiver\",\"stockKind\":\"SodiumHydroxide\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.00000004}",
                "{\"receiver\":\"0\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            var sourceValue = driver.Measurement.DisplayValue;
            Assert.That(driver.SelectMeasurementVessel("receiver"), Is.True);
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.DisplayValue, Is.Not.EqualTo(sourceValue));
            Assert.That(driver.Measurement.Explanation, Does.Contain("receiver"));
            var baseValue = driver.Measurement.DisplayValue;
            if (driver.IsTimeHeld) Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00000004},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"source\":\"1\",\"receiver\":\"1\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 &&
                (driver.Measurement.Availability != MeasurementAvailability.Current ||
                 driver.Measurement.DisplayValue == baseValue); attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                driver.Session.ReadSnapshot());
            Assert.That(driver.Measurement.DisplayValue, Is.Not.EqualTo(baseValue));
            Assert.That(driver.Measurement.Explanation, Does.Contain("receiver"));
            Assert.That(driver.SelectMeasurementVessel("missing"), Is.False);
            Assert.That(driver.SelectedMeasurementVesselId, Is.EqualTo("receiver"));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator V01_LabLiquidVisualsFollowCommittedNativeTransfer()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var tool = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == "DesktopResearchTool");
            var receiver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == "ReceiverVessel");
            var sourceLiquid = tool.GetComponent<LiquidPresenter>();
            var receiverLiquid = receiver.GetComponent<LiquidPresenter>();
            Assert.That(sourceLiquid, Is.Not.Null);
            Assert.That(receiverLiquid, Is.Not.Null);
            Assert.That(driver.TryPrepareSourceStock("nacl", 0.1), Is.True);
            yield return null;
            for (var recoveryAttempt = 0; driver.IsTimeHeld && recoveryAttempt < 100; recoveryAttempt++)
            {
                driver.ResumeAfterTimeHold();
                yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.That(driver.IsTimeHeld, Is.False, "A fresh neutral baseline is required before transfer.");
            Assert.That(sourceLiquid.CommittedVolumeM3, Is.EqualTo(25e-6).Within(1e-12));
            Assert.That(receiverLiquid.CommittedVolumeM3, Is.Zero);
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00000005},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"source\":\"1\",\"receiver\":\"0\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 20 &&
                System.Math.Abs(sourceLiquid.CommittedVolumeM3 - 24.95e-6) > 1e-12; attempt++)
                yield return new WaitForSecondsRealtime(0.02f);
            Assert.That(sourceLiquid.CommittedVolumeM3, Is.EqualTo(24.95e-6).Within(1e-12));
            Assert.That(receiverLiquid.CommittedVolumeM3, Is.EqualTo(5e-8).Within(1e-12));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator D01_ColdDesktopLabShowsToolAndReceiverInCameraFrustum()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var children = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var camera = children.First(item => item.name == "DesktopCamera").GetComponent<Camera>();
            foreach (var name in new[] { "DesktopResearchTool", "ReceiverVessel" })
            {
                var target = children.First(item => item.name == name);
                var viewport = camera.WorldToViewportPoint(target.position + Vector3.up * 0.12f);
                Assert.That(viewport.z, Is.GreaterThan(0f), name);
                Assert.That(viewport.x, Is.InRange(0.1f, 0.9f), name);
                Assert.That(viewport.y, Is.InRange(0.1f, 0.9f), name);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator P05_03_ComputeHoldInLabKeepsSaveResponsiveAndMaterialUnchanged()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).First();
            sessions.Configure(driver, "playmode-hold-" + System.Guid.NewGuid().ToString("N"));
            try
            {
            Assert.That(sessions.SelectStock("hcl", 0.1), Is.True, sessions.LastError);
            for (var attempt = 0; driver.IsTimeHeld && attempt < 100; attempt++)
            {
                driver.ResumeAfterTimeHold();
                yield return new WaitForSecondsRealtime(0.01f);
            }
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            var before = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00001},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"source\":\"1\",\"receiver\":\"0\"}"), Does.Contain("ObservationVolumeHold"));
            StringAssert.Contains("\"reason\":\"Compute\"", driver.Session.ReadSnapshot());
            for (var attempt = 0; attempt < 50 && driver.HoldReason != "Compute"; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.HoldReason, Is.EqualTo("Compute"), driver.Session.ReadSnapshot());
            Assert.That(driver.NativeRecoveryReady, Is.EqualTo(driver.Session.ReadSnapshot().Contains("\"recoveryReady\":true")));
            Assert.That(sessions.Save(), Is.True, sessions.LastError);
            var savePath = sessions.LastDurableSavePath;
            try
            {
                Assert.That(File.Exists(savePath), Is.True);
                var held = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                Assert.That(held.eventSequence, Is.EqualTo(before.eventSequence));
                Assert.That(held.vessels.First(item => item.id == "source").inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(before.vessels.First(item => item.id == "source").inventory.researchAdditiveVolumeM3));
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
                if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            }
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator J02_SceneSaveLoadRestoresMaterialAndRequiresContinue()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).First();
            sessions.Configure(driver, "playmode-" + System.Guid.NewGuid().ToString("N"));
            Assert.That(sessions.SelectStock("nacl", 0.1), Is.True, sessions.LastError);
            Assert.That(sessions.Save(), Is.True, sessions.LastError);
            var savePath = sessions.LastDurableSavePath;
            try
            {
                for (var attempt = 0; driver.IsTimeHeld && attempt < 100; attempt++)
                {
                    driver.ResumeAfterTimeHold();
                    yield return new WaitForSecondsRealtime(0.01f);
                }
                Assert.That(driver.IsTimeHeld, Is.False);
                for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                    yield return new WaitForSecondsRealtime(0.01f);
                Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
                Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
                Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                    "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00000005},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                    "{\"source\":\"1\",\"receiver\":\"0\"}"), Does.Contain("\"accepted\":true"),
                    driver.Session.ReadSnapshot());
                Assert.That(sessions.ContinueFrom(savePath), Is.True, sessions.LastError);
                var snapshot = driver.Session.ReadSnapshot();
                StringAssert.Contains("\"reason\":\"SessionLoad\"", snapshot);
                StringAssert.Contains("\"researchAdditiveVolumeM3\":2.5e-05", snapshot);
                Assert.That(sessions.ContinueHeldSession(), Is.True, sessions.LastError);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
                if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator D04_FocusRecoveryContinueRestoresDesktopInputThroughSceneControl()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var adapter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DesktopInputAdapter>(true)).First();
            var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).First();
            driver.SendMessage("OnApplicationFocus", false);
            adapter.SendMessage("OnApplicationFocus", false);
            Assert.That(driver.IsTimeHeld, Is.True);
            Assert.That(adapter.AwaitingFocusContinue, Is.True);
            Assert.That(sessions.ContinueHeldSession(), Is.True, sessions.LastError);
            Assert.That(driver.IsTimeHeld, Is.False);
            Assert.That(adapter.AwaitingFocusContinue, Is.False);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator K01_LabSceneRecorderSamplesActualFrameDurations()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return new WaitForSecondsRealtime(0.05f);
            var recorder = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == "PerformanceRecorder").GetComponent<PlayerPerformanceRecorder>();
            Assert.That(recorder, Is.Not.Null);
            Assert.That(recorder.SampleCount, Is.GreaterThan(0));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator X03_SelectedHandTrackingLossCannotBeMaskedByOtherHand()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var xrRig = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == "XRPlayerRig").gameObject;
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ModeCoordinator>(true))
                .First().Select(ApplicationMode.VirtualReality);
            var left = xrRig.GetComponentsInChildren<XRDirectInteractor>(true).First(item => item.name.Contains("Left"));
            var grab = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<XRGrabInteractable>(true)).First();
            var adapter = xrRig.GetComponent<XRInputAdapter>();
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            adapter.SetEnabled(true);
            var injectedGamepad = InputSystem.AddDevice<Gamepad>();
            var actionControllers = xrRig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>(true);
            var leftAction = actionControllers.First(item => item.name.Contains("Left"));
            var rightAction = actionControllers.First(item => item.name.Contains("Right"));
            var leftTracking = new InputAction("Injected Left Tracking", InputActionType.Button, "<Gamepad>/leftTrigger");
            var rightTracking = new InputAction("Injected Right Tracking", InputActionType.Button, "<Gamepad>/rightTrigger");
            leftAction.isTrackedAction = new InputActionProperty(leftTracking);
            rightAction.isTrackedAction = new InputActionProperty(rightTracking);
            leftTracking.Enable();
            rightTracking.Enable();
            InputSystem.QueueStateEvent(injectedGamepad, new GamepadState { leftTrigger = 255, rightTrigger = 255 });
            InputSystem.Update();
            yield return null;
            Assert.That(injectedGamepad.rightTrigger.ReadValue(), Is.GreaterThan(0.5f));
            Assert.That(rightTracking.ReadValue<float>(), Is.GreaterThan(0.5f));
            Assert.That(rightAction.isTrackedAction.action.controls.Count, Is.GreaterThan(0));
            Assert.That(driver.ResumeAfterTimeHold(), Is.True);
            yield return null;
            left.StartManualInteraction((IXRSelectInteractable)grab);
            yield return null;
            Assert.That(grab.isSelected, Is.True);
            Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            InputSystem.QueueStateEvent(injectedGamepad, new GamepadState { leftTrigger = 0, rightTrigger = 255 });
            InputSystem.Update();
            yield return null;
            Assert.That(leftAction.isTrackedAction.action.ReadValue<float>(), Is.LessThan(0.5f));
            Assert.That(rightAction.isTrackedAction.action.ReadValue<float>(), Is.GreaterThan(0.5f));
            Assert.That(grab.firstInteractorSelecting.transform.IsChildOf(leftAction.transform), Is.True);
            var lossSample = adapter.SampleInputs((ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000_000.0));
            Assert.That(lossSample[0].trackingValid, Is.False);
            Assert.That(driver.IsTimeHeld, Is.True);
            var holdSnapshot = driver.Session.ReadSnapshot();
            Assert.That(holdSnapshot.Contains("\"reason\":\"TrackingLoss\"") ||
                holdSnapshot.Contains("\"reason\":\"TimeDiscontinuity\""), Is.True, holdSnapshot);
            Assert.That(driver.ResumeAfterTimeHold(), Is.False,
                "A selected hand with lost tracking must prevent recovery even if a slow frame caused the first hold.");
            left.EndManualInteraction();
            InputSystem.RemoveDevice(injectedGamepad);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
