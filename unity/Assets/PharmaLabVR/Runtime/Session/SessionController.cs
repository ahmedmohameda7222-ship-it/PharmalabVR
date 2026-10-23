using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Lab;
using PharmaLabVR.UI;
using PharmaLabVR.Tools;
using PharmaLabVR.Input;
using PharmaLabVR.Protocols;
using UnityEngine;

namespace PharmaLabVR.Session
{
    public sealed class SessionController : MonoBehaviour
    {
        [SerializeField] private CoreDriver driver;
        [SerializeField] private string saveName = "autosave";
        [SerializeField] private string checkpointId = "student-checkpoint";
        public string LastDurableSavePath { get; private set; }
        public string LastPortableArchivePath { get; private set; }
        public string LastCsvExportPath { get; private set; }
        public string LastLearnerCsvExportPath { get; private set; }
        public InstructorPortableReview LastInstructorPortableReview { get; private set; }
        public string InstructorPortableSummary =>
            LastInstructorPortableReview?.Summary ?? "No offline instructor preview selected.";
        private int instructorAnnotationIndex;
        public bool CanNextInstructorAnnotation =>
            router?.Current?.ScreenId == ScreenId.Review &&
            LastInstructorPortableReview?.StudentAnnotations.Count > 1;
        public bool NextInstructorAnnotation()
        {
            if (!CanNextInstructorAnnotation) return false;
            instructorAnnotationIndex =
                (instructorAnnotationIndex + 1) %
                LastInstructorPortableReview.StudentAnnotations.Count;
            router.Show(ScreenId.Review);
            return true;
        }
        public string InstructorPortableCompactSummary =>
            LastInstructorPortableReview == null ?
                "No offline instructor preview selected." :
                LastInstructorPortableReview.CompactSummary +
                (LastInstructorPortableReview.StudentAnnotations.Count == 0 ?
                    string.Empty : "\n" + ResearchLearningReview.RenderPage(
                        LastInstructorPortableReview.StudentAnnotations,
                        LastInstructorPortableReview.NativeBranchId,
                        instructorAnnotationIndex));
        private string selectedArchiveFileName;
        public string SelectedArchiveFileName => selectedArchiveFileName;
        public string LastPortableImportSummary { get; private set; }
        private bool CanUseLocalArchives =>
            router?.Current?.ScreenId == ScreenId.Review ||
            router?.Current?.ScreenId == ScreenId.Pause ||
            router?.Current?.ScreenId == ScreenId.Hold;
        private IReadOnlyList<string> AvailableLocalArchiveNames()
        {
            var root = Path.Combine(Application.persistentDataPath, "sessions");
            if (!Directory.Exists(root)) return Array.Empty<string>();
            // Bounded, top-level only; never list outside the app's offline folder.
            // New immutable student exports must remain selectable even if
            // older research packages exceed the 64-item XR display limit.
            // The picker validates actual files, forbids symlinks/traversal,
            // and orders by durable local modification time, newest first.
            return OfflineArchivePicker.NamesNewest(root,
                Directory.EnumerateFiles(root, "*.plvpack",
                    SearchOption.TopDirectoryOnly), 64);
        }

