using TMPro;
using UnityEngine;

namespace Localo.Runtime
{
    /// <summary>
    /// Binds a uGUI TextMeshProUGUI to a Localo translation key.
    /// See <see cref="LocalizeTextBase"/> for the update/preview behavior;
    /// use <see cref="LocalizeTextMeshPro"/> for 3D (non-uGUI) TextMeshPro objects.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class LocalizeTextMeshProUGUI : LocalizeTextBase
    {
        private TextMeshProUGUI _text;

        protected override void UpdateText(string text)
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            if (_text == null) return;
            _text.text = text;
        }
    }
}
