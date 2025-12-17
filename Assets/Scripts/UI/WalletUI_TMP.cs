using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using global::Reown.AppKit.Unity;
using global::Reown.AppKit.Unity.Model;
using global::Reown.Core.Common.Logging;
using UnityLogger = global::Reown.Sign.Unity.UnityLogger;

namespace ChogZombies.UI
{
    public class WalletUI_TMP : MonoBehaviour
    {
        [Header("Reown")]
        [SerializeField] string projectId;
        [SerializeField] string dappName = "Chog Zombies";
        [SerializeField] string dappDescription = "Chog Zombies";
        [SerializeField] string dappUrl = "https://example.com";
        [SerializeField] string dappIconUrl = "https://example.com/logo.png";
        [SerializeField] string redirectNative = "";
        [SerializeField] string redirectUniversal = "";

        [Header("Chains")]
        [SerializeField] bool useCustomChain;
        [SerializeField] string chainReference = "143";
        [SerializeField] string chainName = "Monad Mainnet";
        [SerializeField] string chainRpcUrl = "";
        [SerializeField] string chainExplorerName = "Explorer";
        [SerializeField] string chainExplorerUrl = "";
        [SerializeField] string chainImageUrl = "";
        [SerializeField] bool chainIsTestnet = false;
        [SerializeField] string nativeCurrencyName = "Monad";
        [SerializeField] string nativeCurrencySymbol = "MON";
        [SerializeField] int nativeCurrencyDecimals = 18;

        [Header("Wallet lists")]
        [SerializeField] string[] featuredWalletIds;
        [SerializeField] string[] includedWalletIds;
        [SerializeField] string[] excludedWalletIds;
        [SerializeField] bool useCustomWallets;

        [Header("Social / Email")]
        [SerializeField] bool enableEmailLogin;
        [SerializeField] bool enableDiscordLogin;
        [SerializeField] bool enableGoogleLogin;
        [SerializeField] bool enableXLogin;
        [SerializeField] bool enableAppleLogin;
        [SerializeField] bool enableGithubLogin;
        [SerializeField] bool enableFacebookLogin;

        [Header("UI")]
        [SerializeField] GameObject walletWaitPanel;
        [SerializeField] Button connectButton;
        [SerializeField] Button disconnectButton;
        [SerializeField] TextMeshProUGUI addressText;
        [SerializeField] Button closeModalButton;
        [SerializeField] Button backToWalletsButton;

        [Header("UX")]
        [SerializeField] bool showWaitPanelWhileInitializing = true;
        [SerializeField] bool showWaitPanelAfterConnectClick = true;

        bool _initializing;
        bool _connectRequested;

        async void Start()
        {
            await EnsureInitializedAndResumeAsync();
            if (AppKit.IsInitialized)
                Debug.Log($"[WalletUI_TMP] AppKit initialized. Platform={Application.platform} ModalController={AppKit.ModalController?.GetType().FullName}");
            RefreshUI();
        }

        void OnEnable()
        {
            // Cet event statique est sûr avant l'initialisation
            AppKit.Initialized += OnInitialized;

            // Les autres events passent par ConnectorController/NetworkController
            // → on ne s'y abonne que si AppKit est déjà initialisé
            if (AppKit.IsInitialized)
            {
                SubscribeAccountAndChainEvents();
            }
        }

        void OnDisable()
        {
            AppKit.Initialized -= OnInitialized;

            if (AppKit.IsInitialized)
            {
                UnsubscribeAccountAndChainEvents();
            }
        }

        public void OnConnectButton()
        {
            if (!AppKit.IsInitialized)
                return;

            if (AppKit.IsAccountConnected)
                return;

            _connectRequested = true;
            RefreshUI();

            Debug.Log($"[WalletUI_TMP] OpenModal called. Platform={Application.platform} ModalController={AppKit.ModalController?.GetType().FullName}");

            AppKit.OpenModal();
        }

        public void OnCloseModalButton()
        {
            if (!AppKit.IsInitialized)
                return;

            AppKit.CloseModal();
            _connectRequested = false;
            RefreshUI();
        }

