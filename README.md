# Localo

Localo is a lightweight localization editor and runtime for Unity 6.3 LTS. It stores translations in ScriptableObject tables, provides a keyboard-first table editor, binds TextMesh Pro components to translation keys, and switches languages at runtime without scene setup.

## Requirements

- Unity 6.3 LTS
- Git 2.14 or newer for Git URL installation
- TextMesh Pro / uGUI 2.0.0

## Install with Unity Package Manager

Open **Window > Package Management > Package Manager**, choose **Install package from Git URL**, and enter:

```text
https://github.com/TakashiHamada/localo.git?path=/Packages/com.takashihamada.localo
```

For a reproducible project, pin a released tag:

```text
https://github.com/TakashiHamada/localo.git?path=/Packages/com.takashihamada.localo#v0.1.0
```

## Quick setup

1. Open **Window > Localo**.
2. On the **Setup** tab, click **Quick Setup**.
3. Localo creates `Assets/LocaloAssets/Resources/LocalizationSettings.asset` and `Assets/LocaloAssets/Tables/MainTable.asset`.
4. Enable the project languages and edit translations on the **Table** tab.

Choose the translation source in Setup > Source Language. It stays first and visible in both Table and Glossary. English remains enabled as the runtime fallback. Project translation data remains under `Assets/LocaloAssets`; updating the package does not replace it.

## Bind TextMesh Pro text

- Add `LocalizeTextMeshProUGUI` to a Canvas `TextMeshProUGUI` object.
- Add `LocalizeTextMeshPro` to a 3D `TextMeshPro` object.
- Set **Localization Key** to a key registered in a Localo table.

The component refreshes when the current language changes. In Edit Mode, **Use Test Language** previews any enabled language without changing Play Mode state.

## Runtime API

```csharp
using Localo.Runtime;
using UnityEngine;

LocaloManager.Instance.SetLanguage(SystemLanguage.Japanese);
string title = LocaloManager.Instance.GetLocalizedString("SAMPLE_TITLE");
```

`LocaloManager` is created automatically before the first scene. Do not add it manually.

To integrate a platform-specific language API, register a project-owned provider before the first scene loads:

```csharp
LocaloManager.SetSystemLanguageProvider(ResolvePlatformLanguage);
```

Keep platform SDK code in the consuming project. Localo itself only depends on Unity APIs.

## Resolution and validation rules

- Text resolves from the requested language, then English.
- A missing key logs a warning and returns the key.
- A registered row with an empty English value remains empty.
- Duplicate keys log a warning; the first registered table wins.
- Store intentional line breaks as `<br>`.

## Editor tabs

The **Window > Localo** window has five tabs in this order: Setup → Glossary → Table → Translation → QA. The `?` button at the right end of the toolbar opens the Localo Guide window with all shortcuts.

- **Setup**: create the settings, choose the Source Language, set the **Project Profile** and **Target Platforms**, register tables (sheets), choose languages and column widths, install the AI project skill.
- **Glossary**: edit project-wide terms in the same grid as Table, using **Source Term**, language columns, and **Notes**. Search, keyboard editing and TSV clipboard operations work the same as Table; each tab keeps its own search, selection and scroll position. Visible languages, column order and widths are shared. Use the selected source language for Source Term (the term or row identifier), and enter actual official text in each language column. The rightmost × deletes the term; Undo restores it. Existing glossary data is retained. AI instructions require reading the whole glossary before translation and checking terminology afterward. **Platform Terms** opens a read-only window with the bundled official button and feature names for the selected target platforms.
- **Table**: edit keys, translations and the human `Comment` in a keyboard-first grid. The icon right of `Comment` (the details icon) opens the row's AI-researched Scene and Display Space.
- **Translation**: per sheet, choose the Text Type, write the Translation Guidelines (speaker, personality, tone), and set the **Translator Lock**. Locked language columns are shown with `■` in the Table tab and are never changed by AI. **Language Guidelines** previews read-only guidance for eight target languages, including their target regions; common rules and sources are collapsible. **Copy All Context as JSON** includes the same guidance for enabled languages alongside the context for AI translation (`Localo.Editor.LocaloTranslationContext`, schema version 6). The Language Guidelines preview starts with the Project Profile and its guidance. **Project Lessons** (inside Language Guidelines) holds free-form notes per language for what this game has settled on in that language, such as lessons from translator revisions; they apply to every sheet, are exported as `languageLessons` in the context JSON, and take priority over the bundled guidelines. AI never edits them.

