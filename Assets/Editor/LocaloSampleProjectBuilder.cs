using System.Collections.Generic;
using System.IO;
using Localo.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Localo.Sample.Editor
{
    public static class LocaloSampleProjectBuilder
    {
        private const string SampleRoot = "Assets/Samples/LocaloDemo";
        private const string ScenePath = SampleRoot + "/Scenes/LocaloDemo.unity";
        private const string SourceFontPath = SampleRoot + "/Fonts/NotoSansJP-Variable.ttf";
        private const string TmpFontPath = SampleRoot + "/Fonts/NotoSansJP-Dynamic.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string SettingsPath = "Assets/LocaloAssets/Resources/LocalizationSettings.asset";
        private const string TablePath = "Assets/LocaloAssets/Tables/MainTable.asset";

        [MenuItem("Tools/Localo/Rebuild Development Sample")]
        public static void Build()
        {
            EnsureFolder("Assets", "LocaloAssets");
            EnsureFolder("Assets/LocaloAssets", "Resources");
            EnsureFolder("Assets/LocaloAssets", "Tables");
            EnsureFolder(SampleRoot, "Scenes");

            TMP_Settings tmpSettings = ImportOrLoadTmpEssentials();
            TMP_FontAsset fontAsset = CreateOrLoadFontAsset();
            TMP_Settings.defaultFontAsset = fontAsset;
            EditorUtility.SetDirty(tmpSettings);
            CreateOrUpdateLocalizationData();
            CreateScene(fontAsset);

            PlayerSettings.productName = "Localo Sample";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Localo Sample] Rebuilt {ScenePath}");
        }

        private static TMP_Settings ImportOrLoadTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/Resources/TMP Settings.asset") != null)
                AssetDatabase.DeleteAsset("Assets/Resources/TMP Settings.asset");

            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                PackageInfo uguiPackage = PackageInfo.FindForPackageName("com.unity.ugui");
                if (uguiPackage == null)
                    throw new System.InvalidOperationException("The com.unity.ugui package is not installed.");

                string essentialsPath = Path.Combine(
                    uguiPackage.resolvedPath,
                    "Package Resources",
                    "TMP Essential Resources.unitypackage");
                AssetDatabase.ImportPackage(essentialsPath, false);
                AssetDatabase.Refresh();
                settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            }

            if (settings == null || TMP_Settings.LoadDefaultSettings() == null)
                throw new System.InvalidOperationException($"TMP settings could not be loaded from {TmpSettingsPath}");

            return settings;
        }

        private static TMP_FontAsset CreateOrLoadFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (existing != null) return existing;

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
                throw new System.InvalidOperationException($"Sample font not found: {SourceFontPath}");

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                64,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            fontAsset.name = "NotoSansJP-Dynamic";
            AssetDatabase.CreateAsset(fontAsset, TmpFontPath);

            if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture != null && !AssetDatabase.Contains(atlasTexture))
                    AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void CreateOrUpdateLocalizationData()
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LocalizationTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.rows.Clear();
            AddRow(table, "SAMPLE_TITLE", "Localo Localization Sample", "Localo ローカライズサンプル");
            AddRow(table, "SAMPLE_GREETING", "Hello from Localo!", "Localoからこんにちは！");
            AddRow(table, "SAMPLE_3D_TEXT", "This is 3D TextMesh Pro", "これは3D TextMesh Proです");
            AddRow(table, "SAMPLE_FALLBACK", "This line falls back to English.", string.Empty);
            AddRow(table, "SAMPLE_CURRENT_LANGUAGE", "Current language: {0}", "現在の言語: {0}");
            EditorUtility.SetDirty(table);

            LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.enabledLanguages = new List<SystemLanguage>
            {
                SystemLanguage.English,
                SystemLanguage.Japanese
            };
            settings.visibleLanguages = new List<SystemLanguage>
            {
                SystemLanguage.English,
                SystemLanguage.Japanese
            };
            settings.visibleLanguagesInitialized = true;
            settings.startupLanguageMode = StartupLanguageMode.FixedLanguage;
            settings.startupLanguage = SystemLanguage.English;
            settings.registeredTables = new List<TableInfo> { new TableInfo { tableAsset = table } };
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            LocaloDatabase.ReloadTables();
        }

        private static void CreateScene(TMP_FontAsset fontAsset)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            var worldTextObject = new GameObject("Localized 3D Text", typeof(TextMeshPro), typeof(LocalizeTextMeshPro));
            worldTextObject.transform.position = new Vector3(0f, -3.25f, 0f);
            TextMeshPro worldText = worldTextObject.GetComponent<TextMeshPro>();
            worldText.font = fontAsset;
            worldText.fontSize = 5f;
            worldText.alignment = TextAlignmentOptions.Center;
            worldText.color = new Color(0.45f, 0.85f, 1f);
            worldTextObject.GetComponent<LocalizeTextMeshPro>().SetKey("SAMPLE_3D_TEXT");

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            TMP_Text title = CreateUiText(canvasObject.transform, "Title", fontAsset, 52f,
                new Vector2(0f, 250f), new Vector2(1000f, 80f), Color.white);
            title.gameObject.AddComponent<LocalizeTextMeshProUGUI>().SetKey("SAMPLE_TITLE");

            TMP_Text greeting = CreateUiText(canvasObject.transform, "Greeting", fontAsset, 38f,
                new Vector2(0f, 135f), new Vector2(1000f, 70f), new Color(0.85f, 0.95f, 1f));
            greeting.gameObject.AddComponent<LocalizeTextMeshProUGUI>().SetKey("SAMPLE_GREETING");

            TMP_Text fallback = CreateUiText(canvasObject.transform, "Fallback Example", fontAsset, 25f,
                new Vector2(0f, 65f), new Vector2(1000f, 55f), new Color(1f, 0.82f, 0.45f));
            fallback.gameObject.AddComponent<LocalizeTextMeshProUGUI>().SetKey("SAMPLE_FALLBACK");

            TMP_Text status = CreateUiText(canvasObject.transform, "Current Language", fontAsset, 25f,
                new Vector2(0f, -15f), new Vector2(700f, 55f), Color.white);

            var controllerObject = new GameObject("Localo Sample Controller", typeof(LocaloSampleController));
            LocaloSampleController controller = controllerObject.GetComponent<LocaloSampleController>();
            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_currentLanguageText").objectReferenceValue = status;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            Button englishButton = CreateButton(canvasObject.transform, "English Button", "English", fontAsset,
                new Vector2(-150f, -100f));
            Button japaneseButton = CreateButton(canvasObject.transform, "Japanese Button", "日本語", fontAsset,
                new Vector2(150f, -100f));
            UnityEventTools.AddPersistentListener(englishButton.onClick, controller.SetEnglish);
            UnityEventTools.AddPersistentListener(japaneseButton.onClick, controller.SetJapanese);

            CreateUiText(canvasObject.transform, "Instructions", fontAsset, 20f,
                new Vector2(0f, -180f), new Vector2(1000f, 50f), new Color(0.7f, 0.75f, 0.82f)).text =
                "Switch languages and observe the bound UI text, 3D text, direct lookup, and English fallback.";

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static TMP_Text CreateUiText(
            Transform parent,
            string name,
            TMP_FontAsset fontAsset,
            float fontSize,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset fontAsset,
            Vector2 position)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(240f, 64f);
            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.63f);

            TMP_Text buttonText = CreateUiText(buttonObject.transform, "Label", fontAsset, 26f,
                Vector2.zero, rect.sizeDelta, Color.white);
            buttonText.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private static void AddRow(LocalizationTable table, string key, string english, string japanese)
        {
            var row = new LocalizationRow { key = key, comment = "Localo development sample" };
            table.rows.Add(row);
            table.SetText(row, SystemLanguage.English, english);
            table.SetText(row, SystemLanguage.Japanese, japanese);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
