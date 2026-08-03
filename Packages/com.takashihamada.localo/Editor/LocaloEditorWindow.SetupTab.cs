// =============================================================================
// LocaloEditorWindow.SetupTab.cs — Screen A: Setup screen
// =============================================================================
// Handles assigning/editing LocalizationSettings, the enabled-language checkboxes,
// and the enabled-language column order (ReorderableList).
//
// English is always fixed at the head of enabledLanguages (can't be removed or
// reordered), so the ReorderableList only targets a "reorder buffer excluding
// English" (_languageOrderBuffer). On drag-commit, enabledLanguages is rebuilt as
// [English] + buffer. LocalizationSettings.OnValidate is only a backstop for this
// constraint and never interferes with the buffer while it's being dragged.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Localo.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        // Project-specific data lives in Assets rather than inside the installed package,
        // so package updates never overwrite a project's translation data.
        private const string DataRootFolder = "Assets/LocaloAssets";
        private const string TablesFolderName = "Tables";
        // The settings asset must sit in a Resources folder so LocaloDatabase can
        // Resources.Load it at runtime. Tables are reached through the settings asset's
        // direct references, so they stay outside Resources.
        private const string ResourcesFolderName = "Resources";
        private const string SettingsAssetName = LocaloDatabase.SettingsResourcesPath;
        private const string SettingsAssetPath =
            DataRootFolder + "/" + ResourcesFolderName + "/" + SettingsAssetName + ".asset";
        private const string MainTableAssetName = "MainTable";

        // Localo ships its AI skill as Editor-only text templates. The installer deliberately
        // writes below the project root (not Assets/) so AI tools discover it as a project skill
        // without including it in the game build.
        private const string ProjectSkillFolderRelativePath = ".agents/skills/localo";
        private const string ProjectSkillTemplateRelativePath =
            "Editor/Resources/LocaloProjectSkillTemplate.txt";
        private const string ProjectSkillMetadataTemplateRelativePath =
            "Editor/Resources/LocaloProjectSkillOpenAiTemplate.txt";

        private ReorderableList _languageOrderList;
        private List<SystemLanguage> _languageOrderBuffer = new List<SystemLanguage>();
        private Vector2 _setupScrollPosition;

        private void DrawSetupTab()
        {
            // The Setup tab's content (language toggles + reorder list + registered tables list)
            // easily exceeds the window height, so it's wrapped in its own scroll view. ScrollViewScope
            // (rather than manual Begin/EndScrollView) guarantees EndScrollView still runs on the early
            // "no settings assigned yet" return path below.
            using (var scroll = new EditorGUILayout.ScrollViewScope(_setupScrollPosition))
            {
                _setupScrollPosition = scroll.scrollPosition;

                EditorGUILayout.LabelField("Localization Settings", EditorStyles.boldLabel);

                var picked = (LocalizationSettings)EditorGUILayout.ObjectField(
                    "Settings Asset", _settings, typeof(LocalizationSettings), false);
                if (picked != _settings)
                {
                    if (picked != null && !IsRuntimeSettingsAsset(picked))
                    {
                        EditorUtility.DisplayDialog(
                            "Invalid Localo Settings Path",
                            $"Localo loads its runtime settings from:\n{SettingsAssetPath}\n\n" +
                            "Move or create the asset at that path, then assign it again.",
                            "OK");
                    }
                    else
                    {
                        _settings = picked;
                        SavePersistedSettings();
                        SelectFirstRegisteredTableWhenCurrentIsInvalid();
                        SavePersistedTable();
                        InitLanguageOrderList();
                    }
                }

                DrawProjectSkillInstaller();

                if (_settings == null)
                {
                    EditorGUILayout.HelpBox(
                        "No LocalizationSettings asset assigned. Click Quick Setup to generate everything " +
                        "you need automatically, or assign an existing asset above.",
                        MessageType.Info);
                    if (GUILayout.Button("Quick Setup", GUILayout.Height(28f)))
                        RunQuickSetup();
                    return;
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Base Language --", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("Language 0",
                            LocalizationLanguages.GetDisplayName(LocalizationLanguages.BaseLanguage));
                    }
                    EditorGUILayout.HelpBox("English is always fixed at the head. It cannot be removed or reordered.", MessageType.None);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Enable Languages --", EditorStyles.boldLabel);
                    DrawEnableLanguagesMaskField();
                }

                EditorGUILayout.Space(8f);
                bool hasHiddenLanguageColumns = HasHiddenLanguageColumns();
                using (var scope = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (hasHiddenLanguageColumns && Event.current.type == EventType.Repaint)
                        EditorGUI.DrawRect(scope.rect, WarningSectionBackground);

                    EditorGUILayout.LabelField("-- Visible Languages --", EditorStyles.boldLabel);
                    DrawVisibleLanguagesMaskField();
                    if (hasHiddenLanguageColumns)
                    {
                        EditorGUILayout.HelpBox(
                            "Some language columns are hidden in the Table tab. Hidden data is preserved.",
                            MessageType.Warning);
                    }
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Column Order --", EditorStyles.boldLabel);
                    if (_languageOrderList == null) InitLanguageOrderList();
                    _languageOrderList.DoLayoutList();
                    EditorGUILayout.HelpBox(
                        "Drag to reorder. This order is reflected in the Table tab's column order (English is not included here).",
                        MessageType.None);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Column Widths --", EditorStyles.boldLabel);
                    DrawColumnWidthFields();
                    EditorGUILayout.HelpBox(
                        "Pixel width of each column in the Table tab's grid.",
                        MessageType.None);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Registered Tables --", EditorStyles.boldLabel);
                    DrawRegisteredTablesList();
                }
            }
        }

        /// <summary>
        /// Installs Localo's AI operating guide as a project-local skill. It is intentionally
        /// available before Quick Setup because AI assistance is useful while creating the first
        /// localization assets too. Existing skill files are replaced only after confirmation so
        /// project-specific edits are never silently lost.
        /// </summary>
        private void DrawProjectSkillInstaller()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("-- AI Project Skill --", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Install Localo's AI guide into .agents/skills/localo for this Unity project only.",
                    EditorStyles.wordWrappedLabel);

                bool isInstalled = File.Exists(GetProjectSkillFilePath());
                EditorGUILayout.HelpBox(
                    isInstalled
                        ? "Installed. Updating replaces the Localo skill files after confirmation."
                        : "Not installed. This does not modify global AI skills.",
                    isInstalled ? MessageType.Info : MessageType.None);

                string buttonLabel = isInstalled ? "Update Project AI Skill" : "Install Project AI Skill";
                if (GUILayout.Button(buttonLabel, GUILayout.Height(24f)))
                    InstallProjectSkill(isInstalled);
            }
        }

        private static string GetProjectSkillFilePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, ProjectSkillFolderRelativePath, "SKILL.md");
        }

        private static bool IsRuntimeSettingsAsset(LocalizationSettings settings)
        {
            return settings != null &&
                   AssetDatabase.GetAssetPath(settings) == SettingsAssetPath;
        }

        /// <summary>
        /// Keeps the Table tab scoped to the selected Settings asset. A table from the previously
        /// selected Settings must not remain editable as an apparently registered runtime table.
        /// </summary>
        private void SelectFirstRegisteredTableWhenCurrentIsInvalid()
        {
            if (_settings?.registeredTables == null)
            {
                _table = null;
                return;
            }

            for (int i = 0; i < _settings.registeredTables.Count; i++)
            {
                if (_settings.registeredTables[i].tableAsset != _table) continue;
                return;
            }

            _table = null;
            for (int i = 0; i < _settings.registeredTables.Count; i++)
            {
                LocalizationTable registeredTable = _settings.registeredTables[i].tableAsset;
                if (registeredTable == null) continue;
                _table = registeredTable;
                return;
            }
        }

        private void InstallProjectSkill(bool isInstalled)
        {
            string packageAssetPath = GetPackageAssetPath();
            var skillTemplate = LoadPackageTextAsset(packageAssetPath, ProjectSkillTemplateRelativePath);
            var metadataTemplate = LoadPackageTextAsset(packageAssetPath, ProjectSkillMetadataTemplateRelativePath);
            if (skillTemplate == null || metadataTemplate == null)
            {
                EditorUtility.DisplayDialog(
                    "Localo Project AI Skill",
                    "The bundled Localo skill templates could not be found. Reinstall the Localo package and try again.",
                    "OK");
                return;
            }

            if (isInstalled && !EditorUtility.DisplayDialog(
                    "Update Localo Project AI Skill",
                    "Replace .agents/skills/localo/SKILL.md and agents/openai.yaml?\n\n" +
                    "Any project-specific edits to those files will be overwritten.",
                    "Replace", "Cancel"))
            {
                return;
            }

            string skillFilePath = GetProjectSkillFilePath();
            string skillDirectory = Path.GetDirectoryName(skillFilePath);
            string metadataDirectory = Path.Combine(skillDirectory, "agents");

            try
            {
                Directory.CreateDirectory(metadataDirectory);
                var utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(skillFilePath, skillTemplate.text, utf8WithoutBom);
                File.WriteAllText(Path.Combine(metadataDirectory, "openai.yaml"), metadataTemplate.text, utf8WithoutBom);

                EditorUtility.DisplayDialog(
                    "Localo Project AI Skill",
                    "Installed Localo's AI skill for this project:\n.agents/skills/localo\n\n" +
                    "Open a new AI task to use the updated project skill.",
                    "OK");
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Localo] Failed to install project AI skill: {exception}");
                EditorUtility.DisplayDialog(
                    "Localo Project AI Skill",
                    "Localo could not write the project AI skill. Check the project folder permissions.",
                    "OK");
            }
        }

        private static string GetPackageAssetPath()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(LocaloEditorWindow).Assembly);
            return packageInfo?.assetPath;
        }

        private static TextAsset LoadPackageTextAsset(string packageAssetPath, string relativePath)
        {
            if (string.IsNullOrEmpty(packageAssetPath)) return null;
            return AssetDatabase.LoadAssetAtPath<TextAsset>($"{packageAssetPath}/{relativePath}");
        }

        /// <summary>
        /// One-click setup: creates a LocalizationSettings asset (plus a starter LocalizationTable,
        /// registered into it) under <see cref="DataRootFolder"/> with sensible default names, and
        /// assigns them to this window. Idempotent — if the settings asset already exists at the
        /// default path, it's simply loaded and assigned rather than recreated.
        /// </summary>
        private void RunQuickSetup()
        {
            var existingSettings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsAssetPath);
            if (existingSettings != null)
            {
                _settings = existingSettings;
                SavePersistedSettings();
                // Also re-assign a table so the Table tab is immediately usable, not just Settings
                // (mirrors the "ready to edit" promise made by the fresh-creation path below).
                SelectFirstRegisteredTableWhenCurrentIsInvalid();
                SavePersistedTable();
                InitLanguageOrderList();
                RecomputeFilteredIndices();
                Repaint();
                EditorUtility.DisplayDialog(
                    "Localo Quick Setup",
                    $"Found an existing settings asset and assigned it:\n{SettingsAssetPath}",
                    "OK");
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(SettingsAssetPath) != null)
            {
                EditorUtility.DisplayDialog(
                    "Localo Quick Setup",
                    $"Another asset already exists at the required settings path:\n{SettingsAssetPath}\n\n" +
                    "Move that asset before running Quick Setup. Localo did not overwrite it.",
                    "OK");
                return;
            }

            EnsureFolder("Assets", "LocaloAssets");
            EnsureFolder(DataRootFolder, ResourcesFolderName);
            EnsureFolder(DataRootFolder, TablesFolderName);

            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            Undo.RegisterCreatedObjectUndo(settings, "Quick Setup Localization Settings");

            string tablePath = $"{DataRootFolder}/{TablesFolderName}/{MainTableAssetName}.asset";
            var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(tablePath);
            bool createdTable = false;
            if (table == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(tablePath) != null)
                    tablePath = AssetDatabase.GenerateUniqueAssetPath(tablePath);

                table = ScriptableObject.CreateInstance<LocalizationTable>();
                AssetDatabase.CreateAsset(table, tablePath);
                Undo.RegisterCreatedObjectUndo(table, "Quick Setup Localization Table");
                createdTable = true;
            }

            settings.registeredTables.Add(new TableInfo { tableAsset = table });

            EditorUtility.SetDirty(settings);
            if (createdTable) EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            _settings = settings;
            _table = table;
            SavePersistedSettings();
            SavePersistedTable();
            InitLanguageOrderList();
            RecomputeFilteredIndices();
            Repaint();

            EditorUtility.DisplayDialog(
                "Localo Quick Setup",
                $"Created:\n{SettingsAssetPath}\n{tablePath}\n\nBoth are assigned and ready to edit on the Table tab.",
                "OK");
        }

        /// <summary>
        /// Ensures a subfolder exists under <paramref name="parentPath"/>, creating it if missing.
        /// Same pattern as BulkStageDataGenerator.RunInternal's output-folder check.
        /// </summary>
        private static void EnsureFolder(string parentPath, string folderName)
        {
            string fullPath = $"{parentPath}/{folderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parentPath, folderName);
        }

        /// <summary>
        /// Generates a "TableN" filename that collides with neither the currently registered
        /// table keys nor any existing file in the Tables folder, starting from N = 2
        /// (the first table is MainTable, created by Quick Setup).
        /// </summary>
        private string GenerateUniqueTableName()
        {
            var usedKeys = new HashSet<string>(
                (_settings.registeredTables ?? new List<TableInfo>()).Select(t => t.Key));

            for (int n = 2; ; n++)
            {
                string candidate = $"Table{n}";
                if (usedKeys.Contains(candidate)) continue;
                string candidatePath = $"{DataRootFolder}/{TablesFolderName}/{candidate}.asset";
                if (File.Exists(candidatePath)) continue;
                return candidate;
            }
        }

        private void InitLanguageOrderList()
        {
            _languageOrderBuffer = _settings != null && _settings.enabledLanguages != null
                ? _settings.enabledLanguages.Skip(1).ToList() // Reorder target excluding English (head)
                : new List<SystemLanguage>();

            _languageOrderList = new ReorderableList(_languageOrderBuffer, typeof(SystemLanguage), true, true, false, false)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Enabled language order (excluding English)"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.y += 2f;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.LabelField(rect, LocalizationLanguages.GetDisplayName(_languageOrderBuffer[index]));
                },
                onReorderCallback = _ => CommitLanguageOrder()
            };
        }

        private void CommitLanguageOrder()
        {
            if (_settings == null) return;

            Undo.RecordObject(_settings, "Reorder Localization Languages");
            var newList = new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };
            newList.AddRange(_languageOrderBuffer);
            _settings.enabledLanguages = newList;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Multi-select dropdown for SelectableLanguages, replacing a per-language checkbox list that
        /// scaled poorly with the growing language count. Bit i of the mask corresponds to
        /// LocalizationLanguages.SelectableLanguages[i] (a fixed-order array, so bit positions are stable
        /// across draws). Only languages whose bit actually flipped go through ToggleLanguage, to keep its
        /// existing Undo / registered-table backfill behavior per language.
        /// </summary>
        private void DrawEnableLanguagesMaskField()
        {
            var selectable = LocalizationLanguages.SelectableLanguages;
            string[] optionLabels = selectable.Select(LocalizationLanguages.GetDisplayName).ToArray();

            int currentMask = 0;
            for (int i = 0; i < selectable.Length; i++)
            {
                if (_settings.enabledLanguages.Contains(selectable[i]))
                    currentMask |= 1 << i;
            }

            int newMask = EditorGUILayout.MaskField("Languages", currentMask, optionLabels);
            if (newMask == currentMask) return;

            for (int i = 0; i < selectable.Length; i++)
            {
                bool wasEnabled = (currentMask & (1 << i)) != 0;
                bool nowEnabled = (newMask & (1 << i)) != 0;
                if (nowEnabled != wasEnabled)
                    ToggleLanguage(selectable[i], nowEnabled);
            }
        }

        private void ToggleLanguage(SystemLanguage lang, bool enable)
        {
            Undo.RecordObject(_settings, enable ? "Enable Localization Language" : "Disable Localization Language");
            EnsureVisibleLanguagesInitialized();
            if (enable)
            {
                if (!_settings.enabledLanguages.Contains(lang))
                    _settings.enabledLanguages.Add(lang);
                if (!_settings.visibleLanguages.Contains(lang))
                    _settings.visibleLanguages.Add(lang);
            }
            else
            {
                _settings.enabledLanguages.Remove(lang);
                _settings.visibleLanguages.Remove(lang);
            }
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            InitLanguageOrderList();

            // When a language is newly enabled, backfill an empty slot for it across every registered table
            // (the column appears the moment the Table tab is opened either way, but this keeps the underlying
            // data consistent instead of relying solely on LocalizationTable.GetText's "no slot = empty" fallback).
            if (enable)
                BackfillLanguageColumnAcrossRegisteredTables(lang);
        }

        private void DrawVisibleLanguagesMaskField()
        {
            EnsureVisibleLanguagesInitialized();

            var enabled = _settings.enabledLanguages;
            string[] optionLabels = enabled.Select(LocalizationLanguages.GetDisplayName).ToArray();

            int currentMask = 0;
            for (int i = 0; i < enabled.Count; i++)
            {
                if (_settings.visibleLanguages.Contains(enabled[i]))
                    currentMask |= 1 << i;
            }

            int newMask = EditorGUILayout.MaskField("Languages", currentMask, optionLabels);
            if (newMask == currentMask) return;

            Undo.RecordObject(_settings, "Change Visible Localization Languages");
            _settings.visibleLanguages.Clear();
            for (int i = 0; i < enabled.Count; i++)
            {
                if ((newMask & (1 << i)) != 0)
                    _settings.visibleLanguages.Add(enabled[i]);
            }
            _settings.visibleLanguagesInitialized = true;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void EnsureVisibleLanguagesInitialized()
        {
            if (_settings == null) return;

            bool changed = false;
            if (_settings.enabledLanguages == null)
            {
                _settings.enabledLanguages = new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };
                changed = true;
            }

            if (_settings.visibleLanguages == null)
            {
                _settings.visibleLanguages = new List<SystemLanguage>();
                changed = true;
            }

            if (!_settings.visibleLanguagesInitialized)
            {
                _settings.visibleLanguages.Clear();
                _settings.visibleLanguages.AddRange(_settings.enabledLanguages);
                _settings.visibleLanguagesInitialized = true;
                changed = true;
            }

            for (int i = _settings.visibleLanguages.Count - 1; i >= 0; i--)
            {
                if (_settings.visibleLanguages.IndexOf(_settings.visibleLanguages[i]) != i ||
                    !_settings.enabledLanguages.Contains(_settings.visibleLanguages[i]))
                {
                    _settings.visibleLanguages.RemoveAt(i);
                    changed = true;
                }
            }

            if (!changed) return;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
        }

        private void BackfillLanguageColumnAcrossRegisteredTables(SystemLanguage lang)
        {
            if (_settings.registeredTables == null) return;

            bool anyChanged = false;
            foreach (var info in _settings.registeredTables)
            {
                if (info.tableAsset == null) continue;
                Undo.RecordObject(info.tableAsset, "Add Localization Language Column");
                info.tableAsset.EnsureLanguageColumns(new[] { lang });
                EditorUtility.SetDirty(info.tableAsset);
                anyChanged = true;
            }
            if (anyChanged) AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Three human-tunable column widths (Key / language / Comment) consumed by the Table tab's
        /// grid via GetKeyColumnWidth / GetLanguageColumnWidth / GetCommentColumnWidth. Clamped to
        /// MinColumnWidth so a column can never be dragged/typed down to zero or negative.
        /// </summary>
        private void DrawColumnWidthFields()
        {
            EditorGUI.BeginChangeCheck();
            int keyWidth = EditorGUILayout.IntField("Key Column", _settings.keyColumnWidth);
            int languageWidth = EditorGUILayout.IntField("Language Column", _settings.languageColumnWidth);
            int commentWidth = EditorGUILayout.IntField("Comment Column", _settings.commentColumnWidth);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(_settings, "Change Localization Column Widths");
            _settings.keyColumnWidth = Mathf.Max((int)MinColumnWidth, keyWidth);
            _settings.languageColumnWidth = Mathf.Max((int)MinColumnWidth, languageWidth);
            _settings.commentColumnWidth = Mathf.Max((int)MinColumnWidth, commentWidth);
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
        }

        private void DrawRegisteredTablesList()
        {
            if (_settings.registeredTables == null)
                _settings.registeredTables = new List<TableInfo>();

            EditorGUILayout.HelpBox(
                "Each key is the table asset filename and updates automatically when the asset is renamed.",
                MessageType.None);

            bool tableInfoChanged = false;
            bool undoRecorded = false;
            int removeIndex = -1;
            for (int i = 0; i < _settings.registeredTables.Count; i++)
            {
                var info = _settings.registeredTables[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.IsNullOrEmpty(info.Key) ? "(no table)" : info.Key,
                        GUILayout.Width(140f));
                    var tableAsset = (LocalizationTable)EditorGUILayout.ObjectField(
                        info.tableAsset, typeof(LocalizationTable), false);
                    if (tableAsset != info.tableAsset)
                    {
                        if (!undoRecorded)
                        {
                            Undo.RecordObject(_settings, "Edit Registered Table");
                            undoRecorded = true;
                        }

                        info.tableAsset = tableAsset;
                        _settings.registeredTables[i] = info;
                        tableInfoChanged = true;
                    }
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                        removeIndex = i;
                }
            }
            if (tableInfoChanged)
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
                MarkDuplicateKeysDirty();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_settings, "Remove Registered Table");
                _settings.registeredTables.RemoveAt(removeIndex);
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
                MarkDuplicateKeysDirty();
            }

            if (GUILayout.Button("+ Add Table", GUILayout.Height(22f)))
                CreateAndRegisterNewTable();
        }

        /// <summary>
        /// Creates a new LocalizationTable asset under Assets/LocaloAssets/Tables/ with an
        /// auto-generated unique name and registers it, so "+ Add Table" is a single click
        /// rather than requiring a manually-created asset to be dragged in afterward.
        /// </summary>
        private void CreateAndRegisterNewTable()
        {
            // Settings may have been assigned manually (bypassing Quick Setup), so the Tables
            // folder isn't guaranteed to exist yet.
            EnsureFolder("Assets", "LocaloAssets");
            EnsureFolder(DataRootFolder, TablesFolderName);

            string name = GenerateUniqueTableName();
            string path = $"{DataRootFolder}/{TablesFolderName}/{name}.asset";

            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            AssetDatabase.CreateAsset(table, path);
            Undo.RegisterCreatedObjectUndo(table, "Add Registered Table");

            Undo.RecordObject(_settings, "Add Registered Table");
            _settings.registeredTables.Add(new TableInfo { tableAsset = table });
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            MarkDuplicateKeysDirty();
        }
    }
}
