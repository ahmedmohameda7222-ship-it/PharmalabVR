using NUnit.Framework;
using PharmaLabVR.UI;
using UnityEditor;
using UnityEngine;

namespace PharmaLabVR.Tests.EditMode
{
    // Authored for the deferred Unity Editor/Player campaign, not executed
    // on GitHub's .NET headless routing job.
    public sealed class LocalizationCatalogHostTests
    {
        [Test]
        public void P10_03_BootAndLabLanguageSwapReadsPinnedAssetsWithoutNativeMutation()
        {
            var en = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/PharmaLabVR/Content/Localization/en.json");
            var ar = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/PharmaLabVR/Content/Localization/ar.json");
            Assert.That(en, Is.Not.Null);
            Assert.That(ar, Is.Not.Null);
            var original = LanguageSelection.Current;
            var go = new GameObject("localized-ui-catalog-test");
            go.SetActive(false);
            try
            {
                var host = go.AddComponent<LocalizationCatalogHost>();
                host.Configure(en, ar);
                go.SetActive(true);
                LanguageSelection.Select("en");
                Assert.That(LocalizationCatalogHost.Get("offline", "fallback"), Is.EqualTo("Offline"));
                LanguageSelection.Select("ar");
                Assert.That(LocalizationCatalogHost.Get("offline", "fallback"), Is.EqualTo("غير متصل"));
                Assert.That(LanguageSelection.IsRightToLeft, Is.True);
                Assert.That(LocalizationCatalogHost.ActiveCatalog, Is.Not.Null);
                Assert.That(LocalizationCatalogHost.ActiveCatalog.Locale, Is.EqualTo("ar"),
                    "Pinned live catalog must refresh after the language change.");
                Assert.That(LocalizationCatalogHost.Get("source", "fallback"), Is.EqualTo("وعاء المصدر"));
                Assert.That(LocalizationCatalogHost.Get("missing-pinned-key", "fallback"),
                    Is.EqualTo("fallback"));
            }
            finally
            {
                Object.DestroyImmediate(go);
                LanguageSelection.Select(original);
            }
        }
    }
}
