// Narrow adapters for compiling actual production ScreenRouter and LabPanelController
// on a runner without Unity. NOT a Native Core or XR simulator.
using System;
using System.Collections.Generic;

namespace TMPro
{
    public sealed class TMP_Text
    {
        public string text { get; set; }
    }
}

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }
    public class MonoBehaviour { }
    public static class Mathf
    {
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.00001f;
    }
    public sealed class TextAsset
    {
        public string text { get; }
        public TextAsset(string value) { text = value; }
    }

    public static class GUILayout
    {
        public static void Label(string value) { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static bool Button(string value) => false;
        public static bool Toggle(bool current, string value) => current;
        public static float HorizontalSlider(float current, float minimum, float maximum) => current;
    }
}

namespace PharmaLabVR.Lab
{
    public sealed class VesselView
    {
        public string Id { get; set; }
        public string MaterialRevision { get; set; } = "0";
        public double CapacityM3 { get; set; } = 5e-5;
        public double CommittedVolumeM3 { get; set; }
    }
    public sealed class ObservationView
    {
        public string VesselId { get; set; }
        public string ObservableId { get; set; }
        public string ModelId { get; set; }
        public string Support { get; set; }
        public string Freshness { get; set; }
        public string Maturity { get; set; }
        public string DatabaseIdentity { get; set; }
        public string AsOfMaterialRevision { get; set; }
        public double Value { get; set; }
        public bool ValueAvailable { get; set; }
        public string ComputationState { get; set; }
    }
    public sealed class LabViewModel
    {
        public string EventSequence { get; set; } = "7";
        public List<VesselView> Vessels { get; } = new()
        {
            new VesselView { Id = "source" },
            new VesselView { Id = "receiver" }
        };
        public List<ObservationView> Observations { get; } = new()
        {
            new ObservationView { VesselId = "receiver", ObservableId = "pH",
                ModelId = "IPhreeqc-research", Support = "Supported",
                Freshness = "Absent", Maturity = "Research" }
        };
    }
}

