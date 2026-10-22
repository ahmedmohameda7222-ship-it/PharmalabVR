using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using PharmaLabVR.Core;
using PharmaLabVR.Session;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;

namespace PharmaLabVR.Input
{
    public sealed class DesktopInputAdapter : MonoBehaviour, ILabInputAdapter
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
        private Vector3 initialDragOffset;
        private bool heldToolOriginalKinematic;
        private readonly ResearchPressBuffer pickPress = new ResearchPressBuffer();
        private readonly ResearchPressBuffer deliveryPress = new ResearchPressBuffer();
        public bool AwaitingFocusContinue => awaitingFocusContinue;
        public bool HasHeldTool => heldTool != null;
        public bool IsHoldingNativeTool => heldTool != null && heldTool == registeredTool;
        public string LastPhysicalDeliveryFeedback { get; private set; }

        private void Awake() => RefreshRegisteredToolId();

        private void Update()
        {
            if (!inputEnabled || awaitingFocusContinue || coreDriver == null ||
                coreDriver.IsNativePaused || coreDriver.IsTimeHeld || Keyboard.current == null)
            {
                pickPress.Clear();
                deliveryPress.Clear();
                return;
            }
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                pickPress.Clear();
                deliveryPress.Clear();
                actuator = 0f;
                instrumentPresenter?.PresentActuatorInput(0f);
                coreDriver.transform.root.GetComponentInChildren<SessionController>(true)
                    ?.RequestDesktopPause();
                return;
            }
            var uiCapture = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (Mouse.current != null &&
                 SessionController.PointerOverDesktopHud(Mouse.current.position.ReadValue()));
            if (uiCapture)
            {
                // Crossing into UI invalidates a pending world gesture; it
                // must not fire later when the pointer leaves the panel.
                pickPress.Clear();
                deliveryPress.Clear();
                return;
            }
            var frame = (ulong)Time.frameCount;
            if (Keyboard.current.fKey.wasPressedThisFrame)
                pickPress.Capture("desktop-pick-or-place", frame, true);
            if (Keyboard.current.gKey.wasPressedThisFrame &&
                heldTool != null && heldTool != registeredTool)
                deliveryPress.Capture(
                    heldTool.GetComponent<DesktopGrabbable>()?.ToolId, frame, true);
        }

        private void OnDisable()
        {
            pickPress.Clear();
            deliveryPress.Clear();
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
                pickPress.Clear();
                deliveryPress.Clear();
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
            // Esc is handled in Update, independently of the Native tick.
            // Sampling must never be the only listener for single-frame input.
            if (coreDriver != null && (coreDriver.IsNativePaused || coreDriver.IsTimeHeld))
            {
                actuator = 0f;
                instrumentPresenter?.PresentActuatorInput(0f);
                pointerDragActive = false;
                pickPress.Clear();
                deliveryPress.Clear();
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
                pointerOverUi, inputEnabled);
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

        public void ResetBaselines()
        {
            actuator = 0f;
            instrumentPresenter?.PresentActuatorInput(0f);
            pointerDragActive = false;
            pickPress.Clear();
            deliveryPress.Clear();
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
            deliveryPress.Clear();
            if (!value)
            {
                actuator = 0f;
                pointerDragActive = false;
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
                deliveryPress.Clear();
                return;
            }
            if ((EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (Mouse.current != null && SessionController.PointerOverDesktopHud(
                    Mouse.current.position.ReadValue())))
            {
                deliveryPress.Clear();
                return;
            }
            var physicalId = heldTool.GetComponent<DesktopGrabbable>()?.ToolId;
            if (!deliveryPress.TryConsume((ulong)Time.frameCount, physicalId, true))
                return;
            var capture = captureTarget != null ?
                LabCaptureTarget.EstimateExclusive(heldTool, captureTarget) :
                System.Array.Empty<LabCaptureEstimate>();
            var ready = coreDriver != null && !coreDriver.SourceNeedsPreparation &&
                !coreDriver.IsTimeHeld && !coreDriver.IsNativePaused;
            var intent = ResearchAuxiliaryDelivery.Resolve(physicalId, true, true,
                capture.Length == 1 && capture[0].destinationInventoryId == "receiver" &&
                Mathf.Approximately(capture[0].fraction, 1f), ready);
            if (intent == ResearchAuxiliaryDeliveryIntent.None)
            {
                LastPhysicalDeliveryFeedback =
                    "Delivery not admitted. Align a held pipette/bottle outlet over the stationary receiver; Native must be active.";
                return;
            }
            var actions = coreDriver.transform.root.GetComponentInChildren<LabPanelController>(true);
            var session = coreDriver.transform.root.GetComponentInChildren<SessionController>(true);
            var accepted = actions != null && actions.Request(
                intent == ResearchAuxiliaryDeliveryIntent.BottlePour ?
                    PanelAction.BottlePour : PanelAction.PipetteDispense);
            LastPhysicalDeliveryFeedback = accepted ?
                (session?.LastInstrumentReceipt ?? "Native command accepted.") :
                (session?.LastError ?? "Native research transfer refused.");
        }

        private void HandlePickOrPlace()
        {
            if (!inputEnabled ||
                !pickPress.TryConsume((ulong)Time.frameCount, "desktop-pick-or-place", true))
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
                // An F release is a real bench placement, not "drop in air".
                // Refusal leaves hold ownership, pose and Rigidbody unchanged.
                TryPlaceHeldTool();
                return;
            }
            if (viewCamera == null || holdAnchor == null || !Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit, 3f)) return;
            var grabbable = hit.collider.GetComponentInParent<DesktopGrabbable>();
            if (grabbable == null) return;
            BeginHold(grabbable.transform);
            if (IsHoldingNativeTool) heldToolId = grabbable.ToolId;
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
            deliveryPress.Clear();
            LastPhysicalDeliveryFeedback = null;
            return true;
        }

        private void BeginHold(Transform tool)
        {
            heldTool = tool;
            deliveryPress.Clear();
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
