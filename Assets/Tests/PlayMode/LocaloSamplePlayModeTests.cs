using System.Collections;
using Localo.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Localo.Sample.Tests
{
    public sealed class LocaloSamplePlayModeTests
    {
        [UnityTest]
        public IEnumerator LanguageSwitch_RefreshesUiAndWorldBindings()
        {
            yield return null;
            Assert.That(LocaloManager.Instance, Is.Not.Null);

            var uiObject = new GameObject("Test UI Text", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI), typeof(LocalizeTextMeshProUGUI));
            var worldObject = new GameObject("Test World Text", typeof(TextMeshPro), typeof(LocalizeTextMeshPro));

            LocalizeTextMeshProUGUI uiBinding = uiObject.GetComponent<LocalizeTextMeshProUGUI>();
            LocalizeTextMeshPro worldBinding = worldObject.GetComponent<LocalizeTextMeshPro>();
            uiBinding.SetKey("SAMPLE_GREETING");
            worldBinding.SetKey("SAMPLE_3D_TEXT");

            LocaloManager.Instance.SetLanguage(SystemLanguage.English);
            Assert.That(uiObject.GetComponent<TextMeshProUGUI>().text, Is.EqualTo("Hello from Localo!"));
            Assert.That(worldObject.GetComponent<TextMeshPro>().text, Is.EqualTo("This is 3D TextMesh Pro"));

            LocaloManager.Instance.SetLanguage(SystemLanguage.Japanese);
            Assert.That(uiObject.GetComponent<TextMeshProUGUI>().text, Is.EqualTo("Localoからこんにちは！"));
            Assert.That(worldObject.GetComponent<TextMeshPro>().text, Is.EqualTo("これは3D TextMesh Proです"));
            Assert.That(LocaloManager.Instance.GetLocalizedString("SAMPLE_FALLBACK"),
                Is.EqualTo("This line falls back to English."));

            LocaloManager.Instance.SetLanguage(SystemLanguage.English);
            Object.Destroy(uiObject);
            Object.Destroy(worldObject);
            yield return null;
        }
    }
}
