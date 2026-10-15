using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PharmaLabVR.Core;
using PharmaLabVR.Input;
using PharmaLabVR.Tools;
using PharmaLabVR.UI;
using PharmaLabVR.Session;
using PharmaLabVR.Lab;
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
        [Test]
        public void P05_03_ComputeHoldCannotOfferContinueWithoutNativeRecovery()
        {
            var root = new GameObject("ComputeHoldReadiness");
            var panel = root.AddComponent<HoldPanel>();
            panel.Present(true, true, true, "Compute");
            Assert.That(panel.CanContinue, Is.False,
                "A neutral controller alone cannot clear an unresolved scientific hold.");
            var requests = 0;
            panel.ContinueRequested += () => requests++;
            panel.RequestContinue();
            Assert.That(requests, Is.Zero);
            panel.Present(true, true, true, "Compute", nativeRecoveryReady: true);
            Assert.That(panel.CanContinue, Is.True);
            panel.RequestContinue();
            Assert.That(requests, Is.EqualTo(1));
            Object.DestroyImmediate(root);
        }

        [System.Serializable] private sealed class StockInventory
        {
            public double solventWaterKg;
            public double sodiumMol;
            public double chlorideMol;
            public double acetateMol;
            public double researchAdditiveVolumeM3;
            public string preparationId;
            public string provenanceId;
        }
        [System.Serializable] private sealed class StockVessel
        {
            public string id;
            public StockInventory inventory;
        }
        [System.Serializable] private sealed class StockTool
        {
            public StockInventory tipInventory;
            public StockInventory inFlightInventory;
        }
        [System.Serializable] private sealed class StockState
        {
            public bool paused;
            public string eventSequence;
            public StockVessel[] vessels;
            public StockVessel[] sinks;
            public StockTool[] tools;
            public StockInventory materialLedger;
            public StockObservation[] observations;
        }
        [System.Serializable] private sealed class StockObservation
        {
            public string vesselId;
            public string freshness;
        }

        private sealed class LivePourAdapter : MonoBehaviour, ILabInputAdapter
        {
            public Transform Tool;
            public Transform Frame;
            public LabCaptureTarget Target;
            public float Actuator;
            private ulong sequence;
            private bool enabledInput = true;

            public LabInputSample[] SampleInputs(ulong nowNs) => !enabledInput ? System.Array.Empty<LabInputSample>() :
                new[] { LabGeometryAdapter.BuildSample("research-tool", ++sequence, nowNs, Frame, Tool,
                    Actuator, "burette-50ml-research-v1", true, Target.Estimate(Tool)) };
            public void ResetBaselines() => Actuator = 0f;
            public void AdvanceSequence(string toolId, ulong watermark)
            {
                if (toolId == "research-tool" && sequence < watermark) sequence = watermark;
            }
            public void SetEnabled(bool value) => enabledInput = value;
        }

        [UnityTest]
        public IEnumerator P02_01_SelectedStocksPrepareNativePoolsFromAnEmptyLab()
        {
            var stockFactors = new[] {
                (id: "water", sodium: 0d, chloride: 0d, acetate: 0d),
                (id: "hcl", sodium: 0d, chloride: 1d, acetate: 0d),
                (id: "naoh", sodium: 1d, chloride: 0d, acetate: 0d),
                (id: "nacl", sodium: 1d, chloride: 1d, acetate: 0d),
                (id: "acetic-acid", sodium: 0d, chloride: 0d, acetate: 1d),
                (id: "sodium-acetate", sodium: 1d, chloride: 0d, acetate: 1d)
            };
            foreach (var (id, sodiumFactor, chlorideFactor, acetateFactor) in stockFactors)
            foreach (var concentration in id == "water" ? new[] { 0d } : new[] { 0.01d, 0.05d, 0.1d })
            {
                var sodium = sodiumFactor * concentration * 0.025d;
                var chloride = chlorideFactor * concentration * 0.025d;
                var acetate = acetateFactor * concentration * 0.025d;
                var root = new GameObject("StockSelection-" + id);
                var driver = root.AddComponent<CoreDriver>();
                var controller = root.AddComponent<SessionController>();
                controller.Configure(driver);
                try
                {
                var before = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                Assert.That(before.paused, Is.True, id);
                Assert.That(before.vessels.Single(v => v.id == "source").inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(0d), id);
                Assert.That(before.vessels.Single(v => v.id == "receiver").inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(0d), id);
                Assert.That(controller.SelectStock(id, concentration), Is.True, controller.LastError);
                var after = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                var prepared = after.vessels.Single(v => v.id == "source").inventory;
                Assert.That(after.paused, Is.False, id);
                Assert.That(prepared.researchAdditiveVolumeM3, Is.EqualTo(2.5e-5).Within(1e-12), id);
                Assert.That(prepared.solventWaterKg, Is.EqualTo(0.025).Within(1e-9), id);
                Assert.That(prepared.sodiumMol, Is.EqualTo(sodium).Within(1e-9), id);
                Assert.That(prepared.chlorideMol, Is.EqualTo(chloride).Within(1e-9), id);
                Assert.That(prepared.acetateMol, Is.EqualTo(acetate).Within(1e-9), id);
                Assert.That(prepared.preparationId, Is.EqualTo("source"), id);
                Assert.That(prepared.provenanceId, Is.EqualTo("aqueous-six-research-v1"), id);
                }
                finally { Object.DestroyImmediate(root); }
                yield return null;
                for (var attempt = 0; attempt < 200 && CoreSession.PendingRetirements > 0; attempt++)
                    yield return new WaitForSecondsRealtime(0.01f);
                Assert.That(CoreSession.PendingRetirements, Is.Zero, "The previous real solver context did not retire.");
            }
        }

        [UnityTest]
        public IEnumerator P02_02_TouchedRevisionFacadeRejectsStaleAndIgnoresUnrelatedVessel()
        {
            var root = new GameObject("CommandFacadeIntegration");
            var driver = root.AddComponent<CoreDriver>();
            try
            {
            var commands = new LabCommandService(driver.Session);
            var sourceAtZero = commands.CaptureTouchedRevisions("source");
            Assert.That(driver.Session.SubmitOrdered("CreateVessel",
                "{\"id\":\"unrelated\",\"capacityM3\":0.00005}"), Does.Contain("\"accepted\":true"));
            var unrelatedAtZero = commands.CaptureTouchedRevisions("unrelated");
            Assert.That(commands.Submit("PrepareStock",
                "{\"vesselId\":\"unrelated\",\"stockKind\":\"Water\",\"concentrationMolPerL\":0.0,\"referenceVolumeM3\":0.000001}",
                unrelatedAtZero).Accepted, Is.True);
            var sourcePayload = "{\"vesselId\":\"source\",\"stockKind\":\"SodiumChloride\",\"concentrationMolPerL\":0.05,\"referenceVolumeM3\":0.000025}";
            var committed = commands.Submit("PrepareStock", sourcePayload, sourceAtZero);
            Assert.That(committed.Accepted, Is.True,
                "An unrelated vessel revision must not invalidate the source intent.");
            var identity = "{\"schemaVersion\":1,\"branchId\":\"local-session\",\"commandSequence\":\"" +
                committed.CommandSequence + "\",\"type\":\"PrepareStock\",\"expectedMaterialRevisions\":{\"source\":\"0\"},\"payload\":" + sourcePayload + "}";
            driver.Session.Submit(identity);
            Assert.That(driver.Session.PollEvents().Any(e => e.Contains("\"commandSequence\":\"" + committed.CommandSequence + "\"") &&
                e.Contains("\"code\":\"Accepted\"")), Is.True, "Identical command identity should replay the accepted receipt.");
            driver.Session.Submit(identity.Replace("\"concentrationMolPerL\":0.05", "\"concentrationMolPerL\":0.1"));
            Assert.That(driver.Session.PollEvents().Any(e => e.Contains("\"commandSequence\":\"" + committed.CommandSequence + "\"") &&
                e.Contains("\"code\":\"CommandIdentityConflict\"")), Is.True);
            var rejected = commands.Submit("PrepareStock", sourcePayload, sourceAtZero);
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Code, Is.EqualTo("StaleRevision"));
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
        }

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

        [UnityTest]
        public IEnumerator P06_01_ReenabledLabDriverKeepsOneSessionViewAndSubscription()
        {
            var root = new GameObject("ReenabledLabDriver");
            try
            {
                var driver = root.AddComponent<CoreDriver>();
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                var originalSession = driver.Session;
                Assert.That(root.GetComponents<MeasurementView>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponents<NativeLabVisuals>().Length, Is.EqualTo(1));
                root.SetActive(false);
                yield return null;
                Assert.That(originalSession.IsOpen, Is.False);
                Assert.That(driver.CurrentLabView, Is.Null);
                root.SetActive(true);
                yield return null;
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.Session, Is.Not.SameAs(originalSession));
                Assert.That(root.GetComponents<MeasurementView>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponents<NativeLabVisuals>().Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_01_LabSceneReactivationDoesNotDuplicateLiquidViewsOrRecorder()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
                var roots = scene.GetRootGameObjects();
                var driver = roots.SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
                var tool = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .First(item => item.name == "DesktopResearchTool");
                var receiver = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .First(item => item.name == "ReceiverVessel");
                var recorder = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .First(item => item.name == "PerformanceRecorder");
                var initialView = driver.CurrentLabView;
                Assert.That(initialView, Is.Not.Null);
                Assert.That(initialView.Vessels.Any(item => item.Id == "source"), Is.True);
                Assert.That(tool.GetComponents<LiquidPresenter>().Length, Is.EqualTo(1));
                Assert.That(receiver.GetComponents<LiquidPresenter>().Length, Is.EqualTo(1));
                Assert.That(recorder.GetComponents<PlayerPerformanceRecorder>().Length, Is.EqualTo(1));
                driver.enabled = false;
                yield return null;
                Assert.That(driver.CurrentLabView, Is.Null);
                driver.enabled = true;
                yield return null;
                Assert.That(driver.Session, Is.Not.Null, driver.StartupError);
                Assert.That(driver.CurrentLabView, Is.Not.SameAs(initialView));
                Assert.That(driver.GetComponents<NativeLabVisuals>().Length, Is.EqualTo(1));
                Assert.That(tool.GetComponents<LiquidPresenter>().Length, Is.EqualTo(1));
                Assert.That(receiver.GetComponents<LiquidPresenter>().Length, Is.EqualTo(1));
                Assert.That(recorder.GetComponents<PlayerPerformanceRecorder>().Length, Is.EqualTo(1));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P06_01_MissingScientificPackageDisablesLiveLabSession()
        {
            var root = new GameObject("MissingScientificPackage");
            root.SetActive(false);
            try
            {
                var driver = root.AddComponent<CoreDriver>();
                driver.ConfigurePackagePathResolver(() => throw new ScientificPackageMismatchException("Pinned package is missing."));
                LogAssert.Expect(LogType.Error, "Native scientific core unavailable: Pinned package is missing.");
                root.SetActive(true);
                yield return null;
                Assert.That(driver.enabled, Is.False);
                Assert.That(driver.Session, Is.Null);
                Assert.That(driver.StartupError, Does.Contain("Pinned package is missing"));
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator O06_CoreDriverPresentsLiveSolverStatusThroughMeasurementView()
        {
            var root = new GameObject("NativeMeasurementIntegration");
            try
            {
                var driver = root.AddComponent<CoreDriver>();
                Assert.That(driver.TryPrepareSourceStock("hcl", 0.05), Is.True);
                Assert.That(driver.Session.SubmitOrdered("CreateVessel", "{\"id\":\"empty-control\",\"capacityM3\":0.000002}"),
                    Does.Contain("\"accepted\":true"));
                for (var attempt = 0; attempt < 200 && (driver.Measurement == null ||
                    driver.Measurement.Availability != MeasurementAvailability.Current); attempt++)
                    yield return new WaitForSecondsRealtime(0.01f);
                Assert.That(driver.Measurement, Is.Not.Null);
                Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                    driver.Session.ReadSnapshot());
                Assert.That(float.TryParse(driver.Measurement.DisplayValue,
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                    out var value), Is.True);
                Assert.That(value, Is.InRange(0f, 14f));
                Assert.That(driver.Measurement.Explanation, Does.Contain("Research"));
                Assert.That(driver.SelectMeasurementVessel("empty-control"), Is.True);
                Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Unsupported));
                Assert.That(driver.Measurement.DisplayValue, Is.EqualTo("--"));
            }
            finally { Object.DestroyImmediate(root); }
            yield return null;
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
            Assert.Throws<ScientificPackageMismatchException>(() => CoreSession.ImportSession(exported, databasePath + ".wrong"));
            using var restored = CoreSession.ImportSession(exported, databasePath);
            Assert.That(restored.IsOpen, Is.True);
        }

        [Test]
        public void P04_02_ColdStartAndImportRejectMismatchedActualPackageBytes()
        {
            var databasePath = ScientificPackage.PrepareLocal(Application.streamingAssetsPath);
            var corrupted = Path.Combine(Application.temporaryCachePath, "p04-02-corrupt-minteq.dat");
            var bytes = File.ReadAllBytes(databasePath);
            bytes[bytes.Length / 2] ^= 0x01;
            File.WriteAllBytes(corrupted, bytes);
            try
            {
                Assert.Throws<ScientificPackageMismatchException>(() =>
                {
                    using var rejected = new CoreSession("wrong-bytes", "Desktop", corrupted,
                        ScientificPackage.DatabaseSha256);
                });
            }
            finally { File.Delete(corrupted); }
            using var session = new CoreSession("package-identity", "Desktop", databasePath,
                ScientificPackage.DatabaseSha256);
            var wrongIdentity = session.ExportSession().Replace(ScientificPackage.DatabaseSha256,
                new string('0', ScientificPackage.DatabaseSha256.Length));
            Assert.Throws<ScientificPackageMismatchException>(() =>
            {
                using var rejected = CoreSession.ImportSession(wrongIdentity);
            });
        }

        [UnityTest]
        public IEnumerator P04_02_MismatchedSaveOpensRecordedOnlyObservationWithoutReplacingLiveSession()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).First();
            Assert.That(driver.TryPrepareSourceStock("hcl", 0.1), Is.True);
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            var actual = driver.Session.ExportSession();
            var mismatched = actual.Replace(ScientificPackage.DatabaseSha256,
                new string('0', ScientificPackage.DatabaseSha256.Length));
            var path = Path.Combine(Application.temporaryCachePath, "p04-02-recorded-only.plv.json");
            File.WriteAllText(path, mismatched);
            File.WriteAllText(path + ".bak", actual);
            try
            {
                Assert.Throws<ScientificPackageMismatchException>(() =>
                    new SaveService().Load(path, driver.ScientificDatabasePath),
                    "A compatible backup must not silently replace a present save with a different model.");
                Assert.That(controller.ContinueFrom(path), Is.False);
                Assert.That(controller.RecordedReview, Is.Not.Null);
                Assert.That(controller.RecordedReview.Observations.Single(item => item.vesselId == "source").DisplayValue,
                    Is.EqualTo(driver.Measurement.DisplayValue));
                Assert.That(controller.LastError, Does.Contain("saved observations only"));
                Assert.That(driver.Session.IsOpen, Is.True);
                Assert.That(driver.Session.ReadSnapshot(), Does.Contain("\"source\""));
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".bak");
            }
            yield return SceneManager.UnloadSceneAsync(scene);
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
        public IEnumerator P02_03_SceneAdapterPourRendersNativeReceiverAndRetainsToolMaterial()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            try
            {
            var children = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var driver = children.Select(item => item.GetComponent<CoreDriver>()).First(item => item != null);
            var tool = children.First(item => item.name == "DesktopResearchTool");
            var receiver = children.First(item => item.name == "ReceiverVessel");
            var pendingDrop = tool.GetComponent<DropPresenter>();
            Assert.That(pendingDrop, Is.Not.Null);
            var target = receiver.GetComponent<LabCaptureTarget>();
            var liquid = receiver.GetComponent<LiquidPresenter>();
            Assert.That(target, Is.Not.Null);
            Assert.That(liquid, Is.Not.Null);
            var input = new GameObject("LivePourActionInjection");
            SceneManager.MoveGameObjectToScene(input, scene);
            var adapter = input.AddComponent<LivePourAdapter>();
            adapter.Tool = tool;
            adapter.Frame = tool.parent;
            adapter.Target = target;
            driver.ConfigureInputs(adapter);
            Assert.That(driver.TryPrepareSourceStock("nacl", 0.1), Is.True);
            for (var attempt = 0; driver.IsTimeHeld && attempt < 100; attempt++)
            {
                driver.ResumeAfterTimeHold();
                yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.That(driver.IsTimeHeld, Is.False);
            Assert.That(driver.Session.SubmitOrdered("CreateVessel", "{\"id\":\"unrelated\",\"capacityM3\":0.000002}"),
                Does.Contain("\"accepted\":true"));
            Assert.That(driver.Session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"unrelated\",\"stockKind\":\"HydrochloricAcid\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.000001}",
                "{\"unrelated\":\"0\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                "The first pour needs an eligible science budget boundary.");
            var unrelatedCurrent = false;
            for (var attempt = 0; attempt < 200 && !unrelatedCurrent; attempt++)
            {
                var state = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                unrelatedCurrent = state.observations != null && state.observations.Any(item =>
                    item.vesselId == "unrelated" && item.freshness == "Current");
                if (!unrelatedCurrent) yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.That(unrelatedCurrent, Is.True, driver.Session.ReadSnapshot());
            tool.position = receiver.position + Vector3.up * 0.08f;
            Assert.That(target.Estimate(tool).Length, Is.EqualTo(1), "The scene outlet must aim into its real capture aperture.");
            Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            adapter.Actuator = 1f;
            for (var attempt = 0; attempt < 100 && liquid.CommittedVolumeM3 <= 0d; attempt++)
            {
                if (driver.IsTimeHeld)
                {
                    adapter.Actuator = 0f;
                    for (var recovery = 0; driver.IsTimeHeld && recovery < 100; recovery++)
                    {
                        driver.ResumeAfterTimeHold();
                        yield return new WaitForSecondsRealtime(0.01f);
                    }
                    Assert.That(driver.IsTimeHeld, Is.False, driver.Session.ReadSnapshot());
                    adapter.Actuator = 1f;
                }
                yield return new WaitForSecondsRealtime(0.02f);
            }
            Assert.That(liquid.CommittedVolumeM3, Is.GreaterThan(0d), driver.Session.ReadSnapshot());
            var flowing = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
            Assert.That(flowing.tools[0].tipInventory.researchAdditiveVolumeM3, Is.GreaterThan(0d));
            Assert.That(pendingDrop.CommittedVolumeM3, Is.GreaterThan(0d));
            Assert.That(pendingDrop.ActiveParcelId, Is.Not.Empty);
            Assert.That(tool.Find("NativePendingDrop").gameObject.activeSelf, Is.True);
            Assert.That(flowing.materialLedger.researchAdditiveVolumeM3, Is.EqualTo(26e-6).Within(1e-12));
            adapter.Actuator = 0f;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                if (driver.IsTimeHeld) driver.ResumeAfterTimeHold();
                yield return new WaitForSecondsRealtime(0.02f);
                var latest = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                if (latest.tools[0].inFlightInventory.researchAdditiveVolumeM3 == 0d) break;
            }
            var landed = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
            Assert.That(landed.tools[0].inFlightInventory.researchAdditiveVolumeM3, Is.Zero);
            Assert.That(pendingDrop.CommittedVolumeM3, Is.Zero);
            Assert.That(pendingDrop.ActiveParcelId, Is.Empty);
            Assert.That(tool.Find("NativePendingDrop").gameObject.activeSelf, Is.False);
            Assert.That(landed.tools[0].tipInventory.researchAdditiveVolumeM3, Is.GreaterThan(0d));
            Assert.That(landed.materialLedger.researchAdditiveVolumeM3, Is.EqualTo(26e-6).Within(1e-12));
            Assert.That(landed.observations.Any(item => item.vesselId == "unrelated" && item.freshness == "Current"),
                Is.True, "A continuous pour must not stale an unrelated vessel's real observation.");
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P04_01_SceneCheckpointRestoresScientificMeasurementGeneration()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            try
            {
            Assert.That(driver.Session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"source\",\"stockKind\":\"HydrochloricAcid\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.00000004}",
                "{\"source\":\"0\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            var acidValue = driver.Measurement.DisplayValue;
            if (driver.IsTimeHeld) Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            Assert.That(driver.Session.SubmitOrdered("CreateVessel", "{\"id\":\"base\",\"capacityM3\":0.00005}"),
                Does.Contain("\"accepted\":true"));
            Assert.That(driver.Session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"base\",\"stockKind\":\"SodiumHydroxide\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.00000009}",
                "{\"base\":\"0\"}"), Does.Contain("\"accepted\":true"));
            var baseCurrent = false;
            for (var attempt = 0; attempt < 200 && !baseCurrent; attempt++)
            {
                var state = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                baseCurrent = state.observations != null && state.observations.Any(item =>
                    item.vesselId == "base" && item.freshness == "Current");
                if (!baseCurrent) yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.That(baseCurrent, Is.True, driver.Session.ReadSnapshot());
            Assert.That(driver.CreateCheckpoint("acid-state"), Is.True);
            if (driver.IsTimeHeld) Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"base\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00000004},\"captureFractions\":[{\"destinationInventoryId\":\"source\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"base\":\"1\",\"source\":\"1\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 && driver.Measurement.DisplayValue == acidValue; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                driver.Session.ReadSnapshot());
            Assert.That(driver.Measurement.DisplayValue, Is.Not.EqualTo(acidValue));
            Assert.That(driver.RestartCheckpoint("acid-state"), Is.True);
            Assert.That(driver.Measurement.DisplayValue, Is.EqualTo("--"),
                "A newer-state pH must disappear as soon as the checkpoint is restored.");
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                driver.Session.ReadSnapshot());
            Assert.That(driver.Measurement.DisplayValue, Is.EqualTo(acidValue));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator P04_02_ReceiverInspectionUsesItsOwnLiveObservation()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            try
            {
            Assert.That(driver.TryPrepareSourceStock("hcl", 0.1), Is.True);
            Assert.That(driver.Session.SubmitOrdered("PrepareStock",
                "{\"vesselId\":\"receiver\",\"stockKind\":\"SodiumHydroxide\",\"concentrationMolPerL\":0.1,\"referenceVolumeM3\":0.00000004}",
                "{\"receiver\":\"0\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            var sourceValue = driver.Measurement.DisplayValue;
            Assert.That(driver.SelectMeasurementVessel("receiver"), Is.True);
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.DisplayValue, Is.Not.EqualTo(sourceValue));
            Assert.That(driver.Measurement.Explanation, Does.Contain("receiver"));
            var baseValue = driver.Measurement.DisplayValue;
            if (driver.IsTimeHeld) Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00000004},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"source\":\"1\",\"receiver\":\"1\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 200 &&
                (driver.Measurement.Availability != MeasurementAvailability.Current ||
                 driver.Measurement.DisplayValue == baseValue); attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current),
                driver.Session.ReadSnapshot());
            Assert.That(driver.Measurement.DisplayValue, Is.Not.EqualTo(baseValue));
            Assert.That(driver.Measurement.Explanation, Does.Contain("receiver"));
            Assert.That(driver.SelectMeasurementVessel("missing"), Is.False);
            Assert.That(driver.SelectedMeasurementVesselId, Is.EqualTo("receiver"));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
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
            Assert.That(driver.TryPrepareSourceStock("nacl", 0.1), Is.True);
            yield return null;
            for (var recoveryAttempt = 0; driver.IsTimeHeld && recoveryAttempt < 100; recoveryAttempt++)
            {
                driver.ResumeAfterTimeHold();
                yield return new WaitForSecondsRealtime(0.01f);
            }
            Assert.That(driver.IsTimeHeld, Is.False, "A fresh neutral baseline is required before transfer.");
            Assert.That(sourceLiquid.CommittedVolumeM3, Is.EqualTo(25e-6).Within(1e-12));
            Assert.That(receiverLiquid.CommittedVolumeM3, Is.Zero);
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00000005},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"source\":\"1\",\"receiver\":\"0\"}"), Does.Contain("\"accepted\":true"));
            for (var attempt = 0; attempt < 20 &&
                System.Math.Abs(sourceLiquid.CommittedVolumeM3 - 24.95e-6) > 1e-12; attempt++)
                yield return new WaitForSecondsRealtime(0.02f);
            Assert.That(sourceLiquid.CommittedVolumeM3, Is.EqualTo(24.95e-6).Within(1e-12));
            Assert.That(receiverLiquid.CommittedVolumeM3, Is.EqualTo(5e-8).Within(1e-12));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator D01_ColdDesktopLabShowsToolAndReceiverInCameraFrustum()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var children = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var camera = children.First(item => item.name == "DesktopCamera").GetComponent<Camera>();
            foreach (var name in new[] { "DesktopResearchTool", "ReceiverVessel" })
            {
                var target = children.First(item => item.name == name);
                var viewport = camera.WorldToViewportPoint(target.position + Vector3.up * 0.12f);
                Assert.That(viewport.z, Is.GreaterThan(0f), name);
                Assert.That(viewport.x, Is.InRange(0.1f, 0.9f), name);
                Assert.That(viewport.y, Is.InRange(0.1f, 0.9f), name);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator P05_03_ComputeHoldInLabKeepsSaveResponsiveAndMaterialUnchanged()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).First();
            sessions.Configure(driver, "playmode-hold-" + System.Guid.NewGuid().ToString("N"));
            try
            {
            Assert.That(sessions.SelectStock("hcl", 0.1), Is.True, sessions.LastError);
            for (var attempt = 0; driver.IsTimeHeld && attempt < 100; attempt++)
            {
                driver.ResumeAfterTimeHold();
                yield return new WaitForSecondsRealtime(0.01f);
            }
            for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
            Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            var before = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
            Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00001},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                "{\"source\":\"1\",\"receiver\":\"0\"}"), Does.Contain("ObservationVolumeHold"));
            StringAssert.Contains("\"reason\":\"Compute\"", driver.Session.ReadSnapshot());
            for (var attempt = 0; attempt < 50 && driver.HoldReason != "Compute"; attempt++)
                yield return new WaitForSecondsRealtime(0.01f);
            Assert.That(driver.HoldReason, Is.EqualTo("Compute"), driver.Session.ReadSnapshot());
            Assert.That(driver.NativeRecoveryReady, Is.EqualTo(driver.Session.ReadSnapshot().Contains("\"recoveryReady\":true")));
            Assert.That(sessions.Save(), Is.True, sessions.LastError);
            var savePath = sessions.LastDurableSavePath;
            try
            {
                Assert.That(File.Exists(savePath), Is.True);
                var held = JsonUtility.FromJson<StockState>(driver.Session.ReadSnapshot());
                Assert.That(held.eventSequence, Is.EqualTo(before.eventSequence));
                Assert.That(held.vessels.First(item => item.id == "source").inventory.researchAdditiveVolumeM3,
                    Is.EqualTo(before.vessels.First(item => item.id == "source").inventory.researchAdditiveVolumeM3));
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
                if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            }
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator J02_SceneSaveLoadRestoresMaterialAndRequiresContinue()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).First();
            sessions.Configure(driver, "playmode-" + System.Guid.NewGuid().ToString("N"));
            Assert.That(sessions.SelectStock("nacl", 0.1), Is.True, sessions.LastError);
            Assert.That(sessions.Save(), Is.True, sessions.LastError);
            var savePath = sessions.LastDurableSavePath;
            try
            {
                for (var attempt = 0; driver.IsTimeHeld && attempt < 100; attempt++)
                {
                    driver.ResumeAfterTimeHold();
                    yield return new WaitForSecondsRealtime(0.01f);
                }
                Assert.That(driver.IsTimeHeld, Is.False);
                for (var attempt = 0; attempt < 200 && driver.Measurement.Availability != MeasurementAvailability.Current; attempt++)
                    yield return new WaitForSecondsRealtime(0.01f);
                Assert.That(driver.Measurement.Availability, Is.EqualTo(MeasurementAvailability.Current));
                Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
                Assert.That(driver.Session.SubmitOrdered("TransferFixed",
                    "{\"sourceInventoryId\":\"source\",\"sourceRegion\":\"Homogeneous\",\"selection\":\"HomogeneousAqueousLiquid\",\"quantity\":{\"basis\":\"LiquidVolumeM3\",\"value\":0.00000005},\"captureFractions\":[{\"destinationInventoryId\":\"receiver\",\"fraction\":1.0}],\"overflowSinkId\":\"spill\"}",
                    "{\"source\":\"1\",\"receiver\":\"0\"}"), Does.Contain("\"accepted\":true"),
                    driver.Session.ReadSnapshot());
                Assert.That(sessions.ContinueFrom(savePath), Is.True, sessions.LastError);
                var snapshot = driver.Session.ReadSnapshot();
                StringAssert.Contains("\"reason\":\"SessionLoad\"", snapshot);
                StringAssert.Contains("\"researchAdditiveVolumeM3\":2.5e-05", snapshot);
                Assert.That(sessions.ContinueHeldSession(), Is.True, sessions.LastError);
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
                if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator D04_FocusRecoveryContinueRestoresDesktopInputThroughSceneControl()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            var driver = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreDriver>(true)).First();
            var adapter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DesktopInputAdapter>(true)).First();
            var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SessionController>(true)).First();
            driver.SendMessage("OnApplicationFocus", false);
            adapter.SendMessage("OnApplicationFocus", false);
            Assert.That(driver.IsTimeHeld, Is.True);
            Assert.That(adapter.AwaitingFocusContinue, Is.True);
            Assert.That(sessions.ContinueHeldSession(), Is.True, sessions.LastError);
            Assert.That(driver.IsTimeHeld, Is.False);
            Assert.That(adapter.AwaitingFocusContinue, Is.False);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator K01_LabSceneRecorderSamplesActualFrameDurations()
        {
#if UNITY_EDITOR
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/PharmaLabVR/Scenes/Lab.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            var scene = SceneManager.LoadScene("Lab", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return new WaitForSecondsRealtime(0.05f);
            var recorder = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(item => item.name == "PerformanceRecorder").GetComponent<PlayerPerformanceRecorder>();
            Assert.That(recorder, Is.Not.Null);
            Assert.That(recorder.SampleCount, Is.GreaterThan(0));
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
            Assert.That(driver.ResumeAfterTimeHold(), Is.True, driver.Session.ReadSnapshot());
            InputSystem.QueueStateEvent(injectedGamepad, new GamepadState { leftTrigger = 0, rightTrigger = 255 });
            InputSystem.Update();
            yield return null;
            Assert.That(leftAction.isTrackedAction.action.ReadValue<float>(), Is.LessThan(0.5f));
            Assert.That(rightAction.isTrackedAction.action.ReadValue<float>(), Is.GreaterThan(0.5f));
            Assert.That(grab.firstInteractorSelecting.transform.IsChildOf(leftAction.transform), Is.True);
            var lossSample = adapter.SampleInputs((ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000_000.0));
            Assert.That(lossSample[0].trackingValid, Is.False);
            Assert.That(driver.IsTimeHeld, Is.True);
            var holdSnapshot = driver.Session.ReadSnapshot();
            Assert.That(holdSnapshot.Contains("\"reason\":\"TrackingLoss\"") ||
                holdSnapshot.Contains("\"reason\":\"TimeDiscontinuity\""), Is.True, holdSnapshot);
            Assert.That(driver.ResumeAfterTimeHold(), Is.False,
                "A selected hand with lost tracking must prevent recovery even if a slow frame caused the first hold.");
            left.EndManualInteraction();
            InputSystem.RemoveDevice(injectedGamepad);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
