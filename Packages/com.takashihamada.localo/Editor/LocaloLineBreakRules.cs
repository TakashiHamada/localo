// =============================================================================
// LocaloLineBreakRules.cs — 行頭禁則・行末禁則の文字と、手で入れた改行の判定
// =============================================================================
// QAタブの禁則チェックが使う、文字列だけで判定できる処理をまとめる（フォントや枠の幅は使わない）。
// - 行頭禁則: 行の先頭に置かない文字（「、」「。」、閉じかっこ、小さい仮名など）。
// - 行末禁則: 行の末尾に置かない文字（開きかっこなど）。
// 文字の考え方は TextMesh Pro の既定（LineBreaking Following / Leading Characters）に合わせる。
// TMP と UI Toolkit（TextCore）の名前は直感と逆で、Following Characters が行頭禁則、
// Leading Characters が行末禁則にあたる。
// 自動の折り返しは TMP や Panel Text Settings の禁則で防げるが、手で入れた改行（<br>・\n）は
// どの描画方式でも禁則の対象外なので、ここで文字列から検出する。
// =============================================================================

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Localo.Editor
{
    /// <summary>
    /// 行頭禁則・行末禁則の文字と、手で入れた改行の前後の判定。
    /// </summary>
    public static class LocaloLineBreakRules
    {
        /// <summary>行の先頭に置かない文字。手で入れた改行の検査に使う。</summary>
        public const string LineStartProhibitedCharacters =
            "、。，．・：；？！‼⁇…‥ー々ゝゞヽヾ）」』】〕〉》］｝’”" +
            "ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ" +
            ",.!?:;)]}";

        /// <summary>行の末尾に置かない文字。手で入れた改行の検査に使う。</summary>
        public const string LineEndProhibitedCharacters = "（「『【〔〈《［｛‘“([{";

        /// <summary>
        /// TMP Settings や Panel Text Settings の行頭禁則リストに、少なくとも入っているべき文字。
        /// TMP の既定のリストに含まれる字だけにして、既定のままのプロジェクトで警告が出ないようにする。
        /// </summary>
        public const string RequiredLineStartCharacters = "、。，．！？：；）」』】ーっゃゅょッャュョ";

        /// <summary>TMP Settings や Panel Text Settings の行末禁則リストに、少なくとも入っているべき文字。</summary>
        public const string RequiredLineEndCharacters = "（「『【";

        // <br> は改行として先に置き換え、残りのタグは LocaloTextPatterns.StripTags で取り除く。
        private static readonly Regex BreakTagPattern = new Regex(@"<br\s*\/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 空白だけの行をはさんでもよい、連続する2つ以上の改行。
        private static readonly Regex PageBreakPattern = new Regex(@"\n(?:[ \t\u3000]*\n)+", RegexOptions.Compiled);

        public static bool IsLineStartProhibited(char character) => LineStartProhibitedCharacters.IndexOf(character) >= 0;

        public static bool IsLineEndProhibited(char character) => LineEndProhibitedCharacters.IndexOf(character) >= 0;

        /// <summary>手で入れた改行の前後で、禁則に当たる箇所。</summary>
        public readonly struct ManualBreakViolation
        {
            /// <summary>true なら改行の直後（行頭）、false なら改行の直前（行末）。</summary>
            public readonly bool AtLineStart;
            /// <summary>問題の文字がある行（1始まり）。</summary>
            public readonly int LineNumber;
            public readonly char Character;

            public ManualBreakViolation(bool atLineStart, int lineNumber, char character)
            {
                AtLineStart = atLineStart;
                LineNumber = lineNumber;
                Character = character;
            }
        }

        /// <summary>
        /// 手で入れた改行（&lt;br&gt;・\n・\r\n）で文字列を区切り、2行目以降の先頭が行頭禁則の文字か、
        /// 改行の直前が行末禁則の文字になっている箇所を返す。リッチテキストのタグは飛ばして判定する。
        /// 改行のない文字列は何も返さない（自動の折り返しは描画側の禁則に任せる）。
        /// </summary>
        public static List<ManualBreakViolation> FindManualBreakViolations(string text)
        {
            var violations = new List<ManualBreakViolation>();
            if (string.IsNullOrEmpty(text)) return violations;

            string normalized = BreakTagPattern.Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'), "\n");
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string visible = LocaloTextPatterns.StripTags(lines[i]);
                if (visible.Length == 0) continue;

                if (i > 0 && IsLineStartProhibited(visible[0]))
                    violations.Add(new ManualBreakViolation(true, i + 1, visible[0]));

                char last = visible[visible.Length - 1];
                if (i < lines.Length - 1 && IsLineEndProhibited(last))
                    violations.Add(new ManualBreakViolation(false, i + 1, last));
            }
            return violations;
        }

        /// <summary>
        /// 空行（連続する2つ以上の改行）で区切ったページの数。文字のないページは数えない。
        /// 空の文字列は0、空行のない文字列は1を返す。
        /// </summary>
        public static int CountPages(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            string normalized = BreakTagPattern.Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'), "\n");
            int pages = 0;
            foreach (string page in PageBreakPattern.Split(normalized))
                if (LocaloTextPatterns.StripTags(page).Trim().Length > 0) pages++;
            return pages;
        }

        /// <summary>required のうち、actual に含まれない文字を順に並べて返す（無ければ空文字）。</summary>
        public static string MissingCharacters(string required, string actual)
        {
            if (string.IsNullOrEmpty(required)) return string.Empty;
            actual = actual ?? string.Empty;
            var missing = new StringBuilder();
            foreach (char character in required)
            {
                if (actual.IndexOf(character) < 0 && missing.ToString().IndexOf(character) < 0)
                    missing.Append(character);
            }
            return missing.ToString();
        }

        /// <summary>メッセージに出すための、文字の見える形（空白や制御文字はコードで示す）。</summary>
        internal static string Describe(char character) => LocaloTextPatterns.DescribeCharacter(character);
    }
}
