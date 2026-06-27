using BepInEx;
using I2.Loc;
using System.IO;
using System;
using UnityEngine;

namespace SunHavenLocalization
{
    public static class LoadTool
    {
        public static bool LoadXylocFilesOnly()
        {
            SunHavenLocalizationPlugin.LogInfo("[LoadXylocOnly] Starting load...");

            string saveDir = Paths.PluginPath + "/SunHavenLocalization/datas";
            SunHavenLocalizationPlugin.LogInfo($"[LoadXylocOnly] Looking for xyloc files in: {saveDir}");

            DirectoryInfo directory = new DirectoryInfo(saveDir);
            if (!directory.Exists)
            {
                SunHavenLocalizationPlugin.LogError($"[LoadXylocOnly] Directory does not exist: {saveDir}");
                return false;
            }

            var files = directory.GetFiles("*.xyloc");
            SunHavenLocalizationPlugin.LogInfo($"[LoadXylocOnly] Found {files.Length} xyloc file(s).");
            if (files.Length == 0)
                return false;

            LocStorage storage = new LocStorage();
            int languageIndex = 0;

            foreach (var file in files)
            {
                string langName = Path.GetFileNameWithoutExtension(file.Name);
                SunHavenLocalizationPlugin.LogInfo($"[LoadXylocOnly] Loading {file.Name} (language: {langName})...");

                try
                {
                    var sheet = CommonTool.LoadLocSheet(file.FullName);
                    if (sheet == null)
                    {
                        SunHavenLocalizationPlugin.LogError($"[LoadXylocOnly]   Failed to load {file.Name} (returned null).");
                        continue;
                    }

                    sheet.LanguageName = langName;
                    sheet.LanguageIndex = languageIndex;
                    storage.LangNameIndexDict[langName] = languageIndex;
                    storage.IndexLangNameDict[languageIndex] = langName;
                    storage.Storage[langName] = sheet;
                    languageIndex++;

                    SunHavenLocalizationPlugin.LogInfo($"[LoadXylocOnly]   Loaded {sheet.Dict.Count} entries for {langName}.");
                }
                catch (Exception ex)
                {
                    SunHavenLocalizationPlugin.LogError($"[LoadXylocOnly]   Exception when loading {file.Name}:\n{ex}");
                }
            }

            if (storage.Storage.Count == 0)
            {
                SunHavenLocalizationPlugin.LogError("[LoadXylocOnly] No xyloc sheet loaded.");
                return false;
            }

            SunHavenLocalizationPlugin.LoadedLocStorage = storage;
            SunHavenLocalizationPlugin.LogInfo("[LoadXylocOnly] Load complete.");
            return true;
        }

        public static void LoadAll()
        {
            SunHavenLocalizationPlugin.LogInfo("[LoadAll] Starting load...");

            var sourceDataArray = CommonTool.FindAllSourceData();
            SunHavenLocalizationPlugin.LogInfo($"[LoadAll] Found {sourceDataArray.Length} source(s).");

            if (sourceDataArray.Length == 0)
            {
                SunHavenLocalizationPlugin.LogError("[LoadAll] No LanguageSourceAsset or LanguageSource found!");
                return;
            }

            LocStorage storage = new LocStorage();
            DumpTool.InitStorage(sourceDataArray[0], storage);
            SunHavenLocalizationPlugin.LogInfo($"[LoadAll] Initialized storage with {storage.Storage.Count} languages.");

            string saveDir = Paths.PluginPath + "/SunHavenLocalization/datas";
            SunHavenLocalizationPlugin.LogInfo($"[LoadAll] Looking for xyloc files in: {saveDir}");

            DirectoryInfo directory = new DirectoryInfo(saveDir);
            if (!directory.Exists)
            {
                SunHavenLocalizationPlugin.LogWarning($"[LoadAll] Directory does not exist: {saveDir}");
                directory.Create();
                SunHavenLocalizationPlugin.LogInfo($"[LoadAll] Created directory: {saveDir}");
                SunHavenLocalizationPlugin.LoadedLocStorage = storage;
                return;
            }

            var files = directory.GetFiles("*.xyloc");
            SunHavenLocalizationPlugin.LogInfo($"[LoadAll] Found {files.Length} xyloc file(s).");

            foreach (var file in files)
            {
                string langName = file.Name.Replace(".xyloc", "");
                SunHavenLocalizationPlugin.LogInfo($"[LoadAll] Loading {file.Name} (language: {langName})...");

                if (storage.Storage.ContainsKey(langName))
                {
                    try
                    {
                        var sheet = CommonTool.LoadLocSheet(file.FullName);
                        if (sheet != null)
                        {
                            storage.Storage[langName] = sheet;
                            SunHavenLocalizationPlugin.LogInfo($"[LoadAll]   Loaded {sheet.Dict.Count} entries for {langName}.");
                        }
                        else
                        {
                            SunHavenLocalizationPlugin.LogError($"[LoadAll]   Failed to load {file.Name} (returned null).");
                        }
                    }
                    catch (Exception ex)
                    {
                        SunHavenLocalizationPlugin.LogError($"[LoadAll]   Exception when loading {file.Name}:\n{ex}");
                    }
                }
                else
                {
                    SunHavenLocalizationPlugin.LogWarning($"[LoadAll]   Language '{langName}' not found in game's language list, skipping.");
                }
            }

            SunHavenLocalizationPlugin.LoadedLocStorage = storage;
            SunHavenLocalizationPlugin.LogInfo("[LoadAll] Load complete.");
        }
    }
}
