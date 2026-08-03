using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Localo.Runtime
{
    /// <summary>
    /// Base class for components that bind a text field to a Localo translation key.
    /// Subscribes to <see cref="LocaloManager.OnLanguageChanged"/> while enabled and pushes the
    /// current translation into <see cref="UpdateText"/>. In Edit Mode, Inspector edits preview
    /// immediately via OnValidate (using <see cref="LocaloDatabase"/> directly — no manager needed).
    /// </summary>
    public abstract class LocalizeTextBase : MonoBehaviour
    {
        [Header("-- Localization --")]
        [Tooltip("Translation key looked up via LocaloManager (e.g. \"Key_StartButton\").")]
        [FormerlySerializedAs("_key")]
        [SerializeField] protected string localizationKey = string.Empty;

#if UNITY_EDITOR
        [Header("-- Edit Mode Preview --")]
        [Tooltip("ON: preview this label in Test Language below. Edit Mode preview only — ignored in Play Mode.")]
        [SerializeField] private bool _useTestLanguage;

        [Tooltip("Language used for the Edit Mode preview while Use Test Language is ON.")]
        [SerializeField] private SystemLanguage _testLanguage = LocalizationLanguages.BaseLanguage;

        private static bool _editModeTableCacheDirty = true;
        private static bool _projectChangedHookRegistered;
#endif

        /// <summary>
        /// Assigns the resolved text to the concrete text component.
        /// </summary>
        protected abstract void UpdateText(string text);

        protected virtual void OnEnable()
        {
            LocaloManager.OnLanguageChanged += HandleLanguageChanged;
            Refresh();
        }

        protected virtual void OnDisable()
        {
            LocaloManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        /// <summary>
        /// Changes the key at runtime and refreshes the text immediately.
        /// </summary>
        public void SetKey(string key)
        {
            localizationKey = key;
            Refresh();
        }

        private void HandleLanguageChanged(SystemLanguage language)
        {
            Refresh();
        }

        /// <summary>
        /// Resolves the current translation and pushes it into <see cref="UpdateText"/>.
        /// Play Mode reads through LocaloManager; Edit Mode previews via LocaloDatabase
        /// (tables reloaded so fresh Localo edits show up).
        /// </summary>
        private void Refresh()
        {
            if (string.IsNullOrEmpty(localizationKey)) return;

            if (Application.isPlaying)
            {
                if (LocaloManager.Instance == null) return;
                UpdateText(LocaloManager.Instance.GetLocalizedString(localizationKey));
                return;
            }

#if UNITY_EDITOR
            EnsureProjectChangedHook();
            if (_editModeTableCacheDirty)
            {
                LocaloDatabase.ReloadTables();
                _editModeTableCacheDirty = false;
            }
            SystemLanguage previewLanguage =
                _useTestLanguage ? _testLanguage : LocalizationLanguages.BaseLanguage;
            UpdateText(LocaloDatabase.GetText(localizationKey, previewLanguage));
#endif
        }

#if UNITY_EDITOR
        // Guards against queuing a new delayCall closure on every OnValidate call (e.g. while a slider
        // or text field is being dragged/typed in the Inspector) — without it, many queued closures
        // pile up and each redundantly calls ReloadTables()/Refresh() once delayCall fires.
        private bool _editModeApplyScheduled;

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                if (isActiveAndEnabled)
                    Refresh();
                return;
            }

            if (_editModeApplyScheduled) return;
            _editModeApplyScheduled = true;

            // Edit Mode: defer — setting TMP text inside OnValidate triggers SendMessage warnings.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _editModeApplyScheduled = false;
                if (this == null || !isActiveAndEnabled || Application.isPlaying) return;
                Refresh();
            };
        }

        [InitializeOnLoadMethod]
        private static void RegisterProjectChangedHook()
        {
            if (_projectChangedHookRegistered) return;
            EditorApplication.projectChanged += HandleProjectChanged;
            _projectChangedHookRegistered = true;
        }

        private static void EnsureProjectChangedHook()
        {
            if (_projectChangedHookRegistered) return;
            RegisterProjectChangedHook();
        }

        private static void HandleProjectChanged()
        {
            _editModeTableCacheDirty = true;
        }
#endif
    }
}