- **QA**: run mechanical text checks. **Run All** runs every registered check in one go; each check also has its own **Run** button. Click an issue's location to jump to the Table cell, ping the scene or prefab, or open the script at that line. Checks never render the screen; visual quality stays a human check.

### Project Profile and Target Platforms

**Project Profile** (Setup) sets the audience, genres, tones and, when Japanese is enabled, the Japanese kanji range for the whole game. The kanji range follows the audience unless chosen explicitly. Only option ids are saved in `LocalizationSettings.projectProfile`; the options and the guidance text given to AI live in the package (`Editor/Resources/LocaloProjectProfileOptions.json`), so options can be reordered or reworded without breaking existing settings. The context JSON includes the selected options with their guidance as `projectProfile`, and the AI skill uses it as the baseline for translations and placeholder text.

**Target Platforms** (Setup) selects platforms whose official button and feature names are bundled in `Editor/Resources/LocaloPlatformTerms.json` (currently Nintendo Switch 2). The data comes from public product pages only, not from developer terminology guidelines such as lotcheck; enter terms from official guidelines in the project Glossary, which takes priority. Simplified Chinese entries are marked `reference` because there is no official Simplified Chinese page. The context JSON includes the terms for enabled languages as `platformTerms`.

### QA checks

The same checks are available from code: `Localo.Editor.LocaloQa.RunAll()` returns every issue (severity, check id, message, location), and `LocaloQa.Run(checkId)` runs one check. Add a check by implementing `ILocaloQaCheck` and registering it in `LocaloQa.Checks`; the QA tab shows it automatically.

Checks that compare a translation with its source use the Source Language selected on the Setup tab, and skip rows whose source text is empty. Severity: **Error** breaks at runtime, **Warning** is likely visible on screen, **Info** may be intentional.

| Check | Id | What it finds |
|---|---|---|
| Missing Translations | `missing-translation` | Rows with source text whose translation in an enabled language is empty or whitespace only. Warning. |
| Missing Keys | `missing-key` | Keys used by Localo components in scenes and prefabs (`localizationKey`, including prefab overrides) and by `GetLocalizedString("KEY")`, `SetKey("KEY")` and `LocaloDatabase.GetText("KEY", …)` in scripts that no registered table contains. At runtime the key itself is displayed. Keys built from strings cannot be checked. Error. |
| Placeholder Mismatch | `placeholder-mismatch` | Translations whose `{0}`-style placeholders differ from the source (order and duplicates are ignored; the same rule as CSV import warnings). Error. |
| Tag Mismatch | `tag-mismatch` | Translations whose rich text tags (names and open/close counts) differ from the source, and unbalanced tags. Attribute values (`<color=red>` vs `<color=#f00>`) and standalone tags such as `<br>` and `<sprite>` are not compared. Warning. |
| Invalid Characters | `invalid-character` | Control characters other than `\n` (tabs, `\r`), U+FFFD, zero-width characters and bidirectional controls in any language. NBSP and the ideographic space are allowed. Warning. |
| Missing Glyphs | `missing-glyph` | Characters that the TMP default font asset, its fallbacks and the global TMP fallbacks cannot draw (shown as □). Dynamic font assets are checked against their source font file without being modified. Fonts set per text, UI Toolkit fonts and legacy Text fonts are not covered. Warning. |
| Manual Line Breaks | `manual-line-break-kinsoku` | Translations where a manual line break (`<br>`, `\n`) is followed by a character that must not start a line (`、` `。` `，` closing brackets, small kana, `,` `.` …) or preceded by one that must not end a line (opening brackets). Rich text tags are skipped. Line-breaking rules of TMP and UI Toolkit do not apply to manual breaks. |
| Page Count | `page-count` | Translations whose number of pages differs from the source. A page is a block separated by a blank line (two or more consecutive `<br>` / `\n`). Line breaks inside a page are not compared, so lines can be reflowed for the target language. Warning. |
| Whitespace | `whitespace` | Translations whose leading or trailing whitespace (presence only) differs from the source, which shifts centered or right-aligned text (Warning); leading or trailing whitespace in the source itself and double spaces not in the source (Info). |
| Punctuation | `punctuation` | Punctuation that does not fit the language: half-width `,` `?` `!` `:` `;` and `.` next to Chinese characters, half-width `,` `.` next to Japanese text (Chinese-style `，` `．` in Japanese is Info), full-width `，` `。` `、` in Korean, and full-width characters in other languages (ideographic space, `、` `。`, full-width punctuation, letters and digits). Tags and placeholders are skipped. Warning. |
| Platform Terms | `platform-terms` | Unofficial wording of button and feature names for the target platforms selected in Setup, in every language including the source (for example `マウスモード` instead of `マウス操作`, `Joy-Con2` instead of `Joy-Con 2`, `L-Knopf` instead of `L-Taste`). Only wording listed as `avoid` in the bundled data is found; wording registered in the Glossary is skipped. Info when no platform is selected. Warning. |
| Kanji Range | `kanji-range` | Kanji in the Japanese column above the Project Profile's kanji range (for example grade 2 or Jōyō kanji), based on the elementary school grade allocation table (in effect since 2020) and the Jōyō kanji table. Readings and furigana are not checked. Info when no range is set. Warning. |
| Legacy Text Usage | `legacy-text-usage` | `UnityEngine.UI.Text` and `TextMesh` in scenes and prefabs under `Assets` (read as YAML text, without opening scenes), plus `AddComponent<Text>()`-style code. Legacy Text has no line-breaking rules, so `、` and `。` can start a wrapped line in Japanese and Chinese. Wrapping legacy Text is a warning. |
| Line Break Settings | `line-break-settings` | Whether TMP Settings and every `PanelSettings` asset under `Assets` have line-breaking character lists. UI Toolkit applies no line-breaking rules unless `PanelSettings.textSettings` references a Panel Text Settings asset with Line Breaking Rules (the default list is empty in Unity 6.3). `PanelSettings` created at runtime cannot be checked. |

