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

The **Window > Localo** window has four tabs: Setup → Glossary → Table → Translation. The `?` button at the right end of the toolbar opens the Localo Guide window with the workflow summary and all shortcuts.

- **Setup**: create the settings, choose the Source Language, register tables (sheets), choose languages and column widths, install the AI project skill.
- **Glossary**: edit project-wide terms in the same grid as Table, using **Source Term**, language columns, and **Notes**. Search, keyboard editing and TSV clipboard operations work the same as Table; each tab keeps its own search, selection and scroll position. Visible languages, column order and widths are shared. Write the Source Term (the term or row identifier) in the source language, and enter the official text in each language column. The rightmost × deletes the term; Undo restores it.
- **Table**: edit keys, translations and the human `Comment` in a keyboard-first grid. The icon right of `Comment` (the details icon) opens the row's AI-researched Group, Scene and Display Space.
- **Translation**: what humans write for translation, in two scopes.
  - **Project** (all sheets): the **Project Profile** (audience, tones, Japanese kanji range), the **Target Platforms**, and the **Project Lessons** (free-form notes per target language for what this game has settled on in that language, such as lessons from translator revisions).
  - **Sheet**: the **Translation Guidelines** (kind of text, speaker, personality, tone) and the **Translator Lock**. Locked language columns are shown with `■` in the Table tab and are never changed by AI.

## Where each kind of knowledge lives

Each kind of knowledge has exactly one home. Humans write on screen; AI uses the skill and editor APIs.

| Knowledge | Home | Written by |
|---|---|---|
| How AI works: steps, priorities, prohibitions | Project skill `SKILL.md` | Package |
| Translation craft, common and per language | Project skill `translation-guide.md` | Package |
| Mechanical text QA | `LocaloQa` API; the skill says when to run it | Package |
| Official platform wording, kanji grades | Bundled JSON (`Editor/Resources`) | Package |
| The whole game: audience, tone, per-language lessons | Translation tab > Project | Human |
| Terms | Glossary tab | Human |
| A sheet's voice | Translation tab > Sheet | Human |
| A row's memo | `Comment` column | Human |
| A row's scene, display space and group | Localo Details | AI, checked by a human |

## Context for AI translation

`Localo.Editor.LocaloTranslationContext` exports the data AI needs as JSON (schema version 9). The JSON holds data only; how to read it and how to translate are in the project skill.

```csharp
using Localo.Editor;

string all = LocaloTranslationContext.ExportToString();            // every sheet and row, plus project-wide data
string group = LocaloTranslationContext.ExportGroupToString("MY_GROUP");
string row = LocaloTranslationContext.ExportRowToString(table, "MY_KEY");
LocaloTranslationContext.SetRowContext(table, "MY_KEY", sceneText, displaySpaceText);
LocaloTranslationContext.SetRowGroups(new[] { new LocaloRowGroupAssignment("MY_KEY", "MY_GROUP", "2-A") });
```

- The full export contains `sourceLanguage`, `glossary`, `projectProfile`, `languageLessons`, `platformTerms`, `groups` and every sheet with its `translationInstructions`, `translatorLanguages` and rows (`translations`, `humanComment`, `aiScene`, `aiDisplaySpace`, `aiGroup`, `aiGroupStep`).
- Row and group exports contain only the target rows plus the glossary, Project Profile and Project Lessons. A row export includes the row's whole group and marks the target with `focusKey`.
- Translator examples are the filled cells of the columns listed in each sheet's `translatorLanguages`.
- **Copy All Context as JSON** on the Translation tab copies the full export.
- `SetRowContext` saves a row's scene and display space (`null` leaves a field unchanged). `SetRowGroups` saves groups in bulk and writes nothing when any assignment is invalid. Both support Undo and never touch comments or translations.

