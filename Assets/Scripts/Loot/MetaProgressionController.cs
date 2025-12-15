using System.Collections.Generic;
using UnityEngine;

namespace ChogZombies.Loot
{
    public class MetaProgressionController : MonoBehaviour
    {
        [SerializeField] List<LootItemDefinition> allLootItems = new List<LootItemDefinition>();

        const string PlayerPrefsKey = "chog_owned_loot";
        const string PlayerPrefsLastKey = "chog_last_loot";
        readonly HashSet<string> _owned = new HashSet<string>();

        LootItemDefinition _lastAcquired;

        void Awake()
        {
            Load();
            LoadLastAcquired();
        }

        public IReadOnlyList<LootItemDefinition> AllLootItems => allLootItems;

        public LootItemDefinition LastAcquiredItem => _lastAcquired;

        public bool IsOwned(LootItemDefinition item)
        {
            if (item == null)
                return false;
            return _owned.Contains(GetKey(item));
        }

        public bool TryAddOwned(LootItemDefinition item)
        {
            if (item == null)
                return false;

            string key = GetKey(item);
            if (_owned.Contains(key))
                return false;

            _owned.Add(key);
            _lastAcquired = item;
            Save();
            SaveLastAcquired(item);
            return true;
        }

        public void ApplyOwnedToPlayer(PlayerLootController loot)
        {
            if (loot == null)
                return;

            for (int i = 0; i < allLootItems.Count; i++)
            {
                var item = allLootItems[i];
                if (item == null)
                    continue;

                if (_owned.Contains(GetKey(item)))
                    loot.TryApplyLoot(item);
            }
        }

        void Load()
        {
            _owned.Clear();
            string raw = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return;

            var parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (string.IsNullOrEmpty(p))
                    continue;
                _owned.Add(p);
            }
        }

        void Save()
        {
            var list = new List<string>(_owned);
            string raw = string.Join(";", list);
            PlayerPrefs.SetString(PlayerPrefsKey, raw);
            PlayerPrefs.Save();
        }

        void LoadLastAcquired()
        {
            string key = PlayerPrefs.GetString(PlayerPrefsLastKey, string.Empty);
            if (string.IsNullOrEmpty(key))
            {
                _lastAcquired = null;
                return;
            }

            for (int i = 0; i < allLootItems.Count; i++)
            {
                var item = allLootItems[i];
                if (item == null)
                    continue;
                if (GetKey(item) == key)
                {
                    _lastAcquired = item;
                    return;
                }
            }

            _lastAcquired = null;
        }

        void SaveLastAcquired(LootItemDefinition item)
        {
            if (item == null)
            {
                PlayerPrefs.DeleteKey(PlayerPrefsLastKey);
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsLastKey, GetKey(item));
            PlayerPrefs.Save();
        }

        static string GetKey(LootItemDefinition item)
        {
            if (item == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(item.Id))
                return item.Id;
            return item.name;
        }
    }
}
