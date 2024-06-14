using System;
using System.Collections.Generic;

namespace SunHavenLocalization
{
    [Serializable]
    public class LocItem
    {
        public string Key;

        /// <summary>
        /// 英文原文
        /// </summary>
        public string Original;

        /// <summary>
        /// 译文原文
        /// </summary>
        public string OriginalTranslation;

        public string Translation;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime;

        public UpdateMode UpdateMode;

        /// <summary>
        /// 原文翻译更新备注
        /// </summary>
        public string OriginalUpdateNote;

        public LocItem Clone()
        {
            return new LocItem()
            {
                Key = Key,
                Original = Original,
                Translation = Translation,
                OriginalTranslation = OriginalTranslation,
                UpdateTime = UpdateTime,
                UpdateMode = UpdateMode,
                OriginalUpdateNote = OriginalUpdateNote
            };
        }

        public LocItemSave ToLocItemSave()
        {
            return new LocItemSave()
            {
                Key = Key,
                Original = Original,
                OriginalTranslation = OriginalTranslation,
                Translation = Translation,
                UpdateTime = DumpTool.TimeToString(UpdateTime),
                UpdateMode = UpdateMode.ToString(),
                OriginalUpdateNote = OriginalUpdateNote
            };
        }
    }

    [Serializable]
    public class LocItemSave
    {
        public string Key = "";
        public string Original = "";
        public string OriginalTranslation = "";
        public string Translation = "";
        public string UpdateTime = "";
        public string UpdateMode = "";
        public string OriginalUpdateNote = "";
    }

    public enum UpdateMode
    {
        None,
        New,
        Remove,
        Update
    }

    [Serializable]
    public class LocStorage
    {
        public Dictionary<string, int> LangNameIndexDict = new Dictionary<string, int>();
        public Dictionary<int, string> IndexLangNameDict = new Dictionary<int, string>();
        public Dictionary<string, LocSheet> Storage = new Dictionary<string, LocSheet>();
    }

    [Serializable]
    public class LocStorageSave
    {
        public Serialization<string, LocSheet> Storage;
    }

    [Serializable]
    public class LocSheet
    {
        public string LanguageName;

        /// <summary>
        /// 此语言在I2语言列表中的索引
        /// </summary>
        public int LanguageIndex;

        public int Version;
        public DateTime LastDumpTime;
        public int LineCount;
        public long OriginalCharCount;
        public Dictionary<string, LocItem> Dict = new Dictionary<string, LocItem>();

        public LocSheetSave ToLocSheetSave()
        {
            Dictionary<string, LocItemSave> dict = new Dictionary<string, LocItemSave>();
            foreach (var kv in Dict)
            {
                dict[kv.Key] = kv.Value.ToLocItemSave();
            }
            return new LocSheetSave()
            {
                LanguageName = LanguageName,
                LanguageIndex = LanguageIndex,
                Version = Version,
                LastDumpTime = LastDumpTime,
                LineCount = LineCount,
                OriginalCharCount = OriginalCharCount,
                Dict = new Serialization<string, LocItemSave>(dict)
            };
        }
    }

    [Serializable]
    public class LocSheetSave
    {
        public string LanguageName;

        /// <summary>
        /// 此语言在I2语言列表中的索引
        /// </summary>
        public int LanguageIndex;

        public int Version;
        public DateTime LastDumpTime;
        public int LineCount;
        public long OriginalCharCount;
        public Serialization<string, LocItemSave> Dict;
    }
}