Use TextMesh Pro (`TextMeshProUGUI` / `TextMeshPro`) or UI Toolkit instead of legacy Text. TMP Essential Resources assign line-breaking rules by default. In both TMP and UI Toolkit, **Following Characters** are characters that cannot start a line and **Leading Characters** are characters that cannot end a line.

The shared guide data lives in `Editor/Resources/LocaloLanguageGuidelines.json` inside the package. The defaults are US English, Korean (South Korea), Simplified Chinese (mainland China), Traditional Chinese (Taiwan), German (Germany), French (France), Latin American Spanish (`es-419`), and Brazilian Portuguese (`pt-BR`). Project-specific instructions and translator-approved text take priority. Before translating, the AI should read the whole sheet, infer a tone for each language and speaker/listener relationship, keep that tone consistent, and report its inferred policy and unresolved conflicts.

Both context export APIs accept an optional `targetLanguages` argument to limit the included language guides to enabled targets, for example `LocaloTranslationContext.ExportToString(targetLanguages: new[] { SystemLanguage.Korean })`. This filters only the guides; source text, glossary entries, and translator examples remain available. Confirm the whole target sheet before using row-only JSON to translate individual lines. The preview language selection does not change project settings or the languages included by **Copy All Context as JSON**.

The context JSON includes `sourceLanguage`. AI translates from that language to the other languages, preserves the source text, and reports missing source text. Changing the source language reorders columns without rewriting existing translations or Source Terms. CSV placeholder warnings also compare against the selected source.

## Table and Glossary shortcuts

| Keys | Action |
| --- | --- |
| `h` `j` `k` `l` or arrows | Move selection |
| `Shift` + movement | Extend selection |
| `i` or double-click | Edit a cell |
| `Enter` or `Esc` | Commit editing |
| `Enter` or `i` on the details icon | Open the row's Scene and Display Space (Table) |
| Click `×`, or select it and press `Enter` / `i` | Delete the term; Undo restores it (Glossary) |
| `Esc` `Esc` | Clear the search filter |
| `x`, `Delete`, `Backspace` | Clear selected cells |
| `o` | Insert a row below |
| `d` `d` | Delete a row |
| `/` | Focus search |
| `Ctrl/Cmd` + `C` `X` `V` | Copy, cut, paste TSV |

## CSV export / import