namespace PharmaLabVR.Core
{
    public sealed class CoreDriver
    {
        public object Session { get; } = new object();
        public string CurrentNativeBranchId { get; set; } = "branch-a";
        public PharmaLabVR.Lab.LabViewModel CurrentLabView { get; } = new();
        public bool IsTimeHeld { get; private set; }
        public bool IsNativePaused { get; private set; }
        public bool SourceNeedsPreparation { get; set; }
        public bool NativeRecoveryReady { get; set; }
        public string HoldReason { get; private set; } = "";
        public string CurrentMode { get; set; } = "Desktop";
        public int PauseCalls { get; private set; }
        public int ContinueCalls { get; private set; }
        public event Action<PharmaLabVR.Lab.LabViewModel> LabViewChanged;
        public event Action<bool> HoldChanged;
        public event Action NativeSessionRestored;
        public ulong NativeRestorationEpoch { get; private set; }
        public ulong NativePublishedRestorationEpoch { get; private set; }
        public bool SelectMeasurementVessel(string id) =>
            CurrentLabView.Vessels.Exists(vessel => vessel.Id == id);
        public bool PauseSession()
        {
            if (IsTimeHeld) return false;
            PauseCalls++;
            IsNativePaused = true;
            return true;
        }
        public bool ContinueSession()
        {
            if (IsTimeHeld) return false;
            ContinueCalls++;
            IsNativePaused = false;
            return true;
        }
        public void SetHold(bool held)
        {
            IsTimeHeld = held;
            HoldReason = held ? "FocusLoss" : "";
            HoldChanged?.Invoke(held);
        }
        public void RaiseNativeSessionRestored()
        {
            NativeRestorationEpoch++;
            NativeSessionRestored?.Invoke();
        }
        public void UpdateView()
        {
            NativePublishedRestorationEpoch = NativeRestorationEpoch;
            LabViewChanged?.Invoke(CurrentLabView);
        }
    }
}
namespace PharmaLabVR.Input
{
    public enum ApplicationMode { Desktop, VirtualReality }
}
namespace PharmaLabVR.Session
{
    public sealed class RecordedSessionReview { }
    public sealed class SessionController
    {
        public RecordedSessionReview RecordedReview { get; private set; }
        public int SaveCalls { get; private set; }
        public int ReviewOpens { get; private set; }
        public int NotebookAppendCalls { get; private set; }
        public bool CanRecordCurrentNativePH { get; set; } = true;
        public bool CanRecordResearchExplanation { get; set; } = true;
        public bool RecordResearchExplanation(string choice)
        {
            if (!CanRecordResearchExplanation ||
                !PharmaLabVR.Protocols.ResearchLessonJourney.IsSupportedExplanation(choice))
                return false;
            NotebookAppendCalls++;
            CanRecordResearchExplanation = false;
            return true;
        }
        public bool RecordCurrentNativeMeasurement()
        {
            if (!CanRecordCurrentNativePH) return false;
            NotebookAppendCalls++;
            return true;
        }
        public int PortableExportCalls { get; private set; }
        public int CsvExportCalls { get; private set; }
        public int PortablePreviewCalls { get; private set; }
        public bool CanPreviewLocalPortableArchive => true;
        public bool CanChooseLocalPortableArchive => true;
        public bool PortableRestorePermitted { get; set; } = true;
        public bool CanRestoreLocalPortableSession => PortableRestorePermitted;
        public string LastPortableImportSummary => "Offline restored student study; not attested.";
        public int PortableRestoreCalls { get; private set; }
        public bool RestoreSelectedLocalPortableSession()
        {
            if (!PortableRestorePermitted) return false;
            PortableRestoreCalls++;
            return true;
        }
        public string SelectedArchiveFileName => "submitted.plvpack";
        public int PortableChooseCalls { get; private set; }
        public bool ChooseNextLocalArchive()
        {
            PortableChooseCalls++;
            return true;
        }
        public string InstructorPortableSummary => "Offline student pack; not attested.";
        public string InstructorPortableCompactSummary => "OFFLINE · NOT ATTESTED.";
        public bool CanNextInstructorAnnotation => true;
        public int NextImportedNoteCalls { get; private set; }
        public bool NextInstructorAnnotation()
        {
            NextImportedNoteCalls++;
            return true;
        }
        public bool PreviewLocalPortableArchive(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            PortablePreviewCalls++;
            return true;
        }
        public int SavedCompareCalls { get; private set; }
        public bool CanCompareSaved => true;
        public string ComparisonSummary => "Recorded Native baseline and candidate.";
        public bool OpenSavedComparison()
        {
            SavedCompareCalls++;
            return true;
        }
        public bool ExportRecordedCsv()
        {
            CsvExportCalls++;
            return true;
        }
        public string LastPortableArchivePath { get; private set; }
        public bool ExportPortableSession()
        {
            PortableExportCalls++;
            LastPortableArchivePath = "/offline/student.plvpack";
            return true;
        }
        public string NotebookSummary => "Headless note stub; not a Native observation.";
        public string LearningReviewSummary => "Local student notes.";
        public string LearningReviewCompact => "Local student notes.";
        public string LastLearnerCsvExportPath => null;
        public bool AppendNotebookNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note)) return false;
            NotebookAppendCalls++;
            return true;
        }
        public bool CanReleaseTool { get; private set; } = true;
        public bool CanParkTool => !CanReleaseTool;
        public bool ParkPermitted { get; set; } = true;
        public bool LoadPermitted { get; set; } = true;
        public int LoadCalls { get; private set; }
        public bool LoadSavedSession()
        {
            if (!LoadPermitted) return false;
            LoadCalls++;
            return true;
        }
        public bool CanLoadSavedSession => LoadPermitted;
        public bool CanChooseLocalNativeSave => true;
        public bool NativeSelectedLoadPermitted { get; set; } = true;
        public bool CanLoadSelectedLocalNativeSave => NativeSelectedLoadPermitted;
        public string ActiveLocalSessionName => "active-study";
        public string SelectedLocalNativeSaveName => "old-study";
        public int LocalNativeChooseCalls { get; private set; }
        public int LocalNativeLoadCalls { get; private set; }
        public bool ChooseNextLocalNativeSave()
        {
            LocalNativeChooseCalls++;
            return true;
        }
        public bool LoadSelectedLocalNativeSave()
        {
            if (!NativeSelectedLoadPermitted) return false;
            LocalNativeLoadCalls++;
            return true;
        }
        public bool DispensePermitted { get; set; } = true;
        public bool CanPipetteDispense => DispensePermitted;
        public bool CanFillBurette => true;
        public double PipetteTipVolumeM3 => DispensePermitted ? 1.5e-7 : 5e-8;
        public string LastError => null;
        public string LastInstrumentReceipt => null;
        public bool HasResearchLesson { get; private set; }
        public bool CanRecordResearchPrediction { get; set; } = true;
        public string ResearchPredictionSummary => CanRecordResearchPrediction ?
            "Prediction not recorded." : "Student prediction recorded.";
        public bool RecordResearchPrediction(string selection)
        {
            if (!CanRecordResearchPrediction ||
                !PharmaLabVR.Protocols.ResearchLessonJourney.IsSupportedPrediction(selection)) return false;
            NotebookAppendCalls++;
            CanRecordResearchPrediction = false;
            return true;
        }
        public string ResearchLessonCompact => HasResearchLesson ?
            "Study steps documented from Native evidence (not graded)." :
            "Choose a Research lesson (not graded).";
        public string ResearchLessonSummary => HasResearchLesson ?
            "Research lesson selected · Native-driven observations." : "Free laboratory.";
        public bool SelectResearchLesson(string id)
        {
            if (id != "strong-acid-titration-research" &&
                id != "mixed-acid-titration-research") return false;
            HasResearchLesson = true;
            return true;
        }
        public int ParkCalls { get; private set; }
        public bool ParkToolAtDock()
        {
            if (!ParkPermitted || CanReleaseTool) return false;
            ParkCalls++;
            CanReleaseTool = true;
            return true;
        }
        public int ReleaseCalls { get; private set; }
        public bool SnapPermitted { get; set; } = true;
        public int SnapCalls { get; private set; }
        public int LastSnapDirection { get; private set; }
        public bool TrySnapTurn(int direction)
        {
            if (!SnapPermitted || (direction != -1 && direction != 1)) return false;
            SnapCalls++;
            LastSnapDirection = direction;
            return true;
        }
        public bool RecenterPermitted { get; set; } = true;
        public int RecenterCalls { get; private set; }
        public bool TryRecenterView() { if (!RecenterPermitted) return false; RecenterCalls++; return true; }
        public bool ReleaseToolFromDock()
        {
            if (!CanReleaseTool) return false;
            ReleaseCalls++;
            CanReleaseTool = false;
            return true;
        }
        public int MeteredCalls { get; private set; }
        public string PreparedStockId { get; private set; }
        public double PreparedConcentration { get; private set; }
        public bool SelectStock(string stock, double concentration)
        {
            if (string.IsNullOrEmpty(stock)) return false;
            PreparedStockId = stock;
            PreparedConcentration = concentration;
            MeteredCalls++;
            return true;
        }
        public bool TryPrepareResearchRinseWater() { MeteredCalls++; return true; }
        public bool TryPipetteAspirate() { MeteredCalls++; return true; }
        public bool TryPipetteDispense() { MeteredCalls++; return true; }
        public bool TryPipetteWaterRinse() { MeteredCalls++; return true; }
        public bool TryBottleMeasuredPour() { MeteredCalls++; return true; }
        public bool TryFillBuretteFromBottle() { MeteredCalls++; return true; }
        public bool TryBottleWaterRinse() { MeteredCalls++; return true; }
        public string ActivePipetteStockInventoryId { get; private set; } = "source";
        public bool TryPipetteSwitchPrimary()
        {
            ActivePipetteStockInventoryId = "source";
            MeteredCalls++;
            return true;
        }
        public bool TryPipetteSwitchReserve()
        {
            ActivePipetteStockInventoryId = "reserve-stock";
            MeteredCalls++;
            return true;
        }
        public bool TryBottleRefillFromReserve() { MeteredCalls++; return true; }
        public bool TryPrepareResearchReserveStock(string stockId, double concentration)
        {
            if (string.IsNullOrEmpty(stockId)) return false;
            PreparedConcentration = concentration;
            MeteredCalls++;
            return true;
        }
        public bool Save() { SaveCalls++; return true; }
        public bool OpenReview()
        {
            ReviewOpens++;
            RecordedReview = new RecordedSessionReview();
            return true;
        }
        public void CloseReview() => RecordedReview = null;
        public bool ContinueHeldSession() => false;
    }
}
namespace PharmaLabVR.UI
{
    public sealed class BootMenu
    {
        public ScreenRouter Router { get; set; }
        public bool Confirmed { get; private set; }
        public bool CanConfirm { get; set; } = true;
        public void ChooseMode(PharmaLabVR.Input.ApplicationMode mode)
        {
            Router.SetBootMode(mode == PharmaLabVR.Input.ApplicationMode.VirtualReality ? "VR" : "Desktop");
            Router.Show(ScreenId.Setup);
        }
        public bool ConfirmSetup() { if (!CanConfirm) return false; Confirmed = true; return true; }
    }
}
