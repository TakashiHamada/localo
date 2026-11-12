// =============================================================================
// LocaloEditorWindow.ProjectProfile.cs — Translation タブの Project Profile と Target Platforms
// =============================================================================
// 作品全体の対象プレイヤー・雰囲気と、対象の機種を選ぶ。選択肢と指針文は同梱のJSONにあり、
// 設定アセットにはIDだけを保存する。選んだ内容はAI向けJSONとQAに使う。
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        private void DrawProjectProfileSection()
        {
            EditorGUILayout.LabelField("-- Project Profile --", EditorStyles.boldLabel);
            ProjectProfileOptionsCatalog catalog;
            try { catalog = LocaloProjectProfileOptions.Load(); }
            catch (InvalidOperationException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }

            LocaloProjectProfile profile = _settings.projectProfile ?? new LocaloProjectProfile();

            string audience = DrawOptionPopup(new GUIContent("Audience", "作品の対象年齢層です。"),
                profile.audience, catalog.audiences, "(none)");
            List<string> tones = DrawOptionMask(new GUIContent("Tone", "作品全体の雰囲気です。複数選べます。"),
                profile.tones, catalog.tones);

            string kanjiRange = profile.kanjiRange ?? LocaloProjectProfileOptions.AutoKanjiRange;
            if (_settings.enabledLanguages != null && _settings.enabledLanguages.Contains(SystemLanguage.Japanese))
            {
                ProjectProfileKanjiRange automatic = catalog.kanjiRanges.FirstOrDefault(range =>
                    range.id == catalog.audiences.FirstOrDefault(option => option.id == audience)?.kanjiRange);
                string autoLabel = automatic != null ? $"Match Audience ({automatic.name})" : "Match Audience (none)";
                kanjiRange = DrawOptionPopup(new GUIContent("Japanese Kanji",
                        "日本語に使う漢字の範囲です。Match Audienceでは対象年齢に合わせます。"),
                    kanjiRange, catalog.kanjiRanges, autoLabel);
            }

            bool changed = audience != (profile.audience ?? string.Empty) ||
                           kanjiRange != (profile.kanjiRange ?? string.Empty) ||
                           !tones.SequenceEqual(profile.tones ?? new List<string>());
            if (changed)
            {
                Undo.RecordObject(_settings, "Change Localo Project Profile");
                _settings.projectProfile = new LocaloProjectProfile
                {
                    audience = audience, tones = tones, kanjiRange = kanjiRange
                };
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssetIfDirty(_settings);
            }

            DrawSelectedProfileGuidance();
            EditorGUILayout.HelpBox(
                "作品全体の対象プレイヤーと雰囲気を選びます。上の指針がAI向けJSONに入り、各言語の翻訳や仮の文字の口調・語彙・表記の基準になります。\n" +
                "漢字の範囲は、QAの kanji-range でも使います。",
                MessageType.None);
        }

        // 選んだ項目についてAIに渡す指針を、その場で読めるように並べる。
        private void DrawSelectedProfileGuidance()
        {
            ProjectProfileContext context = LocaloProjectProfileOptions.CreateContext(_settings);
            if (context == null) return;
            var selected = new List<ProjectProfileOption>();
            if (context.audience != null) selected.Add(context.audience);
            selected.AddRange(context.tones);
            if (context.japaneseKanjiRange != null) selected.Add(context.japaneseKanjiRange);
            foreach (ProjectProfileOption option in selected)
                EditorGUILayout.LabelField($"• {option.name}: {option.guidance}", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawTargetPlatformsSection()
        {
            EditorGUILayout.LabelField("-- Target Platforms --", EditorStyles.boldLabel);
            PlatformTermsCatalog catalog;
            try { catalog = LocaloPlatformTerms.Load(); }
            catch (InvalidOperationException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }

            List<string> selected = _settings.targetPlatforms ?? new List<string>();
            foreach (PlatformTerms platform in catalog.platforms)
            {
                bool wasOn = selected.Contains(platform.id);
                bool isOn = EditorGUILayout.ToggleLeft(platform.name, wasOn);
                if (isOn == wasOn) continue;
                Undo.RecordObject(_settings, "Change Localo Target Platforms");
                if (_settings.targetPlatforms == null) _settings.targetPlatforms = new List<string>();
                if (isOn) _settings.targetPlatforms.Add(platform.id);
                else _settings.targetPlatforms.RemoveAll(id => id == platform.id);
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssetIfDirty(_settings);
            }

            EditorGUILayout.HelpBox(
                "選んだ機種のボタン名・機能名の公式表記を、AI向けJSONと、QAの platform-terms に使います。\n" +
                "公開されている製品ページの表記です。ロットチェックなどの用語規定ではありません。正式な用語集がある場合はGlossaryに記入してください（Glossaryが優先されます）。",
                MessageType.None);
        }

        // 先頭に「未指定」などの項目を置いたポップアップ。戻り値は選んだID（先頭の項目なら空文字）。
        private static string DrawOptionPopup<T>(GUIContent label, string currentId, T[] options, string emptyLabel)
            where T : ProjectProfileOption
        {
            var labels = new List<GUIContent> { new GUIContent(emptyLabel) };
            labels.AddRange(options.Select(option => new GUIContent(option.name, option.guidance)));
            int currentIndex = Array.FindIndex(options, option => option.id == currentId) + 1;
            if (currentIndex == 0 && !string.IsNullOrEmpty(currentId))
            {
                // 同梱の選択肢から消えたIDは、勝手に消さずに表示だけ分かるようにする。
                labels.Add(new GUIContent($"(unknown: {currentId})", "同梱の選択肢に無い値です。別の項目を選ぶと置き換わります。"));
                currentIndex = labels.Count - 1;
            }
            int nextIndex = EditorGUILayout.Popup(label, currentIndex, labels.ToArray());
            if (nextIndex == currentIndex) return currentId ?? string.Empty;
            return nextIndex == 0 || nextIndex > options.Length ? string.Empty : options[nextIndex - 1].id;
        }

        // 複数選択のドロップダウン。保存はIDの並びで、選択肢の並び順に揃える。未知のIDは保持する。
        private static List<string> DrawOptionMask(GUIContent label, List<string> currentIds, ProjectProfileOption[] options)
        {
            currentIds = currentIds ?? new List<string>();
            int mask = 0;
            for (int i = 0; i < options.Length && i < 32; i++)
                if (currentIds.Contains(options[i].id)) mask |= 1 << i;
            int nextMask = EditorGUILayout.MaskField(label, mask, options.Take(32).Select(option => option.name).ToArray());
            if (nextMask == mask) return new List<string>(currentIds);

            var next = new List<string>();
            for (int i = 0; i < options.Length && i < 32; i++)
                if ((nextMask & (1 << i)) != 0) next.Add(options[i].id);
            next.AddRange(currentIds.Where(id => options.All(option => option.id != id)));
            return next;
        }
    }
}
