using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using PharmaLabVR.Core;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;
using PharmaLabVR.Session;

namespace PharmaLabVR.Input
{
    public sealed class XRInputAdapter : MonoBehaviour, ILabInputAdapter
    {
        [SerializeField] private Transform labFrame;
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private ActionBasedController leftActionController;
        [SerializeField] private ActionBasedController rightActionController;
        [SerializeField] private Transform registeredTool;
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private LabCaptureTarget captureTarget;
        [SerializeField] private CoreDriver coreDriver;
        [SerializeField] private ToolPresenter instrumentPresenter;
        [SerializeField] private XRGrabInteractable researchPipetteGrab;
        [SerializeField] private XRGrabInteractable researchBottleGrab;
        [SerializeField] private LabPanelController deliveryPanel;
        public bool HasAuxiliaryDeliveryBindings =>
            researchPipetteGrab != null && researchBottleGrab != null && deliveryPanel != null;
        public string LastPhysicalDeliveryFeedback { get; private set; }
        [SerializeField] private string toolId = "research-tool";
        [SerializeField] private string geometryProfileHash = "unassigned";
        private ulong sequence;
        private bool inputEnabled;
        private readonly ResearchPressBuffer deliveryPress = new ResearchPressBuffer();
        private ActionBasedController deliveryPressOwner;

        private void ClearDeliveryPress()
        {
            deliveryPress.Clear();
            deliveryPressOwner = null;
        }

        public void Configure(
            Transform frame,
            ActionBasedController left,
            ActionBasedController right,
            Transform tool,
            XRGrabInteractable grab,
            LabCaptureTarget target,
            string profileHash,
            CoreDriver driver = null)
        {
            labFrame = frame;
            leftActionController = left;
            rightActionController = right;
            leftController = left != null ? left.transform : null;
            rightController = right != null ? right.transform : null;
            registeredTool = tool;
            instrumentPresenter = tool != null ? tool.GetComponent<ToolPresenter>() : null;
            coreDriver = driver;
            grabInteractable = grab;
            captureTarget = target;
            geometryProfileHash = profileHash;
        }

        public void ConfigureAuxiliaryDelivery(XRGrabInteractable pipette,
            XRGrabInteractable bottle, LabPanelController panel)
        {
            if (pipette == null || bottle == null || panel == null)
                throw new System.ArgumentNullException(nameof(pipette),
                    "Physical XR delivery needs both authored grips and the shared Native command panel.");
            researchPipetteGrab = pipette;
            researchBottleGrab = bottle;
            deliveryPanel = panel;
        }

        private void Update()
        {
            if (!inputEnabled || coreDriver == null ||
                coreDriver.IsNativePaused || coreDriver.IsTimeHeld ||
                coreDriver.SourceNeedsPreparation)
            {
                ClearDeliveryPress();
                return;
            }
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            var headTracked = head.isValid &&
                head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked;
            if (!XRTrackingAdmission.HasFreshTracking(headTracked,
                    Tracked(leftActionController), Tracked(rightActionController)))
            {
                ClearDeliveryPress();
                return;
            }
            foreach (var item in new[] {
                (grip: researchPipetteGrab, id: "research-pipette-visual"),
                (grip: researchBottleGrab, id: "research-stock-bottle-visual")
            })
            {
                if (item.grip == null || !item.grip.isSelected ||
                    item.grip.firstInteractorSelecting is not XRDirectInteractor) continue;
                var owner = OwnerForInteractor(item.grip.firstInteractorSelecting.transform);
                if (!Tracked(owner)) continue;
                var ray = owner.GetComponentInChildren<XRRayInteractor>(true);
                if (ray != null && ray.IsOverUIGameObject()) continue;
                var trigger = owner.activateAction.action;
                if (trigger != null && trigger.enabled && trigger.WasPressedThisFrame())
                {
                    // Admission at the *press* moment prevents a later
                    // receiver alignment from retroactively authorizing it.
                    var capture = captureTarget != null ?
                        LabCaptureTarget.EstimateExclusive(item.grip.transform, captureTarget) :
                        System.Array.Empty<LabCaptureEstimate>();
                    var aligned = capture.Length == 1 &&
                        capture[0].destinationInventoryId == "receiver" &&
                        Mathf.Approximately(capture[0].fraction, 1f);
                    if (ResearchAuxiliaryDelivery.Resolve(item.id, true, true, aligned, true) ==
                        ResearchAuxiliaryDeliveryIntent.None)
                    {
                        LastPhysicalDeliveryFeedback =
                            "Align the direct-gripped instrument outlet over the stationary receiver.";
                        continue;
                    }
                    if (deliveryPress.PendingToolId != null &&
                        !ReferenceEquals(deliveryPressOwner, owner))
                        ClearDeliveryPress();
                    deliveryPressOwner = owner;
                    deliveryPress.Capture(item.id, (ulong)Time.frameCount, true);
                }
            }
        }

        private void OnDisable() => ClearDeliveryPress();