        public void OnBackToWalletsButton()
        {
            if (!AppKit.IsInitialized)
                return;

            AppKit.CloseModal();
            _connectRequested = true;
            Invoke(nameof(OpenWalletsModal), 0.05f);
        }

        public async void OnDisconnectButton()
        {
            if (!AppKit.IsInitialized)
                return;

            if (!AppKit.IsAccountConnected)
                return;

            try
            {
                await AppKit.DisconnectAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Disconnect failed: {e.Message}");
            }

            _connectRequested = false;

            RefreshUI();
        }

        void OpenConnectModal()
        {
            if (!AppKit.IsInitialized)
                return;

            AppKit.OpenModal(ViewType.Connect);
        }

        void OpenWalletsModal()
        {
            if (!AppKit.IsInitialized)
                return;

            AppKit.OpenModal(ViewType.Wallet);
        }

        async System.Threading.Tasks.Task EnsureInitializedAndResumeAsync()
        {
            if (_initializing)
                return;

            if (AppKit.IsInitialized)
                return;

            _initializing = true;

            try
            {
                ReownLogger.Instance = new UnityLogger();

                if (string.IsNullOrWhiteSpace(projectId))
                    throw new Exception("Missing Reown projectId");

                var redirect = (!string.IsNullOrWhiteSpace(redirectNative) || !string.IsNullOrWhiteSpace(redirectUniversal))
                    ? new RedirectData { Native = redirectNative ?? string.Empty, Universal = redirectUniversal ?? string.Empty }
                    : null;

                string effectiveUrl = string.IsNullOrWhiteSpace(dappUrl) ? "https://example.com" : dappUrl;
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    var abs = Application.absoluteURL;
                    if (!string.IsNullOrWhiteSpace(abs))
                    {
                        if (Uri.TryCreate(abs, UriKind.Absolute, out var uri))
                            effectiveUrl = uri.GetLeftPart(UriPartial.Authority);
                        else
                            effectiveUrl = abs;
                    }
                }

                var meta = new Metadata(
                    name: string.IsNullOrWhiteSpace(dappName) ? "Chog Zombies" : dappName,
                    description: string.IsNullOrWhiteSpace(dappDescription) ? "Chog Zombies" : dappDescription,
                    url: effectiveUrl,
                    iconUrl: string.IsNullOrWhiteSpace(dappIconUrl) ? "https://example.com/logo.png" : dappIconUrl,
                    redirect: redirect
                );

                var config = new AppKitConfig(projectId, meta);

                // --- Listes de wallets: fallback sur la config de l'exemple si l'inspecteur est vide ---

                // Wallets custom : optionnel. Attention: certains formats (ex: .ico) ne sont pas décodables par Unity
                // et donnent l'impression d'un modal "incomplet" (icônes manquantes).
                if (useCustomWallets)
                    config.customWallets = BuildCustomWallets();

                // Nombre de wallets affichés dans la vue Connect sur mobile
                config.connectViewWalletsCountMobile = 4;

                // Featured (optionnel, uniquement si renseigné dans l'inspecteur)
                if (featuredWalletIds != null && featuredWalletIds.Length > 0)
                    config.featuredWalletIds = featuredWalletIds;

                // Excluded: si rien n'est défini dans l'inspecteur, utiliser le set de l'exemple
                if (excludedWalletIds != null && excludedWalletIds.Length > 0)
                {
                    config.excludedWalletIds = excludedWalletIds;
                }
                else
                {
                    config.excludedWalletIds = new[]
                    {
                        "walletconnect",
                        "rainbow",
                        "coinbase",
                        "safe"
                    };
                }

                // Included: si rien n'est défini dans l'inspecteur, utiliser les IDs de l'exemple
                if (includedWalletIds != null && includedWalletIds.Length > 0)
                {
                    config.includedWalletIds = includedWalletIds;
                }
                else
                {
                    config.includedWalletIds = new[]
                    {
                        "2bd8c14e035c2d48f184aaa168559e86b0e3433228d3c4075900a221785019b0", // Backpack
                        "719bd888109f5e8dd23419b20e749900ce4d2fc6858cf588395f19c82fd036b3", // HAHA
                        "c57ca95b47569778a828d19178114f4db188b89b763c899ba0be274e97267d96", // MetaMask
                        "4622a2b2d6af1c9844944291e5e7351a6aa24cd7b23099efac1b2fd875da31a0", // Trust Wallet
                        "a797aa35c0fadbfc1a53e7f675162ed5226968b44a19ee3d24385c64d1d3c393", // Phantom
                        "io.rabby"                                                                 // Rabby
                    };
                }

                // Email / socials
                config.enableEmail = enableEmailLogin;
                config.socials = BuildSocials();

                if (useCustomChain)
                {
                    if (string.IsNullOrWhiteSpace(chainReference))
                        throw new Exception("Missing chainReference");

                    string rpcUrl = chainRpcUrl;
                    string explorerUrl = chainExplorerUrl;
                    if (string.IsNullOrWhiteSpace(rpcUrl) && chainReference == "143")
                        rpcUrl = "https://rpc.monad.xyz";
                    if (string.IsNullOrWhiteSpace(explorerUrl) && chainReference == "143")
                        explorerUrl = "https://monadexplorer.com";

                    if (string.IsNullOrWhiteSpace(rpcUrl))
                        throw new Exception("Missing chainRpcUrl");
                    if (string.IsNullOrWhiteSpace(explorerUrl))
                        throw new Exception("Missing chainExplorerUrl");

                    var chain = new Chain(
                        ChainConstants.Namespaces.Evm,
                        chainReference,
                        string.IsNullOrWhiteSpace(chainName) ? chainReference : chainName,
                        new Currency(nativeCurrencyName, nativeCurrencySymbol, nativeCurrencyDecimals),
                        new BlockExplorer(chainExplorerName, explorerUrl),
                        rpcUrl,
                        chainIsTestnet,
                        chainImageUrl
                    );

                    config.supportedChains = new[] { chain };
                }

                await AppKit.InitializeAsync(config);

                Debug.Log($"[WalletUI_TMP] InitializeAsync done. Platform={Application.platform} ModalController={AppKit.ModalController?.GetType().FullName}");

                bool resumed = await AppKit.ConnectorController.TryResumeSessionAsync();
                if (resumed)
                    RefreshUI();
            }
            catch (Exception e)
            {
                Debug.LogError($"Reown AppKit init failed: {e.Message}\n" +
                               "- Vérifie que le prefab 'Reown AppKit' est bien dans la scène.\n" +
                               "- Vérifie le projectId (Reown Dashboard).\n" +
                               "- Vérifie que tu es en Unity 2022.3+.");
            }
            finally
            {
                _initializing = false;
            }
        }

