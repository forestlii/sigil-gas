// Copyright 2026 Likeon All Rights Reserved.
// Unity 不能直接序列化 Dictionary，这里用可序列化列表 + 运行时字典缓存复刻。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>标签 -> 效果容器 的可序列化映射。</summary>
    [Serializable]
    public class SerializableEffectContainerMap : ISerializationCallbackReceiver
    {
        [Serializable]
        private struct Entry
        {
            public GameplayTag Key;
            public GameplayEffectContainer Value;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<GameplayTag, GameplayEffectContainer> _map;

        private Dictionary<GameplayTag, GameplayEffectContainer> Map
        {
            get
            {
                if (_map == null) Rebuild();
                return _map;
            }
        }

        private void Rebuild()
        {
            _map = new Dictionary<GameplayTag, GameplayEffectContainer>();
            foreach (var e in entries)
                if (e.Key.IsValid) _map[e.Key] = e.Value;
        }

        public bool Contains(GameplayTag tag) => Map.ContainsKey(tag);

        public bool TryGet(GameplayTag tag, out GameplayEffectContainer container)
            => Map.TryGetValue(tag, out container);

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => _map = null; // 下次访问时重建
    }
}
