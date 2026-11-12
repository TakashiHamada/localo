# Changelog

All notable changes to Localo are documented in this file.

## [Unreleased]

Each kind of knowledge now has one home: people write on screen, AI uses the project skill and editor APIs.

### Added

- **Translation tab**, in two scopes.
  - Project (all sheets): the Project Profile (audience, tones and the Japanese kanji range, chosen from bundled options in `Editor/Resources/LocaloProjectProfileOptions.json`; only option ids are saved), the Target Platforms (bundled official button and feature names in `Editor/Resources/LocaloPlatformTerms.json`, Nintendo Switch 2 from public product pages), and the Project Lessons (free-form notes per target language, saved as `LocalizationSettings.languageLessons`).
  - Sheet: free-form Translation Guidelines (kind of text, speaker, personality, tone) and a Translator Lock. Locked language columns show `■` in the Table tab; CSV import rejects any change to them unless a human sets `AllowTranslatorCellChanges` (`-localoAllowTranslatorChanges` in batch mode).
- **Glossary tab** for project-wide terms, their official spelling per language, and notes. It uses the same grid and editing shortcuts as Table, with Source Term and Notes columns and a rightmost × button for deletion with Undo.
- **Per-row context researched by AI**: Scene (`aiScene`), Display Space (`aiDisplaySpace`) and Group (`aiGroup`, `aiGroupStep`, e.g. `1`, `2`, `2-A`, `2-A-1`, `2-B`, `3`) for rows shown in sequence across sheets. The details icon right of `Comment` opens them in the Localo Details window, which also has **Delete Row**. The Table tab tints rows of the same group and shows Group / Step in the Key tooltip.
- **`LocaloTranslationContext`** (context JSON, schema version 9): `ExportToString`, `ExportGroupToString` and `ExportRowToString` export data only (`sourceLanguage`, `glossary`, `projectProfile`, `languageLessons`, `platformTerms`, `groups`, sheets and rows). Row and group exports hold the target rows plus the glossary, profile and lessons; a row export includes the row's whole group and marks the target with `focusKey`. `SetRowContext` and `SetRowGroups` save AI research without touching comments or translations.
- **`LocaloQa`** for AI and automation, with no window: `Report(checkIds, keys, languages)` runs the checks and returns the counts and one issue per line; `DescribeChecks()` lists every check; `RunAll()` / `Run(checkId)` return issue objects; `LocaloQaCommandLine.Run` is the batch-mode entry (exit code 1 on any Error). Checks: Missing Translations, Missing Keys, Placeholder Mismatch, Tag Mismatch, Invalid Characters, Missing Glyphs, Manual Line Breaks, Page Count, Whitespace, Punctuation, Platform Terms, Kanji Range (MEXT data in `Editor/Resources/LocaloKanjiLevels.json`), Groups, Legacy Text Usage and Line Break Settings. New checks implement `ILocaloQaCheck` and are registered in `LocaloQa.Checks`. The CSV placeholder warning and the QA check share `LocaloTextPatterns`.
- **Project AI skill** (`.agents/skills/localo`): `SKILL.md` is the only home of the translation steps, the priority table, the prohibitions, the research procedures and when to run QA. `translation-guide.md` holds translation craft for all languages and eight targets with sources, including guidance learned from professional translator revisions. **Open Bundled Translation Guide** on the Translation tab opens the same text.
- **CSV export / import API** for translator review workflows (`Localo.Editor.LocaloCsv`), with dry run, per-column filters, baseline-based conflict detection, and batch-mode entry points (`LocaloCsvCommandLine`).

### Changed

- Setup selects the translation source language, pins it first and visible in both grids, and exports it as `sourceLanguage`. Source Terms use that language; CSV placeholder checks compare against it. The runtime English fallback is retained.
- Tabs are Setup, Glossary, Table, Translation. The Guide tab is now the Localo Guide window, opened with the `?` button, and holds only the workflow summary and shortcuts.
- The editor window loads the settings from the fixed path `Assets/LocaloAssets/Resources/LocalizationSettings.asset`, and remembers the selected table per project. The previously selected table is forgotten once after updating.
- Editor help texts and dialogs are in Japanese.

### Removed (from earlier unreleased builds)

- The QA tab, the read-only Language Guidelines panel and the Platform Terms window. AI runs `LocaloQa.Report`; the guide is a skill file; platform terms reach AI through the full context export.
- The sheet Text Type (`LocalizationTable.textType`, `LocaloTextType`) and the Project Profile Genre (`LocaloProjectProfile.genres`). Saved values are dropped the next time the asset is saved; write the kind of text in the sheet's Translation Guidelines.
- From the context JSON: the long `instructions` text, `languageGuidelines` and `translatorExamples`, and the `targetLanguages` argument of the export methods. Translator examples are the filled cells of each sheet's `translatorLanguages` columns in the full export.
- `LocaloTranslationContext.SetResearch`. Data saved as `aiResearch` still loads as `aiScene`.

### Fixed

- CSV placeholder warnings now use the source text retained after baseline, conflict, translator lock and language filtering decisions, including new rows.
- Project Lessons can be written for every enabled target language (the language list used to come from the bundled guide, which has no Japanese entry).

## [0.1.0] - 2026-08-03

- Initial public UPM package.
- ScriptableObject translation settings and tables.
- TextMesh Pro bindings for Canvas and 3D text.
- Keyboard-first localization table editor.
- Runtime language switching and pluggable system-language provider.
- Optional project-local AI skill installer.
