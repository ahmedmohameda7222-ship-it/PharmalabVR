using System;
using System.Collections.Generic;
using System.Linq;
using PharmaLabVR.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

namespace PharmaLabVR.UI
{
    // Presentation only: every material and session action still goes through LabPanelController.
    public sealed class SpatialPanelPresenter : MonoBehaviour
    {
        private ScreenRouter router;
        private LabPanelController panels;
        private CoreDriver driver;
        private GameObject panelRoot;
        private Camera xrCamera;
        private bool placedWithTrackedHead;
        private int settingsPage;
        private Text title;
        private Text detail;
        private Image background;
        private readonly Dictionary<string, Button> buttons = new();

        public void Configure(ScreenRouter screenRouter, LabPanelController panelController, CoreDriver coreDriver)
        {
            if (router != null) router.Changed -= Present;
            router = screenRouter;
            panels = panelController;
            driver = coreDriver;
            if (panelRoot == null) Build();
            if (router != null) router.Changed += Present;
            if (router?.Current != null) Present(router.Current);
        }

        private void OnDestroy()
        {
            if (router != null) router.Changed -= Present;
        }

        private void Build()
        {
            var origin = driver != null ? driver.transform.root.GetComponentInChildren<XROrigin>(true) : null;
            if (origin == null || origin.Camera == null) return;
            xrCamera = origin.Camera;
            foreach (var ray in origin.GetComponentsInChildren<XRRayInteractor>(true))
            {
                var hand = ray.name.StartsWith("Left", StringComparison.Ordinal) ? "Left" : "Right";
                var usage = hand + "Hand";
                var press = new XRInputButtonReader(hand + " UI Press", inputSourceMode:
                    XRInputButtonReader.InputSourceMode.InputAction);
                press.inputActionPerformed = new InputAction(hand + " UI Press", InputActionType.Button,
                    $"<XRController>{{{usage}}}/triggerPressed");
                press.inputActionValue = new InputAction(hand + " UI Press Value", InputActionType.Value,
                    $"<XRController>{{{usage}}}/trigger", expectedControlType: "Axis");
                ray.uiPressInput = press;
                ray.enableUIInteraction = true;
#pragma warning disable CS0618
                ray.inputCompatibilityMode = XRBaseInputInteractor.InputCompatibilityMode.ForceInputReaders;
#pragma warning restore CS0618
            }
            panelRoot = new GameObject("SpatialPanel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            panelRoot.transform.SetParent(transform, false);
            var rect = panelRoot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 560f);
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = new Vector3(0.36f, 1.37f, 0.95f);
            rect.localRotation = Quaternion.identity;
            var canvas = panelRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = xrCamera;
            background = panelRoot.AddComponent<Image>();
            background.color = new Color(0.91f, 0.94f, 0.94f, 0.97f);
            title = CreateText("Panel title", 214f, 42f, 26, FontStyle.Bold);
            detail = CreateText("Panel detail", 126f, 112f, 20, FontStyle.Normal);
            AddButton("Inspect receiver", 30f, PanelAction.Inspect, "receiver");
            AddButton("Inspect source", -26f, PanelAction.Inspect, "source");
            AddButton("Measure", -82f, PanelAction.Measure, "receiver");
            AddButton("Open review", -138f, PanelAction.Review);
            AddButton("Return to lab", -138f, PanelAction.Lab);
            AddButton("Pause", -194f, PanelAction.Pause);
            AddButton("Save session", -250f, PanelAction.Save);
            AddButton("Continue", -194f, PanelAction.Continue);
            AddButton("Settings", -82f, PanelAction.Settings);
            AddButton("Back", -138f, PanelAction.Back);
            AddButton("Release docked tool", -138f, PanelAction.ReleaseTool);
            AddButton("Recenter view", 30f, PanelAction.Recenter);
            AddButton("Snap left 30", -26f, PanelAction.SnapLeft);
            AddButton("Snap right 30", -26f, PanelAction.SnapRight);
            // Half-width peers share one row without overlapping each other,
            // the Recenter button above, or the Settings button below.
            foreach (var name in new[] { "Snap left 30", "Snap right 30" })
            {
                buttons[name].GetComponent<RectTransform>().sizeDelta = new Vector2(176f, 46f);
                // The Text also raycasts; its child rectangle must not span
                // across the adjacent button.
                var text = buttons[name].GetComponentInChildren<Text>(true);
                text.rectTransform.sizeDelta = new Vector2(164f, 35f);
            }
            buttons["Snap left 30"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(-94f, -26f);
            buttons["Snap right 30"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(94f, -26f);
            AddSettingButton("Settings choice A", 30f, 0);
            AddSettingButton("Settings choice B", -26f, 1);
            AddSettingButton("Settings choice C", -82f, 2);
            AddSettingButton("Settings choice D", -138f, 3);
            AddSettingNavigation("Next settings", -194f, 1);
            AddSettingNavigation("Previous settings", -194f, -1);

            var eventObject = new GameObject("SpatialEventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            eventObject.transform.SetParent(panelRoot.transform, false);
            var module = eventObject.GetComponent<XRUIInputModule>();
            module.enableXRInput = true;
            module.enableMouseInput = false;
            module.enableTouchInput = false;
            module.enableGamepadInput = false;
            module.enableJoystickInput = false;
        }

        private void LateUpdate()
        {
            if (panelRoot == null || !panelRoot.activeSelf || xrCamera == null || placedWithTrackedHead) return;
            UpdatePlacement();
        }

        private void UpdatePlacement()
        {
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            var tracked = head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var value) && value;
            var pose = xrCamera.transform;
            var setup = SetupPreferencesSelection.Current;
            var side = setup.DominantHand == "Left" ? -0.25f : 0.25f;
            var postureOffset = setup.Posture == "Seated" ? -0.10f : 0f;
            var benchOffset = setup.BenchHeightMeters - 0.85f;
            panelRoot.transform.position = pose.position + pose.forward * 0.95f + pose.right * side +
                Vector3.up * (postureOffset + benchOffset);
            panelRoot.transform.rotation = Quaternion.LookRotation(pose.forward, Vector3.up);
            placedWithTrackedHead = tracked;
        }

        private Text CreateText(string name, float y, float height, int fontSize, FontStyle style)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(panelRoot.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(368f, height);
            rect.anchoredPosition = new Vector2(0f, y);
            var label = child.GetComponent<Text>();
            label.font = Resources.Load<Font>("Fonts/NotoSans-Regular");
            if (label.font == null) throw new InvalidOperationException("Pinned Noto Sans UI font is missing.");
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = new Color(0.12f, 0.18f, 0.20f);
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private void AddButton(string name, float y, PanelAction action, string entityId = null)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            child.transform.SetParent(panelRoot.transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(368f, 46f);
            rect.anchoredPosition = new Vector2(0f, y);
            child.GetComponent<Image>().color = new Color(0.14f, 0.37f, 0.39f);
            var button = child.GetComponent<Button>();
            button.onClick.AddListener(() => panels?.Request(action, entityId));
            var label = CreateText(name + " label", 0f, 35f, 21, FontStyle.Normal);
            label.transform.SetParent(child.transform, false);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = name;
            buttons.Add(name, button);
        }

        private void AddSettingButton(string name, float y, int choice)
        {
            AddButton(name, y, PanelAction.Settings);
            var button = buttons[name];
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ChangeSetting(choice));
        }

        private void AddSettingNavigation(string name, float y, int direction)
        {
            AddButton(name, y, PanelAction.Settings);
            var button = buttons[name];
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                settingsPage = Mathf.Clamp(settingsPage + direction, 0, 2);
                if (router?.Current != null) Present(router.Current);
            });
        }

