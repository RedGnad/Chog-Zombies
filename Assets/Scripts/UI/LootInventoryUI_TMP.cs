using System.Text;
using UnityEngine;
using TMPro;
using ChogZombies.Loot;
using ChogZombies.Player;

namespace ChogZombies.UI
{
    public class LootInventoryUI_TMP : MonoBehaviour
    {
        [SerializeField] MetaProgressionController meta;
        [SerializeField] PlayerLootController playerLoot;

        [Header("UI")]
        [SerializeField] TextMeshProUGUI ownedText;
        [SerializeField] TextMeshProUGUI equippedText;
        [SerializeField] TextMeshProUGUI lastAcquiredText;

        float _nextRefreshTime;

        void Start()
        {
            if (meta == null)
                meta = FindObjectOfType<MetaProgressionController>();

            if (playerLoot == null)
            {
                var player = PlayerCombatController.Main;
                if (player != null)
                    playerLoot = player.GetComponent<PlayerLootController>();
            }

            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + 0.25f;
            Refresh();
        }

        public void Refresh()
        {
            if (meta == null)
                meta = FindObjectOfType<MetaProgressionController>();

            if (playerLoot == null)
            {
                var player = PlayerCombatController.Main;
                if (player != null)
                    playerLoot = player.GetComponent<PlayerLootController>();
            }

            UpdateOwned();
            UpdateEquipped();
            UpdateLastAcquired();
        }

        void UpdateOwned()
        {
            if (ownedText == null)
                return;

            if (meta == null)
            {
                ownedText.text = "Owned: -";
                return;
            }

            var all = meta.AllLootItems;
            if (all == null || all.Count == 0)
            {
                ownedText.text = "Owned: (no items configured)";
                return;
            }

            var sb = new StringBuilder(256);
            sb.AppendLine("Owned:");

            int count = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i];
                if (item == null)
                    continue;

                if (!meta.IsOwned(item))
                    continue;

                count++;
                sb.Append("- ");
                sb.Append(item.DisplayName);
                sb.Append(" (\u002B");
                sb.Append(item.EffectValue.ToString("0.##"));
                sb.Append(" ");
                sb.Append(item.EffectType);
                sb.AppendLine(")");
            }

            if (count == 0)
                sb.AppendLine("- none");

            ownedText.text = sb.ToString();
        }

        void UpdateEquipped()
        {
            if (equippedText == null)
                return;

            if (playerLoot == null)
            {
                equippedText.text = "Equipped: -";
                return;
            }

            var list = playerLoot.EquippedItems;
            var sb = new StringBuilder(256);
            sb.AppendLine("Equipped:");

            if (list == null || list.Count == 0)
            {
                sb.AppendLine("- none");
                equippedText.text = sb.ToString();
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null)
                    continue;

                sb.Append("- ");
                sb.Append(item.DisplayName);
                sb.Append(" (\u002B");
                sb.Append(item.EffectValue.ToString("0.##"));
                sb.Append(" ");
                sb.Append(item.EffectType);
                sb.AppendLine(")");
            }

            equippedText.text = sb.ToString();
        }

        void UpdateLastAcquired()
        {
            if (lastAcquiredText == null)
                return;

            if (meta == null)
            {
                lastAcquiredText.text = "Last: -";
                return;
            }

            var item = meta.LastAcquiredItem;
            if (item == null)
            {
                lastAcquiredText.text = "Last: -";
                return;
            }

            lastAcquiredText.text = $"Last: {item.DisplayName} (+{item.EffectValue:0.##} {item.EffectType})";
        }
    }
}
