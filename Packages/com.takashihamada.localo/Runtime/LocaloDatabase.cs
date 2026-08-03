using System.Collections.Generic;
using UnityEngine;

namespace Localo.Runtime
{
    /// <summary>
    /// Static translation data layer for Localo. Loads <see cref="LocalizationSettings"/> from
    /// Resources on first access and caches a key → row lookup across all registered tables.
    /// Works in Edit Mode too (used by Inspector previews and Localo's editor tooling).
    /// Runtime language state (current language / change event) lives in <see cref="LocaloManager"/>.
    /// </summary>
    public static class LocaloDatabase
    {
        /// <summary>
        /// Path passed to Resources.Load. The settings asset must live in a Resources folder
        /// (Localo's Quick Setup places it at Assets/LocaloAssets/Resources/LocalizationSettings.asset).
        /// </summary>
        public const string SettingsResourcesPath = "LocalizationSettings";

        private static LocalizationSettings _settings;
        private static Dictionary<string, CachedEntry> _rowCache;
        private static bool _initializeAttempted;

        private readonly struct CachedEntry
        {
            public readonly LocalizationTable Table;
            public readonly LocalizationRow Row;

            public CachedEntry(LocalizationTable table, LocalizationRow row)
            {
                Table = table;
                Row = row;
            }
        }

        // Domain Reload disabled contract: all static mutable state must be reset here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _settings = null;
            _rowCache = null;
            _initializeAttempted = false;
        }

        /// <summary>
        /// Returns the translation for <paramref name="key"/> in the given language.
        /// Falls back to English when the requested language has no (or empty) text.
        /// If English is also empty, returns an empty string (no text) rather than the key,
        /// so an intentionally blank translation renders as nothing.
        /// </summary>
        public static string GetText(string key, SystemLanguage language)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (!EnsureInitialized()) return key;

            if (!_rowCache.TryGetValue(key, out CachedEntry entry))
            {
                Debug.LogWarning($"[Localo] Translation key not found: \"{key}\"");
                return key;
            }

            string text = entry.Table.GetText(entry.Row, language);
            if (string.IsNullOrEmpty(text) && language != LocalizationLanguages.BaseLanguage)
                text = entry.Table.GetText(entry.Row, LocalizationLanguages.BaseLanguage);

            return text;
        }

        /// <summary>
        /// True when the given key exists in any registered table. Does not log.
        /// </summary>
        public static bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return EnsureInitialized() && _rowCache.ContainsKey(key);
        }

        /// <summary>
        /// True when the given language is in the settings' enabledLanguages. Returns false
        /// when the settings asset can't be loaded.
        /// </summary>
        public static bool IsLanguageEnabled(SystemLanguage language)
        {
            return EnsureInitialized() &&
                   _settings.enabledLanguages != null &&
                   _settings.enabledLanguages.Contains(language);
        }

        /// <summary>
        /// Resolves the language Localo should use before the first scene is initialized.
        /// A disabled fixed language falls back to English and logs a warning.
        /// </summary>
        public static SystemLanguage GetStartupLanguage(SystemLanguage systemLanguage)
        {
            if (!EnsureInitialized()) return LocalizationLanguages.BaseLanguage;

            SystemLanguage requestedLanguage = _settings.startupLanguageMode == StartupLanguageMode.FixedLanguage
                ? _settings.startupLanguage
                : systemLanguage;

            if (IsLanguageEnabled(requestedLanguage)) return requestedLanguage;

            if (_settings.startupLanguageMode == StartupLanguageMode.FixedLanguage)
            {
                Debug.LogWarning($"[Localo] Startup language '{requestedLanguage}' is not in enabledLanguages. " +
                                 "Falling back to English.");
            }

            return LocalizationLanguages.BaseLanguage;
        }

        /// <summary>
        /// Discards the cached settings/rows so the next access reloads them.
        /// Mainly for editor tooling after translation data is edited.
        /// </summary>
        public static void ReloadTables()
        {
            _settings = null;
            _rowCache = null;
            _initializeAttempted = false;
        }

        /// <summary>
        /// Loads the settings asset and builds the key → row cache. Returns false when the
        /// settings asset can't be found (warned once per load attempt cycle).
        /// </summary>
        private static bool EnsureInitialized()
        {
            if (_rowCache != null) return true;
            if (_initializeAttempted && _settings == null) return false;
            if (!_initializeAttempted)
            {
                _initializeAttempted = true;
                _settings = Resources.Load<LocalizationSettings>(SettingsResourcesPath);
                if (_settings == null)
                {
                    Debug.LogError($"[Localo] LocalizationSettings not found at Resources/{SettingsResourcesPath}. " +
                                   "Run Localo's Quick Setup (Setup tab) to create it in a Resources folder.");
                    return false;
                }
            }

            BuildRowCache();
            return true;
        }

        private static void BuildRowCache()
        {
            _rowCache = new Dictionary<string, CachedEntry>();
            if (_settings.registeredTables == null) return;

            foreach (TableInfo info in _settings.registeredTables)
            {
                if (info.tableAsset == null || info.tableAsset.rows == null) continue;
                foreach (LocalizationRow row in info.tableAsset.rows)
                {
                    if (row == null || string.IsNullOrEmpty(row.key)) continue;
                    if (_rowCache.TryGetValue(row.key, out CachedEntry existing))
                    {
                        Debug.LogWarning($"[Localo] Duplicate key \"{row.key}\" in table '{info.Key}' " +
                                         $"(already defined in '{existing.Table.name}'). First one wins.");
                        continue;
                    }
                    _rowCache.Add(row.key, new CachedEntry(info.tableAsset, row));
                }
            }
        }

#if UNITY_INCLUDE_TESTS
        internal static void InitializeForTests(LocalizationSettings settings)
        {
            _settings = settings;
            _rowCache = null;
            _initializeAttempted = true;
            if (_settings != null) BuildRowCache();
        }

        internal static void ResetForTests()
        {
            ResetStaticState();
        }
#endif
    }
}
