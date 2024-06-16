using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;

namespace SunHavenLocalizationPublishTool
{
    internal class Program
    {
        public static string Version;
        private static void Main(string[] args)
        {
            Console.WriteLine("请输入版本号, 例如: 1.0.0");
            Version = Console.ReadLine();
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            // 建立打包文件夹
            DirectoryInfo buildDir = new DirectoryInfo("SunHavenLocalizationPublish");
            if (buildDir.Exists)
            {
                buildDir.Delete(true);
            }
            buildDir.Create();

            DirectoryInfo dataDirInfo = new DirectoryInfo("BepInEx/plugins/SunHavenLocalization/datas");
            var files = dataDirInfo.GetFiles("*.xyloc");
            foreach (var file in files)
            {
                string lang = file.Name.Replace(".xyloc", "");
                Build(lang);
            }

            sw.Stop();
            Console.WriteLine($"执行完毕，共耗时{sw.ElapsedMilliseconds}ms");
            Console.ReadLine();
        }

        public static void Build(string lang)
        {
            string pathNoBepInEx = $"SunHavenLocalizationPublish/SunHavenLocalization_{lang}";
            string pathWithBepInEx = $"SunHavenLocalizationPublish/SunHavenLocalization_{lang}_WithBepInEx";
            DirectoryInfo buildDir1 = new DirectoryInfo(pathNoBepInEx);
            if (buildDir1.Exists)
            {
                buildDir1.Delete(true);
            }
            buildDir1.Create();
            DirectoryInfo buildDir2 = new DirectoryInfo(pathWithBepInEx);
            if (buildDir2.Exists)
            {
                buildDir2.Delete(true);
            }
            buildDir2.Create();
            // 不带Bep的版本
            Console.WriteLine($"开始打包 {lang} 不带BepInex的版本...");
            // 复制插件
            CopyFile("BepInEx/plugins/SunHavenLocalization/SunHavenLocalization.dll", $"{pathNoBepInEx}/BepInEx/plugins/SunHavenLocalization/SunHavenLocalization.dll");
            CopyFile("BepInEx/plugins/SunHavenLocalization/SunHavenLocalizationExcelTool.exe", $"{pathNoBepInEx}/BepInEx/plugins/SunHavenLocalization/SunHavenLocalizationExcelTool.exe");
            // 复制数据
            CopyFile($"BepInEx/plugins/SunHavenLocalization/datas/{lang}.xyloc", $"{pathNoBepInEx}/BepInEx/plugins/SunHavenLocalization/datas/{lang}.xyloc");
            // 文件夹压缩
            Console.WriteLine($"开始压缩 {lang} 不带BepInEx的版本...");
            ZipFile(pathNoBepInEx, $"SunHavenLocalizationPublish/SunHavenLocalization_{lang}_V{Version}.zip");

            // 带Bep的版本
            Console.WriteLine($"开始打包 {lang} 带BepInEx的版本...");
            // 复制BepInEx
            CopyDirectory("BepInEx/core", $"{pathWithBepInEx}/BepInEx/core");
            CopyFile("doorstop_config.ini", $"{pathWithBepInEx}/doorstop_config.ini");
            CopyFile("winhttp.dll", $"{pathWithBepInEx}/winhttp.dll");
            // 复制配置文件
            CopyFile("BepInEx/config/BepInEx.cfg", $"{pathWithBepInEx}/BepInEx/config/BepInEx.cfg");
            // 复制插件
            CopyFile("BepInEx/plugins/SunHavenLocalization/SunHavenLocalization.dll", $"{pathWithBepInEx}/BepInEx/plugins/SunHavenLocalization/SunHavenLocalization.dll");
            CopyFile("BepInEx/plugins/SunHavenLocalization/SunHavenLocalizationExcelTool.exe", $"{pathWithBepInEx}/BepInEx/plugins/SunHavenLocalization/SunHavenLocalizationExcelTool.exe");
            // 复制数据
            CopyFile($"BepInEx/plugins/SunHavenLocalization/datas/{lang}.xyloc", $"{pathWithBepInEx}/BepInEx/plugins/SunHavenLocalization/datas/{lang}.xyloc");
            // 文件夹压缩
            Console.WriteLine($"开始压缩 {lang} 带BepInEx的版本...");
            ZipFile(pathWithBepInEx, $"SunHavenLocalizationPublish/SunHavenLocalization_{lang}_V{Version}_WithBepInEx.zip");
        }

