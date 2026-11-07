// =============================================================================
// LocaloPlatformTerms.cs — パッケージ同梱の、機種ごとのボタン名・機能名の公式表記
// =============================================================================
// データは Editor/Resources/LocaloPlatformTerms.json。一般公開の製品ページの表記だけを載せる
// （ロットチェックなどの開発者向けの用語規定は転載しない）。プロジェクトのGlossaryに同じ語があれば
// Glossaryが優先する。プロジェクトは Setup の Target Platforms で対象の機種を選ぶ。
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Localo.Editor
{
    internal static class LocaloPlatformTerms
    {
        internal const string RelativePath = "Editor/Resources/LocaloPlatformTerms.json";
        internal const string OfficialStatus = "official";

        private static string _loadedJson;
        private static PlatformTermsCatalog _catalog;

        internal static PlatformTermsCatalog Load() =>
            LocaloPackageFiles.LoadJson(RelativePath, "機種の用語", catalog => catalog.platforms != null,
                ref _loadedJson, ref _catalog);

        /// <summary>選んだ機種だけを、カタログの順に返す。未知のIDは無視する。</summary>
        internal static PlatformTerms[] ForPlatforms(IEnumerable<string> platformIds)
        {
            var ids = new HashSet<string>(platformIds ?? Enumerable.Empty<string>());
            if (ids.Count == 0) return Array.Empty<PlatformTerms>();
            return Load().platforms.Where(platform => platform != null && ids.Contains(platform.id)).ToArray();
        }

        /// <summary>
        /// 文中にある、非公式の表記（avoid）を探す。英字で始まる・終わる表記は、前後が英数字でないときだけ一致とする
        /// （「DATA button」の中の「A button」などを拾わないため）。
        /// </summary>
        internal static List<string> FindAvoided(string text, PlatformTermTranslation translation)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text) || translation?.avoid == null) return found;
            foreach (string avoided in translation.avoid)
            {
                if (string.IsNullOrEmpty(avoided) || found.Contains(avoided)) continue;
                if (ContainsWord(text, avoided)) found.Add(avoided);
            }
            return found;
        }

        private static bool ContainsWord(string text, string word)
        {
            int index = text.IndexOf(word, StringComparison.Ordinal);
            while (index >= 0)
            {
                bool startOk = !char.IsLetterOrDigit(word[0]) || index == 0 || !IsLatinLetterOrDigit(text[index - 1]);
                int end = index + word.Length;
                bool endOk = !char.IsLetterOrDigit(word[word.Length - 1]) || end >= text.Length || !IsLatinLetterOrDigit(text[end]);
                if (startOk && endOk) return true;
                index = text.IndexOf(word, index + 1, StringComparison.Ordinal);
            }
            return false;
        }

        // 日本語・中国語は語の間に空白が無いので、境界を判定するのはラテン文字・数字どうしが続く場合だけにする。
        private static bool IsLatinLetterOrDigit(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
            (c >= 'À' && c <= 'ɏ' && char.IsLetter(c));
    }

    // フィールド名はJSONの契約で、コンテキストJSON（platformTerms）にもそのまま出る。
    [Serializable]
    internal sealed class PlatformTermsCatalog
    {
        public string reviewedOn;
        public string notice;
        public PlatformTerms[] platforms;
    }

    [Serializable]
    internal sealed class PlatformTerms
    {
        public string id;
        public string name;
        public string[] notes;
        public LanguageGuidelineSource[] sources;
        public PlatformTerm[] terms;
    }

    [Serializable]
    internal sealed class PlatformTerm
    {
        public string id;
        /// <summary>console / controller / button / stick / mode / feature</summary>
        public string category;
        public string notes;
        public PlatformTermTranslation[] translations;

        internal PlatformTermTranslation For(SystemLanguage language) =>
            translations?.FirstOrDefault(translation => translation?.language == language.ToString());
    }

    [Serializable]
    internal sealed class PlatformTermTranslation
    {
        /// <summary>SystemLanguage の名前（Japanese、ChineseTraditional など）。</summary>
        public string language;
        public string text;
        /// <summary>official（公式ページの表記）か reference（公式ページが無い言語の参考値）。</summary>
        public string status;
        /// <summary>公式ページで使われている別の書き方。QAでは誤りにしない。</summary>
        public string[] variants;
        /// <summary>公式ではない書き方。QAの platform-terms チェックが警告する。</summary>
        public string[] avoid;
    }
}
