// Narrow adapters for compiling actual production ScreenRouter and LabPanelController
// on a runner without Unity. NOT a Native Core or XR simulator.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }
    public class MonoBehaviour { }
    public static class Mathf
    {
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.00001f;
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
    }
    public sealed class ObservationView
    {
        public string VesselId { get; set; }
        public string ObservableId { get; set; }
        public string ModelId { get; set; }
        public string Support { get; set; }
        public string Freshness { get; set; }
        public string Maturity { get; set; }
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
        public PharmaLabVR.Lab.LabViewModel CurrentLabView { get; } = new();
        public bool IsTimeHeld { get; private set; }
        public bool IsNativePaused { get; private set; }
        public bool NativeRecoveryReady { get; set; }
        public string HoldReason { get; private set; } = "";
        public string CurrentMode { get; set; } = "Desktop";
        public int PauseCalls { get; private set; }
        public int ContinueCalls { get; private set; }
        public event Action<PharmaLabVR.Lab.LabViewModel> LabViewChanged;
        public event Action<bool> HoldChanged;
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
        public void UpdateView() => LabViewChanged?.Invoke(CurrentLabView);
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
        public bool CanReleaseTool { get; private set; } = true;
        public int ReleaseCalls { get; private set; }
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