        private void ChangeSetting(int choice)
        {
            if (router?.Current?.ScreenId != ScreenId.Settings) return;
            var current = router.Current.Setup;
            LabSetupPreferences next = current;
            switch (settingsPage)
            {
                case 0:
                    if (choice == 0)
                        next = current.With(posture: current.Posture == "Seated" ? "Standing" : "Seated");
                    else if (choice == 1)
                        next = current.With(dominantHand: current.DominantHand == "Left" ? "Right" : "Left");
                    break;
                case 1:
                    if (choice == 0) next = current.With(benchHeightMeters:
                        Mathf.Clamp(current.BenchHeightMeters - 0.05f, 0.65f, 1.05f));
                    else if (choice == 1) next = current.With(benchHeightMeters:
                        Mathf.Clamp(current.BenchHeightMeters + 0.05f, 0.65f, 1.05f));
                    else if (choice == 2) next = current.With(textScale:
                        Mathf.Clamp(current.TextScale + 0.05f, 0.85f, 1.35f));
                    else if (choice == 3) next = current.With(textScale:
                        Mathf.Clamp(current.TextScale - 0.05f, 0.85f, 1.35f));
                    break;
                case 2:
                    if (choice == 0) next = current.With(captionsEnabled: !current.CaptionsEnabled);
                    else if (choice == 1) next = current.With(reducedMotion: !current.ReducedMotion);
                    else if (choice == 2) next = current.With(highContrast: !current.HighContrast);
                    else if (choice == 3) next = current.With(audioVolume:
                        current.AudioVolume >= 0.99f ? 0f : 1f);
                    break;
            }
            if (!ReferenceEquals(current, next)) router.UpdateSetupPreferences(next);
        }

        private void PresentSettings(PanelState state)
        {
            var setup = state.Setup;
            var labels = settingsPage switch
            {
                0 => new[] { "Posture: " + setup.Posture, "Hand: " + setup.DominantHand, "", "" },
                1 => new[] { "Bench lower", "Bench higher", "Text larger", "Text smaller" },
                _ => new[] { "Captions: " + (setup.CaptionsEnabled ? "On" : "Off"),
                    "Reduced motion: " + (setup.ReducedMotion ? "On" : "Off"),
                    "Contrast: " + (setup.HighContrast ? "High" : "Normal"),
                    "Audio: " + (setup.AudioVolume > 0f ? "On" : "Muted") }
            };
            var names = new[] { "Settings choice A", "Settings choice B",
                "Settings choice C", "Settings choice D" };
            for (var i = 0; i < names.Length; i++)
            {
                SetButton(names[i], i < (settingsPage == 0 ? 2 : 4));
                var label = buttons[names[i]].GetComponentInChildren<Text>(true);
                if (label != null) label.text = labels[i];
            }
            SetButton("Next settings", settingsPage < 2);
            SetButton("Previous settings", settingsPage > 0);
            detail.text = "Settings " + (settingsPage + 1) + "/3 · " +
                setup.BenchHeightMeters.ToString("0.00") + "m · text " +
                setup.TextScale.ToString("0.00") + "×";
        }

