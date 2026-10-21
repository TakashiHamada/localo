# 翻訳ガイド拡張のリファクタリング記録

- 実施日：2026-10-22。
- 対象：既定ブランチ`origin/main`との分岐点からのパッケージ・資料の変更と、新規の言語別ガイド。分岐点は`953f9542da29117aac66c4b7ed5b1c2623ad2626`。
- Programmer視点とHuman視点のレビューを並列実施した。Unityが生成するアセット・設定・metaはレビュー対象から除き、既存の人間の翻訳データは編集していない。

## 指摘と対応

| 視点 | 優先度 | 指摘 | 対応 |
| --- | --- | --- | --- |
| Programmer | Should | 対象言語を指定した場合だけ、ガイドが引数順となり「設定順」の仕様とずれる | 有効言語を対象集合で絞って設定順を保持。逆順・重複・対象外の言語を含む設定で確認する回帰テストを追加 |
| Programmer | Should | optional引数の追加で旧APIのCLRシグネチャとメソッド参照が使えなくなる | 旧シグネチャのオーバーロードを復元し、新実装へ委譲。旧APIをFuncへ代入するテストを追加 |
| Human | Should | 「AIは口調を推定します」が、Localo内で自動実行する機能の説明に読める | プレビューとGuideを、外部AIへ渡すJSONに推定手順を含める説明へ統一 |
| Human | Should | 「全体を読む」と「判断できないときだけ全体取得」が混在する | 対象シート全体が未確認なら必ず取得し、確認後に行単位JSONを利用する手順へ統一 |
| Human | Should | 設計書に「現行version 3」が残る | 現行は4、旧文脈構造は3から継続と明記 |
| Programmer | Nice | 長いURLをリンクの表示名にしている | 出典名を折り返せるリンクにし、URLはツールチップに移した |
| Human | Nice | 新UI partialの責務が初見で分かりにくい | 閲覧状態だけを保持する旨のコメントを追加し、言語表示名の参照を既存コードと揃えた |

Criticalの指摘はなかった。Shouldはすべて対応した。

Human視点の「出力順の説明を引数順へ変更する」案は採用しなかった。Programmer視点を優先し、既存の設定順の仕様に実装を合わせた。

毎描画時の資料・表示名取得の追加キャッシュと、資料更新時の項目ごとの形式検証はNiceとして保留した。現在は固定の8言語を同梱し、JSON構造・言語の重複・必須データを静的に確認している。外部から資料を追加する機能は今回の対象に含まれない。

## 今回の修正ファイル

- [ガイドプレビュー](../Packages/com.takashihamada.localo/Editor/LocaloEditorWindow.LanguageGuidelines.cs)：説明、責務コメント、出典リンク。
- [ガイド読み出し](../Packages/com.takashihamada.localo/Editor/LocaloLanguageGuidelines.cs)：呼び出し側との順序の責任をコメントで明記。
- [コンテキストAPI](../Packages/com.takashihamada.localo/Editor/LocaloTranslationContext.cs)：旧API互換、設定順、対象引数の説明、シート全体確認の指示。
- [Guide](../Packages/com.takashihamada.localo/Editor/LocaloGuideWindow.cs)：外部AIへの指示であることを明確化。
- [Editorテスト](../Packages/com.takashihamada.localo/Tests/Editor/LocaloTranslationContextTests.cs)：API互換性と対象言語の順序の回帰テスト。
- [配布用Skill](../Packages/com.takashihamada.localo/Editor/Resources/LocaloProjectSkillTemplate.txt)：シート全体の確認手順。
- [ルートREADME](../README.md)、[パッケージREADME](../Packages/com.takashihamada.localo/README.md)、[設計書](ai-translation-design.md)、[調査資料](translation-language-guidelines.md)：手順と現行仕様の整合。
- 本記録。

分岐点からの既存のTable詳細ボタン変更も確認し、追加の修正は不要と判断した。

## 検証と残る確認

- Unity 6000.3.20f1同梱Roslynと、このプロジェクトのBee response fileを使用し、出力先を一時フォルダに変更してコンパイルした。プロジェクトのビルド成果物は置き換えていない。
- `Localo.Editor`：終了コード0、コンパイルエラー・警告なし。
- `Localo.Tests.Editor`：今回コンパイルしたEditor参照で終了コード0、コンパイルエラー・警告なし。
- 同梱JSONの8言語・locale・出典・資料リンク・コード差分の空白を静的に確認した。
- EditModeテストの実行は未実施。このプロジェクトにはuloopが導入されていないため、UnityのTest Runnerで実行する確認が残る。
- 人間の確認：Window > Localo > TranslationでLanguage Guidelinesの言語切替、折りたたみ、出典リンクを確認する。見た目・操作感の検証はAIでは行っていない。
