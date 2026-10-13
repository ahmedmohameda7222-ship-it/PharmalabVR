using PharmaLabVR.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
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
#if PHARMALABVR_VR_FIRST
                return ApplicationMode.VirtualReality;
#else
                return ApplicationMode.Desktop;
#endif
            }
        }

        private ScreenRouter router;
        private LabPanelController panels;

        private void Awake()
        {
            router = GetComponent<ScreenRouter>();
            if (router == null) router = gameObject.AddComponent<ScreenRouter>();
            router.Configure(null);
            router.Show(ScreenId.Home);
            panels = GetComponent<LabPanelController>();
            if (panels == null) panels = gameObject.AddComponent<LabPanelController>();
            panels.ConfigureBoot(router, this);
        }

        public void ChooseMode(ApplicationMode mode)
        {
            RequestedMode = mode;
            router.SetBootMode(mode == ApplicationMode.VirtualReality ? "VR" : "Desktop");
            router.Show(ScreenId.Setup);
        }

        public void ConfirmSetup()
        {
            if (router?.Current?.ScreenId != ScreenId.Setup) return;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(LabScene, LoadSceneMode.Single);
#endif
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
            var width = Mathf.Min(520f, Screen.width - 24f);
            var area = new Rect(12f, 12f, width, 300f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("PharmaLabVR — Research Foundation");
            var screen = router?.Current?.ScreenId ?? ScreenId.Home;
            if (screen == ScreenId.Home)
            {
                GUILayout.Label("Choose an interaction mode. Both modes use the same native laboratory state.");
                if (GUILayout.Button("Desktop — mouse and keyboard", GUILayout.Height(56f))) panels?.Request(PanelAction.EnterDesktop);
                if (GUILayout.Button("Virtual reality — OpenXR", GUILayout.Height(56f))) panels?.Request(PanelAction.EnterVR);
            }
            else if (screen == ScreenId.Setup)
            {
                GUILayout.Label("Setup · " + router.Current.Mode);
                GUILayout.Label("Check your posture, dominant hand, bench reach and text preference before entering the lab.");
                if (GUILayout.Button("Enter lab", GUILayout.Height(56f))) panels?.Request(PanelAction.ConfirmSetup);
                if (GUILayout.Button("Back", GUILayout.Height(44f))) panels?.Request(PanelAction.Home);
            }
            GUILayout.Label("Scientific observations show their support, freshness, and maturity. Research output is not certified assessment evidence.");
            GUILayout.EndArea();
        }
    }
}
