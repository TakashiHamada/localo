// =============================================================================
// LocaloQaMissingGlyphCheck.cs — フォントに無い文字（□＝豆腐になる文字）
// =============================================================================
// TMP Settings の既定のフォントアセットと、その代替フォント（fallbackFontAssetTable を再帰的に）、
// TMP Settings 全体の代替フォント（Fallback Font Assets）を順に調べ、訳文の文字がどれにも無ければ報告する。
// - characterLookupTable に登録済みの文字は、あるとみなす。
// - 動的（Dynamic）なフォントアセットは、元のフォントファイルを FontEngine で開いてグリフの有無を調べる。
//   フォントアセット自体は変更しない（文字を追加しない）。
// - 元のフォントファイルを持たない動的なアセット（Dynamic OS など）は判定できないので Info で知らせる。
// 画面ごとに別のフォントを指定している文字、UI Toolkit や legacy Text のフォントは対象外（既定のフォントでの目安）。
// フォントの読み込みを減らすため、全訳文の文字を先に集め、フォントごとに1回だけ開いて調べる。
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Localo.Editor
{
    internal sealed class LocaloQaMissingGlyphCheck : ILocaloQaCheck
    {
        private const string TmpSettingsResourcesName = "TMP Settings";
        private const int MaxCharactersPerIssue = 30;

        public string Id => "missing-glyph";
        public string Description =>
            "TMP Settings の既定のフォントと、その代替フォントに無い文字（画面で□になる文字）を、言語ごとに探します。" +
            "画面ごとに別のフォントを指定している文字や、UI Toolkit・legacy Text のフォントは対象外です（既定のフォントでの目安です）。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            var tmpSettings = Resources.Load<TMP_Settings>(TmpSettingsResourcesName);
            if (tmpSettings == null)
            {
                issues.Add(new LocaloQaIssue
                {
                    Severity = LocaloQaSeverity.Info,
                    Message = "TMP Settings が見つからないため、フォントを調べられません。"
                });
                return;
            }

            List<TMP_FontAsset> fonts = CollectFontChain(tmpSettings);
            if (fonts.Count == 0)
            {
                issues.Add(new LocaloQaIssue
                {
                    Severity = LocaloQaSeverity.Info,
                    Message = "TMP Settings に既定のフォント（Default Font Asset）が設定されていないため、フォントを調べられません。",
                    AssetPath = AssetDatabase.GetAssetPath(tmpSettings),
                    ObjectName = tmpSettings.name
                });
                return;
            }

            // セルごとの文字と、全体で使われている文字を集める。
            var cells = new List<(LocalizationTableCell Cell, List<int> CodePoints)>();
            var allCodePoints = new HashSet<int>();
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                foreach (var language in context.Languages)
                {
                    List<int> codePoints = DrawnCodePoints(table.GetText(row, language));
                    if (codePoints.Count == 0) continue;
                    cells.Add((new LocalizationTableCell(table, rowIndex, row, language), codePoints));
                    allCodePoints.UnionWith(codePoints);
                }
            }

            HashSet<int> missing = FindMissing(fonts, allCodePoints, issues);
            if (missing.Count == 0) return;

            foreach (var (cell, codePoints) in cells)
            {
                List<int> cellMissing = codePoints.Where(missing.Contains).Distinct().ToList();
                if (cellMissing.Count == 0) continue;
                string shown = string.Join(" ", cellMissing.Take(MaxCharactersPerIssue).Select(Describe));
                string more = cellMissing.Count > MaxCharactersPerIssue ? $" ほか{cellMissing.Count - MaxCharactersPerIssue}文字" : string.Empty;
                issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Warning,
                    $"既定のフォントと代替フォントに無い文字があります（□になります）: {shown}{more}",
                    cell.Table, cell.RowIndex, cell.Row, cell.Language));
            }
        }

        private readonly struct LocalizationTableCell
        {
            public readonly Localo.Runtime.LocalizationTable Table;
            public readonly int RowIndex;
            public readonly Localo.Runtime.LocalizationRow Row;
            public readonly SystemLanguage Language;

            public LocalizationTableCell(Localo.Runtime.LocalizationTable table, int rowIndex,
                Localo.Runtime.LocalizationRow row, SystemLanguage language)
            {
                Table = table;
                RowIndex = rowIndex;
                Row = row;
                Language = language;
            }
        }

        /// <summary>既定のフォント → その代替（再帰）→ TMP Settings の代替（再帰）の順に、重複なく並べる。</summary>
        internal static List<TMP_FontAsset> CollectFontChain(TMP_Settings tmpSettings)
        {
            var serialized = new SerializedObject(tmpSettings);
            var roots = new List<TMP_FontAsset>();
            if (serialized.FindProperty("m_defaultFontAsset")?.objectReferenceValue is TMP_FontAsset defaultFont)
                roots.Add(defaultFont);
            SerializedProperty globalFallbacks = serialized.FindProperty("m_fallbackFontAssets");
            if (globalFallbacks != null && globalFallbacks.isArray)
            {
                for (int i = 0; i < globalFallbacks.arraySize; i++)
                    if (globalFallbacks.GetArrayElementAtIndex(i).objectReferenceValue is TMP_FontAsset fallback)
                        roots.Add(fallback);
            }

            var chain = new List<TMP_FontAsset>();
            var visited = new HashSet<TMP_FontAsset>();
            foreach (TMP_FontAsset root in roots)
                AddWithFallbacks(root, chain, visited);
            return chain;
        }

        private static void AddWithFallbacks(TMP_FontAsset font, List<TMP_FontAsset> chain, HashSet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font)) return;
            chain.Add(font);
            if (font.fallbackFontAssetTable == null) return;
            foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable)
                AddWithFallbacks(fallback, chain, visited);
        }

        /// <summary>codePoints のうち、どのフォントにも無い文字を返す。判定できないフォントは Info で知らせる。</summary>
        internal static HashSet<int> FindMissing(IEnumerable<TMP_FontAsset> fonts, IEnumerable<int> codePoints, List<LocaloQaIssue> issues)
        {
            var missing = new HashSet<int>(codePoints);
            foreach (TMP_FontAsset font in fonts)
            {
                if (missing.Count == 0) break;
                Dictionary<uint, TMP_Character> table = font.characterLookupTable;
                if (table != null) missing.RemoveWhere(codePoint => table.ContainsKey((uint)codePoint));
                if (missing.Count == 0 || font.atlasPopulationMode == AtlasPopulationMode.Static) continue;

                if (font.sourceFontFile == null || FontEngine.LoadFontFace(font.sourceFontFile) != FontEngineError.Success)
                {
                    issues?.Add(new LocaloQaIssue
                    {
                        Severity = LocaloQaSeverity.Info,
                        Message = "動的なフォントアセットの元のフォントファイルを開けないため、まだ登録されていない文字の有無を判定できません。",
                        AssetPath = AssetDatabase.GetAssetPath(font),
                        ObjectName = font.name
                    });
                    continue;
                }
                missing.RemoveWhere(codePoint => FontEngine.TryGetGlyphIndex((uint)codePoint, out uint glyphIndex) && glyphIndex != 0);
            }
            return missing;
        }

        /// <summary>タグとプレースホルダーを除き、空白・改行・制御文字・見えない書式文字・異体字セレクタを除いた文字（コードポイント）。</summary>
        internal static List<int> DrawnCodePoints(string text)
        {
            var codePoints = new List<int>();
            string visible = LocaloTextPatterns.VisibleText(text);
            for (int i = 0; i < visible.Length; i++)
            {
                char c = visible[i];
                if (char.IsHighSurrogate(c) && i + 1 < visible.Length && char.IsLowSurrogate(visible[i + 1]))
                {
                    codePoints.Add(char.ConvertToUtf32(c, visible[i + 1]));
                    i++;
                    continue;
                }
                if (char.IsWhiteSpace(c) || char.IsControl(c) || LocaloTextPatterns.IsInvisibleFormat(c) ||
                    (c >= '︀' && c <= '️'))
                    continue;
                codePoints.Add(c);
            }
            return codePoints;
        }

        private static string Describe(int codePoint) =>
            codePoint > 0xFFFF ? $"「{char.ConvertFromUtf32(codePoint)}」" : LocaloTextPatterns.DescribeCharacter((char)codePoint);
    }
}
