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
}