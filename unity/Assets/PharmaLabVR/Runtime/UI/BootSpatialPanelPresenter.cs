using System;
using System.Collections.Generic;
using PharmaLabVR.Input;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

namespace PharmaLabVR.UI
{
    // The Boot scene uses the same router and selection state as the Desktop IMGUI.
    // Its real UI Buttons are serialized by BuildPipelineEntry.AllAssets;
    // controllers drive them through XRRayInteractor -> XRUIInputModule -> Button.
    public sealed class BootSpatialPanelPresenter : MonoBehaviour
    {
        [SerializeField] private BootMenu menu;
        private readonly Dictionary<string, Button> buttons = new();
        private ScreenRouter router;
        private Canvas canvas;
        private Camera xrCamera;
        private Camera desktopCamera;
        private EventSystem eventSystem;
        private Text title;
        private Text status;
        private int page;
        private bool visible;
        private bool placed;

        public void Configure(BootMenu boot) => menu = boot;

        private void Start()
        {
            if (menu == null) menu = FindFirstObjectByType<BootMenu>();
            if (menu == null || menu.Router == null)
                throw new InvalidOperationException("Boot world panel requires the real BootMenu/ScreenRouter.");
            router = menu.Router;
            canvas = GetComponent<Canvas>();
            title = transform.Find("Boot Title")?.GetComponent<Text>();
            status = transform.Find("Boot Status")?.GetComponent<Text>();
            var origin = GetComponentInParent<XROrigin>();
            xrCamera = origin != null ? origin.Camera : null;
            desktopCamera = GameObject.Find("BootCamera")?.GetComponent<Camera>();
            eventSystem = GetComponentInParent<BootMenu>()?
                .GetComponentInChildren<EventSystem>(true);
            if (xrCamera == null || eventSystem == null || canvas == null)
                throw new InvalidOperationException("Boot scene lacks configured XR camera or EventSystem.");
            foreach (var button in GetComponentsInChildren<Button>(true))
                buttons.Add(button.name, button);
            WireActions();
            ConfigureRays(origin);
            router.Changed += Present;
            Present(router.Current);
        }

        private void ConfigureRays(XROrigin origin)
        {
            foreach (var ray in origin.GetComponentsInChildren<XRRayInteractor>(true))
            {
                var hand = ray.name.StartsWith("Left", StringComparison.Ordinal) ? "Left" : "Right";
                var usage = hand + "Hand";
                var press = new XRInputButtonReader(hand + " Boot UI Press",
                    inputSourceMode: XRInputButtonReader.InputSourceMode.InputAction);
                press.inputActionPerformed = new InputAction(hand + " Boot UI Press",
                    InputActionType.Button, $"<XRController>{{{usage}}}/triggerPressed");
                press.inputActionValue = new InputAction(hand + " Boot UI Press Value",
                    InputActionType.Value, $"<XRController>{{{usage}}}/trigger", expectedControlType: "Axis");
                ray.uiPressInput = press;
                ray.enableUIInteraction = true;
#pragma warning disable CS0618
                ray.inputCompatibilityMode = XRBaseInputInteractor.InputCompatibilityMode.ForceInputReaders;
#pragma warning restore CS0618
            }
        }

        private void Hook(string name, Action callback)
        {
            if (!buttons.TryGetValue(name, out var button))
                throw new InvalidOperationException("Missing serialized Boot button: " + name);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback());
        }

        private void WireActions()
        {
            Hook("Enter VR", () => menu.Panels.Request(PanelAction.EnterVR));
            Hook("Desktop", () => menu.Panels.Request(PanelAction.EnterDesktop));
            Hook("Seated", () => Update(p => p.With(posture: "Seated")));
            Hook("Standing", () => Update(p => p.With(posture: "Standing")));
            Hook("Left hand", () => Update(p => p.With(dominantHand: "Left")));
            Hook("Right hand", () => Update(p => p.With(dominantHand: "Right")));
            Hook("Bench lower", () => Update(p => p.With(benchHeightMeters:
                Mathf.Clamp(p.BenchHeightMeters - 0.05f, 0.65f, 1.05f))));
            Hook("Bench higher", () => Update(p => p.With(benchHeightMeters:
                Mathf.Clamp(p.BenchHeightMeters + 0.05f, 0.65f, 1.05f))));
            Hook("Text larger", () => Update(p => p.With(textScale:
                Mathf.Clamp(p.TextScale + 0.05f, 0.85f, 1.35f))));
            Hook("Text smaller", () => Update(p => p.With(textScale:
                Mathf.Clamp(p.TextScale - 0.05f, 0.85f, 1.35f))));
            Hook("Captions", () => Update(p => p.With(captionsEnabled: !p.CaptionsEnabled)));
            Hook("Reduced motion", () => Update(p => p.With(reducedMotion: !p.ReducedMotion)));
            Hook("High contrast", () => Update(p => p.With(highContrast: !p.HighContrast)));
            Hook("Audio mute", () => Update(p => p.With(audioVolume:
                p.AudioVolume > 0.01f ? 0f : 1f)));
            Hook("Next", () => { page = Mathf.Min(page + 1, 2); Present(router.Current); });
            Hook("Previous", () => { page = Mathf.Max(page - 1, 0); Present(router.Current); });
            Hook("Enter lab", () => menu.Panels.Request(PanelAction.ConfirmSetup));
            Hook("Back", () => menu.Panels.Request(PanelAction.Home));
        }