A sheet is one voice: split sheets when the speaker or the base tone changes. Rows shown in sequence (one conversation, or one screen's question and answers) form a group across sheets; a step alternates numbers and branch letters (`1`, `2`, `2-A`, `2-A-1`, `2-B`, `3`). The Table tab tints rows of the same group with the same color.

### Project Profile and Target Platforms

**Project Profile** sets the audience, tones and, when Japanese is enabled, the Japanese kanji range for the whole game. The kanji range follows the audience unless chosen explicitly. Only option ids are saved in `LocalizationSettings.projectProfile`; the options and the guidance text given to AI live in the package (`Editor/Resources/LocaloProjectProfileOptions.json`). The guidance for the selected options is shown below the fields.

**Target Platforms** selects platforms whose official button and feature names are bundled in `Editor/Resources/LocaloPlatformTerms.json` (currently Nintendo Switch 2). The data comes from public product pages only, not from developer terminology guidelines such as lotcheck; enter terms from official guidelines in the project Glossary, which takes priority. Simplified Chinese entries are marked `reference` because there is no official Simplified Chinese page.

## QA for AI and automation

`Localo.Editor.LocaloQa` finds text problems that can be judged mechanically from the tables and assets. It has no window: AI runs it when needed (the project skill says when) and reports the result. Checks never render the screen; visual quality stays a human check.

```csharp
using Localo.Editor;
using UnityEngine;

string all = LocaloQa.Report();
string mine = LocaloQa.Report(keys: new[] { "KEY_A" }, languages: new[] { SystemLanguage.Korean });
string some = LocaloQa.Report(checkIds: new[] { "missing-key", "missing-glyph" });
string checks = LocaloQa.DescribeChecks();
```

- `Report` returns the counts per severity on the first line, then one issue per line, most severe first. `keys` and `languages` narrow the issues that point to a table row; issues that point to an asset or a setting are always kept.
- `DescribeChecks` lists every check id with what it finds and what it cannot see. Each check's description lives only in its class (`Editor/LocaloQa*Check.cs`).
- `RunAll()` and `Run(checkId)` return the issues as objects (severity, check id, message, location).
- Severity: **Error** breaks at runtime, **Warning** is likely visible on screen, **Info** may be intentional.
- Batch mode: `Unity -batchmode -quit -projectPath <project> -executeMethod Localo.Editor.LocaloQaCommandLine.Run`, with optional `-localoQaChecks a,b`, `-localoQaKeys A,B`, `-localoQaLanguages Japanese,Korean` and `-localoQaOut <path>`. The process exits with code 1 when any Error is reported.

Check ids: `missing-translation`, `missing-key`, `placeholder-mismatch`, `tag-mismatch`, `invalid-character`, `missing-glyph`, `manual-line-break-kinsoku`, `page-count`, `whitespace`, `punctuation`, `platform-terms`, `kanji-range`, `groups`, `legacy-text-usage`, `line-break-settings`. Add a check by implementing `ILocaloQaCheck` and registering it in `LocaloQa.Checks`.

Use TextMesh Pro (`TextMeshProUGUI` / `TextMeshPro`) or UI Toolkit instead of legacy Text, which has no line-breaking rules. In both TMP and UI Toolkit, **Following Characters** are characters that cannot start a line and **Leading Characters** are characters that cannot end a line.

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

- `SKILL.md`: the data contract, the translation steps, one priority table, one list of prohibitions, how to research a row's scene, display space and group, when to run QA, the CSV workflow and runtime integration.
- `translation-guide.md`: translation craft for all languages and for eight targets (en-US, ko-KR, zh-CN, zh-TW, de-DE, fr-FR, es-419, pt-BR), with sources. **Open Bundled Translation Guide** on the Translation tab opens the same text.

The skill keeps reusable Localo code separate from project data and requires platform-specific or confidential integrations to stay in the consuming project.

## Development sample

Clone this repository and open it with Unity 6.3 LTS. The `Assets/Samples/LocaloDemo` scene demonstrates English/Japanese switching, Canvas and 3D TextMesh Pro bindings, direct string lookup, and fallback behavior.

## License

Localo is available under the [MIT License](LICENSE).
