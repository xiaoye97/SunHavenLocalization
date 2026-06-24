using BepInEx;
using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SunHavenLocalization
{
    public static class DumpTool
    {
        public static void DumpAll()
        {
            DateTime now = DateTime.Now;
            SunHavenLocalizationPlugin.LogInfo("[DumpAll] Starting dump...");

            var sourceDataArray = CommonTool.FindAllSourceData();
            SunHavenLocalizationPlugin.LogInfo($"[DumpAll] Found {sourceDataArray.Length} source(s).");

            if (sourceDataArray.Length == 0)
            {
                SunHavenLocalizationPlugin.LogError("[DumpAll] No LanguageSourceAsset or LanguageSource found! Localization data may not be loaded yet.");
                return;
            }

            // Log each source
            for (int i = 0; i < sourceDataArray.Length; i++)
            {
                var sd = sourceDataArray[i];
                int termCount = sd != null ? sd.mTerms.Count : 0;
                int langCount = sd != null ? sd.mLanguages.Count : 0;
                SunHavenLocalizationPlugin.LogInfo($"[DumpAll]   Source[{i}]: terms={termCount}, languages={langCount}");
            }

            LocStorage lastStorage = null;
            if (SunHavenLocalizationPlugin.LoadedLocStorage != null)
            {
                lastStorage = SunHavenLocalizationPlugin.LoadedLocStorage;
                SunHavenLocalizationPlugin.LogInfo("[DumpAll] Using existing LoadedLocStorage.");
            }
            else
            {
                SunHavenLocalizationPlugin.LogInfo("[DumpAll] No existing LoadedLocStorage, initializing new one...");
                lastStorage = new LocStorage();
                InitStorage(sourceDataArray[0], lastStorage);
            }

            LocStorage newStorage = new LocStorage();
            InitStorage(sourceDataArray[0], newStorage);
            SunHavenLocalizationPlugin.LogInfo($"[DumpAll] New storage initialized with {newStorage.Storage.Count} languages.");

            // Log language list
            foreach (var kv in newStorage.Storage)
            {
                SunHavenLocalizationPlugin.LogInfo($"[DumpAll]   Language: {kv.Key} (index={kv.Value.LanguageIndex})");
            }

            List<string> newKeyList = new List<string>();
            foreach (var sourceData in sourceDataArray)
            {
                // Special handling: quest table has 22 languages, others have 21
                if (sourceData.mLanguages.Count > sourceDataArray[0].mLanguages.Count)
                {
                    SunHavenLocalizationPlugin.LogWarning($"[DumpAll] Source has {sourceData.mLanguages.Count} languages (expected {sourceDataArray[0].mLanguages.Count}). Removing first language.");
                    sourceData.mLanguages.RemoveAt(0);
                    foreach (var term in sourceData.mTerms)
                    {
                        var newList = term.Languages.ToList();
                        newList.RemoveAt(0);
                        term.Languages = newList.ToArray();
                    }
                }
                foreach (var term in sourceData.mTerms)
                {
                    if (term.TermType == eTermType.Text)
                    {
                        newKeyList.Add(term.Term);
                    }
                }
            }
            SunHavenLocalizationPlugin.LogInfo($"[DumpAll] Total text terms across all sources: {newKeyList.Count}");

            // Copy old entries and mark removed ones
            int removedCount = 0;
            foreach (var sheetKV in lastStorage.Storage)
            {
                var lastSheet = sheetKV.Value;
                if (!newStorage.Storage.ContainsKey(sheetKV.Key))
                {
                    SunHavenLocalizationPlugin.LogWarning($"[DumpAll] Language '{sheetKV.Key}' not found in new storage, skipping.");
                    continue;
                }
                var newSheet = newStorage.Storage[sheetKV.Key];
                newSheet.Version = lastSheet.Version;
                foreach (var kv in lastSheet.Dict)
                {
                    LocItem item = kv.Value.Clone();
                    if (!newKeyList.Contains(kv.Key))
                    {
                        if (item.UpdateMode != UpdateMode.Remove)
                        {
                            item.UpdateMode = UpdateMode.Remove;
                            item.UpdateTime = now;
                            removedCount++;
                        }
                    }
                    newSheet.Dict[kv.Key] = item;
                }
            }
            SunHavenLocalizationPlugin.LogInfo($"[DumpAll] Marked {removedCount} entries as removed.");

            // Add new entries and update existing ones
            int newCount = 0;
            int updatedCount = 0;
            foreach (var sourceData in sourceDataArray)
            {
                int termCount = sourceData.mTerms.Count;
                SunHavenLocalizationPlugin.LogInfo($"[DumpAll] Processing source ({termCount} terms)...");
                for (int i = 0; i < termCount; i++)
                {
                    var term = sourceData.mTerms[i];
                    if (term.TermType == eTermType.Text)
                    {
                        string key = term.Term;
                        string ori = term.Languages[0];
                        for (int langIndex = 0; langIndex < term.Languages.Length; langIndex++)
                        {
                            if (newStorage.IndexLangNameDict.ContainsKey(langIndex))
                            {
                                string oriTranslation = term.Languages[langIndex];
                                string langName = newStorage.IndexLangNameDict[langIndex];
                                LocSheet newSheet = newStorage.Storage[langName];
                                if (newSheet.Dict.ContainsKey(key))
                                {
                                    var item = newSheet.Dict[key];
                                    if (item.Original == ori)
                                    {
                                        item.UpdateMode = UpdateMode.None;
                                        item.OriginalUpdateNote = "";
                                    }
                                    else
                                    {
                                        item.OriginalUpdateNote = $"[{CommonTool.TimeToString(item.UpdateTime)}]{item.Original}\n[{CommonTool.TimeToString(now)}]{ori}";
                                        item.Original = ori;
                                        item.OriginalTranslation = oriTranslation;
                                        item.UpdateTime = now;
                                        item.UpdateMode = UpdateMode.Update;
                                        updatedCount++;
                                    }
                                }
                                else
                                {
                                    LocItem item = new LocItem()
                                    {
                                        Key = key,
                                        Original = ori,
                                        OriginalTranslation = oriTranslation,
                                        UpdateTime = now,
                                        UpdateMode = UpdateMode.New,
                                    };
                                    newSheet.Dict[key] = item;
                                    newCount++;
                                }
                            }
                        }
                    }
                }
            }
            SunHavenLocalizationPlugin.LogInfo($"[DumpAll] New entries: {newCount}, Updated entries: {updatedCount}");

            // Update statistics
            foreach (var sheetKV in newStorage.Storage)
            {
                var sheet = sheetKV.Value;
                sheet.Version++;
                sheet.LastDumpTime = now;
                sheet.LineCount = sheet.Dict.Count;
                long charCount = 0;
                foreach (var kv in sheet.Dict)
                {
                    var item = kv.Value;
                    if (item.UpdateMode != UpdateMode.Remove)
                    {
                        charCount += item.Original.Length;
                    }
                }
                sheet.OriginalCharCount = charCount;
                SunHavenLocalizationPlugin.LogInfo($"[DumpAll]   {sheetKV.Key}: {sheet.LineCount} entries, {charCount} chars");
            }

            SaveLocStorage(newStorage);
            SunHavenLocalizationPlugin.LogInfo("[DumpAll] Dump complete!");
        }

        public static void SaveLocStorage(LocStorage locStorage)
        {
            string saveDir = Paths.PluginPath + "/SunHavenLocalization/datas";
            DirectoryInfo directory = new DirectoryInfo(saveDir);
            if (!directory.Exists)
            {
                directory.Create();
                SunHavenLocalizationPlugin.LogInfo($"[SaveLocStorage] Created directory: {saveDir}");
            }
            foreach (var sheetKV in locStorage.Storage)
            {
                string path = saveDir + "/" + sheetKV.Key + ".xyloc";
                SaveLocSheet(path, sheetKV.Value);
            }
        }

        public static void SaveLocSheet(string path, LocSheet sheet)
        {
            try
            {
                CommonTool.SaveLocSheet(path, sheet);
                SunHavenLocalizationPlugin.LogInfo($"[SaveLocSheet] Saved {path} ({sheet.Dict.Count} entries)");
            }
            catch (Exception ex)
            {
                SunHavenLocalizationPlugin.LogError($"[SaveLocSheet] Exception when save {path}:\n{ex}");
            }
        }

        public static void InitStorage(LanguageSourceData sourceData, LocStorage store)
        {
            store.LangNameIndexDict.Clear();
            store.IndexLangNameDict.Clear();
            for (int i = 0; i < sourceData.mLanguages.Count; i++)
            {
                var lang = sourceData.mLanguages[i];
                if (!string.IsNullOrWhiteSpace(lang.Code))
                {
                    store.LangNameIndexDict[lang.Code] = i;
                    store.IndexLangNameDict[i] = lang.Code;
                    if (!store.Storage.ContainsKey(lang.Code))
                    {
                        store.Storage.Add(lang.Code, new LocSheet());
                    }
                    store.Storage[lang.Code].LanguageName = lang.Code;
                    store.Storage[lang.Code].LanguageIndex = i;
                }
            }
        }
    }
}
