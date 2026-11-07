using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>プレビューとAI向けJSONで共有する、パッケージ同梱の翻訳ガイド。</summary>
    internal static class LocaloLanguageGuidelines
    {
        internal const string RelativePath = "Editor/Resources/LocaloLanguageGuidelines.json";

        private static string _loadedJson;
        private static LanguageGuidelinesContext _catalog;

        internal static LanguageGuidelinesContext Load() =>
            LocaloPackageFiles.LoadJson(RelativePath, "言語別ガイド",
                catalog => catalog.commonPoints != null && catalog.languages != null && catalog.languages.Length > 0,
                ref _loadedJson, ref _catalog);

        /// <summary>呼び出し側で設定順に絞った翻訳先を含める。日本語は今回の言語別ガイドの対象外。</summary>
        internal static LanguageGuidelinesContext ForLanguages(IEnumerable<SystemLanguage> languages)
        {
            LanguageGuidelinesContext catalog = Load();
            var guides = new List<LanguageGuideline>();
            foreach (SystemLanguage language in languages.Distinct())
            {
                LanguageGuideline guide = catalog.languages.FirstOrDefault(item => item.language == language.ToString());
                if (guide != null) guides.Add(guide);
            }
            return new LanguageGuidelinesContext
            {
                reviewedOn = catalog.reviewedOn,
                commonPoints = catalog.commonPoints,
                commonSources = catalog.commonSources,
                languages = guides.ToArray()
            };
        }
    }

    // フィールド名はコンテキストJSONの契約。データはResources内のJSONで管理する。
    [Serializable]
    internal sealed class LanguageGuidelinesContext
    {
        public string reviewedOn;
        public string[] commonPoints;
        public LanguageGuidelineSource[] commonSources;
        public LanguageGuideline[] languages;
    }

    [Serializable]
    internal sealed class LanguageGuideline
    {
        public string language;
        public string locale;
        public string region;
        public string[] points;
        public string[] notes;
        public LanguageGuidelineSource[] sources;
    }

    [Serializable]
    internal sealed class LanguageGuidelineSource
    {
        public string title;
        public string url;
    }
}
