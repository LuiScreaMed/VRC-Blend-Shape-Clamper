using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using nadena.dev.ndmf.localization;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace LuiStudio.Utilities.BlendShapeClamper.Editor
{
    internal static class Localization
    {
        private const string _selfGuid = "efcdb6c20ac1dbd4eb849236df99682a";

        private static string _selfPath = AssetDatabase.GUIDToAssetPath(_selfGuid);

        private static ImmutableList<string>
            _languages = new string[] { "en-us", "ja-jp", "zh-hans" }.ToImmutableList();
        private static ImmutableDictionary<string, string>
            _languagesDisplayNames = new Dictionary<string, string>()
            {
                {"en-us", "English"},
                {"ja-jp", "日本語"},
                {"zh-hans", "中文"},
            }.ToImmutableDictionary();

        private static string[] _initedLangs = new string[0];
        private static string[] _initedLangsDisplayNames = new string[0];
        public static Action OnLanguageChange = null;

        public static Localizer Localizer;

        static Localization()
        {
            Localizer = new Localizer(_languages[0], () =>
            {
                List<(string, Func<string, string>)> lookups = new List<(string, Func<string, string>)>();
                foreach (string language in _languages)
                {
                    if (!TryLookupLanguage(language, out Func<string, string> func)) continue;
                    lookups.Add((language, func));
                }
                _initedLangs = lookups.Select((lookup) => lookup.Item1).ToArray();
                _initedLangsDisplayNames = _initedLangs.Select((lang) => _languagesDisplayNames[lang]).ToArray();
                return lookups;
            });
        }

        private static bool TryLookupLanguage(string lang, out Func<string, string> func)
        {
            var filename = _selfPath + "/" + lang + ".json";

            try
            {
                var langData = File.ReadAllText(filename);
                var langMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(langData);

                func = key =>
                {
                    if (langMap.TryGetValue(key, out var val)) return val;
                    else return null;
                };

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load language file " + filename);
                Debug.LogException(e);
                func = key => null;

                return false;
            }
        }

        public static string L(string key, string fallback)
        {
            if (Localizer != null && Localizer.TryGetLocalizedString(key, out var value))
            {
                return value;
            }
            return fallback;
        }

        public static string L(string key)
        {
            if (Localizer != null && Localizer.TryGetLocalizedString(key, out var value))
            {
                return value;
            }
            return key;
        }

        public static void LanguageSelect()
        {
            EditorGUILayout.Separator();

            var curLang = LanguagePrefs.Language;
            var curIndex = _languages.FindIndex((lang) => lang == curLang);

            EditorGUI.BeginChangeCheck();
            int newLang = EditorGUILayout.Popup("Editor Language", curIndex == -1 ? 0 : curIndex, _initedLangsDisplayNames);
            if (EditorGUI.EndChangeCheck())
            {
                LanguagePrefs.Language = _initedLangs[newLang];
                OnLanguageChange?.Invoke();
            }
        }
    }
}