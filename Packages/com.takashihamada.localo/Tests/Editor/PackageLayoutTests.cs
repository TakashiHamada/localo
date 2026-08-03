using System.IO;
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
        public void PackageManifest_DoesNotContainLocalFileDependencies()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(LocaloEditorWindow).Assembly);
            string manifest = File.ReadAllText(Path.Combine(info.resolvedPath, "package.json"));
            StringAssert.DoesNotContain("file:", manifest);
        }
    }
}
