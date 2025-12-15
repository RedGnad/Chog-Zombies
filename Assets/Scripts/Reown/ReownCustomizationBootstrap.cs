using System;
using UnityEngine;
using Reown.AppKit.Unity;

namespace ChogZombies.ReownIntegration
{
    /// <summary>
    /// Bootstrap de customisation Reown AppKit.
    /// S'enregistre après l'initialisation d'AppKit et remplace certaines vues du modal
    /// par des presenters custom (ex: CustomConnectPresenter).
    /// </summary>
    public class ReownCustomizationBootstrap : MonoBehaviour
    {
        bool _registered;

        void OnEnable()
        {
            // Si AppKit est déjà initialisé (par exemple play mode relancé), on enregistre tout de suite.
            if (AppKit.IsInitialized)
            {
                TryRegisterCustomViews();
            }

            AppKit.Initialized += OnAppKitInitialized;
        }

        void OnDisable()
        {
            AppKit.Initialized -= OnAppKitInitialized;
        }

        void OnAppKitInitialized(object sender, EventArgs e)
        {
            TryRegisterCustomViews();
        }

        void TryRegisterCustomViews()
        {
            if (_registered)
                return;

            if (!AppKit.IsInitialized)
                return;

            // Le ModalControllerUtk est utilisé sur toutes les plateformes sauf WebGL.
            if (AppKit.ModalController is not ModalControllerUtk modalController)
                return;

            var routerController = modalController.RouterController;
            if (routerController == null)
                return;

            // Enregistrer notre presenter custom pour la vue Connect
            try
            {
                var customConnectPresenter = new CustomConnectPresenter(routerController, routerController.RootVisualElement);
                routerController.RegisterModalView(ViewType.Connect, customConnectPresenter);

                _registered = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ReownCustomizationBootstrap: échec enregistrement custom views: {ex.Message}");
            }
        }
    }
}
