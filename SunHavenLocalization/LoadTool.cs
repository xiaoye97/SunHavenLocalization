using BepInEx;
using I2.Loc;
using System.IO;
using UnityEngine;
using System;

namespace SunHavenLocalization
{
    public static class LoadTool
    {
        public static void LoadAll()
        {
            var sourceAssets = Resources.FindObjectsOfTypeAll<LanguageSourceAsset>();
            LocStorage storage = new LocStorage();
            DumpTool.InitStorage(sourceAssets[0].SourceData, storage);
            string saveDir = Paths.PluginPath + "/SunHavenLocalization/datas";
            DirectoryInfo directory = new DirectoryInfo(saveDir);
            if (!directory.Exists)
            {
                directory.Create();
            }
            var files = directory.GetFiles("*.xyloc");
            foreach (var file in files)
            {
                string langName = file.Name.Replace(".xyloc", "");
                if (storage.Storage.ContainsKey(langName))
                {
                    try
                    {
                        var sheet = CommonTool.LoadLocSheet(file.FullName);
                        if (sheet != null)
                        {
                            storage.Storage[langName] = sheet;
                            SunHavenLocalizationPlugin.LogInfo($"loaded language {file.Name}");
                        }
                        else
                        {
                            SunHavenLocalizationPlugin.LogError($"load language {file.Name} fail");
                        }
                    }
                    catch (Exception ex)
                    {
                        SunHavenLocalizationPlugin.LogError($"Exception when load language {file.Name}:\n{ex}");
                    }
                }
            }
            SunHavenLocalizationPlugin.LoadedLocStorage = storage;
        }
    }
}