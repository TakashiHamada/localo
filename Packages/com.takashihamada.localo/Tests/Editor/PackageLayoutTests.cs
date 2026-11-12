using System.IO;
using System.Linq;
using Localo.Editor;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Localo.Tests
{
    public sealed class PackageLayoutTests
    {
        [Test]
        public void AiSkillTemplates_AreAvailableFromInstalledPackage()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(LocaloEditorWindow).Assembly);
            Assert.That(info, Is.Not.Null);

            string skillPath = $"{info.assetPath}/Editor/Resources/LocaloProjectSkillTemplate.txt";
            string metadataPath = $"{info.assetPath}/Editor/Resources/LocaloProjectSkillOpenAiTemplate.txt";
            string guidePath = $"{info.assetPath}/Editor/Resources/LocaloTranslationGuideTemplate.txt";
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(skillPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(metadataPath), Is.Not.Null);

            // 翻訳ガイドは、全言語共通の節と、言語別ガイドの対象の8言語の節を持つ。
            var guide = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(guidePath);
            Assert.That(guide, Is.Not.Null);
            StringAssert.Contains("## 全言語共通", guide.text);
            foreach (string heading in new[]
                     {
                         "### English：", "### Korean：", "### ChineseSimplified：", "### ChineseTraditional：",
                         "### German：", "### French：", "### Spanish：", "### Portuguese："
                     })
                StringAssert.Contains(heading, guide.text);
            foreach (string locale in new[] { "en-US", "ko-KR", "zh-CN", "zh-TW", "de-DE", "fr-FR", "es-419", "pt-BR" })
                StringAssert.Contains(locale, guide.text);
        }

        [Test]
        public void AiSkill_IsTheOnlyHomeOfTranslationRules()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(LocaloEditorWindow).Assembly);
            string skill = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(
                $"{info.assetPath}/Editor/Resources/LocaloProjectSkillTemplate.txt").text;

            // Skillは、コンテキストJSONの版、翻訳ガイド、QAの入口を指している。
            StringAssert.Contains($"schema version {LocaloTranslationContext.SchemaVersion}", skill);
            StringAssert.Contains("translation-guide.md", skill);
            StringAssert.Contains("LocaloQa.Report", skill);
            // SkillがQAの使いどきに挙げるチェックは、実在するIdである。
            foreach (string checkId in new[] { "missing-key", "legacy-text-usage", "line-break-settings", "missing-glyph", "groups" })
            {
                StringAssert.Contains($"`{checkId}`", skill);
                Assert.That(LocaloQa.CheckIds, Does.Contain(checkId));
            }

            // 翻訳ガイドの自己レビューが挙げるチェックも、実在するIdである。
            string guide = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(
                $"{info.assetPath}/Editor/Resources/LocaloTranslationGuideTemplate.txt").text;
            foreach (string checkId in new[] { "page-count", "punctuation" })
            {
                StringAssert.Contains($"`{checkId}`", guide);
                Assert.That(LocaloQa.CheckIds, Does.Contain(checkId));
            }
        }

        [Test]
        public void BundledData_LoadsFromInstalledPackage()
        {
            Assert.That(LocaloPlatformTerms.Load().platforms, Is.Not.Empty);
            ProjectProfileOptionsCatalog profile = LocaloProjectProfileOptions.Load();
            Assert.That(profile.audiences, Is.Not.Empty);
            // 対象年齢の既定の漢字の範囲は、すべて選択肢に存在する。
            foreach (ProjectProfileAudience audience in profile.audiences)
                Assert.That(System.Array.Exists(profile.kanjiRanges, range => range.id == audience.kanjiRange), audience.id);
            // 画面の英語名・メッセージ用の日本語名・AI向けの指針がそろっている。
            foreach (ProjectProfileOption option in profile.audiences.Cast<ProjectProfileOption>()
                         .Concat(profile.tones).Concat(profile.kanjiRanges))
            {
                Assert.That(option.name, Is.Not.Empty, option.id);
                Assert.That(option.label, Is.Not.Empty, option.id);
                Assert.That(option.guidance, Is.Not.Empty, option.id);
            }
            Assert.That(LocaloKanjiLevels.LevelOf('一'), Is.EqualTo(1));
        }

        [Test]
        public void SettingsWithoutProfileFields_LoadWithEmptyDefaults()
        {
            // 新しい項目を持たない古い設定アセット（YAML）を読み込んでも、空の既定値になる。
            var settings = UnityEngine.ScriptableObject.CreateInstance<Localo.Runtime.LocalizationSettings>();
            try
            {
                UnityEngine.JsonUtility.FromJsonOverwrite("{\"keyColumnWidth\":160}", settings);
                Assert.That(settings.projectProfile, Is.Not.Null);
                Assert.That(settings.projectProfile.audience, Is.Empty);
                Assert.That(settings.targetPlatforms, Is.Empty);
                Assert.That(settings.languageLessons, Is.Empty);
                Assert.That(LocaloProjectProfileOptions.CreateContext(settings), Is.Null);
                Assert.That(UnityEngine.JsonUtility.FromJson<SchemaOnly>(LocaloTranslationContext.ExportToString(settings)).schemaVersion,
                    Is.EqualTo(LocaloTranslationContext.SchemaVersion));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [System.Serializable] private sealed class SchemaOnly
        {
            public int schemaVersion;
        }

        [Test]
        public void PackageManifest_DoesNotContainLocalFileDependencies()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(LocaloEditorWindow).Assembly);
            string manifest = File.ReadAllText(Path.Combine(info.resolvedPath, "package.json"));
            StringAssert.DoesNotContain("file:", manifest);
        }
    }
}
