using BepInEx;
using I2.Loc;
using System.IO;
using System;
using UnityEngine;

namespace SunHavenLocalization
{
    public static class LoadTool
    {
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