Localo can round-trip translations through CSV so that translators can review them in a spreadsheet. The feature is an editor API (`Localo.Editor.LocaloCsv`) for scripts and AI tools; it has no window UI.

### Review workflow

1. Export the current data and keep the file as the baseline.
2. Send a copy to the translator. They edit cells without changing the `Table` and `Key` columns.
3. Dry-run the import with the baseline and read the report.
4. Import for real. The write is a single Undo step.

```csharp
using Localo.Editor;
using UnityEngine;

LocaloCsv.ExportToFile("Localization/review.csv");

var options = new LocaloCsvImportOptions
{
    DryRun = true,
    BaselineCsvPath = "Localization/review.csv",
    Languages = new[] { SystemLanguage.Japanese }, // optional: only these language columns
    ImportComments = false,                        // optional: keep comments as they are
};
LocaloCsvReport report = LocaloCsv.ImportFromFile("Localization/review-done.csv", options);
Debug.Log(report);

options.DryRun = false;
report = LocaloCsv.ImportFromFile("Localization/review-done.csv", options);
```

`ExportToString` and `ImportFromString` do the same without touching files.

### Format

- UTF-8 (exported with BOM), RFC 4180 quoting, CRLF line endings. Excel and Google Sheets can open and save it.
- Columns: `Table`, `Key`, one column per enabled language in `enabledLanguages` order, then `Comment`. Language headers use Localo display names (`Chinese (Traditional)`); enum names (`ChineseTraditional`) are also accepted.
- Line breaks stay as `<br>` inside cells.
- Import requires UTF-8. Ask reviewers to use Google Sheets or Excel's **CSV UTF-8** format; files in other encodings (such as Shift_JIS) are rejected with an error.
- Excel may convert cells starting with `+`, `-` or `=` (for example `+{0}`) into formulas. Such damage shows up in the dry-run change list and placeholder warnings.

### Import rules

| Situation | Result |
| --- | --- |
| Key exists | Selected cells are overwritten when they differ |
| Key is in the tables but not in the CSV | Left unchanged (rows are never deleted) |
| Key is in the CSV but not in any table | Skipped and reported; added only with `AddMissingKeys = true` and a `Table` value |
| `Table` value disagrees with the key's table | Error |
| Unknown or disabled column, duplicate key, malformed CSV | Error |
| Baseline given, reviewer left the cell as exported | Skipped, so edits made after the export are kept |
| Baseline given, both the table and the reviewer changed the cell | Conflict: reported and left unchanged |
| A Translator Lock column would change | Error, unless `AllowTranslatorCellChanges = true` (humans only) |
| Empty `Key` with no text (an unfinished row in the table) | Skipped with a warning |
| Empty `Key` with text, same language under two headers, or a table key with leading/trailing spaces | Error |
| CSV file not found or not UTF-8 | Error |

Any error aborts the whole import before anything is written. Warnings flag cleared text, raw line breaks, and placeholders (`{0}`) that differ from the current source-language text, which usually means the CSV is older than the code.

### Batch mode

When the project is not open in an Editor:

```text
Unity -batchmode -quit -projectPath <project> -executeMethod Localo.Editor.LocaloCsvCommandLine.Export -localoCsv review.csv
Unity -batchmode -quit -projectPath <project> -executeMethod Localo.Editor.LocaloCsvCommandLine.Import -localoCsv review-done.csv -localoBaselineCsv review.csv -localoDryRun
```

Import flags: `-localoDryRun`, `-localoLanguages Japanese,Korean`, `-localoNoComments`, `-localoAddMissingKeys`, `-localoBaselineCsv <path>`, `-localoAllowTranslatorChanges` (humans only; same as `AllowTranslatorCellChanges`). The process exits with code 1 when the import reports errors.

## AI project skill

The Setup tab can install a project-local Localo skill at `.agents/skills/localo`. Installation is opt-in and does not modify global AI configuration. Updating the skill asks before replacing existing files.

The generated skill keeps reusable Localo code separate from project data and requires platform-specific or confidential integrations to stay in the consuming project.

## Development sample

Clone this repository and open it with Unity 6.3 LTS. The `Assets/Samples/LocaloDemo` scene demonstrates English/Japanese switching, Canvas and 3D TextMesh Pro bindings, direct string lookup, and fallback behavior.

## License

Localo is available under the [MIT License](LICENSE).
