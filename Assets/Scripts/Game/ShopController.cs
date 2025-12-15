using System.Collections.Generic;
using UnityEngine;
using ChogZombies.Loot;
using ChogZombies.Player;

namespace ChogZombies.Game
{
    public class ShopController : MonoBehaviour
    {
        [SerializeField] RunGameController runGame;
        [SerializeField] LootTable shopLootTable;
        [SerializeField] int slotCost = 3;
        [SerializeField] int slotsCount = 2;

        [SerializeField] bool uniqueOffers = true;

        LootItemDefinition[] _offers;

        void Awake()
        {
            if (slotsCount < 1)
                slotsCount = 1;
        }

        public void GenerateOffers()
        {
            if (shopLootTable == null)
                return;

            if (runGame == null)
                runGame = FindObjectOfType<RunGameController>();

            var player = PlayerCombatController.Main;
            if (player == null)
                return;

            int levelIndex = RunGameController.CurrentLevelIndex;
            int baseSeed = runGame != null ? runGame.Seed : 12345;
            int rngSeed = baseSeed ^ (levelIndex * 19349663) ^ 0x1234abcd;
            var rng = new System.Random(rngSeed);

            if (_offers == null || _offers.Length != slotsCount)
                _offers = new LootItemDefinition[slotsCount];

            var used = uniqueOffers ? new HashSet<LootItemDefinition>() : null;

            for (int i = 0; i < slotsCount; i++)
            {
                LootItemDefinition picked = null;

                if (!uniqueOffers)
                {
                    picked = shopLootTable.RollItem(rng);
                }
                else
                {
                    const int maxAttempts = 12;
                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        var candidate = shopLootTable.RollItem(rng);
                        if (candidate == null)
                        {
                            picked = null;
                            break;
                        }

                        if (!used.Contains(candidate))
                        {
                            picked = candidate;
                            used.Add(candidate);
                            break;
                        }

                        picked = candidate;
                    }
                }

                _offers[i] = picked;
                if (uniqueOffers && _offers[i] != null)
                    used.Add(_offers[i]);
            }
        }

        public void BuySlot(int index)
        {
            if (_offers == null)
                return;
            if (index < 0 || index >= _offers.Length)
                return;

            var item = _offers[index];
            if (item == null)
                return;

            if (runGame == null)
                runGame = FindObjectOfType<RunGameController>();
            if (runGame == null)
                return;

            var player = PlayerCombatController.Main;
            if (player == null)
                return;

            if (!runGame.TrySpendGold(slotCost))
            {
                Debug.Log("Shop: not enough gold.");
                return;
            }

            var lootController = player.GetComponent<PlayerLootController>();
            if (lootController == null)
                lootController = player.gameObject.AddComponent<PlayerLootController>();

            lootController.ApplyLoot(item);
            Debug.Log($"Shop: bought {item.DisplayName} for {slotCost} gold.");

            _offers[index] = null;
        }

        public LootItemDefinition GetOffer(int index)
        {
            if (_offers == null)
                return null;
            if (index < 0 || index >= _offers.Length)
                return null;
            return _offers[index];
        }

        public int SlotCost => slotCost;
    }
}
