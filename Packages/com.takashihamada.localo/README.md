# Localo package

Localo is a ScriptableObject-based localization editor and runtime for Unity 6.3 LTS.

## Start

1. Open **Window > Localo**.
2. Click **Quick Setup** on the Setup tab.
3. Enable languages and edit `MainTable` on the Table tab.
4. Add `LocalizeTextMeshProUGUI` or `LocalizeTextMeshPro` to a TMP object and assign a key.

Project-owned data is created under `Assets/LocaloAssets`. Runtime language changes use:

```csharp
LocaloManager.Instance.SetLanguage(SystemLanguage.Japanese);
string text = LocaloManager.Instance.GetLocalizedString("MY_KEY");
```

## 翻訳設定と行の文脈

SetupのSource Languageで翻訳元を選ぶと、その言語がTableとGlossaryの先頭に固定されます。Source Termも選んだ言語で記入し、各言語の列に正式表記を入力します。翻訳元を切り替えても既存の文章やSource Termは書き換えません。Englishは実行時のフォールバックとして有効なまま保持します。AI向けJSONの`sourceLanguage`に翻訳元を含め、CSVのプレースホルダー比較もこの言語を使います。

`Setup → Glossary → Table → Translation` の順にタブが並びます。「Translation」では、シートごとの文章の種類（Text Type）と自由記述の翻訳方針を編集できます。「Glossary」では、全シート共通の用語集をTableと同じ表形式・検索・キーボード操作・TSVコピー＆ペーストで編集できます。列はSource Term・各言語の正式表記・Notesです。列の表示・順番・幅はTableと共有し、既存の用語集データをそのまま使用します。右端の×で用語を行ごと削除でき、Undoで復元できます。AIは翻訳前に用語集全体を読み、翻訳後に登録用語が守られているか確認します。使い方とショートカットの一覧は、ツールバー右端の「?」ボタンで別ウィンドウ（Localo Guide）に表示します。

文脈は2段で持ちます。シートは文章の種類と声（話者・口調）を持つ大きい文脈で、話者や基本の口調が変わる場合はシートを分けます。各行は、表示される場面（Scene）と、文章が入る器（Display Space：部品・幅・行数・表示時間など）を細かい文脈として持ちます。場面と器はAIがキーをもとに調査して書き、人間は確認します。

ボタン・タブ・項目名・見出しは英語、ツールチップ・補足説明・ダイアログ本文・Guideウィンドウの説明文は日本語で表示します。
ツール側の説明文は、特定の作品・キャラクター・操作機器を前提とせず、入力する情報の役割を説明します。

Tableの各行の `Comment` の右にある詳細アイコンから、AIが調査した場面と器を確認・修正できます。`Comment` は人間が書くメモとして残し、AIは編集しません。自動調査・自動翻訳を実行するボタンではありません。
詳細アイコンも左右キーまたは `h` / `l` で選択できます。選択先が見える位置まで自動でスクロールし、`Enter` または `i` で詳細を開きます。アイコン列は編集・コピー・貼り付け・セル内容の消去には含まれません。

画面のJSONコピーボタン、または以下のEditor APIで、文章の種類・翻訳方針・用語集・文章・人間のコメント・場面・器をまとめて取得できます。

AI向けの共通指示には、原文の意味・使用場面・口調を保ち、特にボタンや短いUI文言を原文とおおむね同じ表示幅・行数に収める方針を含めます。文字数は目安とし、意味を損なう短縮は避けます。両立が難しい文言は理由と短い代案を作業報告に出し、実画面での収まりは人間が確認します。

```csharp
using Localo.Editor;

string allContext = LocaloTranslationContext.ExportToString();
string rowContext = LocaloTranslationContext.ExportRowToString(table, "MY_KEY");
LocaloTranslationContext.SetRowContext(table, "MY_KEY", sceneText, displaySpaceText);
```

