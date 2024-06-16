using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using System.Collections.Generic;
using UnityEngine;

namespace SunHavenLocalization
{
    [BepInPlugin("xiaoye97.SunHavenLocalization", "SunHavenLocalization", "1.0.0")]
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
            LogInfo("SunHavenLocalization Loaded.");
            Harmony.CreateAndPatchAll(typeof(SunHavenLocalizationPlugin));
            Credits.ShowCredits();
        }

        public void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(DumpHotkey.Value))
            {
                DumpTool.DumpAll();
            }
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(LoadHotkey.Value))
            {
                LoadTool.LoadAll();
                var localizes = GameObject.FindObjectsOfType<Localize>();
                foreach (Localize localize in localizes)
                {
                    localize.OnLocalize();
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
            if (LoadedLocStorage == null)
            {
                LoadTool.LoadAll();
            }
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
                }
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