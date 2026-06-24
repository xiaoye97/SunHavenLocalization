using System;
using System.IO;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;
using I2.Loc;
using UnityEngine;

namespace SunHavenLocalization
{
    public static class CommonTool
    {
        public static string TimeFormat = "yyyy-MM-dd HH:mm:ss";

        public static string TimeToString(DateTime time)
        {
            return time.ToString(TimeFormat);
        }

        public static DateTime StringToTime(string str)
        {
            try
            {
                DateTime dateTime = DateTime.ParseExact(str, TimeFormat, CultureInfo.InvariantCulture);
                return dateTime;
            }
            catch (Exception e)
            {
                Console.WriteLine($"字符串转换时间出错:[{str}]，异常信息：\n{e}");
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// 查找所有 LanguageSourceData，兼容 LanguageSourceAsset 和 LanguageSource。
        /// 优先搜索 LanguageSourceAsset（ScriptableObject），找不到则回退到 LanguageSource（MonoBehaviour）。
        /// </summary>
        public static LanguageSourceData[] FindAllSourceData()
        {
            // 优先尝试 LanguageSourceAsset
            var sourceAssets = Resources.FindObjectsOfTypeAll<LanguageSourceAsset>();
            if (sourceAssets.Length > 0)
            {
                SunHavenLocalizationPlugin.LogInfo($"[FindAllSourceData] Found {sourceAssets.Length} LanguageSourceAsset(s).");
                var result = new LanguageSourceData[sourceAssets.Length];
                for (int i = 0; i < sourceAssets.Length; i++)
                    result[i] = sourceAssets[i].SourceData;
                return result;
            }

            // 回退：尝试 LanguageSource（MonoBehaviour）
            var sources = Resources.FindObjectsOfTypeAll<LanguageSource>();
            if (sources.Length > 0)
            {
                SunHavenLocalizationPlugin.LogInfo($"[FindAllSourceData] Found {sources.Length} LanguageSource(s) (fallback).");
                var result = new LanguageSourceData[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                    result[i] = sources[i].SourceData;
                return result;
            }

            SunHavenLocalizationPlugin.LogWarning("[FindAllSourceData] No LanguageSourceAsset or LanguageSource found.");
            return new LanguageSourceData[0];
        }

        public static void SaveLocSheet(string path, LocSheet sheet)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // 序列化
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, sheet);
                ms.Close();
                var uncompressedData = ms.ToArray();

                // 压缩
                using (MemoryStream ms2 = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(ms2, CompressionMode.Compress, true))
                    {
                        gzipStream.Write(uncompressedData, 0, uncompressedData.Length);
                        gzipStream.Flush();
                        gzipStream.Dispose();
                        var compressedData = ms2.ToArray();
                        File.WriteAllBytes(path, compressedData);
                    }
                }
            }
        }

        public static LocSheet LoadLocSheet(string path)
        {
            FileInfo file = new FileInfo(path);
            var compressedData = File.ReadAllBytes(file.FullName);

            using (var ms = new MemoryStream(compressedData))
            {
                using (var gzipStream = new GZipStream(ms, CompressionMode.Decompress))
                using (var outputStream = new MemoryStream())
                {
                    gzipStream.CopyTo(outputStream);
                    var uncompressedData = outputStream.ToArray();
                    using (MemoryStream ms2 = new MemoryStream(uncompressedData))
                    {
                        //创建BinaryFormatter，准备反序列化
                        BinaryFormatter bf = new BinaryFormatter();
                        //反序列化
                        var sheet = (LocSheet)bf.Deserialize(ms2);
                        //关闭流
                        ms2.Close();
                        return sheet;
                    }
                }
            }
        }
    }
}
