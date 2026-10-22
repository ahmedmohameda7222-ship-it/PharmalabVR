using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using PharmaLabVR.Core;
using PharmaLabVR.Session;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;

namespace PharmaLabVR.Input
{
    public sealed class DesktopInputAdapter : MonoBehaviour, ILabInputAdapter,
        INativeRestorationInputAdapter
    {
        [SerializeField] private Transform labFrame;
        [SerializeField] private Transform heldTool;
        [SerializeField] private Transform registeredTool;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform holdAnchor;
        [SerializeField] private LabCaptureTarget captureTarget;
        [SerializeField] private CoreDriver coreDriver;
        [SerializeField] private ToolPresenter instrumentPresenter;
        [SerializeField] private string heldToolId = "desktop-tool";
        [SerializeField] private string geometryProfileHash = "unassigned";
        private ulong sequence;
        private float actuator;
        private bool inputEnabled = true;
        private bool awaitingFocusContinue;
        private bool pointerDragActive;
        // The selection mouse press must never also actuate a Native valve.
        private bool pointerSelectionActive;
        private Vector3 initialDragOffset;
        private bool heldToolOriginalKinematic;
        private readonly ResearchRestorationGestureGate restorationGestureGate = new();
        public bool HasFreshPhysicalGestureSinceRestore => restorationGestureGate.IsArmed;
        private readonly ResearchPressBuffer pickPress = new ResearchPressBuffer();
        private readonly ResearchPressBuffer deliveryPress = new ResearchPressBuffer();
        [SerializeField] private ResearchStockAspirateTarget stockAspirateTarget;
        [SerializeField] private ResearchStockAspirateTarget reserveStockAspirateTarget;
        public bool HasReserveStockAspirateTarget =>
            reserveStockAspirateTarget != null &&
            reserveStockAspirateTarget.NativeInventoryId == "reserve-stock";
        public void ConfigureReserveStockAspirateTarget(ResearchStockAspirateTarget target)
        {
            if (target == null || target.NativeInventoryId != "reserve-stock")
                throw new System.ArgumentException(
                    "The physical reserve target must represent Native reserve-stock.", nameof(target));
            reserveStockAspirateTarget = target;
        }
        private ResearchAuxiliaryDeliveryIntent pendingDeliveryIntent;
        public void ConfigureStockAspirateTarget(ResearchStockAspirateTarget target) =>
            stockAspirateTarget = target != null ? target :
                throw new System.ArgumentNullException(nameof(target));
        private ResearchStockAspirateTarget StockAspirateTarget =>
            stockAspirateTarget != null ? stockAspirateTarget :
                coreDriver != null ? coreDriver.transform.root
                    .GetComponentInChildren<ResearchStockAspirateTarget>(true) : null;
        public bool HasStockAspirateTarget => StockAspirateTarget != null;

        private void ClearDeliveryPress()
        {
            deliveryPress.Clear();
            pendingDeliveryIntent = ResearchAuxiliaryDeliveryIntent.None;
        }

        private ResearchAuxiliaryDeliveryIntent ResolveHeldResearchIntent(
            Transform physicalTool, string physicalId)
        {
            var capture = captureTarget != null ?
                LabCaptureTarget.EstimateExclusive(physicalTool, captureTarget) :
                System.Array.Empty<LabCaptureEstimate>();
            var receiver = capture.Length == 1 &&
                capture[0].destinationInventoryId == "receiver" &&
                Mathf.Approximately(capture[0].fraction, 1f);
            var pipette = physicalTool != null ?
                physicalTool.GetComponent<PipetteActuator>() : null;
            var primaryImmersed = physicalId == "research-pipette-visual" &&
                StockAspirateTarget != null &&
                StockAspirateTarget.IsImmersed(physicalTool);
            var reserveImmersed = physicalId == "research-pipette-visual" &&
                reserveStockAspirateTarget != null &&
                reserveStockAspirateTarget.IsImmersed(physicalTool);
            var primary = primaryImmersed &&
                StockAspirateTarget.NativeSourceMatches(coreDriver?.Session, pipette);
            var reserve = reserveImmersed &&
                reserveStockAspirateTarget.NativeSourceMatches(coreDriver?.Session, pipette);
            // No concurrent source+receiver request; no ambiguous double
            // stock immersion even when only one matches Native tool state.
            if (physicalId == "research-pipette-visual" &&
                (primaryImmersed || reserveImmersed) &&
                (receiver || primaryImmersed == reserveImmersed))
                return ResearchAuxiliaryDeliveryIntent.None;
            var source = primary != reserve;
            var inlet = registeredTool != null ?
                registeredTool.GetComponent<ResearchBuretteInletTarget>() : null;
            var prime = physicalId == "research-stock-bottle-visual" &&
                inlet != null && inlet.AdmitsBottle(physicalTool,
                    !IsHoldingNativeTool, coreDriver?.Session);
            var ready = coreDriver != null && coreDriver.Session != null &&
                !coreDriver.SourceNeedsPreparation && !coreDriver.IsTimeHeld &&
                !coreDriver.IsNativePaused;
            var select = ResearchPhysicalSourceSelection.Resolve(
                physicalId, true, true, receiver,
                primaryImmersed, reserveImmersed, primary, reserve, ready);
            if (select != ResearchAuxiliaryDeliveryIntent.None) return select;
            return ResearchAuxiliaryDelivery.Resolve(physicalId, true, true,
                receiver, source, prime, ready);
        }
        public bool AwaitingFocusContinue => awaitingFocusContinue;
        public bool HasHeldTool => heldTool != null;
        public Transform CurrentHeldPhysicalTool => heldTool;
        public bool IsHoldingNativeTool => heldTool != null && heldTool == registeredTool;
        public string LastPhysicalDeliveryFeedback { get; private set; }
        public string LastPhysicalInspectionFeedback { get; private set; }

        private void Awake() => RefreshRegisteredToolId();

        private void Update()
        {
            if (!restorationGestureGate.IsArmed)
            {
                // A Native restore invalidates the *old* physical selection.
                // Do not let F/G, a held mouse press or a drag auto-reselect
                // a tool after Continue. Only real released controls re-arm.
                var pointerNeutral = Mouse.current == null ||
                    (!Mouse.current.leftButton.isPressed &&
                     !Mouse.current.rightButton.isPressed &&
                     !Mouse.current.middleButton.isPressed);
                var keysNeutral = Keyboard.current != null &&
                    !Keyboard.current.anyKey.isPressed;
                restorationGestureGate.ObserveNeutral(heldTool == null,
                    pointerNeutral && keysNeutral,
                    inputEnabled && !awaitingFocusContinue && coreDriver != null);
                pickPress.Clear();
                ClearDeliveryPress();
                actuator = 0f;
                instrumentPresenter?.PresentActuatorInput(0f);
                return; // The first neutral frame never generates a gesture.
            }
            if (!inputEnabled || awaitingFocusContinue || coreDriver == null ||
                coreDriver.IsNativePaused || coreDriver.IsTimeHeld || Keyboard.current == null)
            {
                pickPress.Clear();
                ClearDeliveryPress();
                return;
            }
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                pickPress.Clear();
                ClearDeliveryPress();
                actuator = 0f;
                instrumentPresenter?.PresentActuatorInput(0f);
                coreDriver.transform.root.GetComponentInChildren<SessionController>(true)
                    ?.RequestDesktopPause();
                return;
            }
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
                pointerSelectionActive = false;
            var uiCapture = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (Mouse.current != null &&
                 SessionController.PointerOverDesktopHud(Mouse.current.position.ReadValue()));
            if (uiCapture)
            {
                // Crossing into UI invalidates a pending world gesture; it
                // must not fire later when the pointer leaves the panel.
                pickPress.Clear();
                ClearDeliveryPress();
                return;
            }
            var frame = (ulong)Time.frameCount;
            if (Keyboard.current.fKey.wasPressedThisFrame)
                pickPress.Capture("desktop-pick-or-place", frame, true);
            if (heldTool == null && Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                pointerSelectionActive = true;
                pickPress.Capture("desktop-pointer-pick", frame, true);
            }
            if (Keyboard.current.gKey.wasPressedThisFrame &&
                heldTool != null && heldTool != registeredTool)
            {
                var heldId = heldTool.GetComponent<DesktopGrabbable>()?.ToolId;
                var candidate = ResolveHeldResearchIntent(heldTool, heldId);
                if (candidate == ResearchAuxiliaryDeliveryIntent.None)
                    LastPhysicalDeliveryFeedback =
                        "Immerse pipette in prepared stock: G first selects a new Native source; press G again to aspirate. Receiver and burette transfers are separate.";
                else
                {
                    pendingDeliveryIntent = candidate;
                    deliveryPress.Capture(heldId, frame, true);
                }
            }
        }

