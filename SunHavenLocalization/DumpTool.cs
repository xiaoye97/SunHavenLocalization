﻿using BepInEx;
using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SunHavenLocalization
{
    public static class DumpTool
    {
        public static void DumpAll()
        {
            DateTime now = DateTime.Now;
            var source = GameObject.FindObjectOfType<LanguageSource>();
            LocStorage lastStorage = null;
            if (SunHavenLocalizationPlugin.LoadedLocStorage != null)
            {
                lastStorage = SunHavenLocalizationPlugin.LoadedLocStorage;
            }
            else
            {
                lastStorage = new LocStorage();
                InitStorage(source, lastStorage);
            }
            LocStorage newStorage = new LocStorage();
            InitStorage(source, newStorage);
            int termCount = source.SourceData.mTerms.Count;
            List<string> newKeyList = new List<string>();
            foreach (var term in source.SourceData.mTerms)
            {
                if (term.TermType == eTermType.Text)
                {
                    newKeyList.Add(term.Term);
                }
            }
            // 复制老表到新表, 并统计老表中被移除的条目, 将这些条目标记为移除
            foreach (var sheetKV in lastStorage.Storage)
            {
                var lastSheet = sheetKV.Value;
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
                        }
                    }
                    newSheet.Dict[kv.Key] = item;
                }
            }
            // 添加新的条目和更新老条目内容
            for (int i = 0; i < termCount; i++)
            {
                var term = source.SourceData.mTerms[i];
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
                            // 如果表里有此条目, 则检查是否更新了
                            if (newSheet.Dict.ContainsKey(key))
                            {
                                var item = newSheet.Dict[key];
                                // 如果条目没有更新, 则将更新类型置为空
                                if (item.Original == ori)
                                {
                                    item.UpdateMode = UpdateMode.None;
                                    item.OriginalUpdateNote = "";
                                }
                                // 如果条目有更新, 则将更新类型置为更新
                                else
                                {
                                    item.OriginalUpdateNote = $"[{CommonTool.TimeToString(item.UpdateTime)}]{item.Original}\n[{CommonTool.TimeToString(now)}]{ori}";

                                    item.Original = ori;
                                    item.OriginalTranslation = oriTranslation;
                                    item.UpdateTime = now;
                                    item.UpdateMode = UpdateMode.Update;
                                }
                            }
                            // 如果表里没有此条目, 则新增
                            else
                            {
                                LocItem item = new LocItem()
                                {
                                    Key = key,
                                    Original = ori,
                                    OriginalTranslation = oriTranslation,
                                    UpdateTime = now,
                                    UpdateMode = UpdateMode.New
                                };
                                newSheet.Dict[key] = item;
                            }
                        }
                    }
                }
            }
            // 更新统计信息
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
            }
            SaveLocStorage(newStorage);
        }

        public static void SaveLocStorage(LocStorage locStorage)
        {
            string saveDir = Paths.PluginPath + "/SunHavenLocalization/datas";
            DirectoryInfo directory = new DirectoryInfo(saveDir);
            if (!directory.Exists)
            {
                directory.Create();
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
                SunHavenLocalizationPlugin.LogInfo($"saved {path}");
            }
            catch (Exception ex)
            {
                SunHavenLocalizationPlugin.LogError($"Exception when save {path}:\n{ex}");
            }
        }

        public static void InitStorage(LanguageSource source, LocStorage store)
        {
            store.LangNameIndexDict.Clear();
            store.IndexLangNameDict.Clear();
            for (int i = 0; i < source.SourceData.mLanguages.Count; i++)
            {
                var lang = source.SourceData.mLanguages[i];
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