using System.Threading.Tasks;
using Reown.AppKit.Unity;
using Reown.AppKit.Unity.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChogZombies.ReownIntegration
{
    /// <summary>
    /// Presenter custom pour la vue "Connect" du modal Reown.
    /// Il garde la logique de base (social login, wallets, etc.) mais rajoute
    /// un header interne avec des boutons Back / Close bien visibles.
    /// </summary>
    public class CustomConnectPresenter : ConnectPresenter
    {
        bool _headerInjected;

        public CustomConnectPresenter(RouterController router, VisualElement parent)
            : base(router, parent)
        {
        }

        protected override async Task BuildAsync()
        {
            // Laisse Reown construire le contenu standard (socials, wallets...).
            await base.BuildAsync();

            // Puis injecte notre header custom.
            TryInjectCustomHeader();
        }

        void TryInjectCustomHeader()
        {
            if (_headerInjected)
                return;

            if (View == null)
                return;

            // Évite d'injecter plusieurs fois si RebuildAsync est appelé.
            if (View.Q<VisualElement>("cz-connect-header") != null)
            {
                _headerInjected = true;
                return;
            }

            var header = new VisualElement
            {
                name = "cz-connect-header"
            };

            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.paddingTop = 6;
            header.style.paddingBottom = 6;

            // Optionnel: légère teinte de fond pour distinguer le header
            header.style.backgroundColor = new Color(0, 0, 0, 0.35f);

            // --- Bouton Back ---
            var backIcon = Resources.Load<VectorImage>("Reown/AppKit/Icons/icon_medium_chevronleft");
            var backButton = new IconLink(backIcon, () =>
            {
                // Utilise le Router Reown pour revenir en arrière dans l'historique
                Router.GoBack();
            }, "cz-connect-back");

            // --- Bouton Close ---
            var closeIcon = Resources.Load<VectorImage>("Reown/AppKit/Icons/icon_bold_xmark");
            var closeButton = new IconLink(closeIcon, () =>
            {
                AppKit.CloseModal();
            }, "cz-connect-close");

            // On peut choisir une variante de style si besoin
            backButton.Variant = IconLinkVariant.Neutral;
            closeButton.Variant = IconLinkVariant.Neutral;

            header.Add(backButton);
            header.Add(closeButton);

            // Insère le header au tout début de la vue "Connect"
            View.Insert(0, header);

            _headerInjected = true;
        }
    }
}