        private void OnDisable()
        {
            pointerSelectionActive = false;
            pickPress.Clear();
            ClearDeliveryPress();
            actuator = 0f;
            instrumentPresenter?.PresentActuatorInput(0f);
        }

        public void Configure(Transform frame, Camera camera, Transform anchor, Transform tool, LabCaptureTarget target, string profileHash, CoreDriver driver = null)
        {
            labFrame = frame;
            viewCamera = camera;
            holdAnchor = anchor;
            registeredTool = tool;
            instrumentPresenter = tool != null ? tool.GetComponent<ToolPresenter>() : null;
            captureTarget = target;
            geometryProfileHash = profileHash;
            coreDriver = driver;
            RefreshRegisteredToolId();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                inputEnabled = false;
                awaitingFocusContinue = true;
                actuator = 0f;
                instrumentPresenter?.PresentActuatorInput(0f);
                pointerDragActive = false;
                pointerSelectionActive = false;
                pickPress.Clear();
                ClearDeliveryPress();
                Debug.LogWarning("InputHold: focus was lost; explicit Continue is required.");
            }
        }

        public bool ContinueAfterFocusRecovery()
        {
            if (coreDriver != null && coreDriver.IsTimeHeld && !coreDriver.ResumeAfterTimeHold()) return false;
            ResetBaselines();
            awaitingFocusContinue = false;
            inputEnabled = true;
            return true;
        }

        public LabInputSample[] SampleInputs(ulong nowNs)
        {
            RefreshRegisteredToolId();
            if (awaitingFocusContinue && registeredTool != null)
            {
                instrumentPresenter?.PresentActuatorInput(0f);
                return new[] { LabGeometryAdapter.BuildSample(heldToolId, ++sequence, nowNs, labFrame,
                    registeredTool, 0f, geometryProfileHash, true) };
            }
            if (!inputEnabled || registeredTool == null)
            {
                instrumentPresenter?.PresentActuatorInput(0f);
                return System.Array.Empty<LabInputSample>();
            }
            // A restored Native session must sample neutral geometry during
            // Hold recovery, but must not carry any old Desktop manipulation.
            if (!restorationGestureGate.IsArmed)
            {
                actuator = 0f;
                instrumentPresenter?.PresentActuatorInput(0f);
                pickPress.Clear();
                ClearDeliveryPress();
                return new[] { LabGeometryAdapter.BuildSample(
                    heldToolId, ++sequence, nowNs, labFrame, registeredTool, 0f,
                    geometryProfileHash, !awaitingFocusContinue,
                    captureTarget != null ?
                        LabCaptureTarget.EstimateExclusive(registeredTool, captureTarget) : null) };
            }
            // Esc is handled in Update, independently of the Native tick.
            // Sampling must never be the only listener for single-frame input.
            if (coreDriver != null && (coreDriver.IsNativePaused || coreDriver.IsTimeHeld))
            {
                actuator = 0f;
                instrumentPresenter?.PresentActuatorInput(0f);
                pointerDragActive = false;
                pickPress.Clear();
                ClearDeliveryPress();
                return System.Array.Empty<LabInputSample>();
            }
            HandlePickOrPlace();
            HandlePhysicalDeliveryRequest();
            var pointerOverUi = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (Mouse.current != null && SessionController.PointerOverDesktopHud(Mouse.current.position.ReadValue()));
            var shiftHeld = Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            var pointerAction = DesktopPointerRouting.ResolveForHeldInstrument(
                heldTool != null, IsHoldingNativeTool, shiftHeld,
                Mouse.current != null && Mouse.current.leftButton.isPressed,
                pointerOverUi, inputEnabled, pointerSelectionActive);
            if (pointerAction == DesktopPointerAction.ToolDrag)
            {
                actuator = 0f; // A physical drag is never a valve opening.
                DragHeldToolOnPlane();
            }
            else
            {
                pointerDragActive = false;
                // Release, focus on UI, or empty hands must close the valve.
                actuator = pointerAction == DesktopPointerAction.Valve && Mouse.current != null ?
                    Mathf.Clamp01(actuator + Mouse.current.delta.ReadValue().x * 0.005f) : 0f;
            }
            ManipulateHeldTool(pointerOverUi);
            // Native geometry and tool ID remain those of the registered
            // burette even while an auxiliary research instrument is held.
            var sampledTool = registeredTool;
            var sampledActuator = DesktopPointerRouting.ShouldSampleHeldAsNative(
                heldTool != null, IsHoldingNativeTool) ? actuator : 0f;
            instrumentPresenter?.PresentActuatorInput(sampledActuator);
            return new[] { LabGeometryAdapter.BuildSample(
                heldToolId, ++sequence, nowNs, labFrame, sampledTool, sampledActuator,
                geometryProfileHash, true, captureTarget != null ?
                    LabCaptureTarget.EstimateExclusive(sampledTool, captureTarget) : null) };
        }

        public void ResetForNativeRestoration()
        {
            // Restore the real body's original physics mode. No pose or
            // material is teleported or fabricated by this interaction reset.
            if (heldTool != null && heldTool.TryGetComponent<Rigidbody>(out var body))
                body.isKinematic = heldToolOriginalKinematic;
            heldTool = null;
            actuator = 0f;
            pointerDragActive = false;
            pointerSelectionActive = false;
            pickPress.Clear();
            ClearDeliveryPress();
            LastPhysicalDeliveryFeedback = null;
            LastPhysicalInspectionFeedback = null;
            instrumentPresenter?.PresentActuatorInput(0f);
            restorationGestureGate.RequireFreshNeutralInput();
        }

        public void ResetBaselines()
        {
            actuator = 0f;
            instrumentPresenter?.PresentActuatorInput(0f);
            pointerDragActive = false;
            pointerSelectionActive = false;
            pickPress.Clear();
            ClearDeliveryPress();
            awaitingFocusContinue = false;
            inputEnabled = gameObject.activeInHierarchy;
        }
        public void AdvanceSequence(string toolId, ulong watermark)
        {
            RefreshRegisteredToolId();
            if (toolId == heldToolId && sequence < watermark) sequence = watermark;
        }
        public void SetEnabled(bool value)
        {
            inputEnabled = value && !awaitingFocusContinue;
            pickPress.Clear();
            ClearDeliveryPress();
            if (!value)
            {
                actuator = 0f;
                pointerDragActive = false;
                pointerSelectionActive = false;
                instrumentPresenter?.PresentActuatorInput(0f);
            }
        }

        private static bool ShouldAdjustActuator(bool pointerOverUi, bool leftButtonPressed) => leftButtonPressed && !pointerOverUi;

        private void RefreshRegisteredToolId()
        {
            if (registeredTool != null && registeredTool.TryGetComponent<DesktopGrabbable>(out var grabbable))
                heldToolId = grabbable.ToolId;
        }

        private void DragHeldToolOnPlane()
        {
            if (heldTool == null || viewCamera == null || Mouse.current == null || labFrame == null)
            {
                pointerDragActive = false;
                return;
            }
            var plane = new Plane(labFrame.up, heldTool.position);
            var cursor = Mouse.current.position.ReadValue();
            var ray = viewCamera.ScreenPointToRay(new Vector3(cursor.x, cursor.y, 0f));
            if (!plane.Raycast(ray, out var distance) || !float.IsFinite(distance) ||
                distance <= 0f || distance > 5f)
            {
                pointerDragActive = false;
                return;
            }
            var positionOnPlane = ray.GetPoint(distance);
            if (!pointerDragActive)
            {
                // No teleport on the first click: preserve the difference
                // between cursor ray-plane hit and the tool's actual pose.
                initialDragOffset = heldTool.position - positionOnPlane;
                pointerDragActive = true;
            }
            heldTool.position = positionOnPlane + initialDragOffset;
        }

        // G is an intentional, geometry-admitted research delivery, not
        // passive proximity or an invented fluid simulation.
        private void HandlePhysicalDeliveryRequest()
        {
            if (heldTool == null || heldTool == registeredTool)
            {
                ClearDeliveryPress();
                return;
            }
            if ((EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (Mouse.current != null && SessionController.PointerOverDesktopHud(
                    Mouse.current.position.ReadValue())))
            {
                ClearDeliveryPress();
                return;
            }
            var physicalId = heldTool.GetComponent<DesktopGrabbable>()?.ToolId;
            if (!deliveryPress.TryConsume((ulong)Time.frameCount, physicalId, true))
                return;
            var admittedAtPress = pendingDeliveryIntent;
            pendingDeliveryIntent = ResearchAuxiliaryDeliveryIntent.None;
            var intent = ResolveHeldResearchIntent(heldTool, physicalId);
            if (intent == ResearchAuxiliaryDeliveryIntent.None ||
                intent != admittedAtPress)
            {
                LastPhysicalDeliveryFeedback =
                    "Delivery refused. Keep the pipette immersed in stock or the outlet aligned over the receiver from G press through the Native step.";
                return;
            }
            var actions = coreDriver.transform.root.GetComponentInChildren<LabPanelController>(true);
            var session = coreDriver.transform.root.GetComponentInChildren<SessionController>(true);
            var selectedStock =
                intent == ResearchAuxiliaryDeliveryIntent.PipetteSelectPrimary ?
                    StockAspirateTarget :
                intent == ResearchAuxiliaryDeliveryIntent.PipetteSelectReserve ?
                    reserveStockAspirateTarget : null;
            var accepted = selectedStock != null ?
                session != null && session.TryPhysicalSelectPipetteSource(
                    heldTool, selectedStock) :
                intent == ResearchAuxiliaryDeliveryIntent.BurettePrime ?
                    session != null && session.TryPhysicalFillBuretteFromBottle(
                        heldTool, registeredTool != null ?
                            registeredTool.GetComponent<ResearchBuretteInletTarget>() : null) :
                actions != null && actions.Request(
                    intent == ResearchAuxiliaryDeliveryIntent.BottlePour ?
                        PanelAction.BottlePour :
                    intent == ResearchAuxiliaryDeliveryIntent.PipetteAspirate ?
                        PanelAction.PipetteAspirate : PanelAction.PipetteDispense);
            LastPhysicalDeliveryFeedback = accepted ?
                (session?.LastInstrumentReceipt ?? "Native command accepted.") :
                (session?.LastError ?? "Native research transfer refused.");
        }

        // Pointer picking selects real bench geometry; F remains crosshair pick/placement.
        public static Ray SelectionRay(Camera camera, Vector2 pointerPixels, bool pointerPick)
        {
            if (camera == null) throw new System.ArgumentNullException(nameof(camera));
            return pointerPick ? camera.ScreenPointToRay(pointerPixels) :
                new Ray(camera.transform.position, camera.transform.forward);
        }

        private void HandlePickOrPlace()
        {
            var pending = pickPress.PendingToolId;
            if (!inputEnabled || pending == null ||
                !pickPress.TryConsume((ulong)Time.frameCount, pending, true))
                return;
            if ((EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (Mouse.current != null && SessionController.PointerOverDesktopHud(Mouse.current.position.ReadValue())))
            {
                pickPress.Clear();
                return;
            }
            actuator = 0f;
            if (heldTool != null)
            {
                // Only F places on the bench; clicking cannot silently drop a tool.
                if (pending == "desktop-pick-or-place")
                    TryPlaceHeldTool();
                return;
            }
            if (viewCamera == null || holdAnchor == null) return;
            var ray = SelectionRay(viewCamera,
                Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero,
                pending == "desktop-pointer-pick");
            if (!Physics.Raycast(ray, out var hit, 3f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)) return;
            var grabbable = hit.collider.GetComponentInParent<DesktopGrabbable>();
            if (grabbable == null)
            {
                // Clicking the receiver opens its real Native observation.
                // This cannot move material, perform a solver command or fake pH.
                var receiver = hit.collider.GetComponentInParent<LabCaptureTarget>();
                if (receiver != null) TryInspectReceiver(receiver);
                return;
            }
            LastPhysicalInspectionFeedback = null;
            BeginHold(grabbable.transform);
            if (IsHoldingNativeTool) heldToolId = grabbable.ToolId;
        }

        // Shared Desktop click -> scene aperture identity -> Native vessel
        // selection -> existing Measure panel. The driver owns observation data.
        public bool TryInspectReceiver(LabCaptureTarget physicallySelected)
        {
            if (!inputEnabled || awaitingFocusContinue || heldTool != null ||
                coreDriver == null || coreDriver.Session == null ||
                coreDriver.IsNativePaused || coreDriver.IsTimeHeld ||
                coreDriver.SourceNeedsPreparation || physicallySelected == null ||
                physicallySelected != captureTarget || !captureTarget.isActiveAndEnabled)
            {
                LastPhysicalInspectionFeedback =
                    "Receiver inspection unavailable until Native stock is prepared and the lab is active.";
                return false;
            }
            var panel = coreDriver.transform.root.GetComponentInChildren<LabPanelController>(true);
            var accepted = panel != null &&
                panel.Request(PanelAction.Measure, physicallySelected.InventoryId);
            LastPhysicalInspectionFeedback = accepted ? null :
                "Native measurement selection was refused. Check the active lab and receiver.";
            return accepted;
        }

        private bool TryPlaceHeldTool()
        {
            if (heldTool == null || labFrame == null) return false;
            var up = labFrame.up;
            var cast = new Ray(heldTool.position + up * 0.02f, -up);
            var hits = Physics.RaycastAll(cast, 0.45f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            // Unity RaycastAll enumeration order is not stable; choose the
            // closest non-self collider, not the first returned result.
            var closestDistance = float.PositiveInfinity;
            RaycastHit nearest = default;
            foreach (var hit in hits)
            {
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(heldTool) ||
                    hit.distance >= closestDistance) continue;
                nearest = hit;
                closestDistance = hit.distance;
            }
            var hitsBench = nearest.collider != null &&
                nearest.collider.name == "BenchSurface" &&
                nearest.collider.transform.IsChildOf(labFrame);
            var height = hitsBench ? Vector3.Dot(heldTool.position - nearest.point, up) :
                double.PositiveInfinity;
            var uprightDot = Vector3.Dot(heldTool.up, up);
            if (!DesktopPlacementAdmission.CanPlace(height, uprightDot, hitsBench, false))
                return false;
            // Root is a tool-foot landmark; snap only a nearby, properly
            // aligned root to the actual collider surface, not arbitrary y.
            heldTool.position = nearest.point + up * 0.005f;
            if (heldTool.TryGetComponent<Rigidbody>(out var heldBody))
                heldBody.isKinematic = heldToolOriginalKinematic;
            heldTool = null;
            pointerDragActive = false;
            ClearDeliveryPress();
            LastPhysicalDeliveryFeedback = null;
            return true;
        }

        private void BeginHold(Transform tool)
        {
            heldTool = tool;
            ClearDeliveryPress();
            LastPhysicalDeliveryFeedback = null;
            pointerDragActive = false;
            if (heldTool != null && heldTool.TryGetComponent<Rigidbody>(out var body))
            {
                heldToolOriginalKinematic = body.isKinematic;
                body.isKinematic = true;
            }
        }

        private void ManipulateHeldTool(bool pointerOverUi)
        {
            if (heldTool == null || Keyboard.current == null || labFrame == null || pointerOverUi) return;
            if (Mouse.current != null)
            {
                var delta = Mouse.current.scroll.ReadValue().y;
                if (DesktopWheelRouting.Resolve(true, false, delta) == DesktopWheelTarget.HeldToolHeight)
                    heldTool.position += labFrame.up *
                        (float)DesktopWheelRouting.HeightDeltaMetres(delta);
            }
            var translation = Vector3.zero;
            if (Keyboard.current.leftArrowKey.isPressed) translation += Vector3.left;
            if (Keyboard.current.rightArrowKey.isPressed) translation += Vector3.right;
            if (Keyboard.current.upArrowKey.isPressed) translation += Vector3.forward;
            if (Keyboard.current.downArrowKey.isPressed) translation += Vector3.back;
            if (Keyboard.current.pageUpKey.isPressed) translation += Vector3.up;
            if (Keyboard.current.pageDownKey.isPressed) translation += Vector3.down;
            heldTool.position += labFrame.TransformDirection(translation.normalized) * (0.20f * Time.unscaledDeltaTime);
            var tilt = 0f;
            if (Keyboard.current.qKey.isPressed) tilt += 1f;
            if (Keyboard.current.eKey.isPressed) tilt -= 1f;
            if (!Mathf.Approximately(tilt, 0f)) heldTool.Rotate(labFrame.forward, tilt * 45f * Time.unscaledDeltaTime, Space.World);
        }
    }

    public sealed class DesktopGrabbable : MonoBehaviour
    {
        [SerializeField] private string toolId = "research-tool";
        public string ToolId => toolId;

        public void Configure(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new System.ArgumentException("Physical tool identity is required.", nameof(id));
            toolId = id;
        }
    }
}
