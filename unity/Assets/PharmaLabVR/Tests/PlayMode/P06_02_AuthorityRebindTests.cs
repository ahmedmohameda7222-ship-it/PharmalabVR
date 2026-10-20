using NUnit.Framework;
using PharmaLabVR.Core;
using PharmaLabVR.UI;
using UnityEngine;

namespace PharmaLabVR.Tests.PlayMode
{
    // Source authored for the final Unity Editor campaign. Disabled driver
    // components deliberately avoid starting IPhreeqc: this checks real
    // MonoBehaviour binding/presentation only, not Native science or saves.
    public sealed class P06_02_AuthorityRebindTests
    {
        [Test]
        public void NativeDriverReplacementClearsOldSettingsAndSavedLabel()
        {
            var oldOwner = new GameObject("P06-old-Native-owner");
            var newOwner = new GameObject("P06-new-Native-owner");
            var panelOwner = new GameObject("P06-router");
            oldOwner.SetActive(false);
            newOwner.SetActive(false);
            panelOwner.SetActive(false);
            try
            {
                var oldDriver = oldOwner.AddComponent<CoreDriver>();
                var newDriver = newOwner.AddComponent<CoreDriver>();
                var router = panelOwner.AddComponent<ScreenRouter>();
                panelOwner.SetActive(true);

                router.Configure(oldDriver);
                router.Show(ScreenId.Settings);
                router.MarkSaved(); // Presentation label fixture, not real Save.
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(router.Current.SaveState, Is.EqualTo("Saved"));

                router.Configure(newDriver);
                Assert.That(router.Current.ScreenId, Is.EqualTo(ScreenId.Lab));
                Assert.That(router.Current.SaveState, Is.EqualTo("Unsaved"));
                Assert.That(router.Current.Dirty, Is.True);
                Assert.That(router.Current.SelectedEntityId, Is.EqualTo("source"));
                Assert.That(router.Current.SupportedActions, Does.Contain("Pause"));
                Assert.That(router.ReturnFromSettings(), Is.False,
                    "A replaced Native session must not inherit old Back history.");
            }
            finally
            {
                Object.DestroyImmediate(panelOwner);
                Object.DestroyImmediate(newOwner);
                Object.DestroyImmediate(oldOwner);
            }
        }
    }
}
