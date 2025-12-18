using UnityEngine;
using TMPro;
using ChogZombies.Game;

namespace ChogZombies.UI
{
    public class ShopUI_TMP : MonoBehaviour
    {
        [SerializeField] ShopController shop;
        [SerializeField] RunGameController runGame;
        [SerializeField] GameObject openShopButtonRoot;
        [SerializeField] GameObject shopPanelRoot;
        [SerializeField] TextMeshProUGUI slot0Text;
        [SerializeField] TextMeshProUGUI slot1Text;
        [SerializeField] TextMeshProUGUI slot2Text;

        bool _isOpen;
        bool _warnedMissingOpenShopRoot;
        bool _warnedMissingRunGame;
        bool _warnedMissingShop;

        void Start()
        {
            if (shop == null)
                shop = FindObjectOfType<ShopController>();

            if (runGame == null)
                runGame = FindObjectOfType<RunGameController>();

            if (openShopButtonRoot != null)
                openShopButtonRoot.SetActive(false);

            SetOpen(false);
            RefreshAll();
        }

        void Update()
        {
            if (runGame == null)
                runGame = FindObjectOfType<RunGameController>();

            if (runGame == null && !_warnedMissingRunGame)
            {
                _warnedMissingRunGame = true;
                Debug.LogWarning("[ShopUI_TMP] RunGameController not found in scene. Shop button cannot be shown.");
            }

            if (shop == null)
                shop = FindObjectOfType<ShopController>();

            if (shop == null && !_warnedMissingShop)
            {
                _warnedMissingShop = true;
                Debug.LogWarning("[ShopUI_TMP] ShopController not found in scene. Shop cannot generate offers.");
            }

            bool canShop = runGame != null && (runGame.State == RunGameController.RunState.Won || runGame.State == RunGameController.RunState.Lost);

            if (openShopButtonRoot != null)
                openShopButtonRoot.SetActive(canShop);
            else if (!_warnedMissingOpenShopRoot)
            {
                _warnedMissingOpenShopRoot = true;
                Debug.LogWarning("[ShopUI_TMP] openShopButtonRoot is not assigned. The shop open button will never appear.");
            }

            if (!canShop)
                SetOpen(false);
        }

        public void RefreshAll()
        {
            if (shop == null)
                shop = FindObjectOfType<ShopController>();

            UpdateSlot(0, slot0Text);
            UpdateSlot(1, slot1Text);
            UpdateSlot(2, slot2Text);
        }

        public void OnGenerateOffersButton()
        {
            if (runGame == null)
                runGame = FindObjectOfType<RunGameController>();
            if (runGame == null || (runGame.State != RunGameController.RunState.Won && runGame.State != RunGameController.RunState.Lost))
                return;

            if (shop == null)
                shop = FindObjectOfType<ShopController>();
            if (shop == null)
                return;

            SetOpen(true);
            if (!shop.OffersGenerated)
                shop.GenerateOffers();
            RefreshAll();
        }

        public void OnBuySlotButton(int index)
        {
            if (runGame == null)
                runGame = FindObjectOfType<RunGameController>();
            if (runGame == null || (runGame.State != RunGameController.RunState.Won && runGame.State != RunGameController.RunState.Lost))
                return;

            if (shop == null)
                shop = FindObjectOfType<ShopController>();
            if (shop == null)
                return;

            shop.BuySlot(index);
            RefreshAll();
        }

        public void OnCloseButton()
        {
            SetOpen(false);
        }

        void SetOpen(bool open)
        {
            _isOpen = open;
            if (shopPanelRoot != null)
                shopPanelRoot.SetActive(_isOpen);
        }

        void UpdateSlot(int index, TextMeshProUGUI text)
        {
            if (text == null)
                return;

            if (shop == null)
            {
                text.text = "-";
                return;
            }

            var offer = shop.GetOffer(index);
            if (offer == null)
            {
                text.text = "-";
            }
            else
            {
                text.text = $"{offer.DisplayName} ({shop.SlotCost} or)";
            }
        }
    }
}
