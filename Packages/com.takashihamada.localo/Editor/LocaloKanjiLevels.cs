// =============================================================================
// LocaloKanjiLevels.cs — 漢字が小学校の何年生で習うか（学年別漢字配当表）と、常用漢字かどうか
// =============================================================================
// データは Editor/Resources/LocaloKanjiLevels.json（文部科学省の資料から作成）。
// QAの kanji-range チェックが、作品プロファイルの漢字の範囲と比べるのに使う。
// =============================================================================

using System;
using System.Collections.Generic;

namespace Localo.Editor
{
    internal static class LocaloKanjiLevels
    {
        internal const string RelativePath = "Editor/Resources/LocaloKanjiLevels.json";

        /// <summary>常用漢字のうち、学年別漢字配当表に無い漢字の段階。</summary>
        internal const int JoyoLevel = 7;
        /// <summary>常用漢字表に無い漢字（表外字）の段階。</summary>
        internal const int NonJoyoLevel = 8;

        private static string _loadedJson;
        private static KanjiLevelsData _data;
        private static Dictionary<int, int> _levels;

        /// <summary>
        /// データを読み直す（JSONが変わっていれば表を作り直す）。チェックの実行ごとに1回呼び、
        /// 1文字ずつの LevelOf では読み込みを繰り返さない。
        /// </summary>
        internal static void Refresh() => Load();

        private static KanjiLevelsData Load()
        {
            KanjiLevelsData previous = _data;
            KanjiLevelsData data = LocaloPackageFiles.LoadJson(RelativePath, "漢字の学年の表",
                loaded => loaded.grades != null && loaded.grades.Length == 6 && !string.IsNullOrEmpty(loaded.joyoOthers),
                ref _loadedJson, ref _data);
            if (_levels == null || !ReferenceEquals(previous, data)) _levels = BuildLevels(data);
            return data;
        }

        /// <summary>
        /// 漢字（コードポイント）の段階。1〜6 = 習う学年、7 = 中学以降の常用漢字、8 = 表外字。
        /// </summary>
        internal static int LevelOf(int codePoint)
        {
            if (_levels == null) Load();
            return _levels.TryGetValue(codePoint, out int level) ? level : NonJoyoLevel;
        }

        private static Dictionary<int, int> BuildLevels(KanjiLevelsData data)
        {
            var levels = new Dictionary<int, int>();
            for (int grade = 0; grade < data.grades.Length; grade++)
                foreach (int codePoint in CodePoints(data.grades[grade]))
                    levels[codePoint] = grade + 1;
            foreach (int codePoint in CodePoints(data.joyoOthers))
                if (!levels.ContainsKey(codePoint)) levels[codePoint] = JoyoLevel;
            return levels;
        }

        /// <summary>文字列をコードポイントの並びにする（「𠮟」などのサロゲートペアを1文字として扱う）。</summary>
        internal static IEnumerable<(int CodePoint, int Index)> CodePointsWithIndex(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    yield return (char.ConvertToUtf32(text[i], text[i + 1]), i);
                    i++;
                }
                else yield return (text[i], i);
            }
        }

        private static IEnumerable<int> CodePoints(string text)
        {
            foreach (var (codePoint, _) in CodePointsWithIndex(text)) yield return codePoint;
        }

        /// <summary>CJK統合漢字（拡張A〜、互換漢字を含む）か。「々」などの記号は含めない。</summary>
        internal static bool IsKanji(int codePoint) =>
            (codePoint >= 0x4E00 && codePoint <= 0x9FFF) || (codePoint >= 0x3400 && codePoint <= 0x4DBF) ||
            (codePoint >= 0xF900 && codePoint <= 0xFAFF) || (codePoint >= 0x20000 && codePoint <= 0x3FFFF);
    }

    [Serializable]
    internal sealed class KanjiLevelsData
    {
        public string reviewedOn;
        public string[] notes;
        public LanguageGuidelineSource[] sources;
        /// <summary>第1学年から第6学年までに配当された漢字（6要素）。</summary>
        public string[] grades;
        /// <summary>常用漢字のうち、学年別漢字配当表に無い漢字。</summary>
        public string joyoOthers;
    }
}
