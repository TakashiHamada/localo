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

## 翻訳設定とAI調査結果

`Setup → Table → Translation → Guide` の順にタブが並びます。「Translation」では、シートごとの自由記述の翻訳方針と、全シート共通の用語集（原文・各言語の正式表記・意味や補足）を編集できます。

ボタン・タブ・項目名・見出しは英語、ツールチップ・補足説明・ダイアログ本文・Guideの説明文は日本語で表示します。
ツール側の説明文は、特定の作品・キャラクター・操作機器を前提とせず、入力する情報の役割を説明します。

Tableの各行の `Comment` の右にあるメモアイコンから、AIが調査した使用状況や文脈を確認・修正できます。`Comment` は人間が書くメモとして残し、AIは編集しません。自動調査・自動翻訳を実行するボタンではありません。
メモアイコンも左右キーまたは `h` / `l` で選択できます。選択先が見える位置まで自動でスクロールし、`Enter` または `i` で詳細を開きます。アイコン列は編集・コピー・貼り付け・セル内容の消去には含まれません。

画面のJSONコピーボタン、または以下のEditor APIで、翻訳方針・用語集・文章・人間のコメント・AI調査結果をまとめて取得できます。

AI向けの共通指示には、原文の意味・使用場面・口調を保ち、特にボタンや短いUI文言を原文とおおむね同じ表示幅・行数に収める方針を含めます。文字数は目安とし、意味を損なう短縮は避けます。両立が難しい文言は理由と短い代案を作業報告に出し、実画面での収まりは人間が確認します。

```csharp
using Localo.Editor;

string allContext = LocaloTranslationContext.ExportToString();
string rowContext = LocaloTranslationContext.ExportRowToString(table, "MY_KEY");
LocaloTranslationContext.SetResearch(table, "MY_KEY", researchText);
```

調査結果は `LocalizationRow.aiResearch`、シートの方針は `LocalizationTable.translationInstructions`、用語集は `LocalizationSettings.glossary` に保存します。変更はUndoに対応します。CSVは従来の列構成を維持し、方針・用語集・調査結果の受け渡しにはJSON APIを使います。

### 翻訳者担当の列を保護する

Translationタブでシートを選び、**Translator Lock** で翻訳者が担当する言語にチェックします。設定はシートの `translatorLanguages` に保存され、その言語の列全体（空欄・今後追加する行も含む）をAIの上書きから保護します。Tableでは対象セルに `■` を付けて表示します。人間はTableで文章を修正できます。チェックを外すと保護を解除できます。

入力済みの翻訳者の訳は、シートの方針・原文を含む各言語の文章・人間のコメントと一緒に、コンテキストJSONの `translatorExamples` に入ります。行単位の書き出しでも全シートのお手本を含み、AIは同じ言語・近い文脈の表現を参考にします。JSONのスキーマはversion 2です。

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
