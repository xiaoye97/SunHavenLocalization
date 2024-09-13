using System;
using System.IO;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;

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