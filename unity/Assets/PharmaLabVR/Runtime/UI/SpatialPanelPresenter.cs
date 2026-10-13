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
        private Text title;
        private Text detail;
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
            }
            panelRoot = new GameObject("SpatialPanel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            panelRoot.transform.SetParent(transform, false);
            var rect = panelRoot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 520f);
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = new Vector3(0.36f, 1.37f, 0.95f);
            rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var canvas = panelRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = origin.Camera;
            var background = panelRoot.AddComponent<Image>();
            background.color = new Color(0.91f, 0.94f, 0.94f, 0.97f);
            title = CreateText("Panel title", 214f, 42f, 26, FontStyle.Bold);
            detail = CreateText("Panel detail", 126f, 112f, 20, FontStyle.Normal);
            AddButton("Inspect receiver", 28f, PanelAction.Inspect, "receiver");
            AddButton("Inspect source", -28f, PanelAction.Inspect, "source");
            AddButton("Measure", -84f, PanelAction.Measure, "receiver");
            AddButton("Return to lab", -130f, PanelAction.Lab);
            AddButton("Pause", -184f, PanelAction.Pause);
            AddButton("Save session", -236f, PanelAction.Save);
            AddButton("Continue", -184f, PanelAction.Continue);

            var eventObject = new GameObject("SpatialEventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            eventObject.transform.SetParent(panelRoot.transform, false);
            var module = eventObject.GetComponent<XRUIInputModule>();
            module.enableXRInput = true;
            module.enableMouseInput = false;
            module.enableTouchInput = false;
            module.enableGamepadInput = false;
            module.enableJoystickInput = false;
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

        private void Present(PanelState state)
        {
            if (panelRoot == null || driver == null) return;
            panelRoot.SetActive(driver.CurrentMode == "VR");
            title.text = state.ScreenId.ToString();
            detail.text = state.ScreenId switch
            {
                ScreenId.Hold => state.HoldReason + " hold. Close the valve and restore a neutral tracked input.",
                ScreenId.Pause => "Material and simulation time are paused.",
                ScreenId.Inspect or ScreenId.Measure => state.SelectedEntityId + " · " +
                    (driver.Measurement?.DisplayValue ?? "--") + " pH · " + state.ObservationFreshness,
                _ => "Select a vessel to inspect. Research model · " + state.SaveState
            };
            SetButton("Inspect receiver", state.SupportedActions.Contains("Inspect") && state.ScreenId == ScreenId.Lab);
            SetButton("Inspect source", state.SupportedActions.Contains("Inspect") && state.ScreenId == ScreenId.Lab);
            SetButton("Measure", state.SupportedActions.Contains("Measure"));
            SetButton("Return to lab", state.SupportedActions.Contains("Lab"));
            SetButton("Pause", state.SupportedActions.Contains("Pause"));
            SetButton("Save session", state.SupportedActions.Contains("Save"));
            SetButton("Continue", state.SupportedActions.Contains("Continue"));
        }

        private void SetButton(string name, bool visible)
        {
            if (buttons.TryGetValue(name, out var button)) button.gameObject.SetActive(visible);
        }
    }
}
