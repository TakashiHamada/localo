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
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(skillPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(metadataPath), Is.Not.Null);
        }

        [Test]
        public void BundledData_LoadsFromInstalledPackage()
        {
            Assert.That(LocaloLanguageGuidelines.Load().languages, Is.Not.Empty);
            Assert.That(LocaloPlatformTerms.Load().platforms, Is.Not.Empty);
            ProjectProfileOptionsCatalog profile = LocaloProjectProfileOptions.Load();
            Assert.That(profile.audiences, Is.Not.Empty);
            // 対象年齢の既定の漢字の範囲は、すべて選択肢に存在する。
            foreach (ProjectProfileAudience audience in profile.audiences)
                Assert.That(System.Array.Exists(profile.kanjiRanges, range => range.id == audience.kanjiRange), audience.id);
            // 画面の英語名・メッセージ用の日本語名・AI向けの指針がそろっている。
            foreach (ProjectProfileOption option in profile.audiences.Cast<ProjectProfileOption>().Concat(profile.genres)
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
                Assert.That(LocaloProjectProfileOptions.CreateContext(settings), Is.Null);
                Assert.That(UnityEngine.JsonUtility.FromJson<SchemaOnly>(LocaloTranslationContext.ExportToString(settings)).schemaVersion,
                    Is.EqualTo(7));
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
