using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunHavenLocalization
{
    [BepInPlugin("xiaoye97.SunHavenLocalization", "SunHavenLocalization", "1.3.8")]
    public class SunHavenLocalizationPlugin : BaseUnityPlugin
    {
        public static SunHavenLocalizationPlugin Instance;
        public static ManualLogSource logger;
        public static ConfigEntry<KeyCode> DumpHotkey;
        public static ConfigEntry<KeyCode> LoadHotkey;
        public static ConfigEntry<bool> LogGetTranslationSuccCall;
        public static ConfigEntry<bool> LogGetTranslationFailCall;
        public static ConfigEntry<bool> AllowMultipleTimesLogGetTranslationSuccCall;
        public static ConfigEntry<bool> AllowMultipleTimesLogGetTranslationFailCall;

        public static LocStorage LoadedLocStorage;

        public static List<string> SuccKeys = new List<string>();
        public static List<string> FailKeys = new List<string>();
        private const string PreferredLanguageCode = "zh-CN";
        private static LocStorage _originalTextCacheStorage;
        private static readonly Dictionary<string, Dictionary<string, string>> OriginalTextTranslationCache =
            new Dictionary<string, Dictionary<string, string>>();
        private static readonly Dictionary<string, Dictionary<string, string>> InvertedEnglishTextCache =
            new Dictionary<string, Dictionary<string, string>>();

        // 标记是否已成功加载
        private static bool _loadSucceeded = false;
        private static bool _isApplyingTextFix = false;

        public void Start()
        {
            Instance = this;
            logger = Logger;
            DumpHotkey = Config.Bind<KeyCode>("dev", "DumpHotkey", KeyCode.F9, "Use Ctrl+DumpHotkey to dump text.");
            LoadHotkey = Config.Bind<KeyCode>("dev", "LoadHotkey", KeyCode.F10, "Use Ctrl+LoadHotkey to reload localization.");
            LogGetTranslationSuccCall = Config.Bind<bool>("dev", "LogGetTranslationSuccCall", false, "Log LocalizationManager.GetTranslation succ call");
            LogGetTranslationFailCall = Config.Bind<bool>("dev", "LogGetTranslationFailCall", false, "Log LocalizationManager.GetTranslation fail call");
            AllowMultipleTimesLogGetTranslationSuccCall = Config.Bind<bool>("dev", "AllowMultipleTimesLogGetTranslationSuccCall", false);
            AllowMultipleTimesLogGetTranslationFailCall = Config.Bind<bool>("dev", "AllowMultipleTimesLogGetTranslationFailCall", false);
            LogInfo("=== SunHavenLocalization v1.3.8 Loading ===");
            try
            {
                var harmony = new Harmony("xiaoye97.SunHavenLocalization");
                harmony.PatchAll(typeof(SunHavenLocalizationPlugin));
                PatchOptionalGameMethods(harmony);
                LogInfo("Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                LogError($"Failed to apply Harmony patches: {ex}");
            }

            // 注册场景加载回调，每次场景加载完后重置标记并尝试加载
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(DelayedLoadAll("startup", 180));

            Credits.ShowCredits();
            LogInfo("=== SunHavenLocalization v1.3.8 Loaded ===");
        }

        /// <summary>
        /// 场景加载完成时回调。重置标记并尝试加载翻译数据。
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LogInfo($"[OnSceneLoaded] Scene loaded: {scene.name}");

            if (!_loadSucceeded)
            {
                // 延迟一段时间尝试加载，避免在 Unity/I2 启动初始化阶段扫描资源。
                Instance?.StartCoroutine(DelayedLoadAll($"scene:{scene.name}", 30));
            }
        }

        private static System.Collections.IEnumerator DelayedLoadAll(string reason, int frameDelay)
        {
            for (int i = 0; i < frameDelay; i++)
                yield return null;

            if (!_loadSucceeded && LoadedLocStorage == null)
            {
                LogInfo($"[DelayedLoadAll] Attempting load after delay ({reason})...");
                try
                {
                    if (LoadTool.LoadXylocFilesOnly())
                    {
                        _loadSucceeded = true;
                        LogInfo("[DelayedLoadAll] Load succeeded!");
                    }
                    else
                    {
                        LogInfo("[DelayedLoadAll] Load failed, will retry on next scene.");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"[DelayedLoadAll] LoadXylocOnly exception ({reason}): {ex}");
                }
            }
        }

        public void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(DumpHotkey.Value))
            {
                LogInfo("Ctrl+F9 detected, starting DumpAll...");
                try
                {
                    DumpTool.DumpAll();
                    LogInfo("DumpAll completed.");
                }
                catch (Exception ex)
                {
                    LogError($"DumpAll failed: {ex}");
                }
            }
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(LoadHotkey.Value))
            {
                LogInfo("Ctrl+F10 detected, starting LoadXylocOnly...");
                try
                {
                    LoadTool.LoadXylocFilesOnly();
                    _loadSucceeded = LoadedLocStorage != null;
                    var localizes = GameObject.FindObjectsOfType<Localize>();
                    LogInfo($"Refreshing {localizes.Length} Localize objects...");
                    foreach (Localize localize in localizes)
                    {
                        localize.OnLocalize();
                    }
                    LogInfo("LoadXylocOnly completed.");
                }
                catch (Exception ex)
                {
                    LogError($"LoadXylocOnly failed: {ex}");
                }
            }
        }

        public static void LogInfo(string msg)
        {
            logger.LogInfo(msg);
        }

        public static void LogWarning(string msg)
        {
            logger.LogWarning(msg);
        }

        public static void LogError(string msg)
        {
            logger.LogError(msg);
        }

        private static void PatchOptionalGameMethods(Harmony harmony)
        {
            var localizeTextType = AccessTools.TypeByName("Wish.LocalizeText");
            var translateText = localizeTextType == null
                ? null
                : AccessTools.Method(localizeTextType, "TranslateText", new[] { typeof(string), typeof(string) });

            if (translateText == null)
            {
                LogWarning("Wish.LocalizeText.TranslateText not found; quest progress fallback patch skipped.");
            }
            else
            {
                harmony.Patch(
                    translateText,
                    postfix: new HarmonyMethod(typeof(SunHavenLocalizationPlugin), nameof(LocalizeText_TranslateText_Postfix)));
                LogInfo("Patched Wish.LocalizeText.TranslateText.");
            }

            var bookHandlerType = AccessTools.TypeByName("Wish.BookHandler");
            var readBook = bookHandlerType == null
                ? null
                : AccessTools.Method(bookHandlerType, "ReadBook", new[] { typeof(string) });

            if (readBook == null)
            {
                LogWarning("Wish.BookHandler.ReadBook not found; book text patch skipped.");
            }
            else
            {
                harmony.Patch(
                    readBook,
                    prefix: new HarmonyMethod(typeof(SunHavenLocalizationPlugin), nameof(BookHandler_ReadBook_Prefix)));
                LogInfo("Patched Wish.BookHandler.ReadBook.");
            }
        }

        private static bool TryGetLoadedTranslation(string term, out string translation)
        {
            translation = null;

            if (string.IsNullOrWhiteSpace(term) || LoadedLocStorage == null)
                return false;

            if (LooksLikeAssetOrFontTerm(term))
                return false;

            if (!TryGetPreferredSheet(out var sheet))
                return false;

            if (!sheet.Dict.TryGetValue(term, out var locItem))
                return false;

            if (string.IsNullOrWhiteSpace(locItem.Translation) && string.IsNullOrWhiteSpace(locItem.Original))
                return false;

            translation = SelectBestTranslation(locItem);
            return true;
        }

        private static bool LooksLikeAssetOrFontTerm(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return false;

            string lower = term.ToLowerInvariant();
            return lower.Contains("font") ||
                   lower.Contains("sdf") ||
                   lower.Contains("material") ||
                   lower.Contains("sprite") ||
                   lower.Contains("asset") ||
                   lower.Contains("prefab") ||
                   lower.Contains("audio") ||
                   lower.Contains("music") ||
                   lower.Contains("texture") ||
                   lower.Contains("icon") ||
                   term.EndsWith("Outlined", StringComparison.Ordinal) ||
                   term.EndsWith(" SDF", StringComparison.Ordinal);
        }

        private static bool ShouldApplyChineseLocalization()
        {
            try
            {
                string currentCode = LocalizationManager.CurrentLanguageCode;
                if (currentCode == "zh-CN" ||
                    currentCode == "zh" ||
                    currentCode == "ChineseSimplified")
                {
                    return true;
                }

                string currentLanguage = LocalizationManager.CurrentLanguage;
                return currentLanguage == "Chinese" ||
                       currentLanguage == "ChineseSimplified" ||
                       currentLanguage == "Chinese (Simplified)" ||
                       currentLanguage == "简体中文";
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetPreferredSheet(out LocSheet sheet)
        {
            sheet = null;

            if (LoadedLocStorage == null || LoadedLocStorage.Storage == null)
                return false;

            if (LoadedLocStorage.Storage.TryGetValue(PreferredLanguageCode, out sheet))
                return true;

            foreach (var kv in LoadedLocStorage.Storage)
            {
                sheet = kv.Value;
                return sheet != null;
            }

            return false;
        }

        private static string SelectBestTranslation(LocItem locItem)
        {
            if (locItem == null)
                return null;

            string translation = locItem.Translation;

            // Some xyloc rows are inverted: Original already contains the Chinese text,
            // while Translation still contains the English source. Prefer Chinese for this plugin.
            if (ContainsCjk(locItem.Original) &&
                (string.IsNullOrWhiteSpace(translation) || !ContainsCjk(translation)))
            {
                translation = locItem.Original;
            }

            if (string.IsNullOrWhiteSpace(translation))
                translation = locItem.Original;

            return CleanupLocalizedText(translation);
        }

        private static bool ContainsCjk(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char ch in text)
            {
                if ((ch >= '\u3400' && ch <= '\u9fff') || (ch >= '\uf900' && ch <= '\ufaff'))
                    return true;
            }

            return false;
        }

        private static bool TryGetLoadedTranslationByOriginal(string originalText, out string translation)
        {
            translation = null;

            if (string.IsNullOrWhiteSpace(originalText) || LoadedLocStorage == null)
                return false;

            if (_originalTextCacheStorage != LoadedLocStorage)
            {
                OriginalTextTranslationCache.Clear();
                InvertedEnglishTextCache.Clear();
                _originalTextCacheStorage = LoadedLocStorage;
            }

            if (!OriginalTextTranslationCache.TryGetValue(PreferredLanguageCode, out var cache))
            {
                cache = new Dictionary<string, string>();
                if (TryGetPreferredSheet(out var sheet))
                {
                    foreach (var item in sheet.Dict.Values)
                    {
                        if (string.IsNullOrWhiteSpace(item.Original))
                            continue;

                        if (!cache.ContainsKey(item.Original))
                            cache[item.Original] = SelectBestTranslation(item);
                    }
                }

                OriginalTextTranslationCache[PreferredLanguageCode] = cache;
            }

            if (cache.TryGetValue(originalText, out translation))
                return true;

            return TryGetLoadedTranslationByInvertedEnglish(originalText, out translation);
        }

        private static bool TryGetLoadedTranslationByInvertedEnglish(string englishText, out string translation)
        {
            translation = null;

            if (string.IsNullOrWhiteSpace(englishText) || LoadedLocStorage == null)
                return false;

            if (!InvertedEnglishTextCache.TryGetValue(PreferredLanguageCode, out var cache))
            {
                cache = new Dictionary<string, string>();
                if (TryGetPreferredSheet(out var sheet))
                {
                    foreach (var item in sheet.Dict.Values)
                    {
                        if (string.IsNullOrWhiteSpace(item.Translation) || !ContainsCjk(item.Original) || ContainsCjk(item.Translation))
                            continue;

                        if (!cache.ContainsKey(item.Translation))
                            cache[item.Translation] = CleanupLocalizedText(item.Original);
                    }
                }

                InvertedEnglishTextCache[PreferredLanguageCode] = cache;
            }

            return cache.TryGetValue(englishText, out translation);
        }

        private static bool TryGetLoadedTranslationByGeneratedKey(string sourceText, out string translation)
        {
            translation = null;

            if (string.IsNullOrWhiteSpace(sourceText) || LoadedLocStorage == null)
                return false;

            string keyBase = BuildLocalizationKeyBase(sourceText);
            if (string.IsNullOrWhiteSpace(keyBase))
                return false;

            string[] candidates =
            {
                keyBase + ".Name",
                keyBase + ".Description",
                keyBase + ".HelpDescription",
                keyBase + ".UseDescription",
                keyBase
            };

            foreach (string candidate in candidates)
            {
                if (TryGetLoadedTranslation(candidate, out translation))
                    return true;
            }

            return false;
        }

        private static string BuildLocalizationKeyBase(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                return null;

            var builder = new System.Text.StringBuilder(sourceText.Length);
            bool nextUpper = true;
            foreach (char ch in sourceText)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(nextUpper ? char.ToUpperInvariant(ch) : ch);
                    nextUpper = false;
                }
                else
                {
                    nextUpper = true;
                }
            }

            return builder.ToString();
        }

        private static string CleanupLocalizedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return FixKnownHardcodedText(text);
        }

        private static string FixKnownHardcodedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string trimmed = text.Trim();
            if (ExactHardcodedTextReplacements.TryGetValue(trimmed, out var exactTranslation))
                return PreserveOuterWhitespace(text, exactTranslation);

            string plaqueOpening = "A grand plaque, inscribed with the names of all those who helped bring Sun Haven to life...";
            if (trimmed.StartsWith(plaqueOpening, StringComparison.Ordinal))
            {
                string names = trimmed.Substring(plaqueOpening.Length).TrimStart();
                string replacement = "一块宏伟的牌匾，上面镌刻着所有帮助《太阳港》诞生的人们的名字。";
                if (!string.IsNullOrWhiteSpace(names))
                    replacement += "\n" + names;

                return PreserveOuterWhitespace(
                    text,
                    replacement);
            }

            string result = FixQuestMapPopupText(text);
            result = FixDynamicEnglishFragments(result);
            result = ApplyPhraseReplacements(result);

            foreach (var kv in HardcodedTextReplacements)
            {
                if (result.Contains(kv.Key))
                    result = result.Replace(kv.Key, kv.Value);
            }

            return NormalizeRichText(result);
        }

        private static string PreserveOuterWhitespace(string original, string replacement)
        {
            int start = 0;
            while (start < original.Length && char.IsWhiteSpace(original[start]))
                start++;

            int end = original.Length - 1;
            while (end >= start && char.IsWhiteSpace(original[end]))
                end--;

            string prefix = start > 0 ? original.Substring(0, start) : string.Empty;
            string suffix = end < original.Length - 1 ? original.Substring(end + 1) : string.Empty;
            return prefix + replacement + suffix;
        }

        private static string FixQuestMapPopupText(string text)
        {
            string trimmed = text.Trim();

            var turnInToMatch = System.Text.RegularExpressions.Regex.Match(
                trimmed,
                @"^Turn in\s+(.+?)\s+to\s+(.+?)\s+at\s+(.+)$");
            if (turnInToMatch.Success)
            {
                string questName = ApplyPhraseReplacements(turnInToMatch.Groups[1].Value);
                string npcName = ApplyPhraseReplacements(turnInToMatch.Groups[2].Value);
                string location = ApplyPhraseReplacements(turnInToMatch.Groups[3].Value);
                return PreserveOuterWhitespace(text, $"在{location}向{npcName}交付{questName}");
            }

            var turnInAtMatch = System.Text.RegularExpressions.Regex.Match(
                trimmed,
                @"^Turn in\s+(.+?)\s+at\s+(.+)$");
            if (turnInAtMatch.Success)
            {
                string questName = ApplyPhraseReplacements(turnInAtMatch.Groups[1].Value);
                string location = ApplyPhraseReplacements(turnInAtMatch.Groups[2].Value);
                return PreserveOuterWhitespace(text, $"在{location}交付{questName}");
            }

            var chineseTurnInAtMatch = System.Text.RegularExpressions.Regex.Match(
                trimmed,
                @"^提交给\s+(.+?)\s+at\s+(.+)$");
            if (chineseTurnInAtMatch.Success)
            {
                string npcName = ApplyPhraseReplacements(chineseTurnInAtMatch.Groups[1].Value);
                string location = ApplyPhraseReplacements(chineseTurnInAtMatch.Groups[2].Value);
                return PreserveOuterWhitespace(text, $"在{location}提交给{npcName}");
            }

            var availableQuestMatch = System.Text.RegularExpressions.Regex.Match(
                trimmed,
                @"^(.+?)\s+quest available from\s+(.+?)\s+at\s+(.+)$");
            if (availableQuestMatch.Success)
            {
                string questName = ApplyPhraseReplacements(availableQuestMatch.Groups[1].Value);
                string npcName = ApplyPhraseReplacements(availableQuestMatch.Groups[2].Value);
                string location = ApplyPhraseReplacements(availableQuestMatch.Groups[3].Value);
                return PreserveOuterWhitespace(text, $"在{location}可从{npcName}处接取{questName}");
            }

            return text;
        }

        private static string FixDynamicEnglishFragments(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string result = text;

            result = FixSkillTreeRequirementText(result);
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"(\d+)\s*point\(s\) in this skill\)",
                "$1 点技能点在这项技能中)");

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"(\d+)\s*point\(s\) in this skill\.",
                "$1 点技能点在这项技能中。");

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"(\d+)\s*point\(s\) in this skill\b",
                "$1 点技能点在这项技能中");

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"Increases\s+(<sprite=""movement_speed_icon""\s+index=0>\s*)?Movement Speed by\s+(\d+)%",
                "移动速度提高 $2%");

            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"\s+and\s+(<sprite=""jump_height_icon""\s*index\s*=\s*0>)",
                " 和 $1");

            return result;
        }

        private static string FixSkillTreeRequirementText(string text)
        {
            if (string.IsNullOrEmpty(text) ||
                !text.Contains("points spent") ||
                !text.Contains("skill tree") ||
                !text.Contains("unlock"))
            {
                return text;
            }

            string stripped = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
            var match = System.Text.RegularExpressions.Regex.Match(stripped, @"Requires\s+(\d+)\s+points?\s+spent");
            if (!match.Success)
                return text;

            string num = match.Groups[1].Value;
            return $"<size=125%><line-height=65%>已锁定</line-height><line-height=55%>\n<size=80%><line-height=55%><i><color=#F8F377>需要在该技能树中投入 <color=#9EFF5D>{num} 点技能点</color> 来解锁此层级。</line-height></line-height>";
        }

        private static string NormalizeRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string result = text;
            result = result.Replace("\0", "");
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"<sprite=""([^""]+)""\s*index\s*=\s*(\d+)>",
                "<sprite=\"$1\" index=$2>");
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"(<sprite=""(?:movement_speed_icon|jump_height_icon)"" index=0>)\s*(\d+%)",
                "$1 $2");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"</color>?\s*和\s*<color", "</color> 和 <color");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"</color>?\s*和", "</color> 和");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(?<=\d%)\s*(移动速度|跳跃高度)", " $1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"(移动速度|跳跃高度)\s*</color>", "$1</color>");
            return result;
        }

        private static string ApplyPhraseReplacements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string result = text;
            foreach (var kv in PhraseTextReplacements)
            {
                if (result.Contains(kv.Key))
                    result = result.Replace(kv.Key, kv.Value);
            }

            return result;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(LocalizationManager), "GetTranslation")]
        public static void LocalizationManager_GetTranslation_Patch(string Term, ref string __result)
        {
            try
            {
                if (LoadedLocStorage == null)
                    return;

                if (!ShouldApplyChineseLocalization())
                    return;

                if (TryGetLoadedTranslation(Term, out var translation))
                {
                    __result = translation;
                    if (LogGetTranslationSuccCall.Value)
                    {
                        if (AllowMultipleTimesLogGetTranslationSuccCall.Value || !SuccKeys.Contains(Term))
                        {
                            LogInfo($"GetTranslation Key:[{Term}] Translation:[{translation}]");
                        }
                        if (!SuccKeys.Contains(Term))
                        {
                            SuccKeys.Add(Term);
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(__result) && LogGetTranslationFailCall.Value)
                {
                    if (AllowMultipleTimesLogGetTranslationFailCall.Value || !FailKeys.Contains(Term))
                    {
                        LogWarning($"GetNoTranslation Key:[{Term}] Output:[{__result}]");
                        if (!FailKeys.Contains(Term))
                        {
                            FailKeys.Add(Term);
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(__result))
                    __result = CleanupLocalizedText(__result);
            }
            catch (Exception ex)
            {
                LogError($"GetTranslation_Patch exception for Term [{Term}]: {ex}");
            }
        }
        /// <summary>
        /// Patch GetTermTranslation。LocalizeText.TranslateText 在 key 非空时会走这里；
        /// key 为空或 key 未命中时由 LocalizeText.TranslateText postfix 处理英文 fallback。
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(LocalizationManager), "GetTermTranslation")]
        public static void LocalizationManager_GetTermTranslation_Patch(string Term, ref string __result)
        {
            try
            {
                if (LoadedLocStorage == null)
                    return;

                if (!ShouldApplyChineseLocalization())
                    return;

                if (TryGetLoadedTranslation(Term, out var translation))
                {
                    __result = translation;
                    if (LogGetTranslationSuccCall.Value)
                    {
                        if (AllowMultipleTimesLogGetTranslationSuccCall.Value || !SuccKeys.Contains(Term))
                        {
                            LogInfo($"GetTermTranslation Key:[{Term}] Translation:[{translation}]");
                        }
                        if (!SuccKeys.Contains(Term))
                        {
                            SuccKeys.Add(Term);
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(__result) && LogGetTranslationFailCall.Value)
                {
                    if (AllowMultipleTimesLogGetTranslationFailCall.Value || !FailKeys.Contains(Term))
                    {
                        LogWarning($"GetTermTranslation NoTranslation Key:[{Term}] Output:[{__result}]");
                        if (!FailKeys.Contains(Term))
                        {
                            FailKeys.Add(Term);
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(__result))
                    __result = CleanupLocalizedText(__result);
            }
            catch (Exception ex)
            {
                LogError($"GetTermTranslation_Patch exception for Term [{Term}]: {ex}");
            }
        }

        public static void LocalizeText_TranslateText_Postfix(string key, string defaultText, ref string __result)
        {
            try
            {
                if (LoadedLocStorage == null)
                    return;

                if (!ShouldApplyChineseLocalization())
                    return;

                if (TryGetLoadedTranslation(key, out var translation))
                {
                    __result = translation;
                    return;
                }

                string fallbackSource = !string.IsNullOrWhiteSpace(defaultText) ? defaultText : __result;
                if (TryGetLoadedTranslationByOriginal(fallbackSource, out translation))
                {
                    __result = translation;
                    return;
                }

                if (TryGetLoadedTranslationByGeneratedKey(fallbackSource, out translation))
                {
                    __result = translation;
                    return;
                }

                string fixedText = FixKnownHardcodedText(fallbackSource);
                if (!string.Equals(fallbackSource, fixedText, StringComparison.Ordinal))
                    __result = fixedText;
            }
            catch (Exception ex)
            {
                LogError($"LocalizeText.TranslateText postfix exception for key [{key}]: {ex}");
            }
        }

        public static void BookHandler_ReadBook_Prefix(ref string text)
        {
            try
            {
                if (LoadedLocStorage == null || string.IsNullOrWhiteSpace(text))
                    return;

                if (!ShouldApplyChineseLocalization())
                    return;

                if (TryGetLoadedBookTextTranslation(text, out var translation))
                    text = translation;
            }
            catch (Exception ex)
            {
                LogError($"BookHandler.ReadBook prefix exception: {ex}");
            }
        }

        private static bool TryGetLoadedBookTextTranslation(string sourceText, out string translation)
        {
            translation = null;

            if (TryBuildBookTextKey(sourceText, out var bookTextKey) &&
                TryGetLoadedTranslation(bookTextKey, out translation))
            {
                return true;
            }

            return TryGetLoadedTranslationByOriginal(sourceText, out translation);
        }

        private static bool TryBuildBookTextKey(string sourceText, out string key)
        {
            key = null;

            if (string.IsNullOrWhiteSpace(sourceText) ||
                !sourceText.Contains("<align=\"center\">") ||
                !sourceText.Contains("</align>"))
            {
                return false;
            }

            string normalized = sourceText.Replace("\r\n", "\n").Replace("\r", "\n");
            var match = System.Text.RegularExpressions.Regex.Match(
                normalized,
                @"<align=""center"">\s*(?:[^\n]*\n)+\s*(Origins of .+?)\s*\n\s*(Book\s+[IVX]+)\s*</align>",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (!match.Success)
                return false;

            string keyBase = BuildLocalizationKeyBase(match.Groups[1].Value + " " + match.Groups[2].Value);
            if (string.IsNullOrWhiteSpace(keyBase))
                return false;

            key = "BookText." + keyBase + ".Text";
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TMPro.TMP_Text), "text", HarmonyLib.MethodType.Setter)]
        public static void TMPText_SetText_Postfix(TMPro.TMP_Text __instance)
        {
            FixKnownTextSetterText(__instance == null ? null : __instance.text, newText => __instance.text = newText);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnityEngine.UI.Text), "text", HarmonyLib.MethodType.Setter)]
        public static void UIText_SetText_Postfix(UnityEngine.UI.Text __instance)
        {
            FixKnownTextSetterText(__instance == null ? null : __instance.text, newText => __instance.text = newText);
        }

        private static void FixKnownTextSetterText(string text, System.Action<string> setText)
        {
            if (_isApplyingTextFix || string.IsNullOrEmpty(text))
                return;

            if (!ShouldApplyChineseLocalization())
                return;

            if (!LooksLikeKnownHardcodedText(text))
                return;

            string fixedText = FixKnownHardcodedText(text);
            if (string.Equals(text, fixedText, StringComparison.Ordinal))
                return;

            try
            {
                _isApplyingTextFix = true;
                setText(fixedText);
            }
            catch (Exception ex)
            {
                LogError($"Text setter hardcoded fix failed: {ex}");
            }
            finally
            {
                _isApplyingTextFix = false;
            }
        }

        private static bool LooksLikeKnownHardcodedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            if (text.Contains("point(s)") ||
                text.Contains("points spent") ||
                text.Contains("skill tree") ||
                text.Contains("Movement Speed") ||
                text.Contains("Jump Height") ||
                text.Contains("Right-Click") ||
                text.Contains("Right Clicking") ||
                text.Contains("Someone") ||
                text.Contains("Use Speed") ||
                text.Contains("Automatic Feeder") ||
                text.Contains("Visit the General Store") ||
                text.Contains("Kitty's Animal Store") ||
                text.Contains("Grand Tree Tavern") ||
                text.Contains("Northern Barracks") ||
                text.Contains("A grand plaque") ||
                text.Contains("Turn in ") ||
                text.Contains("quest available from") ||
                text.Contains(" at "))
            {
                return true;
            }

            string trimmed = text.Trim();
            return ExactHardcodedTextReplacements.ContainsKey(trimmed);
        }

        private static readonly Dictionary<string, string> HardcodedTextReplacements = new Dictionary<string, string>
        {
            // 多人模式 — 完整句子匹配
            { "Someone has begun the ", "有人开始了" },
            { "Would you like to join?", "，要加入吗？" },
            { "Someone is already using this.", "已经有人在使用这个。" },
            { "Multiplayer Global Skill", "多人全局技能" },
            { "Someone else already has ", "已经有人投入了" },
            { " point(s) in this skill.", " 点技能点在这项技能中。" },
            { " point(s) in this skill)", " 点技能点在这项技能中)" },
            // 工具描述
            { "Use Speed", "使用速度" },
            // 物品名 fallback。Automatic Feeder 的资产名可能以 defaultText 形式传入，key 为空时不会命中 AutomaticFeeder.Name。
            { "Automatic Feeder", "自动喂食器" },
            // 任务追踪
            { "Visit the General Store", "参观杂货商店" },
            // 资产文本/名字兜底。
            { "Kitty's Animal Store", "凯蒂的动物商店" },
        };

        private static readonly Dictionary<string, string> ExactHardcodedTextReplacements = new Dictionary<string, string>
        {
            // 交互提示。只做完整文本匹配，避免误改对白里的普通动词。
            { "Take", "拿取" },
            { "Place", "放置" },
            // 地图任务弹窗标题。
            { "Complete Quest", "完成任务" },
            { "Available Quest", "可接任务" },
            { "Quest Turn In", "任务交付" },
            // 头顶名牌/对话框标题可能直接设置对象名，不一定走 I2 key。
            { "Arturo", "阿图罗" },
            { "Frankie", "弗兰基" },
            { "Stephen", "斯蒂芬" },
        };

        private static readonly List<KeyValuePair<string, string>> PhraseTextReplacements = new List<KeyValuePair<string, string>>
        {
            // 地图位置名。Grand Tree Tavern 没有独立 I2 key，SceneSettings.formalSceneName 会直接拼进 popup。
            new KeyValuePair<string, string>("the Grand Tree Tavern", "巨树酒馆"),
            new KeyValuePair<string, string>("Grand Tree Tavern", "巨树酒馆"),
            new KeyValuePair<string, string>("at the Northern Barracks", "在北部兵营"),
            new KeyValuePair<string, string>("the Northern Barracks", "北部兵营"),
            new KeyValuePair<string, string>("Northern Barracks", "北部兵营"),
            new KeyValuePair<string, string>("Kitty's Animal Store", "凯蒂的动物商店"),
            new KeyValuePair<string, string>("Movement Speed", "移动速度"),
            new KeyValuePair<string, string>("Jump Height", "跳跃高度"),
            // 技能树把 KEYBIND 硬编码替换成 Right-Clicking，需要保持中文句式为“按下右键”。
            new KeyValuePair<string, string>("Right-Clicking", "右键"),
            new KeyValuePair<string, string>("Right Clicking", "右键"),
            new KeyValuePair<string, string>("Right-Click", "右键"),
        };

        public void SearchTerm(string Key)
        {
            var source = GameObject.FindObjectOfType<LanguageSource>();
            foreach (var term in source.SourceData.mTerms)
            {
                if (term.Term == Key)
                {
                    LogInfo($"========Term [{Key}]");
                    for (int i = 0; i < term.Languages.Length; i++)
                    {
                        LogInfo($"[{i}] {term.Languages[i]}");
                    }
                    LogInfo($"========");
                    break;
                }
            }
        }
    }
}
