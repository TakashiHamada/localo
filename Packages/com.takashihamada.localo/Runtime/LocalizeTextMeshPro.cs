using TMPro;
using UnityEngine;

namespace Localo.Runtime
{
    /// <summary>
    /// Binds a 3D (non-uGUI) TextMeshPro to a Localo translation key.
    /// See <see cref="LocalizeTextBase"/> for the update/preview behavior;
    /// use <see cref="LocalizeTextMeshProUGUI"/> for uGUI texts.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class LocalizeTextMeshPro : LocalizeTextBase
    {
        private TextMeshPro _text;

        protected override void UpdateText(string text)
        {
            if (_text == null) _text = GetComponent<TextMeshPro>();
            if (_text == null) return;
            _text.text = text;
        }
    }
}
