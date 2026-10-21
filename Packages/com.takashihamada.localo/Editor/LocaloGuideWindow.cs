using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// Localoの使い方とショートカットを表示するウィンドウ。メインウィンドウのツールバー右端の「?」ボタンから開く。
    /// </summary>
    public sealed class LocaloGuideWindow : EditorWindow
    {
        private static readonly Vector2 WindowMinSize = new Vector2(480f, 400f);
        private const float ShortcutKeyColumnWidth = 160f;

        private Vector2 _scrollPosition;

        public static void Open()
        {
            var window = GetWindow<LocaloGuideWindow>();
            window.titleContent = new GUIContent("Localo Guide");
            window.minSize = WindowMinSize;
            window.Show();
        }

        private void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;

                EditorGUILayout.LabelField("Localo Guide", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Localoはキーボードを中心に操作できる多言語管理ツールです。Setupで初期設定を行い、以下のショートカットを使って編集できます。",
                    MessageType.Info);

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Setup --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "1. Quick Setupで設定アセットと最初のシートを作成します。\n" +
                        "2. プロジェクトで使う言語を選びます。基準言語はEnglishです。\n" +
                        "3. Tableに表示する言語列を選びます。非表示の列もデータは保持されます。\n" +
                        "4. 言語列の順番や列幅を調整します。\n" +
                        "5. SetupのRegistered Tablesにある「+ Add Table」でシートを追加できます。",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Table Basics --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "上部のプルダウンでシートを選び、キーボードまたはマウスでセルを選択します。" +
                        "iキーまたはダブルクリックで編集を開始すると、選択したセルに入力できます。",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Translation & Details --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "Translationタブでシートごとの文章の種類（Text Type）と翻訳方針を編集できます。\n" +
                        "シートは文章の種類と声（話者・口調）の単位です。話者や基本の口調が変わる場合は、シートを分けてください。\n" +
                        "Tableの各行のCommentの右にある詳細アイコンで、AIが調査したその行の場面（Scene）と器（Display Space）を確認・修正できます。\n" +
                        "左右キーまたはh/lで詳細アイコンまで移動すると、自動でスクロールします。Enterまたはiで詳細を開きます。\n" +
                        "Commentは人間が書くメモです。AIは読むだけで編集しません。\n" +
                        "Translationタブでシートを選び、Translator Lockで翻訳者担当の言語にチェックすると、列全体をAIの上書きから保護します。表では■を付けて表示します。\n" +
                        "翻訳者担当の入力済みの訳は、ほかの文章を訳すお手本としてJSONに含まれます。人間はTableで文章を修正できます。\n" +
                        "TranslationタブのCopy All Context as JSONで、文章の種類・方針・用語集・コメント・場面・器をまとめてAIに渡せます。",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Glossary --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "Glossaryタブで、全シート共通の用語集を編集できます。\n" +
                        "固有名詞や共通用語について、原文の表記・各言語の正式表記・意味や表記上の注意点を登録します。登録した内容は全シートの翻訳で参照します。",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Keyboard Shortcuts --", EditorStyles.boldLabel);
                    DrawShortcutRow("h / j / k / l", "左・下・上・右へ移動します。矢印キーでも操作できます。");
                    DrawShortcutRow("Shift + movement", "移動先まで選択範囲を広げます。");
                    DrawShortcutRow("i", "選択したセルの編集を開始します。詳細アイコンでは詳細を開きます。");
                    DrawShortcutRow("<br>", "セル内の改行を表します。");
                    DrawShortcutRow("Enter / Esc", "編集中の内容を確定して通常の選択に戻ります。詳細アイコンを選択中はEnterで詳細を開きます。");
                    DrawShortcutRow("Esc Esc", "検索を解除して表にフォーカスを戻します。");
                    DrawShortcutRow("x / Delete / Backspace", "選択したセルの内容を消去します。行は削除しません。");
                    DrawShortcutRow("o", "現在の行の下に新しい行を挿入します。");
                    DrawShortcutRow("d d", "現在の行を削除します。");
                    DrawShortcutRow("/", "検索欄にフォーカスを移します。");
                    DrawShortcutRow("Ctrl/Cmd + C/X/V", "選択範囲をTSV形式でコピー・切り取り・貼り付けします。");
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Mouse Controls --", EditorStyles.boldLabel);
                    DrawShortcutRow("Click", "セルを選択します。");
                    DrawShortcutRow("Drag", "ドラッグした範囲を選択します。");
                    DrawShortcutRow("Shift + click", "選択開始位置からクリックしたセルまでを選択します。");
                    DrawShortcutRow("Double-click", "クリックしたセルの編集を開始します。");
                }
            }
        }

        private static void DrawShortcutRow(string keys, string description)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(keys, EditorStyles.boldLabel, GUILayout.Width(ShortcutKeyColumnWidth));
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            }
        }
    }
}