        void RefreshUI()
        {
            bool isConnected = AppKit.IsInitialized && AppKit.IsAccountConnected;
            bool isModalOpen = AppKit.IsInitialized && AppKit.IsModalOpen;

            if (walletWaitPanel != null)
            {
                bool showWait = false;
                if (showWaitPanelWhileInitializing && _initializing)
                    showWait = true;
                else if (showWaitPanelAfterConnectClick && _connectRequested && !isConnected)
                    showWait = true;

                if (isModalOpen)
                    showWait = false;

                walletWaitPanel.SetActive(showWait);
            }

            if (connectButton != null)
                connectButton.gameObject.SetActive(!isConnected);

            if (disconnectButton != null)
                disconnectButton.gameObject.SetActive(isConnected);

            if (closeModalButton != null)
                closeModalButton.gameObject.SetActive(isModalOpen);

            if (backToWalletsButton != null)
                backToWalletsButton.gameObject.SetActive(isModalOpen);

            if (addressText != null)
            {
                if (!isConnected)
                {
                    addressText.text = "Wallet: -";
                }
                else
                {
                    var account = AppKit.Account;
                    addressText.text = $"Wallet: {account.Address}";
                }
            }
        }

        void OnInitialized(object sender, AppKit.InitializeEventArgs e)
        {
            // Appelé une fois après InitializeAsync
            SubscribeAccountAndChainEvents();
            RefreshUI();
        }