        public bool CanChooseLocalPortableArchive
        {
            get
            {
                if (!CanUseLocalArchives) return false;
                try { return AvailableLocalArchiveNames().Count > 0; }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }
        public bool CanRestoreLocalPortableSession
        {
            get
            {
                var screen = router?.Current?.ScreenId;
                if (screen != ScreenId.Pause && screen != ScreenId.Hold ||
                    driver?.Session == null || !driver.Session.IsOpen ||
                    (driver.CurrentMode != "Desktop" && driver.CurrentMode != "VR") ||
                    (driver.CurrentMode == "VR" && !CanReleaseTool) ||
                    !OfflineArchivePicker.ValidName(selectedArchiveFileName))
                    return false;
                try { return AvailableLocalArchiveNames().Contains(selectedArchiveFileName); }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

        public bool CanPreviewLocalPortableArchive
        {
            get
            {
                if (router?.Current?.ScreenId != ScreenId.Review) return false;
                try
                {
                    return (!string.IsNullOrEmpty(selectedArchiveFileName) &&
                        AvailableLocalArchiveNames().Contains(selectedArchiveFileName)) ||
                        (!string.IsNullOrWhiteSpace(LastPortableArchivePath) &&
                        File.Exists(LastPortableArchivePath));
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }
        public bool ChooseNextLocalArchive()
        {
            try
            {
                if (!CanUseLocalArchives)
                    throw new InvalidOperationException(
                        "Choose a local archive in recorded Review or paused/held Lab.");
                var names = AvailableLocalArchiveNames();
                if (names.Count == 0)
                    throw new InvalidDataException("No local .plvpack files are available.");
                selectedArchiveFileName =
                    OfflineArchivePicker.NextName(names, selectedArchiveFileName);
                LastInstructorPortableReview = null;
                instructorAnnotationIndex = 0;
                LastError = null;
                // Choosing an archive from Native Pause/SessionLoad Hold
                // must NOT navigate to Review: student RestorePortable is
                // supported only on the original guarded screen.
                router.Show(router.Current.ScreenId);
                return true;
            }
            catch (Exception error)
            {
                LastError = "Offline archive selection unavailable: " + error.Message;
                return false;
            }
        }

        public bool PreviewLocalPortableArchive(string fileName)
        {
            try
            {
                if (router?.Current?.ScreenId != ScreenId.Review)
                    throw new InvalidOperationException(
                        "Offline teaching preview requires recorded Review.");
                var requested = string.IsNullOrWhiteSpace(fileName) ?
                    selectedArchiveFileName ?? Path.GetFileName(LastPortableArchivePath) : fileName;
                if (!OfflineArchivePicker.ValidName(requested))
                    throw new InvalidDataException(
                        "Choose a safe .plvpack filename in the local sessions folder.");
                var localPath = Path.Combine(
                    Application.persistentDataPath, "sessions", requested);
                if ((File.GetAttributes(localPath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("An external link cannot be previewed.");
                // No Native import, network request, or grading transition.
                var preview = InstructorPortableReview.Open(localPath);
                LastInstructorPortableReview = preview;
                instructorAnnotationIndex = 0;
                LastError = null;
                router.Show(ScreenId.Review);
                return true;
            }
            catch (Exception error)
            {
                LastInstructorPortableReview = null;
                instructorAnnotationIndex = 0;
                LastError = "Offline teaching preview unavailable: " + error.Message;
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return false;
            }
        }
        // Local student restore from an existing .plvpack is separate from
        // read-only instructor Preview. No cloud, grading or attestation.
        public bool RestoreSelectedLocalPortableSession()
        {
            CoreSession imported = null;
            var priorSaveName = saveName;
            string stagedSave = null;
            string stagedNotes = null;
            var adopted = false;
            try
            {
                if (!CanRestoreLocalPortableSession)
                    throw new InvalidOperationException(
                        "Choose a local study pack in Pause/Hold; VR tools must be docked.");
                var root = Path.Combine(Application.persistentDataPath, "sessions");
                var archivePath = Path.Combine(root, selectedArchiveFileName);
                if ((File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked or external packages cannot be restored.");
                var data = PortableSessionArchive.Read(archivePath);
                // The checksum is NOT a signature. Pinned IPhreeqc and
                // the imported Native scientific identity decide live trust.
                imported = CoreSession.ImportSession(data.NativeJson,
                    driver.ScientificDatabasePath);
                if (!string.Equals(imported.BranchId, data.BranchId,
                        StringComparison.Ordinal))
                    throw new InvalidDataException("Imported Native branch identity disagrees.");
                var importedView = LabViewModel.FromNativeSnapshot(imported.ReadSnapshot());
                if (importedView == null ||
                    !string.Equals(importedView.EventSequence, data.EventSequence,
                        StringComparison.Ordinal))
                    throw new InvalidDataException("Imported Native event revision disagrees.");

                // Separate UUID namespace: an imported classmate's pack cannot
                // overwrite this student's current save or Notebook. All
                // journal bytes are durably staged before touching live Native.
                var name = "portable-" + Guid.NewGuid().ToString("N");
                stagedNotes = Path.Combine(root, name + ".plv-notes");
                var expectedSave = Path.Combine(root, name + ".plv.json");
                if (File.Exists(stagedNotes) || File.Exists(expectedSave) ||
                    File.Exists(stagedNotes + ".bak") || File.Exists(expectedSave + ".bak"))
                    throw new IOException("Portable restore storage identity collision.");
                NotebookService.InstallPortableSnapshot(stagedNotes,
                    data.NotebookSnapshot, data.BranchId, data.EventSequence);
                stagedSave = saves.Save(imported, root, name);

                // Must switch the journal path BEFORE the restored Native
                // snapshot is published: the lesson recovery subscriber needs
                // this imported package's authenticated Notebook, not the old one.
                saveName = name;
                driver.ReplaceSession(imported);
                adopted = true;
                imported = null;
                LastDurableSavePath = stagedSave;
                LastPortableImportSummary = "Restored local package · Native branch " +
                    data.BranchId + " · event " + data.EventSequence +
                    ". New isolated offline session; Native SessionLoad Hold remains active. " +
                    "Checksums are not identity or scientific attestation.";
                LastError = null;
                RecordedReview = null;
                CurrentComparison = null;
                router?.MarkSaved();
                return true;
            }
            catch (Exception error)
            {
                // Never dispose a session the Native driver actually adopted.
                if (imported != null &&
                    !ReferenceEquals(imported, driver?.Session))
                    imported.Dispose();
                adopted = adopted ||
                    (imported != null && ReferenceEquals(imported, driver?.Session));
                if (!adopted)
                {
                    saveName = priorSaveName;
                    foreach (var candidate in new[] { stagedSave, stagedNotes })
                        if (!string.IsNullOrEmpty(candidate))
                            try { if (File.Exists(candidate)) File.Delete(candidate); }
                            catch (IOException) { }
                            catch (UnauthorizedAccessException) { }
                }
                LastPortableImportSummary = null;
                LastError = "Portable student restore refused: " + error.Message;
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return false;
            }
        }

        public string LastError { get; private set; }
        // Exact accepted Native command identity, not a predicted material result.
        public string LastInstrumentReceipt { get; private set; }

        public bool CanRecordCurrentNativePH
        {
            get
            {
                if (driver?.Session == null || !driver.Session.IsOpen ||
                    driver.IsTimeHeld || driver.IsNativePaused ||
                    (router?.Current?.ScreenId != ScreenId.Inspect &&
                     router?.Current?.ScreenId != ScreenId.Measure))
                    return false;
                try
                {
                    var view = driver.CurrentLabView;
                    var vesselId = driver.SelectedMeasurementVesselId;
                    NativeMeasurementNote.Build(view, vesselId,
                        driver.Measurement?.Availability ?? MeasurementAvailability.Unsupported,
                        driver.Measurement?.DisplayValue);
                    if (router.Current.SelectedEntityId != vesselId ||
                        router.Current.NativeRevision != view.EventSequence)
                        return false;
                    var vessel = view.Vessels.First(v => v.Id == vesselId);
                    var notes = notebook.ReadCurrent(NotebookFilePath(),
                        driver.Session.BranchId, view.EventSequence);
                    return !NativeMeasurementNote.WasRecorded(notes,
                        driver.Session.BranchId, view.EventSequence, vesselId,
                        vessel.MaterialRevision);
                }
                catch (Exception error) when (error is InvalidOperationException ||
                                              error is IOException ||
                                              error is UnauthorizedAccessException ||
                                              error is InvalidDataException ||
                                              error is ArgumentException)
                {
                    return false;
                }
            }
        }

        // A measurement is an offline notebook annotation tied to the exact
        // Native branch, event and material revision. It is never a grade.
        public bool RecordCurrentNativeMeasurement()
        {
            try
            {
                if (!CanRecordCurrentNativePH)
                    throw new InvalidOperationException(
                        "A current, revision-matched, previously unrecorded Native pH reading is required.");
                var view = driver.CurrentLabView;
                var vessel = driver.SelectedMeasurementVesselId;
                var note = NativeMeasurementNote.Build(view, vessel,
                    driver.Measurement.Availability, driver.Measurement.DisplayValue);
                if (vessel == "receiver" && !string.IsNullOrEmpty(selectedResearchPrediction) &&
                    selectedResearchLesson != null &&
                    selectedResearchLesson.BranchId == driver.CurrentNativeBranchId)
                    note += "\n" + ResearchLessonJourney.CompareRecordedPrediction(
                        selectedResearchPrediction, view, vessel,
                        driver.Measurement.Availability, driver.Measurement.DisplayValue);
                notebook.Append(NotebookFilePath(), driver.Session.BranchId,
                    view.EventSequence, vessel, note);
                InvalidateLearningReview();
                LastError = null;
                router.Show(ScreenId.Notebook);
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return false;
            }
        }
        public bool CanRecordResearchExplanation
        {
            get
            {
                if (driver?.Session == null || !driver.Session.IsOpen ||
                    selectedResearchLesson == null ||
                    selectedResearchLesson.BranchId != driver.Session.BranchId ||
                    driver.IsNativePaused || driver.IsTimeHeld ||
                    router?.Current?.ScreenId != ScreenId.Notebook ||
                    driver.SelectedMeasurementVesselId != "receiver" ||
                    driver.CurrentLabView == null ||
                    router.Current.NativeRevision != driver.CurrentLabView.EventSequence)
                    return false;
                try
                {
                    NativeMeasurementNote.Build(driver.CurrentLabView, "receiver",
                        driver.Measurement?.Availability ?? MeasurementAvailability.Unsupported,
                        driver.Measurement?.DisplayValue);
                    var recorded = notebook.ReadCurrent(NotebookFilePath(),
                        driver.Session.BranchId, driver.CurrentLabView.EventSequence);
                    return recorded.Any(item => item.BranchId == driver.Session.BranchId &&
                        item.VesselId == "receiver" &&
                        item.EventSequence == driver.CurrentLabView.EventSequence &&
                        item.Text.StartsWith("Native pH ", StringComparison.Ordinal)) &&
                        !ResearchLessonJourney.HasRecordedExplanation(recorded,
                            selectedResearchLesson.Definition.id,
                            driver.Session.BranchId, driver.CurrentLabView.EventSequence);
                }
                catch (Exception error) when (error is InvalidOperationException ||
                                              error is IOException ||
                                              error is UnauthorizedAccessException ||
                                              error is InvalidDataException ||
                                              error is ArgumentException)
                {
                    return false;
                }
            }
        }

        public bool RecordResearchExplanation(string choice)
        {
            try
            {
                if (!CanRecordResearchExplanation ||
                    !ResearchLessonJourney.IsSupportedExplanation(choice))
                    throw new InvalidOperationException(
                        "A current recorded Native pH is required before choosing one learner explanation.");
                var view = driver.CurrentLabView;
                var note = ResearchLessonJourney.BuildRecordedExplanation(
                    selectedResearchLesson.Definition.id, choice, view, "receiver",
                    driver.Measurement.Availability, driver.Measurement.DisplayValue);
                // A second journal read inside CanRecordResearchExplanation prevents
                // normal UI retries from overwriting the first durable choice.
                notebook.Append(NotebookFilePath(), driver.Session.BranchId,
                    view.EventSequence, "receiver", note);
                InvalidateLearningReview();
                LastError = null;
                router.Show(ScreenId.Notebook);
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return false;
            }
        }

        private readonly NativeReviewAuthorityCache<RecordedSessionReview,
            RecordedRunComparison> recordedCache = new();
        private CoreDriver subscribedNativeDriver;
        private CoreDriver observedLessonDriver;
        private ulong observedLessonEpoch;
        private ulong? pendingLessonRecoveryEpoch;
        private string lastLessonRecoveryError;

        public RecordedSessionReview RecordedReview
        {
            get
            {
                ReconcileNativeReviewAuthority();
                return recordedCache.Review;
            }
            private set
            {
                ReconcileNativeReviewAuthority();
                recordedCache.Review = value;
            }
        }

        public RecordedRunComparison CurrentComparison
        {
            get
            {
                ReconcileNativeReviewAuthority();
                return recordedCache.Comparison;
            }
            private set
            {
                ReconcileNativeReviewAuthority();
                recordedCache.Comparison = value;
            }
        }
        private readonly SaveService saves = new();
        private readonly NotebookService notebook = new();
        private RecordedSessionReview cachedLearningReviewSource;
        private string cachedLearningReviewFull;
        private string cachedLearningReviewCompact;

        private void InvalidateLearningReview()
        {
            cachedLearningReviewSource = null;
            cachedLearningReviewFull = null;
            cachedLearningReviewCompact = null;
        }

        public string LearningReviewSummary => LearningReview(false);
        public string LearningReviewCompact => LearningReview(true);

        private string LearningReview(bool compact)
        {
            var recorded = RecordedReview;
            if (recorded == null) return "Recorded study review unavailable.";
            if (!ReferenceEquals(recorded, cachedLearningReviewSource))
            {
                // A recoverable saved branch never borrows the live branch's notes.
                if (driver?.Session == null || !driver.Session.IsOpen ||
                    recorded.BranchId != driver.Session.BranchId)
                {
                    cachedLearningReviewFull =
                        "Local student notebook unavailable for this recorded branch.";
                    cachedLearningReviewCompact = cachedLearningReviewFull;
                }
                else
                {
                    try
                    {
                        var path = NotebookFilePath();
                        // Both variants read branch- and event-bounded,
                        // checksummed annotations from the exact session file.
                        cachedLearningReviewFull =
                            ResearchLearningReview.ReadRecordedJournal(
                                notebook, path, driver.Session.BranchId,
                                recorded.BranchId, recorded.EventSequence, false);
                        cachedLearningReviewCompact =
                            ResearchLearningReview.ReadRecordedJournal(
                                notebook, path, driver.Session.BranchId,
                                recorded.BranchId, recorded.EventSequence, true);
                    }
                    catch (Exception error) when (error is IOException ||
                            error is UnauthorizedAccessException ||
                            error is ArgumentException ||
                            error is InvalidOperationException)
                    {
                        // Invalid paths cannot crash an otherwise valid
                        // recorded Native Review or reveal journal bytes.
                        cachedLearningReviewFull =
                            "Local notebook unavailable: offline journal could not be verified.";
                        cachedLearningReviewCompact = cachedLearningReviewFull;
                    }
                }
                cachedLearningReviewSource = recorded;
            }
            return compact ? cachedLearningReviewCompact : cachedLearningReviewFull;
        }
        [SerializeField] private TextAsset strongAcidProtocol;
        [SerializeField] private TextAsset mixedAcidProtocol;
        private ResearchLessonJourney selectedResearchLesson;
        private string selectedResearchPrediction;
        public bool CanRecordResearchPrediction =>
            driver?.Session != null && driver.Session.IsOpen &&
            router?.Current?.ScreenId == ScreenId.Lab &&
            selectedResearchLesson?.CanPredict(driver.CurrentNativeBranchId,
                driver.CurrentLabView, driver.IsTimeHeld, selectedResearchPrediction) == true;
        public string ResearchPredictionSummary => selectedResearchLesson == null ?
            "Select a Research lesson before making a prediction." :
            string.IsNullOrEmpty(selectedResearchPrediction) ?
                "Prediction not recorded. Choose before transferring material." :
                "Student prediction recorded: " +
                (selectedResearchPrediction == "below-7" ? "receiver pH below 7" :
                 "receiver pH at least 7") + " (not a Native result).";

        public bool RecordResearchPrediction(string prediction)
        {
            try
            {
                if (!CanRecordResearchPrediction ||
                    !ResearchLessonJourney.IsSupportedPrediction(prediction))
                    throw new InvalidOperationException(
                        "Choose one supported prediction before transferring receiver material.");
                var expected = prediction == "below-7" ? "below 7" : "at least 7";
                var note = "Student hypothesis for " + selectedResearchLesson.Definition.id +
                    ": receiver pH predicted " + expected +
                    ". This is a learner prediction, not a Native result or certified endpoint.";
                var view = driver.CurrentLabView;
                if (view.EventSequence != router.Current.NativeRevision)
                    throw new InvalidOperationException("Native event changed before prediction.");
                notebook.Append(NotebookFilePath(), driver.Session.BranchId,
                    view.EventSequence, "receiver", note);
                InvalidateLearningReview();
                selectedResearchPrediction = prediction; // only after durable note append
                LastError = null;
                router.Show(ScreenId.Lab);
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return false;
            }
        }
        public string ResearchLessonSummary => selectedResearchLesson == null ?
            "Free laboratory · no lesson selected. Choose a Research lesson to follow its procedure." :
            selectedResearchLesson.Summary(driver?.CurrentNativeBranchId,
                driver?.CurrentLabView, driver?.IsNativePaused == true, driver?.IsTimeHeld == true) +
            "\n" + selectedResearchLesson.EvidenceChecklist(
                driver?.CurrentNativeBranchId, driver?.CurrentLabView,
                ReadNotebookEntries());
        public string ResearchLessonCompact => selectedResearchLesson == null ?
            "Choose a Research lesson (not graded)." :
            selectedResearchLesson.CompactEvidenceStatus(
                driver?.CurrentNativeBranchId, driver?.CurrentLabView,
                ReadNotebookEntries(),
                paused: driver?.IsNativePaused == true && driver?.SourceNeedsPreparation != true,
                held: driver?.IsTimeHeld == true);
        public string ResearchLessonSteps => selectedResearchLesson == null ?
            string.Empty : selectedResearchLesson.Instructions;
        public bool HasResearchLesson => selectedResearchLesson != null;
        public void ConfigureResearchLessons(TextAsset strongAcid, TextAsset mixedAcid)
        {
            strongAcidProtocol = strongAcid;
            mixedAcidProtocol = mixedAcid;
        }

        public bool SelectResearchLesson(string id)
        {
            try
            {
                if (driver?.Session == null || router?.Current?.ScreenId != ScreenId.Lab ||
                    driver.IsTimeHeld)
                    throw new InvalidOperationException("Choose a lesson in the active Native Lab.");
                var file = id == "strong-acid-titration-research" ? strongAcidProtocol :
                    id == "mixed-acid-titration-research" ? mixedAcidProtocol : null;
                if (file == null) throw new InvalidOperationException("Research lesson asset unavailable.");
                var definition = JsonUtility.FromJson<ProtocolDefinition>(file.text);
                if (definition == null || definition.id != id)
                    throw new InvalidOperationException("Research lesson identity mismatch.");
                var lesson = new ResearchLessonJourney(
                    definition, driver.CurrentNativeBranchId);
                var recoveredPrediction = ResearchLessonJourney.RecoverSavedPrediction(
                    notebook.ReadCurrent(NotebookFilePath(),
                        driver.Session.BranchId, driver.CurrentLabView?.EventSequence),
                    lesson.Definition.id, driver.Session.BranchId,
                    driver.CurrentLabView?.EventSequence);
                // Rebuild memory only after durable journal recovery succeeds.
                selectedResearchLesson = lesson;
                selectedResearchPrediction = recoveredPrediction;
                LastError = null;
                if (router?.Current != null) router.Show(ScreenId.Lab);
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return false;
            }
        }
        public static Rect DesktopHudBounds => new Rect(12f, 12f,
            Mathf.Max(1f, Mathf.Min(370f, Screen.width - 24f)),
            Mathf.Max(1f, Mathf.Min(620f, Screen.height - 24f)));
        private int selectedConcentrationIndex = 2;
        private Vector2 hudScrollPosition;
        private string notebookDraft = string.Empty;
        public string ResearchConceptSummary =>
            ResearchConceptReadout.Describe(
                driver?.CurrentLabView,
                router?.Current?.SelectedEntityId ?? "receiver");
        private string instructorArchiveDraft = string.Empty;
        private ScreenRouter router;
        private LabPanelController panels;
        private SpatialPanelPresenter spatialPanel;

        private void Awake()
        {
            ReconcileNativeReviewAuthority();
            router = GetComponent<ScreenRouter>();
            if (router == null) router = gameObject.AddComponent<ScreenRouter>();
            router.Configure(driver);
            panels = GetComponent<LabPanelController>();
            if (panels == null) panels = gameObject.AddComponent<LabPanelController>();
            panels.Configure(router, driver, this);
            spatialPanel = GetComponent<SpatialPanelPresenter>();
            if (spatialPanel == null) spatialPanel = gameObject.AddComponent<SpatialPanelPresenter>();
            spatialPanel.Configure(router, panels, driver);
        }

        public void Configure(CoreDriver coreDriver, string sessionName = null)
        {
            if (!ReferenceEquals(driver, coreDriver))
            {
                DetachNativeRestoration();
                selectedResearchLesson = null;
                selectedResearchPrediction = null;
                pendingLessonRecoveryEpoch = null;
                observedLessonDriver = null;
                InvalidateLearningReview();
            }
            driver = coreDriver;
            ReconcileNativeReviewAuthority();
            if (isActiveAndEnabled)
                AttachNativeRestoration();
            ReconcileLessonAuthority();
            router?.Configure(driver);
            panels?.Configure(router, driver, this);
            spatialPanel?.Configure(router, panels, driver);
            if (!string.IsNullOrEmpty(sessionName) &&
                !string.Equals(saveName, sessionName, StringComparison.Ordinal))
            {
                // A different saved-file identity cannot reuse an old
                // baseline comparison, even with the same Native driver.
                recordedCache.InvalidateComparison();
                // Reusing a Native driver does not authorize notes cached
                // under the previous session filename.
                InvalidateLearningReview();
                LastInstructorPortableReview = null;
                LastPortableArchivePath = null;
                selectedArchiveFileName = null;
                instructorArchiveDraft = string.Empty;
                saveName = sessionName;
                selectedResearchLesson = null;
                selectedResearchPrediction = null;
                pendingLessonRecoveryEpoch = null;
            }
        }

        private void OnEnable()
        {
            ReconcileNativeReviewAuthority();
            AttachNativeRestoration();
            ReconcileLessonAuthority();
        }

        private void OnDisable() => DetachNativeRestoration();
        private void OnDestroy() => DetachNativeRestoration();

        private void AttachNativeRestoration()
        {
            if (ReferenceEquals(subscribedNativeDriver, driver)) return;
            DetachNativeRestoration();
            if (driver == null) return;
            driver.NativeSessionRestored += OnNativeSessionRestored;
            driver.LabViewChanged += OnRestoredLabView;
            subscribedNativeDriver = driver;
        }

        private void DetachNativeRestoration()
        {
            if (subscribedNativeDriver != null)
            {
                subscribedNativeDriver.NativeSessionRestored -= OnNativeSessionRestored;
                subscribedNativeDriver.LabViewChanged -= OnRestoredLabView;
            }
            subscribedNativeDriver = null;
        }

        private void OnNativeSessionRestored()
        {
            LastInstrumentReceipt = null;
            LastPortableImportSummary = null;
            LastDurableSavePath = null;
            LastPortableArchivePath = null;
            LastCsvExportPath = null;
            LastLearnerCsvExportPath = null;
            // A restored session must not inherit unsaved in-memory guidance.
            // Rebuild only after the authoritative replacement Native snapshot.
            selectedResearchLesson = null;
            selectedResearchPrediction = null;
            observedLessonDriver = driver;
            observedLessonEpoch = driver != null ? driver.NativeRestorationEpoch : 0UL;
            pendingLessonRecoveryEpoch = driver != null ?
                driver.NativeRestorationEpoch : (ulong?)null;
            lastLessonRecoveryError = null;
            LastInstructorPortableReview = null;
            instructorAnnotationIndex = 0;
            selectedArchiveFileName = null;
            InvalidateLearningReview();
            ReconcileNativeReviewAuthority();
        }

        private void OnRestoredLabView(LabViewModel _) => ReconcileLessonAuthority();

        private void ReconcileLessonAuthority()
        {
            if (driver == null) return;
            if (!ReferenceEquals(observedLessonDriver, driver))
            {
                observedLessonDriver = driver;
                observedLessonEpoch = driver.NativeRestorationEpoch;
            }
            else if (observedLessonEpoch != driver.NativeRestorationEpoch)
            {
                // A disabled session panel can miss the restore signal.
                OnNativeSessionRestored();
            }
            if (pendingLessonRecoveryEpoch == null ||
                pendingLessonRecoveryEpoch.Value != driver.NativeRestorationEpoch ||
                driver.NativePublishedRestorationEpoch != driver.NativeRestorationEpoch ||
                driver.CurrentLabView == null)
                return;
            pendingLessonRecoveryEpoch = null; // one bounded recovery attempt
            try
            {
                if (driver.Session == null || !driver.Session.IsOpen)
                    throw new InvalidOperationException("No restored Native session is available.");
                var branch = driver.Session.BranchId;
                if (!string.Equals(driver.CurrentNativeBranchId, branch, StringComparison.Ordinal))
                    throw new InvalidOperationException("Restored Native branch mismatch.");
                var eventSequence = driver.CurrentLabView.EventSequence;
                var entries = notebook.ReadCurrent(NotebookFilePath(), branch, eventSequence);
                var lessonId = ResearchLessonJourney.RecoverSavedLessonId(
                    entries, branch, eventSequence);
                if (lessonId == null)
                {
                    if (ResearchLessonJourney.HasAmbiguousSavedPredictions(
                            entries, branch, eventSequence))
                    {
                        lastLessonRecoveryError =
                            "Saved learner guidance is ambiguous: conflicting local " +
                            "hypotheses at this Native checkpoint. Inspect the Notebook; " +
                            "no prediction or lesson was auto-selected.";
                        LastError = lastLessonRecoveryError;
                    }
                    return;
                }
                var asset = lessonId == "strong-acid-titration-research" ?
                    strongAcidProtocol : mixedAcidProtocol;
                if (asset == null)
                    throw new InvalidOperationException("Saved Research lesson asset is unavailable.");
                var definition = JsonUtility.FromJson<ProtocolDefinition>(asset.text);
                if (definition == null || definition.id != lessonId)
                    throw new InvalidOperationException("Saved Research lesson identity mismatch.");
                var recovered = new ResearchLessonJourney(definition, branch);
                var prediction = ResearchLessonJourney.RecoverSavedPrediction(
                    entries, lessonId, branch, eventSequence);
                if (!ResearchLessonJourney.IsSupportedPrediction(prediction))
                    throw new InvalidOperationException("Saved student hypothesis is unsupported.");
                selectedResearchLesson = recovered;
                selectedResearchPrediction = prediction;
            }
            catch (Exception error)
            {
                selectedResearchLesson = null;
                selectedResearchPrediction = null;
                lastLessonRecoveryError =
                    "Saved Research guidance unavailable: " + error.Message;
                LastError = lastLessonRecoveryError;
            }
        }

        private void ReconcileNativeReviewAuthority()
        {
            // This check also handles restoring while the controller is
            // disabled, unsubscribed or temporarily missing the event.
            recordedCache.Observe(driver, driver != null ?
                driver.NativeRestorationEpoch : 0UL);
        }

        public bool CanReleaseTool
        {
            get
            {
                if (driver == null || driver.CurrentMode != "VR") return false;
                var dock = driver.transform.root.GetComponentInChildren<ToolMount>(true);
                return dock != null && dock.IsMounted;
            }
        }

        public bool CanParkTool
        {
            get
            {
                if (driver?.Session == null || driver.CurrentMode != "VR") return false;
                var dock = driver.transform.root.GetComponentInChildren<ToolMount>(true);
                return dock != null && !dock.IsMounted;
            }
        }

        public bool CanLoadSavedSession
        {
            get
            {
                if (driver?.Session == null) return false;
                // The dock is a VR-only safety prerequisite. Desktop has no
                // physical tool dock, but still loads only via Pause/Hold.
                if (driver.CurrentMode == "VR" && !CanReleaseTool) return false;
                if (driver.CurrentMode != "VR" && driver.CurrentMode != "Desktop")
                    return false;
                var file = Path.Combine(Application.persistentDataPath, "sessions",
                    saveName + ".plv.json");
                return SaveService.HasSaveCandidate(file);
            }
        }

        public bool LoadSavedSession()
        {
            var screen = router?.Current?.ScreenId;
            if (screen != ScreenId.Pause && screen != ScreenId.Hold) return false;
            if (!CanLoadSavedSession)
            {
                LastError = driver?.CurrentMode == "VR"
                    ? "Load requires a parked VR tool and an existing saved session."
                    : "Load requires an existing saved session.";
                router.Show(screen.Value);
                return false;
            }
            var file = Path.Combine(Application.persistentDataPath, "sessions",
                saveName + ".plv.json");
            var result = ContinueFrom(file);
            if (!result && router?.Current != null) router.Show(router.Current.ScreenId);
            return result;
        }

        public bool ParkToolAtDock()
        {
            var screen = router?.Current?.ScreenId;
            if (!CanParkTool || (screen != ScreenId.Lab &&
                screen != ScreenId.Pause && screen != ScreenId.Hold)) return false;
            var mount = driver.transform.root.GetComponentInChildren<ToolMount>(true);
            if (mount == null || !mount.TryMount()) return false;
            // Presentation/physics only. Do not Save, submit Native PlaceTool,
            // update observation revisions or silently release a Native hold.
            router.Show(screen.Value);
            return true;
        }

        public bool ReleaseToolFromDock()
        {
            if (!CanReleaseTool || router?.Current?.ScreenId != ScreenId.Lab) return false;
            var dock = driver.transform.root.GetComponentInChildren<ToolMount>(true);
            if (dock == null || !dock.ReleaseFromDock()) return false;
            router.Show(ScreenId.Lab);
            return true;
        }

        public bool TrySnapTurn(int direction)
        {
            if (direction != -1 && direction != 1 || !CanAlignXrView(out var alignment))
                return false;
            return alignment.TrySnapTurn(direction);
        }

        public bool TryRecenterView()
        {
            if (!CanAlignXrView(out var alignment)) return false;
            // Same Native-held safety boundary as snap-turn.
            return alignment.TryRecenter();
        }

        private bool CanAlignXrView(out RecoveryAlignment alignment)
        {
            alignment = null;
            if (driver?.Session == null || router?.Current == null) return false;
            var root = driver.transform.root;
            var dock = root.GetComponentInChildren<ToolMount>(true);
            alignment = root.GetComponentInChildren<RecoveryAlignment>(true);
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            var tracked = head.isValid &&
                head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracking) && tracking;
            if (!RecoveryAlignmentPolicy.CanRecenter(driver.CurrentMode == "VR",
                    driver.IsNativePaused, driver.IsTimeHeld, tracked,
                    dock != null && dock.IsMounted)) return false;
            // Neither view operation submits Native Continue or changes material.
            // Continue still needs fresh neutral input through the Native authority.
            return alignment != null;
        }

        public bool RequestDesktopPause()
        {
            if (driver?.Session == null || driver.CurrentMode != "Desktop" ||
                router?.Current == null || panels == null) return false;
            return panels.Request(PanelAction.Pause);
        }

        public static bool PointerOverDesktopHud(Vector2 screenPosition)
        {
            return DesktopHudBounds.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private void OnGUI()
        {
            if (driver == null || driver.CurrentMode != "Desktop") return;
            var previousLabelSize = GUI.skin.label.fontSize;
            var previousButtonSize = GUI.skin.button.fontSize;
            try
            {
                var textScale = router?.Current?.Setup.TextScale ?? 1f;
                GUI.skin.label.fontSize = Mathf.RoundToInt(14f * textScale);
                GUI.skin.button.fontSize = Mathf.RoundToInt(14f * textScale);
                GUILayout.BeginArea(DesktopHudBounds, GUI.skin.box);
            hudScrollPosition = GUILayout.BeginScrollView(hudScrollPosition);
            GUILayout.Label("PharmaLabVR · Research Lab");
            var screen = router?.Current?.ScreenId ?? ScreenId.Lab;
            GUILayout.Label(screen.ToString());
            if (!string.IsNullOrEmpty(driver.StartupError))
            {
                GUILayout.Label("Lab unavailable: " + driver.StartupError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Hold)
            {
                GUILayout.Label("Paused: " + driver.HoldReason + ". Close the valve and restore tracking.");
                var desktopInput = driver.transform.root.GetComponentInChildren<DesktopInputAdapter>(true);
                if (driver.CurrentMode == "Desktop" && desktopInput != null &&
                    !desktopInput.HasFreshPhysicalGestureSinceRestore)
                    GUILayout.Label("Session restored: release the held instrument, all keyboard keys and mouse buttons. Then pick up a tool again.");
                if (HasResearchLesson)
                    GUILayout.Label("Saved learner lesson recovered from local Notebook. " +
                        ResearchPredictionSummary + " Native Hold remains active until Continue.");
                if (!driver.NativeRecoveryReady)
                    GUILayout.Label(driver.HoldReason == "Compute" ?
                        "Waiting for the scientific result; saving and review remain available." :
                        "Waiting for a fresh neutral input baseline.");
                GUI.enabled = driver.NativeRecoveryReady;
                if (GUILayout.Button("Continue", GUILayout.Height(44f))) panels?.Request(PanelAction.Continue);
                GUI.enabled = true;
                if (GUILayout.Button("Save session", GUILayout.Height(44f))) panels?.Request(PanelAction.Save);
                GUI.enabled = CanLoadSavedSession;
                if (GUILayout.Button("Load saved session", GUILayout.Height(44f))) panels?.Request(PanelAction.Load);
                GUI.enabled = CanChooseLocalPortableArchive;
                if (GUILayout.Button("Choose next local study package", GUILayout.Height(36f)))
                    panels?.Request(PanelAction.ChoosePortableArchive);
                GUI.enabled = CanRestoreLocalPortableSession;
                if (GUILayout.Button("Restore selected local study package", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.RestorePortable);
                GUI.enabled = true;
                GUILayout.Label("Local study package: " + (SelectedArchiveFileName ?? "none") +
                    " · checksum integrity does not prove identity or scientific attestation.");
                if (!string.IsNullOrEmpty(LastPortableImportSummary))
                    GUILayout.Label(LastPortableImportSummary);
                if (GUILayout.Button("Open recorded review", GUILayout.Height(44f))) panels?.Request(PanelAction.Review);
                if (GUILayout.Button("Settings", GUILayout.Height(44f))) panels?.Request(PanelAction.Settings);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Pause)
            {
                GUILayout.Label("Session paused. Material and simulation time are frozen.");
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f))) panels?.Request(PanelAction.Lab);
                if (GUILayout.Button("Save session", GUILayout.Height(44f))) panels?.Request(PanelAction.Save);
                GUI.enabled = CanLoadSavedSession;
                if (GUILayout.Button("Load saved session", GUILayout.Height(44f))) panels?.Request(PanelAction.Load);
                GUI.enabled = CanChooseLocalPortableArchive;
                if (GUILayout.Button("Choose next local study package", GUILayout.Height(36f)))
                    panels?.Request(PanelAction.ChoosePortableArchive);
                GUI.enabled = CanRestoreLocalPortableSession;
                if (GUILayout.Button("Restore selected local study package", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.RestorePortable);
                GUI.enabled = true;
                GUILayout.Label("Local study package: " + (SelectedArchiveFileName ?? "none") +
                    " · checksum integrity does not prove identity or scientific attestation.");
                if (!string.IsNullOrEmpty(LastPortableImportSummary))
                    GUILayout.Label(LastPortableImportSummary);
                if (GUILayout.Button("Open recorded review", GUILayout.Height(44f))) panels?.Request(PanelAction.Review);
                if (GUILayout.Button("Settings", GUILayout.Height(44f))) panels?.Request(PanelAction.Settings);
                if (GUILayout.Button("Restart checkpoint", GUILayout.Height(44f))) RestartCheckpoint();
                if (GUILayout.Button("Export portable session + notebook", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.ExportPortable);
                if (!string.IsNullOrEmpty(LastPortableArchivePath))
                    GUILayout.Label("Portable archive written: " + LastPortableArchivePath);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Review)
            {
                if (RecordedReview == null) GUILayout.Label("No recorded review is open.");
                else
                {
                    GUILayout.Label("Recorded-only review · no model recomputation");
                    GUILayout.Label("Saved branch: " + RecordedReview.BranchId);
                    GUILayout.Label("Saved model identity: " + RecordedReview.ModelIdentity);
                    GUILayout.Label("Native command receipts (ordered; no synthetic timestamps): " +
                        RecordedReview.CommandReceipts.Count);
                    foreach (var receipt in RecordedReview.CommandReceipts.Skip(
                        Math.Max(0, RecordedReview.CommandReceipts.Count - 8)))
                        GUILayout.Label("#" + receipt.CommandSequence + " " + receipt.CommandType +
                            " · " + (receipt.Accepted ? "Accepted" : receipt.OutcomeCode));
                    foreach (var observation in RecordedReview.Observations)
                        GUILayout.Label($"Recorded {observation.vesselId} pH: {observation.DisplayValue} " +
                            $"({observation.freshness}, rev {observation.asOfMaterialRevision})");
                    GUILayout.Label(LearningReviewSummary);
                }
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Lab);
                if (router?.Current?.SupportedActions.Contains("Notebook") == true &&
                    GUILayout.Button("Open student notebook", GUILayout.Height(38f)))
                    panels?.Request(PanelAction.Notebook);
                if (GUILayout.Button("Settings", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Settings);
                GUILayout.Label("Offline study pack preview — checksums, NOT signed institutional attestation.");
                GUILayout.Label("Place imported .plvpack files in: " +
                    Path.Combine(Application.persistentDataPath, "sessions"));
                GUILayout.Label("XR/local selection: " +
                    (SelectedArchiveFileName ?? "none"));
                if (GUILayout.Button("Choose next local study package", GUILayout.Height(36f)))
                    panels?.Request(PanelAction.ChoosePortableArchive);
                instructorArchiveDraft = GUILayout.TextField(instructorArchiveDraft,
                    GUILayout.Height(32f));
                if (GUILayout.Button("Preview local student package", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.PreviewPortableArchive, instructorArchiveDraft);
                if (LastInstructorPortableReview != null)
                {
                    GUILayout.Label(InstructorPortableSummary);
                    GUILayout.Label(ResearchLearningReview.RenderPage(
                        LastInstructorPortableReview.StudentAnnotations,
                        LastInstructorPortableReview.NativeBranchId,
                        instructorAnnotationIndex));
                    if (CanNextInstructorAnnotation &&
                        GUILayout.Button("Next imported student note", GUILayout.Height(38f)))
                        panels?.Request(PanelAction.NextInstructorAnnotation);
                }
                if (GUILayout.Button("Export Native results and local study notes (separate CSVs)", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.ExportCsv);
                var comparisonEnabled = GUI.enabled;
                GUI.enabled = comparisonEnabled && CanCompareSaved;
                var requestComparison = GUILayout.Button("Compare with last saved Native checkpoint",
                    GUILayout.Height(44f));
                GUI.enabled = comparisonEnabled;
                if (requestComparison) panels?.Request(PanelAction.CompareSaved);
                if (!string.IsNullOrEmpty(LastCsvExportPath))
                    GUILayout.Label("Native results CSV written: " + LastCsvExportPath);
                if (!string.IsNullOrEmpty(LastLearnerCsvExportPath))
                    GUILayout.Label("Student annotations CSV written: " + LastLearnerCsvExportPath);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Compare)
            {
                GUILayout.Label("Two recorded Native runs · no recomputation");
                if (CurrentComparison == null) GUILayout.Label("Comparison unavailable.");
                else
                {
                    GUILayout.Label("Saved baseline branch: " + CurrentComparison.BaselineBranchId);
                    GUILayout.Label("Review candidate branch: " + CurrentComparison.CandidateBranchId);
                    GUILayout.Label("Baseline package: " + CurrentComparison.BaselineModelIdentity);
                    GUILayout.Label("Candidate package: " + CurrentComparison.CandidateModelIdentity);
                    foreach (var row in CurrentComparison.Rows)
                    {
                        var difference = row.DeltaAvailable ?
                            row.Delta.ToString("+0.00;-0.00;0.00",
                                System.Globalization.CultureInfo.InvariantCulture) : "--";
                        GUILayout.Label(row.VesselId + " " + row.ObservableId + " · saved " +
                            row.BaselineDisplay + " (" + row.BaselineStatus + ") vs review " +
                            row.CandidateDisplay + " (" + row.CandidateStatus + ") · Δ " +
                            difference + "\n" + row.DeltaReason);
                    }
                }
                if (GUILayout.Button("Back to recorded review", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Review);
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Lab);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Notebook)
            {
                GUILayout.Label("Research notebook · notes reference actual Native events. " +
                    "Physical material gestures are disabled until you return to Lab.");
                var notes = ReadNotebookEntries();
                foreach (var note in notes.Skip(Math.Max(0, notes.Count - 5)))
                    GUILayout.Label("Event " + note.EventSequence + " · " + note.VesselId +
                        " · " + note.RecordedAtUtc + "\n" + note.Text);
                GUILayout.Label("Native event revision: " + router.Current.NativeRevision +
                    ". Notes do not change the scientific state.");
                if (CanRecordResearchExplanation)
                {
                    GUILayout.Label("Choose one research explanation; this is your interpretation, not Native science.");
                    foreach (var item in new[] {
                        ("Composition hypothesis", "composition-hypothesis"),
                        ("Method uncertainty", "method-uncertainty"),
                        ("Model limitations", "model-limitations")
                    })
                        if (GUILayout.Button("Explain: " + item.Item1, GUILayout.Height(32f)))
                            panels?.Request(PanelAction.RecordResearchExplanation, item.Item2);
                }
                notebookDraft = GUILayout.TextArea(notebookDraft, GUILayout.Height(96f));
                if (GUILayout.Button("Record note for current event", GUILayout.Height(44f)) &&
                    panels?.Request(PanelAction.AddNotebookNote, notebookDraft) == true)
                    notebookDraft = string.Empty;
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Lab);
                if (GUILayout.Button("Save Native session", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Save);
                if (GUILayout.Button("Export portable session + notebook", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.ExportPortable);
                if (!string.IsNullOrEmpty(LastPortableArchivePath))
                    GUILayout.Label("Portable archive written: " + LastPortableArchivePath);
                if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Learn)
            {
                GUILayout.Label("Native-backed research concept inspection");
                GUILayout.Label(ResearchConceptSummary);
                if (GUILayout.Button("Return to laboratory", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Lab);
                if (GUILayout.Button("Save session", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Save);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Settings)
            {
                SetupPreferenceControls.Draw(router);
                if (GUILayout.Button("Back", GUILayout.Height(44f))) panels?.Request(PanelAction.Back);
                if (GUILayout.Button("Save session", GUILayout.Height(44f))) panels?.Request(PanelAction.Save);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (screen == ScreenId.Lab)
            {
                GUILayout.Label("First experiment · Research-only, not a certified endpoint");
                if (GUILayout.Button("Guide: strong-acid research", GUILayout.Height(32f)))
                    panels?.Request(PanelAction.SelectResearchLesson, "strong-acid-titration-research");
                if (GUILayout.Button("Guide: mixed-acid research", GUILayout.Height(32f)))
                    panels?.Request(PanelAction.SelectResearchLesson, "mixed-acid-titration-research");
                GUILayout.Label(ResearchLessonSummary);
                if (HasResearchLesson) GUILayout.Label(ResearchLessonSteps);
                GUILayout.Label(ResearchPredictionSummary);
                if (CanRecordResearchPrediction)
                {
                    if (GUILayout.Button("Predict: receiver pH below 7", GUILayout.Height(32f)))
                        panels?.Request(PanelAction.RecordResearchPrediction, "below-7");
                    if (GUILayout.Button("Predict: receiver pH at least 7", GUILayout.Height(32f)))
                        panels?.Request(PanelAction.RecordResearchPrediction, "at-least-7");
                }
                GUILayout.Label(LabBenchReadout.NextAction(driver.CurrentLabView,
                    driver.SourceNeedsPreparation, driver.IsNativePaused, driver.IsTimeHeld));
                GUILayout.Label(LabBenchReadout.Inventory(driver.CurrentLabView));
            }
            if (screen == ScreenId.Lab && driver.SourceNeedsPreparation)
            {
                GUILayout.Label("Prepare the source stock · Research aqueous profile");
                var concentrations = driver.ResearchConcentrations;
                if (concentrations.Count > 0)
                {
                    selectedConcentrationIndex = Mathf.Clamp(selectedConcentrationIndex, 0, concentrations.Count - 1);
                    GUILayout.BeginHorizontal();
                    for (var index = 0; index < concentrations.Count; index++)
                        if (GUILayout.Toggle(selectedConcentrationIndex == index,
                            concentrations[index].ToString("0.00") + " mol/L", GUI.skin.button))
                            selectedConcentrationIndex = index;
                    GUILayout.EndHorizontal();
                }
                foreach (var stockId in driver.AvailableStockIds)
                    if (GUILayout.Button("Prepare " + stockId, GUILayout.Height(28f)))
                        SelectStock(stockId, stockId == "water" ? 0.0 : concentrations[selectedConcentrationIndex]);
                GUILayout.Label("Stock volume: 25 ml. Prepared quantities come from the native model.");
            }
            if (screen == ScreenId.Lab && driver.CurrentMode == "Desktop")
                GUILayout.Label("Desktop: left-click an instrument to pick it up; F picks from view center or places a held tool on the bench. " +
                    "Held burette: left-drag actuates stopcock, Shift+left-drag moves it. " +
                    "Held pipette/bottle: left-drag moves; G in other prepared stock changes pipette Native source, next G aspirates. G over receiver dispenses, inverted source bottle over burette primes; inverted reserve bottle over source mouth refills 0.01 ml. G with pipette tip or inverted stock-bottle outlet submerged in the open prepared-water cup rinses 0.01 ml through Native waste; the bottle stock inventory is not replaced. " +
                    "Left-click receiver = inspect current Native pH; Alt+left-click source/reserve bottle = inspect its actual Native inventory. Point view center at receiver and F = pick receiver, drag/arrow reposition, F upright near bench to put its foot down. Wheel = held height / empty-hand zoom; Q/E = tilt; Esc = Native Pause.");
            if (screen == ScreenId.Lab && driver.SourceNeedsPreparation && !driver.ReserveStockPrepared)
            {
                GUILayout.Label("Optional: prepare reserve stock BEFORE primary stock (no material created after setup).");
                var concentrations = driver.ResearchConcentrations;
                foreach (var id in driver.AvailableStockIds)
                {
                    var stock = id;
                    if (GUILayout.Button("Prepare reserve " + stock, GUILayout.Height(27f)))
                        TryPrepareResearchReserveStock(stock,
                            stock == "water" ? 0.0 : concentrations[Mathf.Clamp(
                                selectedConcentrationIndex, 0, concentrations.Count - 1)]);
                }
            }
            if (screen == ScreenId.Lab && driver.SourceNeedsPreparation && !driver.RinseWaterPrepared)
            {
                GUILayout.Label("Optional: prepare measured clean water before entering the live lab.");
                if (GUILayout.Button("Prepare 1 ml rinse water (Native setup)", GUILayout.Height(32f)))
                    TryPrepareResearchRinseWater();
            }
            if (screen == ScreenId.Lab && !driver.SourceNeedsPreparation)
            {
                GUILayout.Label("Native Research instruments · no certified physical calibration");
                GUI.enabled = CanFillBurette;
                var requestPrime = GUILayout.Button("Burette: prime 0.05 ml from stock", GUILayout.Height(32f));
                GUI.enabled = true;
                if (requestPrime) TryFillBuretteFromBottle();
                if (GUILayout.Button("Pipette: aspirate 0.05 ml", GUILayout.Height(32f)))
                    TryPipetteAspirate();
                GUILayout.Label($"Pipette tip: {PipetteTipVolumeM3 * 1e6:0.00} ml (Research retained tip 0.10 ml). " +
                    "A 0.05 ml dispense requires at least 0.15 ml staged over repeated Native aspiration intents.");
                var previousGuiEnabled = GUI.enabled;
                GUI.enabled = previousGuiEnabled && CanPipetteDispense;
                var requestedPipetteDispense = GUILayout.Button("Pipette: dispense 0.05 ml", GUILayout.Height(32f));
                GUI.enabled = previousGuiEnabled;
                if (requestedPipetteDispense) TryPipetteDispense();
                if (GUILayout.Button("Bottle: pour 0.01 ml", GUILayout.Height(32f)))
                    TryBottleMeasuredPour();
                if (driver.ReserveStockPrepared)
                {
                    GUILayout.Label("Pipette Native source: " +
                        (ActivePipetteStockInventoryId ?? "unavailable"));
                    if (ActivePipetteStockInventoryId == "reserve-stock")
                    {
                        if (GUILayout.Button("Pipette: switch to prepared source", GUILayout.Height(32f)))
                            TryPipetteSwitchPrimary();
                    }
                    else if (GUILayout.Button("Pipette: switch to prepared reserve", GUILayout.Height(32f)))
                        TryPipetteSwitchReserve();
                    if (GUILayout.Button("Bottle: refill from prepared reserve 0.01 ml", GUILayout.Height(32f)))
                        TryBottleRefillFromReserve();
                }
                if (driver.RinseWaterPrepared)
                {
                    GUILayout.Label("Physical rinse: dip the directly held pipette tip OR the inverted source-bottle outlet into prepared Native water and press G; only the tool film is washed into Native waste.");
                    if (GUILayout.Button("Pipette: water rinse 0.01 ml", GUILayout.Height(32f)))
                        TryPipetteWaterRinse();
                    if (GUILayout.Button("Bottle: water rinse 0.01 ml", GUILayout.Height(32f)))
                        TryBottleWaterRinse();
                }
                else GUILayout.Label("Rinse unavailable: no clean water was prepared during setup.");
            }
            var measurement = driver.Measurement;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Inspect source", GUILayout.Height(28f))) panels?.Request(PanelAction.Inspect, "source");
            if (GUILayout.Button("Inspect receiver", GUILayout.Height(28f))) panels?.Request(PanelAction.Inspect, "receiver");
            GUILayout.EndHorizontal();
            if (screen == ScreenId.Inspect || screen == ScreenId.Measure)
            {
                GUILayout.Label(measurement == null ? "pH: pending" :
                    $"pH: {measurement.DisplayValue} ({measurement.Availability})");
                if (measurement != null) GUILayout.Label(measurement.Explanation);
                if (screen == ScreenId.Inspect && GUILayout.Button("Measure selected vessel", GUILayout.Height(44f)))
                    panels?.Request(PanelAction.Measure, router.Current.SelectedEntityId);
                if (screen == ScreenId.Measure && router.Current.SelectedEntityId == "receiver")
                    GUILayout.Label("Desktop: release the inspection click, then click the physical receiver again to record only a fresh Native pH in the Notebook.");
                var wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && CanRecordCurrentNativePH;
                var recordReading = GUILayout.Button("Record current Native pH in notebook",
                    GUILayout.Height(36f));
                GUI.enabled = wasEnabled;
                if (recordReading) panels?.Request(PanelAction.RecordCurrentPH);
                if (GUILayout.Button("Return to lab", GUILayout.Height(44f))) panels?.Request(PanelAction.Lab);
            }
            if (GUILayout.Button("Pause", GUILayout.Height(44f))) panels?.Request(PanelAction.Pause);
            if (GUILayout.Button("Save session", GUILayout.Height(30f))) panels?.Request(PanelAction.Save);
            var savedPath = Path.Combine(Application.persistentDataPath, "sessions", saveName + ".plv.json");
            // Live Lab never performs an unguarded Native context replacement.
            // Saved-session restore is an explicit Pause/Hold panel action, where
            // recovery baselines and (for VR) parked tool admission are enforced.
            if (SaveService.HasSaveCandidate(savedPath))
                GUILayout.Label("To load a saved session, pause the lab first.");
            if (GUILayout.Button("Create checkpoint", GUILayout.Height(30f))) CreateCheckpoint();
            if (GUILayout.Button("Restart checkpoint", GUILayout.Height(30f))) RestartCheckpoint();
            if (router?.Current?.SupportedActions.Contains("Review") == true &&
                GUILayout.Button("Open recorded review", GUILayout.Height(44f)))
                panels?.Request(PanelAction.Review);
            if (router?.Current?.SupportedActions.Contains("Notebook") == true &&
                GUILayout.Button("Open research notebook", GUILayout.Height(44f)))
                panels?.Request(PanelAction.Notebook);
            if (!string.IsNullOrEmpty(LastInstrumentReceipt))
                GUILayout.Label(LastInstrumentReceipt);
            var desktopInspection = driver?.transform.root
                .GetComponentInChildren<DesktopInputAdapter>(true)?.LastPhysicalInspectionFeedback;
            if (driver?.CurrentMode == "Desktop" && !string.IsNullOrEmpty(desktopInspection))
                GUILayout.Label("Receiver inspection: " + desktopInspection);
            var desktopDelivery = driver?.transform.root
                .GetComponentInChildren<DesktopInputAdapter>(true)?.LastPhysicalDeliveryFeedback;
            if (driver?.CurrentMode == "Desktop" &&
                !string.IsNullOrEmpty(desktopDelivery))
                GUILayout.Label("Physical delivery: " + desktopDelivery);
            if (!string.IsNullOrEmpty(LastError)) GUILayout.Label(LastError);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            }
            finally
            {
                GUI.skin.label.fontSize = previousLabelSize;
                GUI.skin.button.fontSize = previousButtonSize;
            }
        }

        // Actions are public so Desktop and future world-space XR panels can use
        // the same transaction/receipt boundary. They do not fabricate chemistry.
        private PipetteActuator ResearchPipette
        {
            get
            {
                var tool = GetComponent<PipetteActuator>();
                if (tool == null && driver != null)
                    tool = driver.transform.root.GetComponentInChildren<PipetteActuator>(true);
                if (tool == null) tool = gameObject.AddComponent<PipetteActuator>();
                tool.Configure(driver);
                return tool;
            }
        }

        private BottlePourActuator ResearchBottle
        {
            get
            {
                var tool = GetComponent<BottlePourActuator>();
                if (tool == null && driver != null)
                    tool = driver.transform.root.GetComponentInChildren<BottlePourActuator>(true);
                if (tool == null) tool = gameObject.AddComponent<BottlePourActuator>();
                tool.Configure(driver);
                return tool;
            }
        }

        private bool TryResearchCommand(string action, Func<LabCommandReceipt> command)
        {
            LastInstrumentReceipt = null;
            try
            {
                if (driver?.Session == null || driver.SourceNeedsPreparation ||
                    driver.IsNativePaused || driver.IsTimeHeld)
                    throw new InvalidOperationException("Prepare stock and leave Native Pause/Hold before transfer.");
                var receipt = command();
                if (!receipt.Accepted)
                    throw new InvalidOperationException("Native instrument refused: " + receipt.Code);
                // This message reports only what the Native receipt confirms.
                // Observable volumes must always come from PublishSnapshot.
                LastInstrumentReceipt = action + " · Native accepted command #" +
                    receipt.CommandSequence;
                LastError = null;
                driver.RefreshAfterInstrumentCommand();
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return true;
            }
            catch (Exception error)
            {
                LastInstrumentReceipt = null;
                LastError = error.Message;
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return false;
            }
        }

        public double PipetteTipVolumeM3
        {
            get
            {
                if (driver?.Session == null) return 0.0;
                try { return ResearchPipette.RepresentedTipVolumeM3; }
                catch (InvalidOperationException) { return 0.0; }
            }
        }

        public bool CanPipetteDispense =>
            driver?.Session != null && !driver.SourceNeedsPreparation &&
            !driver.IsNativePaused && !driver.IsTimeHeld &&
            ResearchInstrumentPayloads.CanDispensePipette(PipetteTipVolumeM3,
                ResearchInstrumentPayloads.DefaultPipetteDispenseM3);

        public bool TryPipetteAspirate() => TryResearchCommand("Pipette aspirate 0.05 ml",
            () => ResearchPipette.Aspirate(5e-8));

        public bool TryPipetteDispense()
        {
            if (!CanPipetteDispense)
            {
                LastInstrumentReceipt = null;
                LastError = "Pipette retained tip is 0.10 ml (Research). " +
                    "Aspirate additional real prepared stock before dispensing 0.05 ml.";
                return false;
            }
            return TryResearchCommand("Pipette dispense 0.05 ml", () => ResearchPipette.Dispense(
                ResearchInstrumentPayloads.DefaultPipetteDispenseM3));
        }
        // 0.05 mL is the precision-session transport budget for one Native solve.
        // This is a deliberate small prime, NOT an instant 50 mL refill.
        private const double BurettePrimeM3 = 5e-8;

        private bool HasNativeBurettePrimeCapacity
        {
            get
            {
                if (driver?.Session == null || driver.CurrentLabView?.Vessels == null ||
                    driver.SourceNeedsPreparation || driver.IsNativePaused || driver.IsTimeHeld)
                    return false;
                var vessels = driver.CurrentLabView.Vessels;
                var stock = vessels.FirstOrDefault(item => item.Id == "source");
                var burette = vessels.FirstOrDefault(item => item.Id == "burette");
                return !string.IsNullOrEmpty(stock.Id) && !string.IsNullOrEmpty(burette.Id) &&
                    stock.CommittedVolumeM3 + 1e-12 >= BurettePrimeM3 &&
                    burette.CapacityM3 - burette.CommittedVolumeM3 + 1e-12 >= BurettePrimeM3;
            }
        }

        public bool CanFillBurette
        {
            get
            {
                if (!HasNativeBurettePrimeCapacity) return false;
                if (driver.CurrentMode == "VR")
                    return driver.transform.root.GetComponentInChildren<ToolMount>(true)?.IsMounted == true;
                if (driver.CurrentMode == "Desktop")
                    return driver.transform.root.GetComponentInChildren<DesktopInputAdapter>(true)?.HasHeldTool != true;
                return false;
            }
        }

        // Called ONLY by the physical input route when the bottle outlet
        // has actually stayed at the burette mouth through the Native step.
        // The Desktop menu's empty-hand admission remains unchanged.
        public bool TryPhysicalFillBuretteFromBottle(Transform physicalBottle,
            ResearchBuretteInletTarget inlet)
        {
            var realInlet = driver != null ? driver.transform.root
                .GetComponentInChildren<ResearchBuretteInletTarget>(true) : null;
            var validBottle = physicalBottle != null &&
                physicalBottle.GetComponent<BottlePourActuator>() != null &&
                physicalBottle.GetComponent<ResearchStockAspirateTarget>()?.NativeInventoryId == "source";
            var parked = false;
            if (driver != null && driver.CurrentMode == "Desktop")
            {
                var desktop = driver.transform.root
                    .GetComponentInChildren<DesktopInputAdapter>(true);
                parked = desktop != null && desktop.CurrentHeldPhysicalTool == physicalBottle &&
                    !desktop.IsHoldingNativeTool && desktop.HasFreshPhysicalGestureSinceRestore;
            }
            else if (driver != null && driver.CurrentMode == "VR")
            {
                var mount = driver.transform.root.GetComponentInChildren<ToolMount>(true);
                var xr = driver.transform.root.GetComponentInChildren<XRInputAdapter>(true);
                parked = mount != null && mount.IsMounted &&
                    xr != null && xr.IsSourceBottleDirectlyGripped(physicalBottle) &&
                    xr.HasFreshGripSinceRestore;
            }
            if (router?.Current?.ScreenId != ScreenId.Lab ||
                !validBottle || !parked || !HasNativeBurettePrimeCapacity ||
                inlet == null || inlet != realInlet ||
                !inlet.AdmitsBottle(physicalBottle, parked, driver.Session))
            {
                LastInstrumentReceipt = null;
                LastError = "Prime refused: close the Native burette valve, park the burette, " +
                    "and hold the source bottle's downward outlet inside the upper inlet.";
                return false;
            }
            return TransferNativePrime("Physical burette prime 0.05 ml");
        }

        private bool TransferNativePrime(string label) => TryResearchCommand(label, () =>
        {
            var commands = new LabCommandService(driver.Session);
            return commands.Submit("TransferFixed",
                ResearchInstrumentPayloads.RefillFromPreparedStock(
                    "source", "burette", "spill", BurettePrimeM3),
                commands.CaptureTouchedRevisions("source", "burette"));
        });

        public bool TryFillBuretteFromBottle()
        {
            if (!CanFillBurette)
            {
                LastInstrumentReceipt = null;
                LastError = "Park the burette and prepare sufficient Native stock before priming.";
                if (router?.Current != null) router.Show(router.Current.ScreenId);
                return false;
            }
            return TransferNativePrime("Burette prime 0.05 ml");
        }

        public bool TryBottleMeasuredPour() => TryResearchCommand("Bottle pour 0.01 ml",
            () => ResearchBottle.Pour(1e-8));
        // The actual scene station must still contain Native water at the
        // command edge; a stale target or distant held pipette never rinses.
        public bool TryPhysicalRinsePipette(Transform physicalPipette,
            ResearchRinseWaterTarget physicalWater)
        {
            var root = driver != null ? driver.transform.root : null;
            var actualPipette = root != null ?
                root.GetComponentInChildren<PipetteActuator>(true) : null;
            var actualWater = root != null ?
                root.GetComponentInChildren<ResearchRinseWaterTarget>(true) : null;
            if (driver?.Session == null || driver.SourceNeedsPreparation ||
                driver.IsNativePaused || driver.IsTimeHeld ||
                !driver.RinseWaterPrepared || !driver.Session.IsOpen ||
                !PhysicallyOwnsPipette(physicalPipette) ||
                physicalPipette == null ||
                actualPipette == null || actualPipette.transform != physicalPipette ||
                physicalWater == null || physicalWater != actualWater ||
                !physicalWater.IsImmersed(physicalPipette) ||
                router?.Current?.ScreenId != ScreenId.Lab)
            {
                LastInstrumentReceipt = null;
                LastError = "Rinse refused. Prepare real Native water, immerse the physical pipette tip and return to an active Lab.";
                return false;
            }
            return TryPipetteWaterRinse();
        }

        public bool TryPipetteWaterRinse() => TryResearchCommand("Pipette rinse 0.01 ml",
            () => ResearchPipette.Rinse(1e-8));
        // Directly dip the real SOURCE bottle's downward outlet into the
        // Native-water meniscus. This washes its metered tip film; it never
        // silently dilutes or replaces the bottle's original stock inventory.
        // Never allow a public call to skip the real Desktop/XR hand authority.
        public bool TryPhysicalRinseBottle(Transform heldSourceBottle,
            ResearchRinseWaterTarget physicalWater)
        {
            var root = driver != null ? driver.transform.root : null;
            var nativeBottle = root != null ?
                root.GetComponentInChildren<BottlePourActuator>(true) : null;
            var stock = root != null ? root
                .GetComponentsInChildren<ResearchStockAspirateTarget>(true)
                .FirstOrDefault(item => item.NativeInventoryId == "source") : null;
            var water = root != null ?
                root.GetComponentInChildren<ResearchRinseWaterTarget>(true) : null;
            var receiver = root != null ?
                root.GetComponentInChildren<LabCaptureTarget>(true) : null;
            var inlet = root != null ?
                root.GetComponentInChildren<ResearchBuretteInletTarget>(true) : null;

            var owned = false;
            if (driver != null && driver.CurrentMode == "Desktop")
            {
                var desktop = root.GetComponentInChildren<DesktopInputAdapter>(true);
                owned = desktop != null &&
                    desktop.CurrentHeldPhysicalTool == heldSourceBottle &&
                    desktop.HasFreshPhysicalGestureSinceRestore &&
                    desktop.HasFreshLabScreenGesture &&
                    !desktop.IsHoldingNativeTool;
            }
            else if (driver != null && driver.CurrentMode == "VR")
            {
                var xr = root.GetComponentInChildren<XRInputAdapter>(true);
                owned = xr != null &&
                    xr.IsSourceBottleDirectlyGripped(heldSourceBottle) &&
                    xr.HasFreshGripSinceRestore && xr.HasFreshLabScreenGesture;
            }

            var active = driver?.Session?.IsOpen == true &&
                !driver.SourceNeedsPreparation && !driver.IsNativePaused &&
                !driver.IsTimeHeld && driver.RinseWaterPrepared &&
                router?.Current?.ScreenId == ScreenId.Lab;
            var receiverCapture = heldSourceBottle != null && receiver != null &&
                LabCaptureTarget.EstimateExclusive(heldSourceBottle, receiver).Length != 0;
            var inletCapture = heldSourceBottle != null && inlet != null &&
                driver?.Session?.IsOpen == true &&
                inlet.AdmitsBottle(heldSourceBottle, true, driver.Session);
            if (!active || !owned || heldSourceBottle == null ||
                nativeBottle == null || nativeBottle.transform != heldSourceBottle ||
                stock == null || stock.transform != heldSourceBottle ||
                physicalWater == null || physicalWater != water ||
                !physicalWater.IsImmersed(heldSourceBottle) ||
                receiverCapture || inletCapture)
            {
                LastInstrumentReceipt = null;
                LastError = "Bottle rinse refused: hold the actual source bottle, " +
                    "dip its downward outlet in prepared Native water, and keep " +
                    "it clear of the receiver and burette inlet in an active Lab.";
                return false;
            }
            return TryBottleWaterRinse();
        }

        public bool TryBottleWaterRinse() => TryResearchCommand("Bottle rinse 0.01 ml",
            () => ResearchBottle.Rinse(1e-8));
        public string ActivePipetteStockInventoryId
        {
            get
            {
                if (driver?.Session == null || !driver.Session.IsOpen) return null;
                var pipette = driver.transform.root.GetComponentInChildren<PipetteActuator>(true);
                if (pipette == null) return null;
                foreach (var target in driver.transform.root
                    .GetComponentsInChildren<ResearchStockAspirateTarget>(true))
                    if (target.NativeSourceMatches(driver.Session, pipette))
                        return target.NativeInventoryId;
                return null;
            }
        }

        // Both gestures (pipette source selection and water cleaning) must
        // independently prove current physical ownership at the command edge.
        // A public controller entry point may never rely only on the caller
        // saying the tip is immersed.
        private bool PhysicallyOwnsPipette(Transform physicalPipette)
        {
            if (driver == null || physicalPipette == null) return false;
            if (driver.CurrentMode == "Desktop")
            {
                var desktop = driver.transform.root
                    .GetComponentInChildren<DesktopInputAdapter>(true);
                return desktop != null &&
                    desktop.CurrentHeldPhysicalTool == physicalPipette &&
                    desktop.HasFreshPhysicalGestureSinceRestore &&
                    desktop.HasFreshLabScreenGesture;
            }
            if (driver.CurrentMode == "VR")
            {
                var xr = driver.transform.root
                    .GetComponentInChildren<XRInputAdapter>(true);
                return xr != null && xr.IsPipetteDirectlyGripped(physicalPipette) &&
                    xr.HasFreshGripSinceRestore && xr.HasFreshLabScreenGesture;
            }
            return false;
        }

        // Missing production route for physical first-press source switching.
        // Re-check the *actual* immersed prefab and Native tool source before
        // calling the existing revision-checked SwitchPipetteSource command.
        public bool TryPhysicalSelectPipetteSource(Transform heldPipette,
            ResearchStockAspirateTarget physicalStock)
        {
            var root = driver != null ? driver.transform.root : null;
            var actualPipette = root != null ?
                root.GetComponentInChildren<PipetteActuator>(true) : null;
            var stockId = physicalStock != null ? physicalStock.NativeInventoryId : null;
            var actualStock = root != null ? root
                .GetComponentsInChildren<ResearchStockAspirateTarget>(true)
                .FirstOrDefault(t => t.NativeInventoryId == stockId) : null;
            var active = driver?.Session?.IsOpen == true &&
                !driver.SourceNeedsPreparation && !driver.IsNativePaused &&
                !driver.IsTimeHeld && router?.Current?.ScreenId == ScreenId.Lab;
            var prepared = active && (stockId == "source" ||
                (stockId == "reserve-stock" && driver.ReserveStockPrepared));
            var actualPhysicalTool = heldPipette != null && actualPipette != null &&
                actualPipette.transform == heldPipette;
            var alreadySelected = actualPhysicalTool && physicalStock != null &&
                physicalStock.NativeSourceMatches(driver.Session, actualPipette);
            var immersed = actualPhysicalTool && physicalStock != null &&
                physicalStock == actualStock && physicalStock.IsImmersed(heldPipette);
            if (!ResearchPhysicalPipetteSelectionAdmission.MaySelect(
                    stockId, prepared, actualPhysicalTool &&
                    PhysicallyOwnsPipette(heldPipette), active, immersed,
                    alreadySelected))
            {
                LastInstrumentReceipt = null;
                LastError = "Source switch refused: directly hold the pipette, " +
                    "immerse its tip in a different prepared Native stock, then " +
                    "press once. Release stale gestures after Native restoration.";
                return false;
            }
            return stockId == "source" ?
                TryPipetteSwitchPrimary() : TryPipetteSwitchReserve();
        }

        public bool TryPipetteSwitchPrimary() => TryResearchCommand("Pipette switch to source",
            () => ResearchPipette.SwitchPreparedSource("source"));
        public bool TryPipetteSwitchReserve() => TryResearchCommand("Pipette switch to reserve",
            () => ResearchPipette.SwitchPreparedSource("reserve-stock"));
        // Explicit reserve-bottle gesture routed to the existing Native
        // measured refill. The geometry is re-evaluated at the command edge
        // and no presenter ever creates material in the source bottle.
        public bool TryPhysicalRefillSourceFromReserve(Transform heldReserveBottle,
            ResearchStockAspirateTarget physicalSource)
        {
            var actualReserve = driver != null ? driver.transform.root
                .GetComponentsInChildren<ResearchStockAspirateTarget>(true)
                .FirstOrDefault(t => t.NativeInventoryId == "reserve-stock") : null;
            var actualSource = driver != null ? driver.transform.root
                .GetComponentsInChildren<ResearchStockAspirateTarget>(true)
                .FirstOrDefault(t => t.NativeInventoryId == "source") : null;
            var owned = false;
            if (driver != null && driver.CurrentMode == "Desktop")
            {
                var desktop = driver.transform.root.GetComponentInChildren<DesktopInputAdapter>(true);
                owned = desktop != null &&
                    desktop.CurrentHeldPhysicalTool == heldReserveBottle &&
                    desktop.HasFreshPhysicalGestureSinceRestore;
            }
            else if (driver != null && driver.CurrentMode == "VR")
            {
                var xr = driver.transform.root.GetComponentInChildren<XRInputAdapter>(true);
                owned = xr != null && xr.HasFreshGripSinceRestore &&
                    xr.IsReserveBottleDirectlyGripped(heldReserveBottle);
            }
            if (router?.Current?.ScreenId != ScreenId.Lab ||
                driver?.Session == null || !driver.Session.IsOpen ||
                driver.IsNativePaused || driver.IsTimeHeld ||
                driver.SourceNeedsPreparation || !driver.ReserveStockPrepared ||
                !owned || heldReserveBottle == null ||
                actualReserve == null || actualReserve.transform != heldReserveBottle ||
                physicalSource == null || physicalSource != actualSource ||
                !physicalSource.AdmitsReservePour(heldReserveBottle, true))
            {
                LastInstrumentReceipt = null;
                LastError = "Refill refused: grip the prepared reserve bottle and hold " +
                    "its inverted outlet over the upright, stationary source mouth.";
                return false;
            }
            return TryBottleRefillFromReserve();
        }

        public bool TryBottleRefillFromReserve() => TryResearchCommand("Bottle refill 0.01 ml",
            () => ResearchBottle.RefillFromPreparedStock("reserve-stock", 1e-8));

        public bool TryPrepareResearchReserveStock(string stockId, double concentrationMolPerL)
        {
            try
            {
                if (driver == null || !driver.TryPrepareReserveStock(stockId, concentrationMolPerL))
                    throw new InvalidOperationException(
                        "Reserve stock must be deliberately prepared before leaving Native setup.");
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                return false;
            }
        }


        public bool TryPrepareResearchRinseWater()
        {
            try
            {
                if (driver == null || !driver.TryPrepareRinseWater())
                    throw new InvalidOperationException("Clean Native rinse water can only be prepared during setup.");
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = error.Message;
                return false;
            }
        }

        public bool SelectStock(string stockId, double concentrationMolPerL)
        {
            try
            {
                if (driver == null || !driver.TryPrepareSourceStock(stockId, concentrationMolPerL))
                    throw new InvalidOperationException("Stock selection was rejected by the native session or Research catalog.");
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }


        private string NotebookFilePath()
        {
            if (string.IsNullOrWhiteSpace(saveName) || Path.GetFileName(saveName) != saveName ||
                saveName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException("Notebook session filename is not safe.");
            return Path.Combine(Application.persistentDataPath, "sessions", saveName + ".plv-notes");
        }

        public IReadOnlyList<NotebookEntry> ReadNotebookEntries()
        {
            try
            {
                if (driver?.Session == null || !driver.Session.IsOpen)
                    throw new InvalidOperationException("No open Native session.");
                // Reading a healthy notebook must not erase an earlier failed
                // note-commit error before Desktop/XR has rendered it.
                return notebook.ReadCurrent(NotebookFilePath(), driver.Session.BranchId,
                    driver.CurrentLabView?.EventSequence);
            }
            catch (Exception exception)
            {
                LastError = "Research notebook unavailable: " + exception.Message;
                return Array.Empty<NotebookEntry>();
            }
        }

        public string NotebookSummary
        {
            get
            {
                if (driver?.Session == null || !driver.Session.IsOpen ||
                    driver.CurrentLabView == null)
                    return "Research notebook unavailable: no active Native session.";
                try
                {
                    // Do not convert a corrupt or torn journal into an empty
                    // learner notebook; render only verified Native-bounded
                    // annotations from the exact current session namespace.
                    return ResearchLearningReview.ReadActiveNotebookSummary(
                        notebook, NotebookFilePath(), driver.Session.BranchId,
                        driver.CurrentLabView.EventSequence);
                }
                catch (Exception error) when (error is IOException ||
                        error is UnauthorizedAccessException ||
                        error is ArgumentException ||
                        error is InvalidOperationException)
                {
                    return "Research notebook unavailable: offline journal could not be verified.";
                }
            }
        }

        public bool AppendNotebookNote(string value)
        {
            try
            {
                if (router?.Current?.ScreenId != ScreenId.Notebook ||
                    driver?.Session == null || !driver.Session.IsOpen ||
                    driver.CurrentLabView == null || driver.IsTimeHeld)
                    throw new InvalidOperationException("Notes require an active Notebook and Native snapshot.");
                var nativeRevision = driver.CurrentLabView.EventSequence;
                if (nativeRevision != router.Current.NativeRevision)
                    throw new InvalidOperationException("Native event revision changed before note commit.");
                notebook.Append(NotebookFilePath(), driver.Session.BranchId, nativeRevision,
                    router.Current.SelectedEntityId, value);
                InvalidateLearningReview();
                LastError = null;
                router.Show(ScreenId.Notebook);
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }

        public bool ExportRecordedCsv()
        {
            LastCsvExportPath = null;
            LastLearnerCsvExportPath = null;
            try
            {
                if (router?.Current?.ScreenId != ScreenId.Review || RecordedReview == null)
                    throw new InvalidOperationException("CSV export requires an open recorded Native Review.");
                var notesPath = NotebookFilePath(); // Enforces safe session file naming.
                var target = Path.Combine(Path.GetDirectoryName(notesPath), saveName + ".plv-results.csv");
                var review = RecordedReview;
                // Verify current learner evidence before publishing a scientific
                // CSV, so a damaged journal cannot cause a partial export.
                IReadOnlyList<NotebookEntry> localNotes = null;
                if (driver?.Session != null && driver.Session.IsOpen &&
                    driver.Session.BranchId == review.BranchId &&
                    !string.IsNullOrWhiteSpace(review.EventSequence))
                {
                    localNotes = notebook.ReadCurrent(notesPath,
                        review.BranchId, review.EventSequence);
                }
                SessionExportService.WriteCsv(target, review);
                LastCsvExportPath = target;
                // Scientific output and student annotations are distinct.
                // Do not silently export an older recovered backup as current.
                if (localNotes != null)
                {
                    var learnerTarget = Path.Combine(Path.GetDirectoryName(notesPath),
                        saveName + ".plv-student-annotations.csv");
                    ResearchLearningReview.WriteCsv(learnerTarget, localNotes, review.BranchId);
                    LastLearnerCsvExportPath = learnerTarget;
                }
                LastError = null;
                router.Show(ScreenId.Review);
                return true;
            }
            catch (Exception error)
            {
                LastError = "Recorded CSV export failed: " + error.Message;
                return false;
            }
        }

        public bool ExportPortableSession()
        {
            LastPortableArchivePath = null;
            try
            {
                if (router?.Current == null ||
                    (router.Current.ScreenId != ScreenId.Pause &&
                     router.Current.ScreenId != ScreenId.Notebook) ||
                    driver?.Session == null || !driver.Session.IsOpen ||
                    driver.CurrentLabView == null)
                    throw new InvalidOperationException("Portable export requires Pause/Notebook and an open Native session.");
                // The Native export and lab view must identify the same revision.
                // The archive validates exact Native branch and revision before writing.
                var branch = driver.Session.BranchId;
                var revision = driver.CurrentLabView.EventSequence;
                if (revision != router.Current.NativeRevision)
                    throw new InvalidOperationException("Native revision changed before archive export.");
                var notesPath = NotebookFilePath();
                var notes = notebook.ExportBranch(notesPath, branch, revision);
                var fileName = OfflineArchivePicker.NewStudentExportName(
                    saveName, revision, Guid.NewGuid().ToString("N"));
                var target = Path.Combine(Path.GetDirectoryName(notesPath), fileName);
                // A new immutable package for every explicit student export;
                // additional Notebook evidence must never erase an earlier
                // event-bounded research artifact at the same Native revision.
                PortableSessionArchive.WriteNew(target, driver.Session.ExportSession(),
                    notes, branch, revision);
                LastPortableArchivePath = target;
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = "Portable export failed: " + exception.Message;
                return false;
            }
        }

        public bool Save()
        {
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                LastDurableSavePath = saves.Save(driver.Session, Path.Combine(Application.persistentDataPath, "sessions"), saveName);
                // SaveService returned a confirmed durable path. A cached
                // comparison against the previous saved baseline is no
                // longer valid; the Native Review remains unchanged.
                recordedCache.InvalidateComparison();
                LastError = null;
                router?.MarkSaved();
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                router?.MarkSaveFailed();
                return false;
            }
        }

        private string SavedNativePath()
        {
            var notesPath = NotebookFilePath(); // Validate safe session naming.
            return Path.Combine(Path.GetDirectoryName(notesPath), saveName + ".plv.json");
        }

        public bool CanCompareSaved
        {
            get
            {
                if (RecordedReview == null) return false;
                try { return SaveService.HasSaveCandidate(SavedNativePath()); }
                catch (ArgumentException) { return false; }
                catch (InvalidOperationException) { return false; }
            }
        }

        public string ComparisonSummary
        {
            get
            {
                var comparison = CurrentComparison;
                if (comparison == null) return "No recorded run comparison is open.";
                var summary = "Saved " + comparison.BaselineBranchId + " vs review " +
                    comparison.CandidateBranchId + " (recorded; no recomputation)";
                var shown = 0;
                foreach (var row in comparison.Rows)
                {
                    if (shown++ >= 2) break;
                    var difference = row.DeltaAvailable ?
                        row.Delta.ToString("+0.00;-0.00;0.00",
                            System.Globalization.CultureInfo.InvariantCulture) : "--";
                    summary += "\n" + row.VesselId + " pH: " + row.BaselineDisplay +
                        " (" + row.BaselineStatus + ") / " + row.CandidateDisplay +
                        " (" + row.CandidateStatus + ") · Δ " + difference;
                }
                if (comparison.Rows.Count == 0) summary += "\nNo common or recorded observations.";
                return summary;
            }
        }

        public bool OpenSavedComparison()
        {
            CurrentComparison = null;
            try
            {
                if (router?.Current?.ScreenId != ScreenId.Review || RecordedReview == null)
                    throw new InvalidOperationException("Compare requires an open recorded Review.");
                var path = SavedNativePath();
                if (!SaveService.HasSaveCandidate(path))
                    throw new InvalidDataException("No confirmed saved Native checkpoint is available.");
                CurrentComparison = RecordedRunComparison.CompareSaved(path, RecordedReview);
                LastError = null;
                return true;
            }
            catch (Exception error)
            {
                LastError = "Saved recorded comparison unavailable: " + error.Message;
                return false;
            }
        }

        public bool OpenReview()
        {
            if (RecordedReview != null) return true;
            try
            {
                if (driver?.Session == null) throw new InvalidOperationException("No open native session.");
                RecordedReview = RecordedSessionReview.FromJson(driver.Session.ExportSession());
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = "Recorded review unavailable: " + exception.Message;
                return false;
            }
        }

        public void CloseReview()
        {
            RecordedReview = null;
            CurrentComparison = null;
            LastInstructorPortableReview = null;
            InvalidateLearningReview();
        }

        public bool ContinueFrom(string path)
        {
            CoreSession imported = null;
            RecordedReview = null;
            CurrentComparison = null;
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                imported = saves.Load(path, driver.ScientificDatabasePath);
                driver.ReplaceSession(imported);
                imported = null;
                RecordedReview = null;
                LastError = lastLessonRecoveryError;
                router?.MarkSaved();
                return true;
            }
            catch (ScientificPackageMismatchException exception)
            {
                imported?.Dispose();
                try { RecordedReview = RecordedSessionReview.OpenRecoverable(path); }
                catch (Exception reviewError)
                {
                    LastError = "Live resume blocked and recorded review unavailable: " + reviewError.Message;
                    return false;
                }
                LastError = "Live resume blocked: " + exception.Message + " Viewing saved observations only.";
                router?.Show(ScreenId.Review);
                return false;
            }
            catch (Exception exception)
            {
                imported?.Dispose();
                LastError = exception.Message;
                return false;
            }
        }

        public bool CreateCheckpoint()
        {
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                if (!driver.CreateCheckpoint(checkpointId)) throw new InvalidOperationException("Native checkpoint was rejected.");
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }

        public bool RestartCheckpoint()
        {
            try
            {
                if (driver == null) throw new InvalidOperationException("Core driver is not assigned.");
                if (!driver.RestartCheckpoint(checkpointId)) throw new InvalidOperationException("Native checkpoint restart was rejected.");
                LastError = lastLessonRecoveryError;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }

        public bool ContinueHeldSession()
        {
            try
            {
                if (driver == null || !driver.ResumeAfterTimeHold())
                    throw new InvalidOperationException("Fresh neutral tracked baselines are required before continuing.");
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }
    }
}