        private void Update(Func<LabSetupPreferences, LabSetupPreferences> change)
        {
            if (router?.Current?.ScreenId != ScreenId.Setup) return;
            router.UpdateSetupPreferences(change(router.Current.Setup));
        }

        private void Set(string name, bool enabled, float y)
        {
            var button = buttons[name];
            button.gameObject.SetActive(enabled);
            if (enabled)
                button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);
        }

        private void Present(PanelState state)
        {
            if (state == null) return;
            if (state.ScreenId != ScreenId.Setup) page = 0;
            if (title != null) title.text = "PharmaLabVR · " + state.ScreenId;
            if (status != null)
            {
                var availability = menu.LaunchAvailability();
                status.text = state.ScreenId == ScreenId.Setup ?
                    "Setup " + (page + 1) + "/3 · " + state.Mode +
                    "\n" + (availability.CanEnter(BootMenu.RequestedMode) ?
                        "XR ready" : availability.UnavailableReason(BootMenu.RequestedMode)) :
                    "Choose a laboratory mode";
                if (!string.IsNullOrEmpty(menu.LastLaunchError))
                    status.text += "\n" + menu.LastLaunchError;
            }
            var home = state.ScreenId == ScreenId.Home;
            Set("Enter VR", home, 80f);
            Set("Desktop", home, 12f);
            string[] first = { "Seated", "Standing", "Left hand", "Right hand" };
            string[] second = { "Bench lower", "Bench higher", "Text larger", "Text smaller" };
            string[] third = { "Captions", "Reduced motion", "High contrast", "Audio mute" };
            var choices = page == 0 ? first : page == 1 ? second : third;
            foreach (var item in first) Set(item, !home && page == 0, 78f - Array.IndexOf(first, item) * 56f);
            foreach (var item in second) Set(item, !home && page == 1, 78f - Array.IndexOf(second, item) * 56f);
            foreach (var item in third) Set(item, !home && page == 2, 78f - Array.IndexOf(third, item) * 56f);
            Set("Next", !home && page < 2, -153f);
            Set("Previous", !home && page > 0, -153f);
            Set("Enter lab", !home, -222f);
            Set("Back", !home, -279f);
            var contrast = state.Setup.HighContrast;
            foreach (var button in buttons.Values)
            {
                var label = button.GetComponentInChildren<Text>(true);
                if (label != null) label.fontSize = Mathf.RoundToInt(21f * state.Setup.TextScale);
                var image = button.GetComponent<Image>();
                if (image != null) image.color = contrast ?
                    new Color(0.02f, 0.10f, 0.11f) : new Color(0.13f, 0.33f, 0.36f);
            }
        }

        private void LateUpdate()
        {
            if (menu == null || xrCamera == null || eventSystem == null) return;
            var available = menu.LaunchAvailability();
            var enable = available.XrLoaderActive && available.HeadsetPresent;
            if (enable != visible)
            {
                visible = enable;
                canvas.enabled = enable;
                eventSystem.gameObject.SetActive(enable);
                xrCamera.enabled = enable;
                var audio = xrCamera.GetComponent<AudioListener>();
                if (audio != null) audio.enabled = enable;
                if (desktopCamera != null) desktopCamera.enabled = !enable;
                placed = false;
                if (enable) Present(router.Current);
            }
            if (!enable || placed) return;
            var pose = xrCamera.transform;
            var side = SetupPreferencesSelection.Current.DominantHand == "Left" ? -0.15f : 0.15f;
            transform.position = pose.position + pose.forward * 0.95f + pose.right * side;
            transform.rotation = Quaternion.LookRotation(pose.forward, Vector3.up);
            placed = true;
        }

        private void OnDestroy()
        {
            if (router != null) router.Changed -= Present;
        }
    }
}
