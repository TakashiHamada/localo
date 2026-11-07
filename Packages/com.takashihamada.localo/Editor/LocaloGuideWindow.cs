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
                        "2. プロジェクトで使う言語を有効にし、Source Languageで翻訳元の言語を選びます。翻訳元はTableとGlossaryの先頭に固定されます。\n" +
                        "3. TableとGlossaryに表示する言語列を選びます。非表示の列もデータは保持されます。\n" +
                        "4. 言語列の順番や列幅を調整します。\n" +
                        "5. SetupのRegistered Tablesにある「+ Add Table」でシートを追加できます。\n" +
                        "6. Project Profileで作品全体の対象年齢・ジャンル・トーンと日本語の漢字の範囲を、Target Platformsで対象の機種を選びます。AIの翻訳・仮の文字の基準と、QAに使います。",
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
                        "Language Guidelinesでは、8言語の対象地域と翻訳の注意点を閲覧できます。共通の注意点と出典は折りたたみで表示します。\n" +
                        "AI向けJSONには、翻訳前にシート全体の原文と既存訳を確認し、言語ごとの口調を推定する手順を含めます。JSONを渡すAIが、人間の指定と翻訳者の確定訳を優先して翻訳します。\n" +
                        "TranslationタブのCopy All Context as JSONで、文章の種類・方針・用語集・コメント・場面・器と、有効な言語のガイドをまとめてAIに渡せます。",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Glossary --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "Glossaryタブで、全シート共通の用語集をTableと同じ操作で編集できます。\n" +
                        "Keyの代わりにSource Term、Commentの代わりにNotes（意味・使い方）を表示し、その間に各言語の正式表記を並べます。\n" +
                        "Source Termには、翻訳元の言語で用語や行の意味を記入します。実際の正式表記は各言語の列に記入します。\n" +
                        "列の表示・順番・幅はSetupの設定を共有します。+ Add Termまたはoで用語を追加し、iまたはダブルクリックで編集できます。\n" +
                        "右端の×をクリックすると用語を行ごと削除します。×を選択してEnterまたはiでも削除でき、Undoで元に戻せます。\n" +
                        "AI向けJSONには、翻訳前に用語集全体を読み、翻訳後に登録用語の使用を確認する手順を含めます。\n" +
                        "Platform Termsで、SetupのTarget Platformsで選んだ機種のボタン名などの公式表記を閲覧できます（同梱データ・閲覧専用）。用語集に同じ語を書くと、用語集が優先されます。",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- QA --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "QAタブで、文字に関する機械的なチェックを実行できます。Run Allで登録されたすべてのチェックを一括で、各チェックのRunでそのチェックだけを実行します。\n" +
                        "指摘の場所のボタンを押すと、Tableのそのセル、シーン・プレハブ、またはスクリプトのその行へ移ります。\n" +
                        "重大度: Errorは実行時に壊れるもの、Warningは画面に出る可能性が高いもの、Infoは意図的な場合もあるものです。\n" +
                        "Missing Translations: 原文があるのに訳が空欄の行を探します。\n" +
                        "Missing Keys: シーン・プレハブ・スクリプトで使っているキーが、どのテーブルにも無い箇所を探します。\n" +
                        "Placeholder Mismatch: 原文と訳で {0} などの組が違う行を探します。\n" +
                        "Tag Mismatch: 原文と訳で <color> などのタグが違う行や、閉じ忘れたタグを探します。\n" +
                        "Invalid Characters: タブ・ゼロ幅文字・文字化けの記号など、見えない文字や表示できない文字を探します。\n" +
                        "Missing Glyphs: TMPの既定のフォントと代替フォントに無い（□になる）文字を探します。\n" +
                        "Whitespace: 先頭・末尾の空白の有無が原文と違う行や、連続した半角スペースを探します。\n" +
                        "Punctuation: 中国語・日本語の文中の半角の , . や、韓国語・欧米の言語に混じった全角の句読点を探します。\n" +
                        "Manual Line Breaks: 手で入れた改行（<br>・\\n）の直後が「、」「。」などで始まる訳文を探します。手で入れた改行には禁則処理が効きません。\n" +
                        "Platform Terms: 選んだ機種のボタン名・機能名について、同梱の一覧で公式ではないとした書き方を探します。\n" +
                        "Kanji Range: 日本語の列で、Project Profileの漢字の範囲（例: 小2まで）を超える漢字を探します。\n" +
                        "Legacy Text Usage: legacy Text（UnityEngine.UI.Text）と TextMesh の使用箇所を探します。legacy Text には禁則処理が無いため、TextMesh Pro か UI Toolkit を使ってください。\n" +
                        "Line Break Settings: TMP Settings と PanelSettings（UI Toolkit）に、行頭禁則・行末禁則のリストが割り当てられているかを調べます。\n" +
                        "チェックは画面を使いません。表示の見た目は人間が確認してください。",
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
