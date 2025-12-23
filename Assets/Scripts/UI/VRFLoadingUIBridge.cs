using UnityEngine;
using ChogZombies.Reown;

namespace ChogZombies.UI
{
    /// <summary>
    /// Connecte automatiquement VRFLoadingUI aux events de SwitchboardRandomnessService.
    /// Placer ce script sur le même GameObject que VRFLoadingUI ou un parent.
    /// </summary>
    public class VRFLoadingUIBridge : MonoBehaviour
    {
        [SerializeField] VRFLoadingUI loadingUI;
        [SerializeField] SwitchboardRandomnessService vrfService;

        VRFLoadingUI.VRFContext _currentContext = VRFLoadingUI.VRFContext.RunSeed;

        void Start()
        {
            if (loadingUI == null)
                loadingUI = GetComponent<VRFLoadingUI>();

            if (loadingUI == null)
                loadingUI = VRFLoadingUI.Instance;

            if (vrfService == null)
                vrfService = FindObjectOfType<SwitchboardRandomnessService>();

            if (vrfService != null)
            {
                vrfService.OnWaitingWallet += HandleWaitingWallet;
                vrfService.OnSigningTransaction += HandleSigningTransaction;
                vrfService.OnTransactionPending += HandleTransactionPending;
                vrfService.OnWaitingSettlement += HandleWaitingSettlement;
                vrfService.OnResolvingRandomness += HandleResolvingRandomness;
                vrfService.OnSettling += HandleSettling;
                vrfService.OnComplete += HandleComplete;
                vrfService.OnError += HandleError;
            }
            else
            {
                Debug.LogWarning("[VRFLoadingUIBridge] SwitchboardRandomnessService not found.");
            }
        }

        void OnDestroy()
        {
            if (vrfService != null)
            {
                vrfService.OnWaitingWallet -= HandleWaitingWallet;
                vrfService.OnSigningTransaction -= HandleSigningTransaction;
                vrfService.OnTransactionPending -= HandleTransactionPending;
                vrfService.OnWaitingSettlement -= HandleWaitingSettlement;
                vrfService.OnResolvingRandomness -= HandleResolvingRandomness;
                vrfService.OnSettling -= HandleSettling;
                vrfService.OnComplete -= HandleComplete;
                vrfService.OnError -= HandleError;
            }
        }

        /// <summary>
        /// Appelé par RunGameController AVANT de lancer un appel VRF pour définir le contexte.
        /// </summary>
        public void SetContext(VRFLoadingUI.VRFContext context)
        {
            _currentContext = context;
        }

        /// <summary>
        /// Affiche l'overlay avec le contexte actuel.
        /// </summary>
        public void ShowLoading(VRFLoadingUI.VRFContext context, string customTitle = null)
        {
            _currentContext = context;
            if (loadingUI != null)
                loadingUI.Show(context, customTitle);
        }

        /// <summary>
        /// Cache l'overlay manuellement.
        /// </summary>
        public void HideLoading()
        {
            if (loadingUI != null)
                loadingUI.Hide();
        }

        /// <summary>
        /// Affiche une erreur et permet le retry.
        /// </summary>
        public void ShowError(string message)
        {
            if (loadingUI != null)
                loadingUI.SetError(message);
        }

        void HandleWaitingWallet()
        {
            if (loadingUI != null)
                loadingUI.SetState(VRFLoadingUI.VRFState.WaitingWallet);
        }

        void HandleSigningTransaction()
        {
            if (loadingUI != null)
                loadingUI.SetState(VRFLoadingUI.VRFState.SigningTransaction);
        }

        void HandleTransactionPending(string txHash)
        {
            if (loadingUI != null)
            {
                loadingUI.SetState(VRFLoadingUI.VRFState.TransactionPending);
                loadingUI.SetTxHash(txHash);
            }
        }

        void HandleWaitingSettlement(float durationSeconds)
        {
            if (loadingUI != null)
                loadingUI.SetSettlementWait(durationSeconds);
        }

        void HandleResolvingRandomness()
        {
            if (loadingUI != null)
                loadingUI.SetState(VRFLoadingUI.VRFState.ResolvingRandomness);
        }

        void HandleSettling()
        {
            if (loadingUI != null)
                loadingUI.SetState(VRFLoadingUI.VRFState.Settling);
        }

        void HandleComplete(SwitchboardRandomnessService.RandomnessResult result)
        {
            if (loadingUI != null)
                loadingUI.SetComplete();
        }

        void HandleError(string errorMessage)
        {
            if (loadingUI != null)
                loadingUI.SetError(errorMessage);
        }
    }
}
