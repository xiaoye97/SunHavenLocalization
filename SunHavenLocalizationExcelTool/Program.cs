using MiniExcelLibs;
using SunHavenLocalization;
using System;
using System.Data;
using System.Collections.Generic;

namespace SunHavenLocalizationExcelTool
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                LogUseage();
            }
            else
            {
                string arg = args[0];
                if (arg.EndsWith(".xyloc"))
                {
                    LocToExcel(arg);
                }
                else if (arg.EndsWith(".xlsx"))
                {
                    ExcelToLoc(arg);
                }
                else
                {
                    LogUseage();
                }
            }
            Console.ReadLine();
        }

        public static void LocToExcel(string locPath)
        {
            Console.WriteLine($"Loading {locPath}");
            LocSheet sheet = null;
            try
            {
                sheet = CommonTool.LoadLocSheet(locPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception when loading file:\n{ex}");
                return;
            }

            if (sheet == null)
            {
                Console.WriteLine($"Deserialize file fail.");
                return;
            }
            DataTable infoTable = new DataTable();
            infoTable.Columns.Add("Info", typeof(string));
            infoTable.Columns.Add("Value", typeof(string));
            infoTable.Rows.Add("Plugin Author", "xiaoye97");
            infoTable.Rows.Add("bilibili", "https://space.bilibili.com/1306433");
            infoTable.Rows.Add("GitHub", "https://github.com/xiaoye97");
            infoTable.Rows.Add("LanguageName", sheet.LanguageName);
            infoTable.Rows.Add("Version", sheet.Version);
            infoTable.Rows.Add("LastDumpTime", CommonTool.TimeToString(sheet.LastDumpTime));
            infoTable.Rows.Add("LineCount", sheet.LineCount.ToString());
            infoTable.Rows.Add("OriginalCharCount", sheet.OriginalCharCount.ToString());

            List<LocItemSave> locItemSaves = new List<LocItemSave>();
            foreach (var kv in sheet.Dict)
            {
                locItemSaves.Add(LocItemToSave(kv.Value));
            }
            locItemSaves.Sort((a, b) => a.Key.CompareTo(b.Key));
            LocItemSave[] dataSheet = locItemSaves.ToArray();

            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("Key", typeof(string));
            dataTable.Columns.Add("Original", typeof(string));
            dataTable.Columns.Add("OriginalTranslation", typeof(string));
            dataTable.Columns.Add("Translation", typeof(string));
            dataTable.Columns.Add("UpdateTime", typeof(string));
            dataTable.Columns.Add("UpdateMode", typeof(string));
            dataTable.Columns.Add("OriginalUpdateNote", typeof(string));
            foreach (var item in dataSheet)
            {
                dataTable.Rows.Add(item.Key, item.Original, item.OriginalTranslation, item.Translation, item.UpdateTime, item.UpdateMode, item.OriginalUpdateNote);
            }
            DataSet dataSet = new DataSet();
            dataSet.Tables.Add(infoTable);
            dataSet.Tables.Add(dataTable);
            string path = $"{Environment.CurrentDirectory}/{sheet.LanguageName}.xlsx";
            try
            {
                MiniExcel.SaveAs(path, dataSet, excelType: ExcelType.XLSX, overwriteFile: true);
                Console.WriteLine($"saved {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception when save {path}:\n{ex}");
            }
        }

        public static void ExcelToLoc(string excelPath)
        {
            LocSheet sheet = new LocSheet();
            Dictionary<string, string> Table1Dict = new Dictionary<string, string>();
            var t1 = MiniExcel.Query(excelPath, sheetName: "Table1");
            foreach (IDictionary<string, object> row in t1)
            {
                List<string> list = new List<string>();
                foreach (dynamic data in row)
                {
                    list.Add(data.Value);
                }
                Table1Dict[list[0]] = list[1];
            }
            sheet.LanguageName = Table1Dict["LanguageName"];
            int.TryParse(Table1Dict["Version"], out sheet.Version);
            sheet.LastDumpTime = CommonTool.StringToTime(Table1Dict["LastDumpTime"]);
            int.TryParse(Table1Dict["LineCount"], out sheet.LineCount);
            long.TryParse(Table1Dict["OriginalCharCount"], out sheet.OriginalCharCount);

            sheet.Dict = new Dictionary<string, LocItem>();
            var t2 = MiniExcel.Query(excelPath, sheetName: "Table2");
            int line = 0;
            foreach (IDictionary<string, object> row in t2)
            {
                if (line == 0)
                {
                    line++;
                    continue;
                }
                List<string> list = new List<string>();
                foreach (dynamic data in row)
                {
                    list.Add(data.Value);
                }

                LocItem locItem = new LocItem();
                locItem.Key = list[0];
                locItem.Original = list[1];
                locItem.OriginalTranslation = list[2];
                locItem.Translation = list[3];
                locItem.UpdateTime = CommonTool.StringToTime(list[4]);
                locItem.UpdateMode = (UpdateMode)Enum.Parse(typeof(UpdateMode), list[5]);
                locItem.OriginalUpdateNote = list[6];
                sheet.Dict[locItem.Key] = locItem;
                line++;
            }
            string path = $"{Environment.CurrentDirectory}/{sheet.LanguageName}.xyloc";
            try
            {
                CommonTool.SaveLocSheet(path, sheet);
                Console.WriteLine($"saved {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception when save {path}:\n{ex}");
            }
        }

        public static void LogUseage()
        {
            Console.WriteLine("Useage: \n\tinput <langName.xyloc> to generate excel.\n\tinput <langName.xlsx> to generate xyloc");
        }

        public static LocItemSave LocItemToSave(LocItem locItem)
        {
            return new LocItemSave()
            {
                Key = locItem.Key,
                Original = locItem.Original,
                OriginalTranslation = locItem.OriginalTranslation,
                Translation = locItem.Translation,
                UpdateTime = CommonTool.TimeToString(locItem.UpdateTime),
                UpdateMode = locItem.UpdateMode.ToString(),
                OriginalUpdateNote = locItem.OriginalUpdateNote
            };
        }
    }
}