        private void Present(PanelState state)
        {
            if (panelRoot == null || driver == null) return;
            panelRoot.SetActive(driver.CurrentMode == "VR");
            if (panelRoot.activeSelf) UpdatePlacement();
            title.fontSize = Mathf.RoundToInt(26f * state.Setup.TextScale);
            detail.fontSize = Mathf.RoundToInt(20f * state.Setup.TextScale);
            background.color = state.Setup.HighContrast ? Color.white :
                new Color(0.91f, 0.94f, 0.94f, 0.97f);
            foreach (var button in buttons.Values)
            {
                var label = button.GetComponentInChildren<Text>(true);
                if (label != null) label.fontSize = Mathf.RoundToInt(21f * state.Setup.TextScale);
                button.GetComponent<Image>().color = state.Setup.HighContrast ?
                    new Color(0.04f, 0.17f, 0.18f) : new Color(0.14f, 0.37f, 0.39f);
            }
            if (state.ScreenId != ScreenId.Settings) settingsPage = 0;
            title.text = state.ScreenId.ToString();
            detail.text = state.ScreenId switch
            {
                ScreenId.Hold => state.HoldReason + " hold. Close the valve and restore a neutral tracked input.",
                ScreenId.Pause => "Material and simulation time are paused.",
                ScreenId.Review => ReviewDetails(),
                ScreenId.Inspect or ScreenId.Measure => state.SelectedEntityId + " · " +
                    (driver.Measurement?.DisplayValue ?? "--") + " pH · " + state.ObservationFreshness,
                _ => state.Setup.CaptionsEnabled ?
                    "Select a vessel to inspect. Research model · " + state.SaveState :
                    "Research model · " + state.SaveState
            };
            SetButton("Inspect receiver", state.SupportedActions.Contains("Inspect") && state.ScreenId == ScreenId.Lab);
            SetButton("Inspect source", state.SupportedActions.Contains("Inspect") && state.ScreenId == ScreenId.Lab);
            SetButton("Measure", state.SupportedActions.Contains("Measure"));
            // Keep the Lab controls on distinct physical hit targets. While
            // docked, Review remains reachable through Pause -> Review;
            // once released the same slot returns to the direct Review action.
            var dockReleaseVisible = state.ScreenId == ScreenId.Lab &&
                panels != null && panels.CanReleaseTool;
            SetButton("Open review", state.SupportedActions.Contains("Review") &&
                !dockReleaseVisible);
            buttons["Open review"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, state.ScreenId == ScreenId.Lab ? -138f : 30f);
            SetButton("Return to lab", state.SupportedActions.Contains("Lab"));
            SetButton("Pause", state.SupportedActions.Contains("Pause"));
            SetButton("Save session", state.SupportedActions.Contains("Save") &&
                state.ScreenId != ScreenId.Settings);
            SetButton("Continue", state.SupportedActions.Contains("Continue"));
            SetButton("Settings", state.SupportedActions.Contains("Settings"));
            SetButton("Back", state.SupportedActions.Contains("Back"));
            SetButton("Release docked tool", state.SupportedActions.Contains("ReleaseTool") &&
                dockReleaseVisible);
            SetButton("Recenter view", state.SupportedActions.Contains("Recenter"));
            SetButton("Snap left 30", state.SupportedActions.Contains("SnapLeft"));
            SetButton("Snap right 30", state.SupportedActions.Contains("SnapRight"));
            buttons["Back"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, state.ScreenId == ScreenId.Settings ? -250f : -138f);
            if (state.ScreenId == ScreenId.Settings) PresentSettings(state);
            else
            {
                foreach (var name in new[] { "Settings choice A", "Settings choice B",
                    "Settings choice C", "Settings choice D", "Next settings", "Previous settings" })
                    SetButton(name, false);
            }
        }

        private void SetButton(string name, bool visible)
        {
            if (buttons.TryGetValue(name, out var button)) button.gameObject.SetActive(visible);
        }

        private string ReviewDetails()
        {
            var recorded = panels?.CurrentReview;
            if (recorded == null) return "Recorded review unavailable.";
            var values = recorded.Observations.Take(3)
                .Select(observation => observation.vesselId + " pH " +
                    observation.DisplayValue + " · " + observation.freshness);
            return "Recorded · no recomputation\n" +
                (recorded.Observations.Length == 0 ? "No observations recorded." : string.Join("\n", values));
        }
    }
}
