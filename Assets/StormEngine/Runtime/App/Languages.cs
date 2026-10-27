using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// The game's languages. Translations live in Resources/StormEngine/Lang/{code}.json as
    /// { "English text": "translation" } (see Tools/loc). The phone's language is picked on first
    /// launch; players can change it in Settings.
    /// </summary>
    public static class Languages
    {
        public sealed class Info
        {
            public string Code;
            /// <summary>The language's own name, shown in the picker.</summary>
            public string Native;
            public string English;
            /// <summary>Needs the advanced text generator (shaping, right-to-left).</summary>
            public bool Complex;
        }

        static Info Lang(string code, string native, string english, bool complex = false) =>
            new Info { Code = code, Native = native, English = english, Complex = complex };

        public static readonly Info[] All =
        {
            Lang("en", "English", "English"),
            Lang("es", "Español", "Spanish"),
            Lang("pt", "Português (Brasil)", "Portuguese"),
            Lang("fr", "Français", "French"),
            Lang("de", "Deutsch", "German"),
            Lang("it", "Italiano", "Italian"),
            Lang("ru", "Русский", "Russian"),
            Lang("tr", "Türkçe", "Turkish"),
            Lang("pl", "Polski", "Polish"),
            Lang("id", "Bahasa Indonesia", "Indonesian"),
            Lang("vi", "Tiếng Việt", "Vietnamese"),
            Lang("th", "ไทย", "Thai", true),
            // Not translated yet: add each line back once its Lang/{code}.json is built.
            // Players whose phones use one of these get English until then, and switch automatically after.
            // Lang("hi", "हिन्दी", "Hindi", true),
            // Lang("ar", "العربية", "Arabic", true),
            // Lang("ja", "日本語", "Japanese"),
            // Lang("ko", "한국어", "Korean"),
            // Lang("zh-Hans", "简体中文", "Chinese (Simplified)"),
            // Lang("zh-Hant", "繁體中文", "Chinese (Traditional)"),
        };

        public static Info Current { get; private set; } = All[0];

        public static Info Find(string code)
        {
            foreach (var l in All)
                if (l.Code == code) return l;
            return null;
        }

        /// <summary>The phone's language if the game speaks it, otherwise English.</summary>
        public static string Detect()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Russian:
                case SystemLanguage.Belarusian: return "ru";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Vietnamese: return "vi";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Hindi: return "hi";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                default: return "en";
            }
        }

        /// <summary>Loads a language's table and makes it current. Unknown codes fall back to English.</summary>
        public static void Apply(string code)
        {
            var info = Find(code) ?? All[0];
            Current = info;
            if (info.Code == "en")
            {
                L.Use("en", null);
                return;
            }
            L.Use(info.Code, Load(info.Code));
        }

        public static Dictionary<string, string> Load(string code)
        {
            var table = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("StormEngine/Lang/" + code);
            if (asset == null)
            {
                Debug.LogWarning("[StormEngine] No translation file for " + code);
                return table;
            }
            if (JsonReader.TryParse(asset.text, out var root) && root is Dictionary<string, object> map)
                foreach (var kv in map)
                    if (kv.Value is string s && s.Length > 0) table[kv.Key] = s;
            Resources.UnloadAsset(asset);
            return table;
        }
    }
}
