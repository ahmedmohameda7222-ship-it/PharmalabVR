using System.Collections;
using NUnit.Framework;
using PharmaLabVR.Core;
using UnityEngine;
using UnityEngine.TestTools;

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
    }
}
