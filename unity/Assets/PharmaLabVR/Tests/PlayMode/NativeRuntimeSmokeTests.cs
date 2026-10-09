using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using PharmaLabVR.Tools;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace PharmaLabVR.Tests.PlayMode
{
    public sealed class NativeRuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator B02_PlayerLifecycleBootstrapsAndDisposesTheNativeAuthority()
        {
            var root = new GameObject("NativeRuntimeSmoke");
            var driver = root.AddComponent<CoreDriver>();
            yield return null;

            Assert.That(driver.enabled, Is.True);
            Assert.That(driver.Session, Is.Not.Null);
            Assert.That(driver.Session.IsOpen, Is.True);
            var snapshot = driver.Session.ReadSnapshot();
            StringAssert.Contains("\"id\":\"source\"", snapshot);
            StringAssert.Contains("\"id\":\"receiver\"", snapshot);
            StringAssert.Contains("\"id\":\"research-tool\"", snapshot);

            Object.Destroy(root);
            yield return null;
            Assert.That(driver == null || driver.Session == null, Is.True);
        }

        [Test]
        public void B02_VrSessionIdentityStartsInNativeVrMode()
        {
            using var session = new CoreSession("playmode-vr", "VR");
            StringAssert.Contains("\"mode\":\"VR\"", session.ReadSnapshot());
        }

        [UnityTest]
        public IEnumerator B01_DesktopAdapterInputIsAcceptedByNativeAuthority()
        {
            var root = new GameObject("DesktopNativeIntegration");
            root.SetActive(false);
            var tool = new GameObject("ResearchTool");
            tool.transform.SetParent(root.transform, false);
            tool.AddComponent<DesktopGrabbable>();
            var adapter = root.AddComponent<DesktopInputAdapter>();
            adapter.Configure(root.transform, null, null, tool.transform, null, "burette-50ml-research-v1");
            var driver = root.AddComponent<CoreDriver>();
            driver.ConfigureInputs(adapter);

            root.SetActive(true);
            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(driver.enabled, Is.True);
            Assert.That(driver.Session, Is.Not.Null);
            StringAssert.Contains("\"research-tool\"", driver.Session.ReadSnapshot());
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator H02_LoadedSessionKeepsNativeInputWatermarkAcrossAdapterReset()
        {
            var root = new GameObject("LoadedNativeSession");
            root.SetActive(false);
            var tool = new GameObject("ResearchTool");
            tool.transform.SetParent(root.transform, false);
            tool.AddComponent<DesktopGrabbable>();
            var adapter = root.AddComponent<DesktopInputAdapter>();
            adapter.Configure(root.transform, null, null, tool.transform, null, "burette-50ml-research-v1");
            var driver = root.AddComponent<CoreDriver>();
            driver.ConfigureInputs(adapter);
            root.SetActive(true);
            yield return null;
            var first = adapter.SampleInputs(1000000000)[0];
            first.sampleSequence = 50;
            driver.Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(new[] { first }));
            var imported = CoreSession.ImportSession(driver.Session.ExportSession());
            driver.ReplaceSession(imported);
            adapter.ResetBaselines();
            var next = adapter.SampleInputs(1020000000)[0];
            Assert.That(next.sampleSequence, Is.GreaterThan(50UL));
            Assert.DoesNotThrow(() => driver.Session.SubmitInputs(NativeJsonCodec.EncodeInputBatch(new[] { next })));
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator S01_PackagedDatabaseProducesLiveProductObservation()
        {
            var databasePath = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
            using var session = new CoreSession("player-science", "Desktop", databasePath, ScientificPackage.DatabaseSha256);
            Assert.That(session.SubmitOrdered("CreateVessel", "{\"id\":\"acid\",\"capacityM3\":0.00002}"), Does.Contain("\"accepted\":true"));
            Assert.That(session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"acid\",\"stockKind\":\"HydrochloricAcid\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.00001}",
                "{\"acid\":\"0\"}"), Does.Contain("\"accepted\":true"));
            var current = false;
            for (var attempt = 0; attempt < 200 && !current; attempt++)
            {
                session.Step(0.0, 1000000000);
                var snapshot = session.ReadSnapshot();
                current = snapshot.Contains("\"vesselId\":\"acid\"") &&
                          snapshot.Contains("\"freshness\":\"Current\"") &&
                          snapshot.Contains("\"databaseIdentity\":\"" + ScientificPackage.DatabaseSha256 + "\"");
                if (!current) yield return null;
            }
            Assert.That(current, Is.True, "Packaged database did not produce an eligible current pH observation.");
        }

        [Test]
        public void S01_LoadRequiresTheCurrentPinnedScientificPackage()
        {
            var databasePath = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
            using var session = new CoreSession("package-load", "Desktop", databasePath, ScientificPackage.DatabaseSha256);
            var exported = session.ExportSession();
            Assert.Throws<InvalidDataException>(() => CoreSession.ImportSession(exported, databasePath + ".wrong"));
            using var restored = CoreSession.ImportSession(exported, databasePath);
            Assert.That(restored.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator X03_InjectedHmdPoseMovesGeneratedXrCamera()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var hmd = InputSystem.AddDevice<XRHMD>();
            yield return null;
            var xrRig = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "XRPlayerRig")?.gameObject;
            Assert.That(xrRig, Is.Not.Null);
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ModeCoordinator>(true))
                .First().Select(ApplicationMode.VirtualReality);
            var camera = xrRig.GetComponentInChildren<Camera>(true);
            Assert.That(camera, Is.Not.Null);
            InputSystem.QueueDeltaStateEvent(hmd.trackingState, 3);
            InputSystem.QueueDeltaStateEvent(hmd.devicePosition, new Vector3(0.15f, 1.6f, 0.25f));
            InputSystem.QueueDeltaStateEvent(hmd.deviceRotation, Quaternion.Euler(0f, 35f, 0f));
            InputSystem.Update();
            yield return null;
            Assert.That(camera.transform.localPosition.x, Is.EqualTo(0.15f).Within(0.03f));
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(1.6f).Within(0.03f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.Euler(0f, 35f, 0f)), Is.LessThan(3f));
            InputSystem.RemoveDevice(hmd);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator V01_LabLiquidVisualsFollowCommittedNativeTransfer()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var tool = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == "DesktopResearchTool");
            var receiver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == "ReceiverVessel");
            var sourceLiquid = tool.GetComponent<LiquidPresenter>();
            var receiverLiquid = receiver.GetComponent<LiquidPresenter>();
            Assert.That(sourceLiquid, Is.Not.Null);
            Assert.That(receiverLiquid, Is.Not.Null);
            Assert.That(sourceLiquid.CommittedVolumeM3, Is.EqualTo(25e-6).Within(1e-12));
            Assert.That(receiverLiquid.CommittedVolumeM3, Is.Zero);
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.000001},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"source\":\"1\",\"receiver\":\"0\"}"), Does.Contain("\"accepted\":true"));
            yield return null;
            Assert.That(sourceLiquid.CommittedVolumeM3, Is.EqualTo(24e-6).Within(1e-12));
            Assert.That(receiverLiquid.CommittedVolumeM3, Is.EqualTo(1e-6).Within(1e-12));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator X03_SelectedHandTrackingLossCannotBeMaskedByOtherHand()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var xrRig = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == "XRPlayerRig").gameObject;
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ModeCoordinator>(true))
                .First().Select(ApplicationMode.VirtualReality);
            var left = xrRig.GetComponentsInChildren<XRDirectInteractor>(true).First(item => item.name.Contains("Left"));
            var grab = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<XRGrabInteractable>(true)).First();
            var adapter = xrRig.GetComponent<XRInputAdapter>();
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            adapter.SetEnabled(true);
            var injectedGamepad = InputSystem.AddDevice<Gamepad>();
            var actionControllers = xrRig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>(true);
            var leftAction = actionControllers.First(item => item.name.Contains("Left"));
            var rightAction = actionControllers.First(item => item.name.Contains("Right"));
            var leftTracking = new InputAction("Injected Left Tracking", InputActionType.Button, "<Gamepad>/leftTrigger");
            var rightTracking = new InputAction("Injected Right Tracking", InputActionType.Button, "<Gamepad>/rightTrigger");
            leftAction.isTrackedAction = new InputActionProperty(leftTracking);
            rightAction.isTrackedAction = new InputActionProperty(rightTracking);
            leftTracking.Enable();
            rightTracking.Enable();
            InputSystem.QueueStateEvent(injectedGamepad, new GamepadState { leftTrigger = 255, rightTrigger = 255 });
            InputSystem.Update();
            yield return null;
            Assert.That(injectedGamepad.rightTrigger.ReadValue(), Is.GreaterThan(0.5f));
            Assert.That(rightTracking.ReadValue<float>(), Is.GreaterThan(0.5f));
            Assert.That(rightAction.isTrackedAction.action.controls.Count, Is.GreaterThan(0));
            Assert.That(driver.ResumeAfterTimeHold(), Is.True);
            yield return null;
            left.StartManualInteraction((IXRSelectInteractable)grab);
            yield return null;
            Assert.That(grab.isSelected, Is.True);
            InputSystem.QueueStateEvent(injectedGamepad, new GamepadState { leftTrigger = 0, rightTrigger = 255 });
            InputSystem.Update();
            yield return null;
            Assert.That(leftAction.isTrackedAction.action.ReadValue<float>(), Is.LessThan(0.5f));
            Assert.That(rightAction.isTrackedAction.action.ReadValue<float>(), Is.GreaterThan(0.5f));
            Assert.That(grab.firstInteractorSelecting.transform.IsChildOf(leftAction.transform), Is.True);
            Assert.That(adapter.SampleInputs(1000000000)[0].trackingValid, Is.False);
            for (var attempt = 0; attempt < 10 && !driver.IsTimeHeld; attempt++) yield return null;
            Assert.That(driver.IsTimeHeld, Is.True);
            StringAssert.Contains("\"reason\":\"TrackingLoss\"", driver.Session.ReadSnapshot());
            left.EndManualInteraction();
            InputSystem.RemoveDevice(injectedGamepad);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
