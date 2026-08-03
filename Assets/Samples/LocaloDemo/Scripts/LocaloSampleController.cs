using Localo.Runtime;
using TMPro;
using UnityEngine;

namespace Localo.Sample
{
    public sealed class LocaloSampleController : MonoBehaviour
    {
        [SerializeField] private TMP_Text _currentLanguageText;

        private void OnEnable()
        {
            LocaloManager.OnLanguageChanged += HandleLanguageChanged;
            RefreshStatus();
        }

        private void OnDisable()
        {
            LocaloManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        public void SetEnglish()
        {
            LocaloManager.Instance.SetLanguage(SystemLanguage.English);
        }

        public void SetJapanese()
        {
            LocaloManager.Instance.SetLanguage(SystemLanguage.Japanese);
        }

        private void HandleLanguageChanged(SystemLanguage language)
        {
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_currentLanguageText == null || LocaloManager.Instance == null) return;

            string format = LocaloManager.Instance.GetLocalizedString("SAMPLE_CURRENT_LANGUAGE");
            _currentLanguageText.text = string.Format(format, LocaloManager.Instance.CurrentLanguage);
        }
    }
}