        void OnAccountConnected(object sender, Connector.AccountConnectedEventArgs e)
        {
            RefreshUI();
        }

        void OnAccountDisconnected(object sender, Connector.AccountDisconnectedEventArgs e)
        {
            RefreshUI();
        }

        void OnAccountChanged(object sender, Connector.AccountChangedEventArgs e)
        {
            RefreshUI();
        }

        void OnChainChanged(object sender, NetworkController.ChainChangedEventArgs e)
        {
            RefreshUI();
        }

        void SubscribeAccountAndChainEvents()
        {
            try
            {
                AppKit.AccountConnected += OnAccountConnected;
                AppKit.AccountDisconnected += OnAccountDisconnected;
                AppKit.AccountChanged += OnAccountChanged;
                AppKit.ChainChanged += OnChainChanged;
                AppKit.ModalController.OpenStateChanged += OnModalOpenStateChanged;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WalletUI_TMP: échec abonnement events Reown: {ex.Message}");
            }
        }

        void UnsubscribeAccountAndChainEvents()
        {
            try
            {
                AppKit.AccountConnected -= OnAccountConnected;
                AppKit.AccountDisconnected -= OnAccountDisconnected;
                AppKit.AccountChanged -= OnAccountChanged;
                AppKit.ChainChanged -= OnChainChanged;
                AppKit.ModalController.OpenStateChanged -= OnModalOpenStateChanged;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WalletUI_TMP: échec désabonnement events Reown: {ex.Message}");
            }
        }

        void OnModalOpenStateChanged(object sender, ModalOpenStateChangedEventArgs e)
        {
            if (!e.IsOpen)
                _connectRequested = false;
            RefreshUI();
        }

        SocialLogin[] BuildSocials()
        {
            var list = new System.Collections.Generic.List<SocialLogin>();

            if (enableGoogleLogin)
                list.Add(SocialLogin.Google);
            if (enableXLogin)
                list.Add(SocialLogin.X);
            if (enableDiscordLogin)
                list.Add(SocialLogin.Discord);
            if (enableAppleLogin)
                list.Add(SocialLogin.Apple);
            if (enableGithubLogin)
                list.Add(SocialLogin.GitHub);
            if (enableFacebookLogin)
                list.Add(SocialLogin.Facebook);

            return list.Count == 0 ? Array.Empty<SocialLogin>() : list.ToArray();
        }

        global::Reown.AppKit.Unity.Model.Wallet[] BuildCustomWallets()
        {
            return new[]
            {
                new global::Reown.AppKit.Unity.Model.Wallet { Name = "Backpack", ImageUrl = "https://backpack.app/favicon.ico", MobileLink = "backpack://", WebappLink = "https://backpack.app/", Id = "2bd8c14e035c2d48f184aaa168559e86b0e3433228d3c4075900a221785019b0" },
                new global::Reown.AppKit.Unity.Model.Wallet { Name = "MetaMask", ImageUrl = "https://metamask.io/images/favicon.ico", MobileLink = "metamask://wc", WebappLink = "https://metamask.io/", Id = "c57ca95b47569778a828d19178114f4db188b89b763c899ba0be274e97267d96" },
                new global::Reown.AppKit.Unity.Model.Wallet { Name = "Trust Wallet", ImageUrl = "https://trustwallet.com/assets/images/favicon.ico", MobileLink = "trust://wc", Id = "4622a2b2d6af1c9844944291e5e7351a6aa24cd7b23099efac1b2fd875da31a0" },
                new global::Reown.AppKit.Unity.Model.Wallet { Name = "Phantom", ImageUrl = "https://phantom.app/img/phantom-logo.png", WebappLink = "https://phantom.app/ul/browse", Id = "a797aa35c0fadbfc1a53e7f675162ed5226968b44a19ee3d24385c64d1d3c393" },
                new global::Reown.AppKit.Unity.Model.Wallet { Name = "Rabby", ImageUrl = "https://rabby.io/assets/images/logo.png", WebappLink = "https://rabby.io/", Id = "io.rabby" },
            };
        }
    }
}
