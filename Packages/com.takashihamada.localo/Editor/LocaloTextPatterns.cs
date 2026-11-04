// =============================================================================
// LocaloTextPatterns.cs — 訳文を調べるときに共有する文字列処理
// =============================================================================
// CSV取り込みの警告（LocaloCsv）と QA チェック（LocaloQa*Check）が、同じ基準で
// プレースホルダー・リッチテキストのタグ・文字の種類を扱うためにまとめる。
// ここを変えると CSV の警告と QA の結果が同時に変わる。
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Localo.Editor
{
    internal static class LocaloTextPatterns
    {
        // string.Format の書式項目（{0}、{1,5}、{2:N0}）。
        internal static readonly Regex PlaceholderPattern =
            new Regex(@"\{\d+(?:,[^}:]*)?(?::[^}]*)?\}", RegexOptions.Compiled);

        // リッチテキストのタグ（<color=#fff>、</b>、<size=30>、<br> など）。名前は Groups["name"]。
        internal static readonly Regex RichTextTagPattern =
            new Regex(@"<(?<close>\/)?(?<name>[A-Za-z#][A-Za-z0-9\-]*)[^<>]*>", RegexOptions.Compiled);

        /// <summary>
        /// プレースホルダーの組を、重複を除いて並べ替えた文字列で返す（原文と訳の比較用。順序は問わない）。
        /// </summary>
        internal static string PlaceholderSignature(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return string.Join(" ", PlaceholderPattern.Matches(text).Cast<Match>()
                .Select(match => match.Value).Distinct().OrderBy(value => value, StringComparer.Ordinal));
        }

        /// <summary>リッチテキストのタグを取り除く。</summary>
        internal static string StripTags(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : RichTextTagPattern.Replace(text, string.Empty);

        /// <summary>タグとプレースホルダーを取り除き、画面に出る文字だけにする。</summary>
        internal static string VisibleText(string text) =>
            PlaceholderPattern.Replace(StripTags(text), string.Empty);

        /// <summary>タグを（開閉の別, 小文字の名前）の並びで返す。属性の値は含めない。</summary>
        internal static IEnumerable<(bool IsClosing, string Name)> Tags(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            foreach (Match match in RichTextTagPattern.Matches(text))
                yield return (match.Groups["close"].Success, match.Groups["name"].Value.ToLowerInvariant());
        }

        internal static bool IsKana(char c) => c >= '぀' && c <= 'ヿ';

        internal static bool IsHan(char c) =>
            (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || c == '々';

        internal static bool IsHangul(char c) =>
            (c >= '가' && c <= '힯') || (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '㄰' && c <= '㆏');

        /// <summary>日本語・中国語の文字（かな・漢字）。</summary>
        internal static bool IsCjkIdeographOrKana(char c) => IsKana(c) || IsHan(c);

        /// <summary>メッセージに出すための文字の表記（見えない文字はコードで示す）。</summary>
        internal static string DescribeCharacter(char c) =>
            char.IsWhiteSpace(c) || char.IsControl(c) || IsInvisibleFormat(c) ? $"U+{(int)c:X4}" : $"「{c}」";

        internal static bool IsInvisibleFormat(char c) =>
            c == '​' || c == '‌' || c == '‍' || c == '⁠' || c == '﻿' ||
            (c >= '‪' && c <= '‮') || (c >= '⁦' && c <= '⁩');
    }
}
