using BepInEx;
using I2.Loc;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace SunHavenLocalization
{
    public static class LoadTool
    {
        public static void LoadAll()
        {
            var source = GameObject.FindObjectOfType<LanguageSource>();
            LocStorage storage = new LocStorage();
            DumpTool.InitStorage(source, storage);
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
                    var bytes = File.ReadAllBytes(file.FullName);
                    MemoryStream stream = new MemoryStream(bytes);
                    //创建BinaryFormatter，准备反序列化
                    BinaryFormatter bf = new BinaryFormatter();
                    //反序列化
                    var sheet = (LocSheet)bf.Deserialize(stream);
                    //关闭流
                    stream.Close();
                    if (sheet != null)
                    {
                        storage.Storage[langName] = sheet;
                        Debug.Log($"加载了语言库 {file.Name}");
                    }
                }
            }
            SunHavenLocalizationPlugin.LoadedLocStorage = storage;
        }
    }
}