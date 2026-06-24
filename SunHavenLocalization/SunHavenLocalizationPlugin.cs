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
    [BepInPlugin("xiaoye97.SunHavenLocalization", "SunHavenLocalization", "1.3.1")]
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

        // 防重复加载：标记当前场景是否已尝试过 LoadAll
        private static bool _loadAttemptedThisScene = false;
        // 标记是否已成功加载
        private static bool _loadSucceeded = false;

        public void Start()
        {
            Instance = this;
            logger = Logger;
            DumpHotkey = Config.Bind<KeyCode>("dev", "DumpHotkey", KeyCode.F9, "Use Ctrl+DumpHotkey to dump text.");
            LoadHotkey = Config.Bind<KeyCode>("dev", "LoadHotkey", KeyCode.F10, "Use Ctrl+LoadHotkey to reload localization.");
            LogGetTranslationSuccCall = Config.Bind<bool>("dev", "LogGetTranslationSuccCall", false, "Log LocalizationManager.GetTranslation succ call");
            LogGetTranslationFailCall = Config.Bind<bool>("dev", "LogGetTranslationFailCall", true, "Log LocalizationManager.GetTranslation fail call");
            AllowMultipleTimesLogGetTranslationSuccCall = Config.Bind<bool>("dev", "AllowMultipleTimesLogGetTranslationSuccCall", false);
            AllowMultipleTimesLogGetTranslationFailCall = Config.Bind<bool>("dev", "AllowMultipleTimesLogGetTranslationFailCall", false);
            LogInfo("=== SunHavenLocalization v1.3.1 Loading ===");
            try
            {
                Harmony.CreateAndPatchAll(typeof(SunHavenLocalizationPlugin));
                LogInfo("Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                LogError($"Failed to apply Harmony patches: {ex}");
            }

            // 注册场景加载回调，每次场景加载完后重置标记并尝试加载
            SceneManager.sceneLoaded += OnSceneLoaded;

            Credits.ShowCredits();
            LogInfo("=== SunHavenLocalization v1.3.1 Loaded ===");
        }

        /// <summary>
        /// 场景加载完成时回调。重置标记并尝试加载翻译数据。
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LogInfo($"[OnSceneLoaded] Scene loaded: {scene.name}");
            _loadAttemptedThisScene = false;

            if (!_loadSucceeded)
            {
                // 延迟一帧尝试加载，确保场景对象都初始化完毕
                Instance?.StartCoroutine(DelayedLoadAll());
            }
        }

        private static System.Collections.IEnumerator DelayedLoadAll()
        {
            // 等两帧，让场景内的对象完成初始化
            yield return null;
            yield return null;

            if (!_loadSucceeded && LoadedLocStorage == null)
            {
                LogInfo("[DelayedLoadAll] Attempting load after scene transition...");
                LoadTool.LoadAll();
                if (LoadedLocStorage != null)
                {
                    _loadSucceeded = true;
                    LogInfo("[DelayedLoadAll] Load succeeded!");
                }
                else
                {
                    _loadAttemptedThisScene = true;
                    LogInfo("[DelayedLoadAll] Load failed, will retry on next scene.");
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
                LogInfo("Ctrl+F10 detected, starting LoadAll...");
                try
                {
                    LoadTool.LoadAll();
                    var localizes = GameObject.FindObjectsOfType<Localize>();
                    LogInfo($"Refreshing {localizes.Length} Localize objects...");
                    foreach (Localize localize in localizes)
                    {
                        localize.OnLocalize();
                    }
                    LogInfo("LoadAll completed.");
                }
                catch (Exception ex)
                {
                    LogError($"LoadAll failed: {ex}");
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

        [HarmonyPostfix, HarmonyPatch(typeof(LocalizationManager), "GetTranslation")]
        public static void LocalizationManager_GetTranslation_Patch(string Term, ref string __result)
        {
            try
            {
                // 英文是源语言，不需要翻译，跳过所有逻辑避免刷 log 影响性能
                string currentLang = LocalizationManager.CurrentLanguageCode;
                if (currentLang == "en" || currentLang == "English")
                    return;

                // 如果已成功加载，正常工作
                if (LoadedLocStorage != null)
                {
                    var termData = LocalizationManager.GetTermData(Term);
                    if (termData != null)
                    {
                        if (termData.TermType == eTermType.Text)
                        {
                            if (LoadedLocStorage.Storage.TryGetValue(LocalizationManager.CurrentLanguageCode, out var sheet))
                            {
                                if (sheet.Dict.TryGetValue(Term, out var locItem))
                                {
                                    if (!string.IsNullOrWhiteSpace(locItem.Translation))
                                    {
                                        __result = locItem.Translation;
                                        if (LogGetTranslationSuccCall.Value)
                                        {
                                            if (AllowMultipleTimesLogGetTranslationSuccCall.Value || !SuccKeys.Contains(Term))
                                            {
                                                LogInfo($"GetTranslation Key:[{Term}] Translation:[{locItem.Translation}]");
                                            }
                                            if (!SuccKeys.Contains(Term))
                                            {
                                                SuccKeys.Add(Term);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrWhiteSpace(__result))
                                        {
                                            if (LogGetTranslationFailCall.Value)
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
                                        }
                                    }
                                }
                            }
                            else
                            {
                                LogWarning($"LanguageCode '{LocalizationManager.CurrentLanguageCode}' not found in LoadedLocStorage.");
                            }
                        }
                    }
                    return;
                }

                // 尚未加载成功 —— 每个场景只尝试一次，避免刷屏
                if (_loadAttemptedThisScene || _loadSucceeded)
                    return;

                _loadAttemptedThisScene = true;
                LogInfo("[GetTranslation] LocStorage not loaded yet, attempting LoadAll (once per scene)...");
                LoadTool.LoadAll();
                if (LoadedLocStorage != null)
                {
                    _loadSucceeded = true;
                    LogInfo("[GetTranslation] LoadAll succeeded!");
                }
                else
                {
                    LogWarning("[GetTranslation] LoadAll failed (LanguageSourceAsset not ready). Will retry on next scene load.");
                }
            }
            catch (Exception ex)
            {
                LogError($"GetTranslation_Patch exception for Term [{Term}]: {ex}");
            }
        }

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
