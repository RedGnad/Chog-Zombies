using System.Collections.Generic;
using UnityEngine;

namespace ChogZombies.Loot
{
    public class MetaProgressionController : MonoBehaviour
    {
        [SerializeField] List<LootItemDefinition> allLootItems = new List<LootItemDefinition>();

        const string PlayerPrefsKey = "chog_owned_loot";
        const string PlayerPrefsLastKey = "chog_last_loot";
        const string PlayerPrefsEquippedKey = "chog_equipped_loot";
        readonly HashSet<string> _owned = new HashSet<string>();
        readonly HashSet<string> _equipped = new HashSet<string>();

        LootItemDefinition _lastAcquired;

        void Awake()
        {
            Load();
            LoadLastAcquired();
            LoadEquipped();
        }

        public IReadOnlyList<LootItemDefinition> AllLootItems => allLootItems;

        public LootItemDefinition LastAcquiredItem => _lastAcquired;
        public IReadOnlyCollection<LootItemDefinition> EquippedItems
        {
            get
            {
                var result = new List<LootItemDefinition>();
                for (int i = 0; i < allLootItems.Count; i++)
                {
                    var item = allLootItems[i];
                    if (item == null)
                        continue;
                    if (_equipped.Contains(GetKey(item)))
                        result.Add(item);
                }
                return result;
            }
        }

        public List<LootItemDefinition> GetOwnedItems()
        {
            var result = new List<LootItemDefinition>();
            for (int i = 0; i < allLootItems.Count; i++)
            {
                var item = allLootItems[i];
                if (item != null && _owned.Contains(GetKey(item)))
                    result.Add(item);
            }
            return result;
        }

        public int OwnedCount => _owned.Count;
        public int TotalCount => allLootItems.Count;

        public bool IsOwned(LootItemDefinition item)
        {
            if (item == null)
                return false;
            return _owned.Contains(GetKey(item));
        }

        public bool IsEquipped(LootItemDefinition item)
        {
            if (item == null)
                return false;
            return _equipped.Contains(GetKey(item));
        }

        public bool TryAddOwned(LootItemDefinition item)
        {
            if (item == null)
                return false;

            string newKey = GetKey(item);
            string family = item.FamilyId;

            // Rechercher le meilleur tier déjà possédé dans cette famille
            LootItemDefinition bestOwnedInFamily = null;
            for (int i = 0; i < allLootItems.Count; i++)
            {
                var candidate = allLootItems[i];
                if (candidate == null)
                    continue;
                if (candidate.FamilyId != family)
                    continue;

                string candidateKey = GetKey(candidate);
                if (!_owned.Contains(candidateKey))
                    continue;

                if (bestOwnedInFamily == null || candidate.Rarity > bestOwnedInFamily.Rarity)
                    bestOwnedInFamily = candidate;
            }

            // Si on possède déjà un tier de rareté >=, on ignore le drop (pas de nouvel item)
            if (bestOwnedInFamily != null && bestOwnedInFamily.Rarity >= item.Rarity)
            {
                Debug.Log($"[Meta] Loot '{item.DisplayName}' ignoré : tier existant plus rare ou égal déjà possédé dans la famille '{family}'.");
                return false;
            }

            // Sinon, on remplace tous les tiers plus faibles de cette famille par le nouveau
            bool ownedChanged = false;
            bool equippedChanged = false;
            for (int i = 0; i < allLootItems.Count; i++)
            {
                var candidate = allLootItems[i];
                if (candidate == null)
                    continue;
                if (candidate.FamilyId != family)
                    continue;

                string candidateKey = GetKey(candidate);
                if (_owned.Remove(candidateKey))
                    ownedChanged = true;
                if (_equipped.Remove(candidateKey))
                    equippedChanged = true;
            }

            _owned.Add(newKey);
            ownedChanged = true;

            _lastAcquired = item;
            if (ownedChanged)
                Save();
            if (equippedChanged)
                SaveEquipped();
            SaveLastAcquired(item);

            Debug.Log($"[Meta] New loot acquired: {item.DisplayName} (family={family}, key={newKey})");
            LogOwnedItemsDebug();
            return true;
        }

        public bool SetEquipped(LootItemDefinition item, bool equipped)
        {
            if (item == null)
                return false;

            string key = GetKey(item);
            if (equipped && !_owned.Contains(key))
                return false;

            bool changed = false;
            if (equipped)
            {
                if (!_equipped.Contains(key))
                {
                    _equipped.Add(key);
                    changed = true;
                }
            }
            else
            {
                if (_equipped.Remove(key))
                    changed = true;
            }

            if (changed)
                SaveEquipped();

            return changed;
        }

        public List<string> GetOwnedKeys()
        {
            return new List<string>(_owned);
        }

        public List<string> GetEquippedKeys()
        {
            return new List<string>(_equipped);
        }

        public void ApplyRemoteState(IEnumerable<string> ownedKeys, IEnumerable<string> equippedKeys)
        {
            _owned.Clear();
            _equipped.Clear();

            if (ownedKeys != null)
            {
                foreach (var key in ownedKeys)
                {
                    if (string.IsNullOrEmpty(key))
                        continue;
                    _owned.Add(key);
                }
            }

            if (equippedKeys != null)
            {
                foreach (var key in equippedKeys)
                {
                    if (string.IsNullOrEmpty(key))
                        continue;
                    if (_owned.Contains(key))
                        _equipped.Add(key);
                }
            }

            Save();
            SaveEquipped();
        }

        public void LogOwnedItemsDebug()
        {
            var names = new List<string>();
            for (int i = 0; i < allLootItems.Count; i++)
            {
                var item = allLootItems[i];
                if (item == null)
                    continue;

                if (_owned.Contains(GetKey(item)))
                {
                    names.Add(string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName);
                }
            }

            Debug.Log($"[Meta] Owned items ({names.Count}/{allLootItems.Count}): " + (names.Count > 0 ? string.Join(", ", names) : "(none)"));
        }

        public void ApplyEquippedToPlayer(PlayerLootController loot)
        {
            if (loot == null)
                return;

            for (int i = 0; i < allLootItems.Count; i++)
            {
                var item = allLootItems[i];
                if (item == null)
                    continue;

                if (_equipped.Contains(GetKey(item)))
                    loot.TryEquip(item);
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

        void LoadEquipped()
        {
            _equipped.Clear();
            string raw = PlayerPrefs.GetString(PlayerPrefsEquippedKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return;

            var parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (string.IsNullOrEmpty(p))
                    continue;
                if (_owned.Contains(p))
                    _equipped.Add(p);
            }
        }

        void SaveEquipped()
        {
            var list = new List<string>(_equipped);
            string raw = string.Join(";", list);
            PlayerPrefs.SetString(PlayerPrefsEquippedKey, raw);
            PlayerPrefs.Save();
        }

        public static string GetKey(LootItemDefinition item)
        {
            if (item == null)
                return string.Empty;

            // Identifiant de base (asset ou champ Id explicite)
            string baseId = !string.IsNullOrEmpty(item.Id) ? item.Id : item.name;

            // Clé = identifiant + rareté pour distinguer les tiers d'une même famille
            return baseId + "_" + item.Rarity.ToString();
        }
    }
}
