using System;
using System.Collections.Generic;
using System.Linq;
using PharmaLabVR.Core;
using PharmaLabVR.Lab;
using PharmaLabVR.Input;
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
        private int instrumentPage;
        private bool portableRecoveryPage;
        private int researchStockIndex;
        private int researchConcentrationIndex = 2;
        private static readonly string[] ResearchStockIds =
        {
            "nacl", "hcl", "naoh", "acetic-acid", "sodium-acetate", "water"
        };
        private Text title;
        private Text detail;
        private ScrollRect detailScroller;
        private string baselineDetail = string.Empty;
        private string lastPhysicalFeedback = null;
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
            rect.sizeDelta = new Vector2(420f, 700f);
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = new Vector3(0.36f, 1.37f, 0.95f);
            rect.localRotation = Quaternion.identity;
            var canvas = panelRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = xrCamera;
            background = panelRoot.AddComponent<Image>();
            background.color = new Color(0.91f, 0.94f, 0.94f, 0.97f);
            title = CreateText("Panel title", 214f, 42f, 26, FontStyle.Bold);
            // A bounded, scrollable area keeps Native scientific provenance
            // and long student/teaching notes above every working XR button.
            var scrollWindow = new GameObject("Detail scroller",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollWindow.transform.SetParent(panelRoot.transform, false);
            var scrollBounds = scrollWindow.GetComponent<RectTransform>();
            scrollBounds.sizeDelta = new Vector2(368f, 112f);
            scrollBounds.anchoredPosition = new Vector2(0f, 126f);
            var scrollHit = scrollWindow.GetComponent<Image>();
            scrollHit.color = new Color(1f, 1f, 1f, 0.01f);
            scrollHit.raycastTarget = true;
            var viewport = new GameObject("Detail viewport",
                typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollWindow.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.anchoredPosition = Vector2.zero;
            detail = CreateText("Panel detail", 0f, 112f, 20, FontStyle.Normal);
            detail.transform.SetParent(viewport.transform, false);
            detail.rectTransform.anchorMin = new Vector2(0f, 1f);
            detail.rectTransform.anchorMax = Vector2.one;
            detail.rectTransform.pivot = new Vector2(0.5f, 1f);
            detail.rectTransform.sizeDelta = new Vector2(0f, 112f);
            detail.rectTransform.anchoredPosition = Vector2.zero;
            var fitter = detail.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            detailScroller = scrollWindow.GetComponent<ScrollRect>();
            detailScroller.viewport = viewportRect;
            detailScroller.content = detail.rectTransform;
            detailScroller.horizontal = false;
            detailScroller.vertical = true;
            detailScroller.movementType = ScrollRect.MovementType.Clamped;
            detailScroller.scrollSensitivity = 22f;
            AddButton("Inspect receiver", 30f, PanelAction.Inspect, "receiver");
            AddButton("Inspect source", -26f, PanelAction.Inspect, "source");
            AddButton("Measure", -82f, PanelAction.Measure, "receiver");
            AddButton("Record Native pH", -306f, PanelAction.RecordCurrentPH);
            AddButton("Explain: composition", 30f, PanelAction.RecordResearchExplanation,
                "composition-hypothesis");
            AddButton("Explain: method", -26f, PanelAction.RecordResearchExplanation,
                "method-uncertainty");
            AddButton("Explain: model", -306f, PanelAction.RecordResearchExplanation,
                "model-limitations");
            AddButton("Open review", -138f, PanelAction.Review);
            AddButton("Open notebook", -306f, PanelAction.Notebook);
            AddButton("Next imported note", -306f, PanelAction.NextInstructorAnnotation);
            AddButton("Export archive", -250f, PanelAction.ExportPortable);
            AddButton("Export results CSV", -250f, PanelAction.ExportCsv);
            AddButton("Preview portable archive", 30f, PanelAction.PreviewPortableArchive);
            AddButton("Choose local archive", -26f, PanelAction.ChoosePortableArchive);
            AddButton("Compare saved", -194f, PanelAction.CompareSaved);
            AddButton("Return to lab", -138f, PanelAction.Lab);
            AddButton("Pause", -194f, PanelAction.Pause);
            AddButton("Save session", -250f, PanelAction.Save);
            AddButton("Continue", -194f, PanelAction.Continue);
            AddButton("Settings", -82f, PanelAction.Settings);
            AddButton("Back", -138f, PanelAction.Back);
            AddButton("Release docked tool", -138f, PanelAction.ReleaseTool);
            AddButton("Park tool", -306f, PanelAction.ParkTool);
            AddButton("Load saved session", -306f, PanelAction.Load);
            AddInstrumentButton("Portable study restore", -194f,
                () => portableRecoveryPage = true);
            AddInstrumentButton("Choose study package", 30f,
                () => panels?.Request(PanelAction.ChoosePortableArchive));
            AddInstrumentButton("Restore study package", -26f, () =>
            {
                if (panels?.Request(PanelAction.RestorePortable) == true)
                    portableRecoveryPage = false;
            });
            AddInstrumentButton("Back to pause/hold", -306f,
                () => portableRecoveryPage = false);
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
            AddInstrumentButton("Research instruments", -306f, () => instrumentPage = 1);
            AddInstrumentButton("Pipette aspirate 0.05 ml", 30f,
                () => panels?.Request(PanelAction.PipetteAspirate));
            AddInstrumentButton("Pipette dispense 0.05 ml", -26f,
                () => panels?.Request(PanelAction.PipetteDispense));
            AddInstrumentButton("Bottle pour 0.01 ml", -82f,
                () => panels?.Request(PanelAction.BottlePour));
            AddInstrumentButton("Rinse pipette", -138f,
                () => panels?.Request(PanelAction.PipetteRinse));
            AddInstrumentButton("Rinse bottle", -194f,
                () => panels?.Request(PanelAction.BottleRinse));
            AddInstrumentButton("Stock setup", -250f, () => instrumentPage = 2);
            AddInstrumentButton("More tools and lessons", -138f, () => instrumentPage = 4);
            AddInstrumentButton("Exit tools", -306f, () => instrumentPage = 0);
            AddInstrumentButton("Choose stock", 30f,
                () => researchStockIndex = (researchStockIndex + 1) % ResearchStockIds.Length);
            AddInstrumentButton("Prepare chosen stock", -26f,
                () => panels?.Request(PanelAction.PrepareResearchStock,
                    ResearchStockIds[researchStockIndex], SelectedResearchMolarity()));
            AddInstrumentButton("Prepare rinse water", -82f,
                () => panels?.Request(PanelAction.PrepareRinseWater));
            AddInstrumentButton("Prepare reserve stock", -138f,
                () => panels?.Request(PanelAction.PrepareReserveStock,
                    ResearchStockIds[researchStockIndex], SelectedResearchMolarity()));
            AddInstrumentButton("Choose concentration", -194f, () =>
            {
                var count = driver?.ResearchConcentrations?.Count ?? 0;
                if (count > 0) researchConcentrationIndex =
                    (researchConcentrationIndex + 1) % count;
            });
            AddInstrumentButton("Switch pipette to reserve", 30f,
                () => panels?.Request(PanelAction.PipetteSwitchReserve));
            AddInstrumentButton("Refill bottle 0.01 ml", -26f,
                () => panels?.Request(PanelAction.BottleRefill));
            AddInstrumentButton("Prime burette 0.05 ml", -82f,
                () => panels?.Request(PanelAction.FillBurette));
            AddInstrumentButton("Strong acid lesson", -138f, () =>
            {
                if (panels?.Request(PanelAction.SelectResearchLesson, "strong-acid-titration-research") == true)
                    instrumentPage = 3;
            });
            AddInstrumentButton("Mixed acid lesson", -194f, () =>
            {
                if (panels?.Request(PanelAction.SelectResearchLesson, "mixed-acid-titration-research") == true)
                    instrumentPage = 3;
            });
            AddInstrumentButton("Predict pH below 7", 30f,
                () => panels?.Request(PanelAction.RecordResearchPrediction, "below-7"));
            AddInstrumentButton("Predict pH at least 7", -26f,
                () => panels?.Request(PanelAction.RecordResearchPrediction, "at-least-7"));
            AddInstrumentButton("Inspect Native concepts", -138f,
                () => panels?.Request(PanelAction.Learn, "receiver"));
            AddInstrumentButton("Back to instruments", -250f, () => instrumentPage = 1);

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
            if (panelRoot == null || !panelRoot.activeSelf) return;
            if (xrCamera != null && !placedWithTrackedHead) UpdatePlacement();
            RefreshPhysicalDeliveryFeedback();
        }

        // The adapter can refuse geometry without a router transition.
        // Refresh the Lab status independently of navigation, and keep the
        // latest status in the first visible line on the XR panel.
        private void RefreshPhysicalDeliveryFeedback()
        {
            if (detail == null || router?.Current?.ScreenId != ScreenId.Lab ||
                driver == null) return;
            var current = driver.transform.root
                .GetComponentInChildren<XRInputAdapter>(true)?.LastPhysicalDeliveryFeedback;
            if (string.Equals(lastPhysicalFeedback, current, StringComparison.Ordinal)) return;
            lastPhysicalFeedback = current;
            detail.text = string.IsNullOrEmpty(current) ? baselineDetail :
                "Physical delivery: " + current + "\n" + baselineDetail;
            if (detailScroller != null) detailScroller.verticalNormalizedPosition = 1f;
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
            // Scientific/readout text can overflow visually, but never
            // intercept a ray aimed at any of the working scene controls.
            label.raycastTarget = false;
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
            // Button Image is the interaction target, never a transparent
            // child Text element that can steal or mask a tracked-device ray.
            label.raycastTarget = false;
            buttons.Add(name, button);
        }

        private double SelectedResearchMolarity() =>
            ResearchConcentrationSelection.Resolve(driver.ResearchConcentrations,
                researchConcentrationIndex, ResearchStockIds[researchStockIndex]);

        private void AddInstrumentButton(string name, float y, Action action)
        {
            // Presentation-only page navigation and real shared panel requests.
            AddButton(name, y, PanelAction.Lab);
            var button = buttons[name];
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                action();
                if (router?.Current != null) Present(router.Current);
            });
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
            if (state.ScreenId != ScreenId.Lab) instrumentPage = 0;
            if (state.ScreenId != ScreenId.Pause && state.ScreenId != ScreenId.Hold)
                portableRecoveryPage = false;
            title.text = state.ScreenId.ToString();
            detail.text = state.ScreenId switch
            {
                ScreenId.Hold => state.HoldReason +
                    " hold. Close the valve and restore a neutral tracked input." +
                    (driver.transform.root.GetComponentInChildren<XRInputAdapter>(true) is var xr &&
                     xr != null && !xr.HasFreshGripSinceRestore ?
                        "\nAfter SessionLoad: release every tool grip and trigger on both tracked hands, then re-grip a tool." :
                        string.Empty) +
                    (panels != null && panels.HasResearchLesson ?
                        "\nSaved Research lesson and hypothesis recovered from local Notebook; " +
                        "Native Continue is still required.\n" + panels.ResearchPredictionSummary :
                        string.Empty),
                ScreenId.Pause => "Material and simulation time are paused.",
                ScreenId.Review => ReviewDetails(),
                ScreenId.Learn => ResearchConceptReadout.Describe(
                    driver.CurrentLabView, state.SelectedEntityId),
                ScreenId.Notebook => panels?.NotebookSummary ?? "Research notebook unavailable.",
                ScreenId.Compare => panels?.ComparisonSummary ?? "Recorded comparison unavailable.",
                ScreenId.Inspect or ScreenId.Measure => ScientificReadoutPolicy.Describe(
                    state.SelectedEntityId,
                    driver.Measurement?.Availability ?? MeasurementAvailability.Unsupported,
                    driver.Measurement?.DisplayValue ?? "--",
                    state.ObservationValidity, state.ObservationFreshness,
                    state.Maturity, state.ModelIdentity),
                _ => state.Setup.CaptionsEnabled ?
                    "Select a vessel to inspect. Research model · " + state.SaveState :
                    "Research model · " + state.SaveState
            };
            if (state.ScreenId == ScreenId.Lab)
                detail.text = "Native research bench\n" +
                    LabBenchReadout.Compact(driver.CurrentLabView) + "\n" +
                    LabBenchReadout.NextAction(driver.CurrentLabView,
                        driver.SourceNeedsPreparation, driver.IsNativePaused, driver.IsTimeHeld);
            SetButton("Inspect receiver", state.SupportedActions.Contains("Inspect") && state.ScreenId == ScreenId.Lab);
            SetButton("Inspect source", state.SupportedActions.Contains("Inspect") && state.ScreenId == ScreenId.Lab);
            SetButton("Measure", state.SupportedActions.Contains("Measure"));
            SetButton("Record Native pH",
                state.SupportedActions.Contains("RecordCurrentPH") &&
                panels != null && panels.CanRecordCurrentNativePH);
            var explainable = state.ScreenId == ScreenId.Notebook &&
                panels != null && panels.CanRecordResearchExplanation &&
                state.SupportedActions.Contains("RecordResearchExplanation");
            foreach (var name in new[] {
                "Explain: composition", "Explain: method", "Explain: model" })
                SetButton(name, explainable);
            // Keep the Lab controls on distinct physical hit targets. While
            // docked, Review remains reachable through Pause -> Review;
            // once released the same slot returns to the direct Review action.
            var dockReleaseVisible = state.ScreenId == ScreenId.Lab &&
                panels != null && panels.CanReleaseTool;
            var parkVisible = panels != null && panels.CanParkTool &&
                state.SupportedActions.Contains("ParkTool");
            buttons["Park tool"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, state.ScreenId == ScreenId.Lab ? -138f : -306f);
            SetButton("Open review", state.SupportedActions.Contains("Review") &&
                !dockReleaseVisible && !(parkVisible && state.ScreenId == ScreenId.Lab));
            buttons["Open review"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, state.ScreenId == ScreenId.Lab ? -138f :
                    state.ScreenId == ScreenId.Notebook ? -82f : 30f);
            var importedStudentNotes = state.ScreenId == ScreenId.Review &&
                panels != null && panels.CanNextInstructorAnnotation;
            SetButton("Open notebook", state.ScreenId == ScreenId.Review &&
                state.SupportedActions.Contains("Notebook") && !importedStudentNotes);
            SetButton("Next imported note", importedStudentNotes &&
                state.SupportedActions.Contains("NextInstructorAnnotation"));
            SetButton("Export archive", state.ScreenId == ScreenId.Notebook &&
                state.SupportedActions.Contains("ExportPortable"));
            SetButton("Export results CSV", state.ScreenId == ScreenId.Review &&
                state.SupportedActions.Contains("ExportCsv"));
            SetButton("Preview portable archive", state.ScreenId == ScreenId.Review &&
                state.SupportedActions.Contains("PreviewPortableArchive") &&
                panels != null && panels.CanPreviewLocalPortableArchive);
            SetButton("Choose local archive", state.ScreenId == ScreenId.Review &&
                state.SupportedActions.Contains("ChoosePortableArchive") &&
                panels != null && panels.CanChooseLocalPortableArchive);
            var portableRestoreAvailable =
                (state.ScreenId == ScreenId.Pause || state.ScreenId == ScreenId.Hold) &&
                state.SupportedActions.Contains("RestorePortable");
            SetButton("Portable study restore", portableRestoreAvailable &&
                !portableRecoveryPage);
            buttons["Portable study restore"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, state.ScreenId == ScreenId.Hold ? -138f : -194f);
            SetButton("Choose study package", false);
            SetButton("Restore study package", false);
            SetButton("Back to pause/hold", false);
            SetButton("Compare saved", state.ScreenId == ScreenId.Review &&
                state.SupportedActions.Contains("CompareSaved") &&
                panels != null && panels.CanCompareSaved);
            SetButton("Return to lab", state.SupportedActions.Contains("Lab"));
            SetButton("Pause", state.SupportedActions.Contains("Pause"));
            SetButton("Save session", state.SupportedActions.Contains("Save") &&
                state.ScreenId != ScreenId.Settings && state.ScreenId != ScreenId.Review);
            buttons["Save session"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, state.ScreenId == ScreenId.Notebook ? -194f : -250f);
            SetButton("Continue", state.SupportedActions.Contains("Continue"));
            SetButton("Settings", state.SupportedActions.Contains("Settings") &&
                (state.ScreenId != ScreenId.Lab ||
                 ResearchInstrumentMenuLayout.ShowGlobalSettings(instrumentPage)));
            SetButton("Back", state.SupportedActions.Contains("Back"));
            SetButton("Release docked tool", state.SupportedActions.Contains("ReleaseTool") &&
                dockReleaseVisible);
            SetButton("Park tool", parkVisible);
            SetButton("Load saved session", state.SupportedActions.Contains("Load") &&
                panels != null && panels.CanLoadSavedSession && !parkVisible);
            SetButton("Recenter view", state.SupportedActions.Contains("Recenter"));
            SetButton("Snap left 30", state.SupportedActions.Contains("SnapLeft"));
            SetButton("Snap right 30", state.SupportedActions.Contains("SnapRight"));
            if (panels != null && !string.IsNullOrEmpty(panels.LastError))
                detail.text += "\n" + panels.LastError;
            buttons["Back"].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0f, state.ScreenId == ScreenId.Settings ? -250f : -138f);
            // A separate instrument page keeps XR ray targets from sharing
            // hit rectangles with standard Lab/Review/Park controls.
            if (state.ScreenId == ScreenId.Lab && instrumentPage != 0)
            {
                foreach (var name in new[] { "Inspect receiver", "Inspect source", "Measure",
                    "Open review", "Pause", "Save session", "Release docked tool", "Park tool",
                    "Research instruments", "Settings" }) SetButton(name, false);
                var mayTransfer = !driver.SourceNeedsPreparation &&
                    !driver.IsNativePaused && !driver.IsTimeHeld;
                var visible = ResearchInstrumentMenuLayout.Visible(
                    instrumentPage, driver.SourceNeedsPreparation, mayTransfer,
                    driver.ReserveStockPrepared, panels != null && panels.CanFillBurette,
                    panels != null && panels.CanRecordResearchPrediction,
                    panels != null && panels.CanPipetteDispense, driver.RinseWaterPrepared,
                    state.SupportedActions.Contains("Notebook"));
                foreach (var name in ResearchInstrumentMenuLayout.AllNames)
                    SetButton(name, false);
                foreach (var item in visible)
                {
                    SetButton(item.Name, true);
                    buttons[item.Name].GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(0f, 30f - 56f * item.Row);
                }
                var predictionPage = instrumentPage == 3;
                var stockLabel = buttons["Choose stock"].GetComponentInChildren<Text>(true);
                if (stockLabel != null) stockLabel.text = "Stock: " + ResearchStockIds[researchStockIndex];
                var concentrationLabel = buttons["Choose concentration"].GetComponentInChildren<Text>(true);
                if (concentrationLabel != null && driver.ResearchConcentrations.Count > 0)
                {
                    var concentration = SelectedResearchMolarity();
                    concentrationLabel.text = "Concentration: " +
                        concentration.ToString("0.00") + " mol/L";
                }
                detail.text = predictionPage ?
                    (panels?.ResearchLessonCompact ?? "Research lesson unavailable.") + "\n" +
                    (panels?.ResearchPredictionSummary ?? "Select a prediction.") :
                    driver.SourceNeedsPreparation ?
                    "Prepare a supported stock and optional rinse water before entering the Lab." :
                    (panels?.ResearchLessonCompact ?? "Free Research laboratory") + "\n" +
                    "Native pipette tip " +
                    ((panels?.PipetteTipVolumeM3 ?? 0.0) * 1e6).ToString("0.00") +
                    " ml. Direct-grip the pipette: immerse its tip in prepared source stock " +
                    "and press trigger to aspirate Native 0.05 ml, then align the tip over receiver " +
                    "and trigger to dispense after the retained-tip volume is satisfied. " +
                    "A switched reserve source has no physical stock target yet.";
                if (panels != null && !string.IsNullOrEmpty(panels.LastInstrumentReceipt))
                    detail.text += "\n" + panels.LastInstrumentReceipt;
                if (panels != null && !string.IsNullOrEmpty(panels.LastError))
                    detail.text += "\n" + panels.LastError;
            }
            else
            {
                foreach (var name in ResearchInstrumentMenuLayout.AllNames)
                    SetButton(name, false);
                SetButton("Research instruments",
                    state.ScreenId == ScreenId.Lab && !state.SupportedActions.Contains("Load"));
            }
            if (portableRestoreAvailable && portableRecoveryPage)
            {
                // Dedicated UI page: an XR ray must never click a restore
                // action through overlapping Save/Continue/tool buttons.
                foreach (var entry in buttons.Values)
                    entry.gameObject.SetActive(false);
                SetButton("Choose study package",
                    state.SupportedActions.Contains("ChoosePortableArchive") &&
                    panels != null && panels.CanChooseLocalPortableArchive);
                SetButton("Restore study package",
                    state.SupportedActions.Contains("RestorePortable") &&
                    panels != null && panels.CanRestoreLocalPortableSession);
                SetButton("Back to pause/hold", true);
                detail.text = "OFFLINE STUDENT RESTORE · NOT ATTESTED / NOT GRADED\n" +
                    "Place a .plvpack in the local sessions folder. Choose a package, " +
                    "then restore it to a new isolated session. The Native model, " +
                    "branch, event and Notebook integrity must all validate.\n" +
                    "Selected: " + (panels?.SelectedArchiveFileName ?? "none") +
                    "\nNative SessionLoad Hold requires an explicit new neutral grip.";
                if (!string.IsNullOrEmpty(panels?.LastError))
                    detail.text += "\n" + panels.LastError;
            }
            if (state.ScreenId == ScreenId.Hold &&
                !string.IsNullOrEmpty(panels?.LastPortableImportSummary))
                detail.text += "\n" + panels.LastPortableImportSummary;
            if (state.ScreenId == ScreenId.Settings) PresentSettings(state);
            else
            {
                foreach (var name in new[] { "Settings choice A", "Settings choice B",
                    "Settings choice C", "Settings choice D", "Next settings", "Previous settings" })
                    SetButton(name, false);
            }
            baselineDetail = detail.text;
            lastPhysicalFeedback = null; // re-evaluate even when router updates mid-gesture
            RefreshPhysicalDeliveryFeedback();
            if (detailScroller != null)
            {
                detailScroller.StopMovement();
                detailScroller.verticalNormalizedPosition = 1f;
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
            var latest = recorded.CommandReceipts.Count == 0 ? "No command receipts." :
                "Cmd #" + recorded.CommandReceipts[recorded.CommandReceipts.Count - 1].CommandSequence +
                " " + recorded.CommandReceipts[recorded.CommandReceipts.Count - 1].CommandType +
                " · " + (recorded.CommandReceipts[recorded.CommandReceipts.Count - 1].Accepted ?
                    "Accepted" : recorded.CommandReceipts[recorded.CommandReceipts.Count - 1].OutcomeCode);
            return "Recorded · no recomputation\n" +
                (recorded.Observations.Length == 0 ? "No observations recorded." : string.Join("\n", values)) +
                "\n" + latest + "\n" +
                (panels?.LearningReviewCompact ?? "Local student notebook unavailable.") +
                (panels?.SelectedArchiveFileName != null ?
                    "\nSelected local .plvpack: " + panels.SelectedArchiveFileName : "") +
                (panels != null &&
                 panels.InstructorPortableCompactSummary != "No offline instructor preview selected." ?
                    "\n" + panels.InstructorPortableCompactSummary : "") +
                (panels?.HasLearnerCsvExport == true ?
                    "\nStudent annotation CSV saved separately." : "");
        }
    }
}
