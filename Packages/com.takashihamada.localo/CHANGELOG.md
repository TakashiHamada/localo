# Changelog

All notable changes to Localo are documented in this file.

## [Unreleased]

### Added

- CSV export / import API for translator review workflows (`Localo.Editor.LocaloCsv`), with dry run, per-column filters, baseline-based conflict detection, and batch-mode entry points (`LocaloCsvCommandLine`).
- Translation tab: per sheet, a Text Type, free-form Translation Guidelines (speaker, personality, tone), and a Translator Lock. Locked language columns show `■` in the Table tab; CSV import rejects any change to them unless a human sets `AllowTranslatorCellChanges` (`-localoAllowTranslatorChanges` in batch mode).
- Glossary tab for project-wide terms, their official spelling per language, and notes.
- Per-row AI-researched Scene (`aiScene`) and Display Space (`aiDisplaySpace`). The details icon right of `Comment` in the Table tab opens them in the Localo Details window; it is reachable with `h`/`l` and opens with `Enter` or `i`.
- `LocaloTranslationContext`: `ExportToString` / `ExportRowToString` write the context JSON for AI translation (schema version 4), and `SetRowContext` saves a row's scene and display space.
- Read-only Language Guidelines in the Translation tab: target regions and researched guidance for eight languages, with collapsible common rules and sources. The same packaged JSON supplies the preview and AI context; optional `targetLanguages` limits exported guides to enabled targets. AI instructions include inferring a consistent sheet tone for each language and speaker/listener relationship.
- The project AI skill template describes the translation context, the Translator Lock, and how to research a key's scene and display space.

### Changed

- The Guide tab is now the Localo Guide window, opened with the `?` button at the right end of the toolbar.
- The editor window loads the settings from the fixed path `Assets/LocaloAssets/Resources/LocalizationSettings.asset`, and remembers the selected table per project. The previously selected table is forgotten once after updating.
- Editor help texts and dialogs are in Japanese.

## [0.1.0] - 2026-08-03

- Initial public UPM package.
- ScriptableObject translation settings and tables.
- TextMesh Pro bindings for Canvas and 3D text.
- Keyboard-first localization table editor.
- Runtime language switching and pluggable system-language provider.
- Optional project-local AI skill installer.
