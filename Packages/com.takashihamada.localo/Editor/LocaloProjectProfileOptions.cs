// =============================================================================
// LocaloProjectProfileOptions.cs — 作品プロファイル（対象年齢・ジャンル・トーン・漢字の範囲）の選択肢
// =============================================================================
// 選択肢と、AIに渡す具体的な指針文は Editor/Resources/LocaloProjectProfileOptions.json で管理する。
// プロジェクトの設定（LocalizationSettings.projectProfile）には選択肢のIDだけを保存するので、
// 選択肢の並べ替え・追加・指針文の調整はJSONの編集だけで済み、既存の設定は壊れない。
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;

namespace Localo.Editor
{
    internal static class LocaloProjectProfileOptions
    {
        internal const string RelativePath = "Editor/Resources/LocaloProjectProfileOptions.json";

        /// <summary>漢字の範囲を「対象年齢に合わせる」ときに保存する値。</summary>
        internal const string AutoKanjiRange = "";

        private static string _loadedJson;
        private static ProjectProfileOptionsCatalog _catalog;

        internal static ProjectProfileOptionsCatalog Load() =>
            LocaloPackageFiles.LoadJson(RelativePath, "作品プロファイルの選択肢",
                catalog => catalog.audiences != null && catalog.genres != null && catalog.tones != null && catalog.kanjiRanges != null,
                ref _loadedJson, ref _catalog);

        /// <summary>保存された漢字の範囲。未指定（自動）なら対象年齢の既定の範囲。どちらも無ければ null。</summary>
        internal static ProjectProfileKanjiRange ResolveKanjiRange(LocaloProjectProfile profile)
        {
            if (profile == null) return null;
            ProjectProfileOptionsCatalog catalog = Load();
            string id = profile.kanjiRange;
            if (string.IsNullOrEmpty(id))
                id = catalog.audiences.FirstOrDefault(audience => audience.id == profile.audience)?.kanjiRange;
            return string.IsNullOrEmpty(id) ? null : catalog.kanjiRanges.FirstOrDefault(range => range.id == id);
        }

        /// <summary>何か1つでも選ばれているか。</summary>
        internal static bool HasAnySelection(LocaloProjectProfile profile) =>
            profile != null && (!string.IsNullOrEmpty(profile.audience) ||
                                (profile.genres?.Count ?? 0) > 0 || (profile.tones?.Count ?? 0) > 0 ||
                                !string.IsNullOrEmpty(profile.kanjiRange));

        /// <summary>
        /// AI向けコンテキストとプレビューに使う形にする。IDに加えて表示名と指針文を展開する。
        /// 日本語が有効でないプロジェクトには、漢字の範囲を含めない。
        /// </summary>
        internal static ProjectProfileContext CreateContext(LocalizationSettings settings)
        {
            LocaloProjectProfile profile = settings?.projectProfile;
            if (!HasAnySelection(profile)) return null;
            ProjectProfileOptionsCatalog catalog = Load();
            var context = new ProjectProfileContext
            {
                audience = Find(catalog.audiences, profile.audience),
                genres = FindAll(catalog.genres, profile.genres),
                tones = FindAll(catalog.tones, profile.tones)
            };
            bool japaneseEnabled = settings.enabledLanguages != null &&
                                   settings.enabledLanguages.Contains(UnityEngine.SystemLanguage.Japanese);
            ProjectProfileKanjiRange range = japaneseEnabled ? ResolveKanjiRange(profile) : null;
            if (range != null)
                context.japaneseKanjiRange = range.Copy();
            return context;
        }

        /// <summary>選択内容を画面用に1行で表す（例: Lower Elementary (6-8) / Puzzle / Gentle, Cute / Up to Grade 2）。</summary>
        internal static string Summarize(ProjectProfileContext context)
        {
            if (context == null) return string.Empty;
            var parts = new List<string>();
            if (context.audience != null) parts.Add(context.audience.name);
            if (context.genres.Count > 0) parts.Add(string.Join(", ", context.genres.Select(genre => genre.name)));
            if (context.tones.Count > 0) parts.Add(string.Join(", ", context.tones.Select(tone => tone.name)));
            if (context.japaneseKanjiRange != null) parts.Add(context.japaneseKanjiRange.name);
            return string.Join(" / ", parts);
        }

        private static ProjectProfileOption Find(ProjectProfileOption[] options, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return options.FirstOrDefault(candidate => candidate.id == id)?.Copy();
        }

        // 選択肢の並び順で返す。JSONから消えたIDは無視する。
        private static List<ProjectProfileOption> FindAll(ProjectProfileOption[] options, List<string> ids)
        {
            var selected = new HashSet<string>(ids ?? new List<string>());
            return options.Where(option => selected.Contains(option.id)).Select(option => option.Copy()).ToList();
        }
    }

    [Serializable]
    internal sealed class ProjectProfileOptionsCatalog
    {
        public string reviewedOn;
        public ProjectProfileAudience[] audiences;
        public ProjectProfileOption[] genres;
        public ProjectProfileOption[] tones;
        public ProjectProfileKanjiRange[] kanjiRanges;
    }

    [Serializable]
    internal class ProjectProfileOption
    {
        public string id;
        /// <summary>画面に出す英語の名前（UIの項目名は英語に揃える）。</summary>
        public string name;
        /// <summary>日本語の名前。QAのメッセージとAI向けJSONに使う。</summary>
        public string label;
        /// <summary>AIに渡す、その選択肢の具体的な指針（日本語）。</summary>
        public string guidance;

        internal ProjectProfileOption Copy() => new ProjectProfileOption { id = id, name = name, label = label, guidance = guidance };
    }

    [Serializable]
    internal sealed class ProjectProfileAudience : ProjectProfileOption
    {
        /// <summary>漢字の範囲を自動にしたときに使う kanjiRanges のID。</summary>
        public string kanjiRange;
    }

    [Serializable]
    internal sealed class ProjectProfileKanjiRange : ProjectProfileOption
    {
        /// <summary>0 = かなのみ、1〜6 = その学年まで、7 = 常用漢字、8 = 制限なし。</summary>
        public int maxGrade;
    }

    // コンテキストJSON（projectProfile）の形。フィールド名がJSONのキーになる。
    [Serializable]
    // 使い方の説明は LocaloTranslationContext の instructions にまとめる。未選択の項目は空の値になる。
    internal sealed class ProjectProfileContext
    {
        public ProjectProfileOption audience;
        public List<ProjectProfileOption> genres = new List<ProjectProfileOption>();
        public List<ProjectProfileOption> tones = new List<ProjectProfileOption>();
        public ProjectProfileOption japaneseKanjiRange;
    }
}