場面は `LocalizationRow.aiScene`、器は `LocalizationRow.aiDisplaySpace`、文章の種類は `LocalizationTable.textType`、シートの方針は `LocalizationTable.translationInstructions`、用語集は `LocalizationSettings.glossary` に保存します。`SetRowContext` は `null` を渡した項目を変更しません。変更はUndoに対応します。旧版の `aiResearch` の内容は `aiScene` として読み込まれ、`SetResearch` は場面を保存する互換APIとして残しています。CSVは従来の列構成を維持し、文章の種類・方針・用語集・場面・器の読み出しにはJSON出力APIを使います。場面・器の保存にはSetRowContextを使い、用語集専用の取り込みAPIはありません。JSONのスキーマはversion 5です。

### 言語別ガイドと口調の統一

Translationタブの **Language Guidelines** は、8言語の対象地域と注意点を閲覧するプレビューです。共通ルールと出典は折りたたみで確認できます。作品固有の地域・口調は既存の **Translation Guidelines** に記入します。プレビューの言語選択は設定アセットを変更しません。

画面とAI向けJSONは、同梱の `Editor/Resources/LocaloLanguageGuidelines.json` を共通で参照します。既定の地域はen-US・ko-KR・zh-CN・zh-TW・de-DE・fr-FR・es-419・pt-BRです。**Copy All Context as JSON** は、プレビューで選んだ言語に関係なく、有効な全言語のガイドを `languageGuidelines` に含めます。

AIには、翻訳前にシート全体の原文と既存訳を読み、言語ごと・相手との関係ごとに口調を決めて統一する手順を渡します。人間の指定と翻訳者の確定訳を優先し、推定した方針と未解決の矛盾は作業報告に記載します。対象シート全体が未確認なら全体のJSONを取得し、確認後は行単位のJSONも使えます。

APIは `ExportToString(targetLanguages: new[] { SystemLanguage.Korean })` のように、ガイドを有効な対象言語だけに絞れます。`ExportRowToString` も同じ引数を持ちます。この引数はガイドだけを絞り、原文・用語集・翻訳者のお手本は削除しません。配布用Skillを導入済みのプロジェクトは、SetupからSkillを更新すると新しい参照手順も反映されます。

### 翻訳者担当の列を保護する

Translationタブでシートを選び、**Translator Lock** で翻訳者が担当する言語にチェックします。設定はシートの `translatorLanguages` に保存され、その言語の列全体（空欄・今後追加する行も含む）をAIの上書きから保護します。Tableでは対象セルに `■` を付けて表示します。人間はTableで文章を修正できます。チェックを外すと保護を解除できます。

入力済みの翻訳者の訳は、シートの方針・原文を含む各言語の文章・人間のコメントと一緒に、コンテキストJSONの `translatorExamples` に入ります。行単位の書き出しでも全シートのお手本を含み、AIは同じ言語・近い文脈の表現を参考にします。

CSV取り込みも既定で保護対象への変更をerrorにし、ほかの変更を含めて何も書き込みません。同じ訳が入った列は変更扱いになりません。翻訳者の納品CSVや確定訳の修正を**人間が意図して取り込む場合だけ**、dry-run・本番ともに `AllowTranslatorCellChanges = true`（batch modeでは `-localoAllowTranslatorChanges`）を指定します。ロックは解除されません。AIはこの指定を使いません。

## CSV export / import (scripts and AI tools)

`Localo.Editor.LocaloCsv` exports every registered table to CSV and imports a reviewed CSV back. There is no UI; call it from editor scripts, AI tools that can run editor C#, or batch mode.

```csharp
using Localo.Editor;
using UnityEngine;

LocaloCsv.ExportToFile("Localization/review.csv");
LocaloCsvReport preview = LocaloCsv.ImportFromFile("Localization/review-done.csv",
    new LocaloCsvImportOptions { DryRun = true, BaselineCsvPath = "Localization/review.csv" });
Debug.Log(preview); // Review the listed changes, then import again without DryRun.
```

Columns are `Table`, `Key`, one column per enabled language, and `Comment`. Existing keys are overwritten cell by cell, rows missing from the CSV are kept, and unknown keys are skipped unless `AddMissingKeys` is set. See the repository README for all options.

Have reviewers save as UTF-8 (Google Sheets, or Excel "CSV UTF-8"); other encodings are rejected. Excel may turn cells starting with `+`, `-` or `=` into formulas, which the dry run exposes as changes.

See the [repository README](https://github.com/TakashiHamada/localo#readme) for installation, shortcuts, fallback behavior, and the development sample.
