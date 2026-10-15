using PharmaLabVR.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace PharmaLabVR.UI
{
    public sealed class BootMenu : MonoBehaviour
    {
        public const string LabScene = "Lab";
        public static ApplicationMode RequestedMode { get; private set; } = DefaultMode;

        public static ApplicationMode DefaultMode
        {
            get
            {
#if PHARMALABVR_VR_FIRST || UNITY_ANDROID
                return ApplicationMode.VirtualReality;
#else
                return ApplicationMode.Desktop;
#endif
            }
        }

        private ScreenRouter router;
        private LabPanelController panels;
        private Vector2 setupScroll;
        public ScreenRouter Router => router;
        public LabPanelController Panels => panels;
        public string LastLaunchError { get; private set; }

        private void Awake()
        {
            // Boot is the persistence boundary for local ergonomics only.
            // Do not store or restore Native chemistry through PlayerPrefs.
            SetupPreferencesPersistence.Initialize();
            router = GetComponent<ScreenRouter>();
            if (router == null) router = gameObject.AddComponent<ScreenRouter>();
            router.Configure(null);
            router.Show(ScreenId.Home);
            panels = GetComponent<LabPanelController>();
            if (panels == null) panels = gameObject.AddComponent<LabPanelController>();
            panels.ConfigureBoot(router, this);
            // Standalone Android has no Desktop route: enter VR Setup directly.
            if (Application.platform == RuntimePlatform.Android)
                ChooseMode(ApplicationMode.VirtualReality);
        }

        public void ChooseMode(ApplicationMode mode)
        {
            RequestedMode = mode;
            router.SetBootMode(mode == ApplicationMode.VirtualReality ? "VR" : "Desktop");
            router.Show(ScreenId.Setup);
        }

        // A successful panel action means a scene transition was admitted, not
        // merely that a button handler happened to run.
        public BootLaunchAvailability LaunchAvailability()
        {
#if UNITY_EDITOR
            var hasScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/PharmaLabVR/Scenes/Lab.unity") != null;
#else
            var hasScene = Application.CanStreamedLevelBeLoaded(LabScene);
#endif
            var manager = XRGeneralSettings.Instance != null
                ? XRGeneralSettings.Instance.Manager : null;
            var xrLoaderReady = manager != null && manager.activeLoader != null;
            var headset = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return new BootLaunchAvailability(Application.platform == RuntimePlatform.Android,
                xrLoaderReady, headset.isValid, hasScene);
        }

        public bool ConfirmSetup()
        {
            if (router?.Current?.ScreenId != ScreenId.Setup)
            {
                LastLaunchError = "Finish Setup before entering the lab.";
                return false;
            }
            var status = LaunchAvailability();
            if (!status.CanEnter(RequestedMode))
            {
                LastLaunchError = status.UnavailableReason(RequestedMode);
                return false;
            }
            try
            {
#if UNITY_EDITOR
                EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadScene(LabScene, LoadSceneMode.Single);
#endif
                LastLaunchError = null;
                return true;
            }
            catch (System.Exception exception)
            {
                LastLaunchError = "Could not load the Lab scene: " + exception.Message;
                return false;
            }
        }

        public void StartDesktop()
        {
            ChooseMode(ApplicationMode.Desktop);
            ConfirmSetup();
        }

        public void StartVirtualReality()
        {
            ChooseMode(ApplicationMode.VirtualReality);
            ConfirmSetup();
        }

        private void OnGUI()
        {
            // XR renders its own world-space controls; never draw an IMGUI layer
            // over a tracked head-mounted display. Desktop remains a fallback.
            if (LaunchAvailability().CanEnter(ApplicationMode.VirtualReality)) return;
            var area = new Rect(12f, 12f, Mathf.Max(1f, Mathf.Min(520f, Screen.width - 24f)),
                Mathf.Max(1f, Screen.height - 24f));
            var previousLabelSize = GUI.skin.label.fontSize;
            var previousButtonSize = GUI.skin.button.fontSize;
            try
            {
                var scale = router?.Current?.Setup.TextScale ?? 1f;
                GUI.skin.label.fontSize = Mathf.RoundToInt(14f * scale);
                GUI.skin.button.fontSize = Mathf.RoundToInt(14f * scale);
                GUILayout.BeginArea(area, GUI.skin.box);
                setupScroll = GUILayout.BeginScrollView(setupScroll);
                GUILayout.Label("PharmaLabVR — Research Foundation");
                var screen = router?.Current?.ScreenId ?? ScreenId.Home;
                if (screen == ScreenId.Home)
                {
                    GUILayout.Label("Choose an interaction mode. Both use the same native laboratory authority.");
                    if (GUILayout.Button("Desktop — mouse and keyboard", GUILayout.Height(56f)))
                        panels?.Request(PanelAction.EnterDesktop);
                    if (GUILayout.Button("Virtual reality — OpenXR", GUILayout.Height(56f)))
                        panels?.Request(PanelAction.EnterVR);
                }
                else if (screen == ScreenId.Setup)
                {
                    GUILayout.Label("Setup · " + router.Current.Mode);
                    var availability = LaunchAvailability();
                    var selected = RequestedMode;
                    if (!availability.CanEnter(selected))
                        GUILayout.Label("Cannot enter yet: " + availability.UnavailableReason(selected));
                    SetupPreferenceControls.Draw(router);
                    GUILayout.Label("Physical reach and headset alignment still require device calibration.");
                    if (GUILayout.Button("Enter lab", GUILayout.Height(56f)))
                        panels?.Request(PanelAction.ConfirmSetup);
                    if (GUILayout.Button("Back", GUILayout.Height(44f)))
                        panels?.Request(PanelAction.Home);
                }
                if (!string.IsNullOrEmpty(LastLaunchError))
                    GUILayout.Label("Entry blocked: " + LastLaunchError);
                GUILayout.Label("Research observations state support, freshness and maturity; not certified evidence.");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.skin.label.fontSize = previousLabelSize;
                GUI.skin.button.fontSize = previousButtonSize;
            }
        }

    }
}
