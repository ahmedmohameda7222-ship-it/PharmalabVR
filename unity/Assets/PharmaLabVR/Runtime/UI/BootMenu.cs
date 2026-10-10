using PharmaLabVR.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public void StartDesktop()
        {
            RequestedMode = ApplicationMode.Desktop;
            SceneManager.LoadScene(LabScene, LoadSceneMode.Single);
        }

        public void StartVirtualReality()
        {
            RequestedMode = ApplicationMode.VirtualReality;
            SceneManager.LoadScene(LabScene, LoadSceneMode.Single);
        }

        private void OnGUI()
        {
            var width = Mathf.Min(520f, Screen.width - 40f);
            var area = new Rect((Screen.width - width) * 0.5f, Mathf.Max(30f, Screen.height * 0.2f), width, 300f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("PharmaLabVR — Research Foundation");
            GUILayout.Label("Choose an interaction mode. Both modes use the same native laboratory state.");
            if (GUILayout.Button("Desktop — mouse and keyboard", GUILayout.Height(56f))) StartDesktop();
            if (GUILayout.Button("Virtual reality — OpenXR", GUILayout.Height(56f))) StartVirtualReality();
            GUILayout.Label("Scientific observations show their support, freshness, and maturity. Research output is not certified assessment evidence.");
            GUILayout.EndArea();
        }
    }
}
