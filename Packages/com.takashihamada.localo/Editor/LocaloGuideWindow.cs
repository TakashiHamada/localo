using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// 作業の流れの要約とショートカットを表示するウィンドウ。メインウィンドウのツールバー右端の「?」ボタンから開く。
    /// 各項目の説明は、その項目がある画面の HelpBox とツールチップに書く（ここに写しを置かない）。
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
                    EditorGUILayout.LabelField("-- Workflow --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "1. Setup: Quick Setupで設定アセットと最初のシートを作成し、使う言語と翻訳元（Source Language）を選びます。AIスキルもここでインストールします。\n" +
                        "2. Translation: Projectに作品全体の設定（対象年齢・トーン・漢字の範囲、対象の機種、言語ごとのProject Lessons）を、Sheetにシートごとの翻訳方針とTranslator Lockを記入します。\n" +
                        "3. Glossary: 全シート共通の用語の正式表記と、意味・使い方を記入します。\n" +
                        "4. Table: 文章を編集します。Commentは人間のメモで、AIは読むだけです。\n" +
                        "5. AI: スキルを入れたAIが、翻訳、行の場面・器・グループの調査、文字の機械的なチェック（QA）を行い、結果を報告します。",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Table Marks --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "Commentの右の詳細アイコンで、AIが調査したその行のグループ（Group・Step）、場面（Scene）、器（Display Space）を確認・修正できます。行の削除もここからできます。\n" +
                        "同じグループの行は、同じ色で薄く塗られます（Keyにマウスを乗せるとGroupとStepを表示）。\n" +
                        "■の付いたセルは、Translator Lockで翻訳者担当にした列です。AIは変更しません。\n" +
                        "赤いKeyは、登録されたシートの中で重複しているキーです。",
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
                    DrawShortcutRow("× (Glossary)", "用語を行ごと削除します。選択してEnterまたはiでも削除でき、Undoで元に戻せます。");
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
