// Copyright 2026 Likeon All Rights Reserved.
// 编辑器：查找或创建 GameplayTagsSettings 资产。

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    internal static class GameplayTagSettingsUtil
    {
        private const string DefaultDir = "Assets/LikeonGAS";
        private const string DefaultPath = DefaultDir + "/GameplayTagsSettings.asset";

        private static GameplayTagsSettings _cached;

        /// <summary>查找工程内已有的标签表；没有则在 Assets/LikeonGAS 下创建一个。</summary>
        public static GameplayTagsSettings GetOrCreate()
        {
            if (_cached != null) return _cached;

            var guids = AssetDatabase.FindAssets("t:GameplayTagsSettings");
            if (guids.Length > 0)
            {
                _cached = AssetDatabase.LoadAssetAtPath<GameplayTagsSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
                return _cached;
            }

            if (!Directory.Exists(DefaultDir)) Directory.CreateDirectory(DefaultDir);
            var settings = ScriptableObject.CreateInstance<GameplayTagsSettings>();
            AssetDatabase.CreateAsset(settings, DefaultPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _cached = settings;
            return settings;
        }

        public static void Save(GameplayTagsSettings settings)
        {
            if (settings == null) return;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