        public static void CopyDirectory(string srcPath, string destPath, List<string> ignoreDirs = null, List<string> ignoreFiles = null)
        {
            try
            {
                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                }
                if (ignoreDirs == null) ignoreDirs = new List<string>();
                if (ignoreFiles == null) ignoreFiles = new List<string>();
                DirectoryInfo dir = new DirectoryInfo(srcPath); FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();  //获取目录下（不包含子目录）的文件和子目录
                foreach (FileSystemInfo i in fileinfo)
                {
                    // 判断是否文件夹
                    if (i is DirectoryInfo)
                    {
                        // 检查是否需要忽略，是的话就跳过
                        if (ignoreDirs.Contains(i.Name))
                        {
                            continue;
                        }
                        if (!Directory.Exists(destPath + "\\" + i.Name))
                        {
                            // 目标目录下不存在此文件夹即创建子文件夹
                            Directory.CreateDirectory(destPath + "\\" + i.Name);
                        }
                        // 递归调用复制子文件夹
                        CopyDirectory(i.FullName, destPath + "\\" + i.Name, ignoreDirs, ignoreFiles);
                    }
                    else
                    {
                        // 检查是否需要忽略，是的话就跳过
                        if (ignoreFiles.Contains(i.Name))
                        {
                            continue;
                        }
                        // 不是文件夹即复制文件，true表示可以覆盖同名文件
                        File.Copy(i.FullName, destPath + "\\" + i.Name, true);
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static void CopyFilesWithSearch(string srcPath, string destPath, List<string> fileContainsName = null)
        {
            try
            {
                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                }
                if (fileContainsName == null) fileContainsName = new List<string>();
                DirectoryInfo dir = new DirectoryInfo(srcPath); FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();  //获取目录下（不包含子目录）的文件和子目录
                foreach (FileSystemInfo i in fileinfo)
                {
                    if (i is FileInfo)
                    {
                        bool canCopy = false;
                        // 检查是否需要复制
                        foreach (var name in fileContainsName)
                        {
                            if (i.Name.Contains(name))
                            {
                                canCopy = true;
                                break;
                            }
                        }
                        if (canCopy)
                        {
                            // 不是文件夹即复制文件，true表示可以覆盖同名文件
                            File.Copy(i.FullName, destPath + "\\" + i.Name, true);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static void CopyFile(string srcPath, string destPath)
        {
            FileInfo src = new FileInfo(srcPath);
            if (src.Exists)
            {
                FileInfo dest = new FileInfo(destPath);
                if (!dest.Directory.Exists)
                {
                    dest.Directory.Create();
                }
                File.Copy(src.FullName, destPath, true);
            }
        }

        #region 文件压缩

        /// <summary>
        /// 将文件夹压缩
        /// </summary>
        /// <param name="srcFiles">文件夹路径</param>
        /// <param name="strZip">压缩之后的名称</param>
        public static void ZipFile(string srcFiles, string strZip)
        {
            ZipOutputStream zipStream = null;
            try
            {
                var len = srcFiles.Length;
                var strlen = srcFiles[len - 1];
                if (srcFiles[srcFiles.Length - 1] != Path.DirectorySeparatorChar)
                {
                    srcFiles += Path.DirectorySeparatorChar;
                }
                zipStream = new ZipOutputStream(File.Create(strZip));
                zipStream.SetLevel(6);
                zip(srcFiles, zipStream, srcFiles);
                zipStream.Finish();
                zipStream.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                //Clear Resource
                if (zipStream != null)
                {
                    zipStream.Finish();
                    zipStream.Close();
                }
            }
        }

        /// <summary>
        /// 将文件夹压缩
        /// </summary>
        /// <param name="srcFiles">文件夹路径</param>
        /// <param name="outstream">压缩包流</param>
        /// <param name="strZip">压缩之后的名称</param>
        public static void zip(string srcFiles, ZipOutputStream outstream, string staticFile)
        {
            if (srcFiles[srcFiles.Length - 1] != Path.DirectorySeparatorChar)
            {
                srcFiles += Path.DirectorySeparatorChar;
            }
            Crc32 crc = new Crc32();
            //获取指定目录下所有文件和子目录文件名称
            string[] filenames = Directory.GetFileSystemEntries(srcFiles);
            //遍历文件
            foreach (string file in filenames)
            {
                if (Directory.Exists(file))
                {
                    zip(file, outstream, staticFile);
                }
                //否则，直接压缩文件
                else
                {
                    //打开文件
                    FileStream fs = File.OpenRead(file);
                    //定义缓存区对象
                    byte[] buffer = new byte[fs.Length];
                    //通过字符流，读取文件
                    fs.Read(buffer, 0, buffer.Length);
                    //得到目录下的文件（比如:D:\Debug1\test）,test
                    string tempfile = file.Substring(staticFile.LastIndexOf("\\") + 1);
                    ZipEntry entry = new ZipEntry(tempfile);
                    entry.DateTime = DateTime.Now;
                    entry.Size = fs.Length;
                    fs.Close();
                    crc.Reset();
                    crc.Update(buffer);
                    entry.Crc = crc.Value;
                    outstream.PutNextEntry(entry);
                    //写文件
                    outstream.Write(buffer, 0, buffer.Length);
                }
            }
        }

        #endregion 文件压缩
    }
}