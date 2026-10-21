# Changelog

All notable changes to Localo are documented in this file.

## [Unreleased]

- Two-level translation context: each sheet has a Text Type (selected in the Translation tab) alongside its guidelines, and each row has AI-researched Scene (`aiScene`) and Display Space (`aiDisplaySpace`) fields. Legacy `aiResearch` data loads into `aiScene`.
- The Localo Details window now shows only the Scene and Display Space fields; the row-level Copy Row Context as JSON button was removed (use `ExportRowToString` or Copy All Context as JSON).
- `LocaloTranslationContext.SetRowContext` saves a row's scene and display space; `SetResearch` is now obsolete. Context JSON schema version 3.
- The project skill template adds a step-by-step procedure for researching a key's scene and display space.
- CSV export / import API for translator review workflows (`Localo.Editor.LocaloCsv`), with dry run, per-column filters, baseline-based conflict detection, and batch-mode entry points.

## [0.1.0] - Unreleased

- Initial public UPM package.
- ScriptableObject translation settings and tables.
- TextMesh Pro bindings for Canvas and 3D text.
- Keyboard-first localization table editor.
- Runtime language switching and pluggable system-language provider.
- Optional project-local AI skill installer.
