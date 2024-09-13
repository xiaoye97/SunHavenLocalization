using MiniExcelLibs;
using SunHavenLocalization;
using System;
using System.Collections.Generic;
using System.Data;

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
            infoTable.TableName = "Info";
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

            Dictionary<string, DataTable> tableDict = new Dictionary<string, DataTable>();

            foreach (var item in dataSheet)
            {
                if (!string.IsNullOrWhiteSpace(item.Table))
                {
                    if (!tableDict.ContainsKey(item.Table))
                    {
                        DataTable dataTable = new DataTable();
                        dataTable.TableName = item.Table;
                        dataTable.Columns.Add("Key", typeof(string));
                        dataTable.Columns.Add("Original", typeof(string));
                        dataTable.Columns.Add("OriginalTranslation", typeof(string));
                        dataTable.Columns.Add("Translation", typeof(string));
                        dataTable.Columns.Add("UpdateTime", typeof(string));
                        dataTable.Columns.Add("UpdateMode", typeof(string));
                        dataTable.Columns.Add("OriginalUpdateNote", typeof(string));
                        tableDict[item.Table] = dataTable;
                    }
                    tableDict[item.Table].Rows.Add(item.Key, item.Original, item.OriginalTranslation, item.Translation, item.UpdateTime, item.UpdateMode, item.OriginalUpdateNote);
                }
            }
            List<string> tableNameList = new List<string>();
            foreach (var kv in tableDict)
            {
                tableNameList.Add(kv.Key);
            }
            string tables = "";
            for (int i = 0; i < tableNameList.Count; i++)
            {
                tables += tableNameList[i];
                if (i != tableNameList.Count - 1)
                {
                    tables += ",";
                }
            }
            infoTable.Rows.Add("SubTables", tables);
            DataSet dataSet = new DataSet();
            dataSet.Tables.Add(infoTable);
            foreach (var kv in tableDict)
            {
                dataSet.Tables.Add(kv.Value);
            }
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
            Dictionary<string, string> infoDict = new Dictionary<string, string>();
            var t1 = MiniExcel.Query(excelPath, sheetName: "Info");
            foreach (IDictionary<string, object> row in t1)
            {
                List<string> list = new List<string>();
                foreach (dynamic data in row)
                {
                    list.Add(data.Value);
                }
                infoDict[list[0]] = list[1];
            }
            sheet.LanguageName = infoDict["LanguageName"];
            int.TryParse(infoDict["Version"], out sheet.Version);
            sheet.LastDumpTime = CommonTool.StringToTime(infoDict["LastDumpTime"]);
            int.TryParse(infoDict["LineCount"], out sheet.LineCount);
            long.TryParse(infoDict["OriginalCharCount"], out sheet.OriginalCharCount);
            string tables = infoDict["SubTables"];
            string[] tableNames = tables.Split(',');

            sheet.Dict = new Dictionary<string, LocItem>();
            foreach (string tableName in tableNames)
            {
                var t2 = MiniExcel.Query(excelPath, sheetName: tableName);
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
                        if (data.Value != null)
                        {
                            list.Add(data.Value.ToString());
                        }
                        else
                        {
                            list.Add("");
                        }
                    }

                    LocItem locItem = new LocItem();
                    locItem.Key = list[0];
                    locItem.Original = list[1];
                    locItem.OriginalTranslation = list[2];
                    locItem.Translation = list[3];
                    locItem.UpdateTime = CommonTool.StringToTime(list[4]);
                    locItem.UpdateMode = (UpdateMode)Enum.Parse(typeof(UpdateMode), list[5]);
                    locItem.OriginalUpdateNote = list[6];
                    locItem.Table = tableName;
                    sheet.Dict[locItem.Key] = locItem;
                    line++;
                }
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
                OriginalUpdateNote = locItem.OriginalUpdateNote,
                Table = locItem.Table,
            };
        }
    }
}