        public LabInputSample[] SampleInputs(ulong nowNs)
        {
            if (!inputEnabled || labFrame == null || leftController == null ||
                rightController == null || registeredTool == null)
            {
                instrumentPresenter?.PresentActuatorInput(0f);
                ClearDeliveryPress();
                return System.Array.Empty<LabInputSample>();
            }
            var selected = grabInteractable != null && grabInteractable.isSelected;
            var owner = selected ? SelectingController() : null;
            var directBodyGrip = selected && grabInteractable.firstInteractorSelecting is XRDirectInteractor;
            var blocked = coreDriver != null &&
                (coreDriver.IsTimeHeld || coreDriver.IsNativePaused);
            var actuator = selected && !blocked ? Actuator(owner, directBodyGrip) : 0f;
            // Every admitted recovery baseline must reflect actual head and both
            // controller tracking. An unheld tool is not evidence of tracked XR.
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            var headTracked = head.isValid &&
                head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked;
            var trackingValid = XRTrackingAdmission.HasFreshTracking(
                headTracked, Tracked(leftActionController), Tracked(rightActionController));
            // Never show an apparently open valve if Native will reject input
            // because hand/head tracking or ownership is not valid.
            if (!trackingValid || (selected && !Tracked(owner))) actuator = 0f;
            instrumentPresenter?.PresentActuatorInput(actuator);
            TryPhysicalAuxiliaryDelivery(trackingValid && !blocked);
            return new[] {
                LabGeometryAdapter.BuildSample(
                    toolId, ++sequence, nowNs, labFrame, registeredTool, actuator, geometryProfileHash,
                    trackingValid && (!selected || Tracked(owner)),
                    captureTarget != null ?
                        LabCaptureTarget.EstimateExclusive(registeredTool, captureTarget) : null)
            };
        }

        private void TryPhysicalAuxiliaryDelivery(bool trackingAndNativeReady)
        {
            if (coreDriver == null || captureTarget == null || deliveryPanel == null ||
                !trackingAndNativeReady || coreDriver.SourceNeedsPreparation)
            {
                ClearDeliveryPress();
                return;
            }
            var pendingId = deliveryPress.PendingToolId;
            if (pendingId == null) return;
            var grip = pendingId == "research-pipette-visual" ? researchPipetteGrab :
                pendingId == "research-stock-bottle-visual" ? researchBottleGrab : null;
            if (grip == null || !grip.isSelected ||
                grip.firstInteractorSelecting is not XRDirectInteractor)
            {
                ClearDeliveryPress();
                return;
            }
            var controller = OwnerForInteractor(grip.firstInteractorSelecting.transform);
            var ray = controller != null ?
                controller.GetComponentInChildren<XRRayInteractor>(true) : null;
            if (!Tracked(controller) ||
                !ReferenceEquals(controller, deliveryPressOwner) ||
                (ray != null && ray.IsOverUIGameObject()) ||
                !deliveryPress.TryConsume((ulong)Time.frameCount, pendingId, true))
            {
                ClearDeliveryPress();
                return;
            }
            // The queued input is consumed by this point. The second capture
            // check remains mandatory because tools/receivers may have moved
            // after the triggering frame.
            deliveryPressOwner = null;
            var capture = LabCaptureTarget.EstimateExclusive(grip.transform, captureTarget);
            var intent = ResearchAuxiliaryDelivery.Resolve(pendingId, true, true,
                capture.Length == 1 && capture[0].destinationInventoryId == "receiver" &&
                Mathf.Approximately(capture[0].fraction, 1f), true);
            if (intent == ResearchAuxiliaryDeliveryIntent.None)
            {
                LastPhysicalDeliveryFeedback =
                    "Align the direct-gripped instrument outlet over the stationary receiver.";
                return;
            }
            var accepted = deliveryPanel.Request(
                intent == ResearchAuxiliaryDeliveryIntent.BottlePour ?
                    PanelAction.BottlePour : PanelAction.PipetteDispense);
            var session = coreDriver.transform.root.GetComponentInChildren<SessionController>(true);
            LastPhysicalDeliveryFeedback = accepted ?
                (session?.LastInstrumentReceipt ?? "Native operation accepted.") :
                (session?.LastError ?? "Native operation refused.");
        }

        private ActionBasedController OwnerForInteractor(Transform selection)
        {
            if (selection == null) return null;
            if (leftController != null && selection.IsChildOf(leftController))
                return leftActionController;
            if (rightController != null && selection.IsChildOf(rightController))
                return rightActionController;
            return null;
        }

        private static float Actuator(ActionBasedController controller, bool directBodyGrip)
        {
            if (controller == null) return 0f;
            var ray = controller.GetComponentInChildren<XRRayInteractor>(true);
            if (!InteractionOwnership.MayActuateValve(directBodyGrip, Tracked(controller),
                ray != null && ray.IsOverUIGameObject())) return 0f;
            var grip = controller.selectActionValue.action;
            var trigger = controller.activateActionValue.action;
            return ChooseActuatorValue(
                grip != null ? grip.ReadValue<float>() : 0f,
                trigger != null ? trigger.ReadValue<float>() : 0f);
        }

        private static float ChooseActuatorValue(float gripValue, float triggerValue) => Mathf.Clamp01(triggerValue);

        private ActionBasedController SelectingController()
        {
            var interactor = grabInteractable != null ? grabInteractable.firstInteractorSelecting : null;
            var selectingTransform = interactor?.transform;
            if (selectingTransform == null) return null;
            if (leftController != null && selectingTransform.IsChildOf(leftController)) return leftActionController;
            if (rightController != null && selectingTransform.IsChildOf(rightController)) return rightActionController;
            return null;
        }

        private static bool Tracked(ActionBasedController controller)
        {
            if (controller == null || !controller.gameObject.activeInHierarchy) return false;
            var action = controller.isTrackedAction.action;
            return action != null && action.enabled && action.ReadValue<float>() > 0.5f;
        }

        public void ResetBaselines()
        {
            instrumentPresenter?.PresentActuatorInput(0f);
            ClearDeliveryPress();
        }
        public void AdvanceSequence(string id, ulong watermark)
        {
            if (id == toolId && sequence < watermark) sequence = watermark;
        }
        public void SetEnabled(bool value)
        {
            inputEnabled = value;
            ClearDeliveryPress();
            if (!value) instrumentPresenter?.PresentActuatorInput(0f);
        }
    }
}
