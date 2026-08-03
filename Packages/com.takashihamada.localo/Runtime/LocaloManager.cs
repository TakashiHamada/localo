using System;
using UnityEngine;

namespace Localo.Runtime
{
    /// <summary>
    /// Runtime language hub for Localo. Auto-created before the first scene loads and kept
    /// across scene changes (DontDestroyOnLoad) — no manual scene setup required.
    /// Owns the current language and the change notification; translation data itself lives
    /// in <see cref="LocaloDatabase"/>.
    /// Usage: <c>LocaloManager.Instance.SetLanguage(SystemLanguage.Japanese);</c>
    /// </summary>
    public sealed class LocaloManager : MonoBehaviour
    {
        private static Func<SystemLanguage> _systemLanguageProvider;

        /// <summary>
        /// Singleton instance. Non-null from BeforeSceneLoad until application quit
        /// (created before any scene object's Awake/OnEnable runs).
        /// </summary>
        public static LocaloManager Instance { get; private set; }

        /// <summary>
        /// Current display language. Startup behavior comes from <see cref="LocalizationSettings"/>,
        /// then it can be changed via <see cref="SetLanguage"/>.
        /// </summary>
        public SystemLanguage CurrentLanguage { get; private set; } = LocalizationLanguages.BaseLanguage;

        /// <summary>
        /// Fired after <see cref="CurrentLanguage"/> changes. Static event — subscribers must
        /// unsubscribe in OnDisable/OnDestroy. Localo also resets the event when entering Play Mode,
        /// including when Domain Reload is disabled.
        /// </summary>
        public static event Action<SystemLanguage> OnLanguageChanged;

        // Domain Reload disabled contract: all static mutable state must be reset here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
            OnLanguageChanged = null;
            _systemLanguageProvider = null;
        }

        // BeforeSceneLoad runs before any scene object's Awake/OnEnable, so LocalizeTextBase
        // components in the initial scene always see a live Instance.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(LocaloManager));
            DontDestroyOnLoad(go);
            go.AddComponent<LocaloManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CurrentLanguage = LocaloDatabase.GetStartupLanguage(GetSystemLanguage());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Registers the platform-specific system-language resolver used during startup.
        /// When no resolver is registered, Localo falls back to
        /// <see cref="Application.systemLanguage"/>.
        /// </summary>
        public static void SetSystemLanguageProvider(Func<SystemLanguage> provider)
        {
            _systemLanguageProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private static SystemLanguage GetSystemLanguage()
        {
            if (_systemLanguageProvider == null)
                return Application.systemLanguage;

            try
            {
                return _systemLanguageProvider();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Localo] System language provider failed. Falling back to Unity: {exception.Message}");
                return Application.systemLanguage;
            }
        }

        /// <summary>
        /// Switches the current language. No-op when the value is unchanged; fires
        /// <see cref="OnLanguageChanged"/> only on an actual change. A language outside
        /// enabledLanguages is accepted with a warning (texts fall back to English).
        /// </summary>
        public void SetLanguage(SystemLanguage language)
        {
            if (CurrentLanguage == language) return;

            if (!LocaloDatabase.IsLanguageEnabled(language))
            {
                Debug.LogWarning($"[Localo] Language '{language}' is not in enabledLanguages. " +
                                 "Texts will fall back to English where translations are missing.");
            }

            CurrentLanguage = language;
            OnLanguageChanged?.Invoke(language);
        }

        /// <summary>
        /// Returns the translation for <paramref name="key"/> in <see cref="CurrentLanguage"/>.
        /// Missing key or empty text returns the key itself so untranslated spots are visible in-game.
        /// </summary>
        public string GetLocalizedString(string key)
        {
            return LocaloDatabase.GetText(key, CurrentLanguage);
        }
    }
}
