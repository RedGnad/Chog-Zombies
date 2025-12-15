using System;
using Reown.AppKit.Unity;
using Reown.AppKit.Unity.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChogZombies.ReownIntegration
{
    public class ReownModalOverlayControls : MonoBehaviour
    {
        bool _initialized;
        ModalControllerUtk _modalController;
        RouterController _router;

        IconLink _close;
        IconLink _back;

        void OnEnable()
        {
            AppKit.Initialized += OnAppKitInitialized;

            if (AppKit.IsInitialized)
                TryInit();
        }

        void OnDisable()
        {
            AppKit.Initialized -= OnAppKitInitialized;

            if (_modalController != null)
            {
                _modalController.OpenStateChanged -= OnOpenStateChanged;
            }

            if (_router != null)
            {
                _router.ViewChanged -= OnViewChanged;
            }

            _initialized = false;
            _modalController = null;
            _router = null;
            _close = null;
            _back = null;
        }

        void OnAppKitInitialized(object sender, EventArgs e)
        {
            TryInit();
        }

        void TryInit()
        {
            if (_initialized)
                return;

            if (!AppKit.IsInitialized)
                return;

            // Utk uniquement (Editor/Standalone). En WebGL build, le modal est JS (ModalControllerWebGl).
            if (AppKit.ModalController is not ModalControllerUtk mc)
                return;

            _modalController = mc;
            _router = mc.RouterController;

            InjectButtonsIfNeeded();

            _modalController.OpenStateChanged += OnOpenStateChanged;
            if (_router != null)
                _router.ViewChanged += OnViewChanged;

            // Sync initial state
            ApplyVisibility(_modalController.IsOpen);

            _initialized = true;
        }

        void InjectButtonsIfNeeded()
        {
            if (_modalController == null || _modalController.Modal == null || _modalController.Modal.header == null)
                return;

            var header = _modalController.Modal.header;

            // Close
            if (_close == null)
            {
                _close = new IconLink(
                    Resources.Load<VectorImage>("Reown/AppKit/Icons/icon_bold_xmark"),
                    () => AppKit.CloseModal(),
                    "cz-overlay-close"
                );

                _close.Variant = IconLinkVariant.Neutral;
                _close.image.tintColor = Color.white;
                _close.style.display = DisplayStyle.Flex;

                header.rightSlot.Add(_close);
            }

            // Back
            if (_back == null)
            {
                _back = new IconLink(
                    Resources.Load<VectorImage>("Reown/AppKit/Icons/icon_medium_chevronleft"),
                    () =>
                    {
                        if (_router != null && _router.HistoryCount > 1)
                            _router.GoBack();
                        else
                            AppKit.CloseModal();
                    },
                    "cz-overlay-back"
                );

                _back.Variant = IconLinkVariant.Neutral;
                _back.image.tintColor = Color.white;
                _back.style.display = DisplayStyle.Flex;

                header.leftSlot.Add(_back);
            }
        }

        void OnOpenStateChanged(object sender, ModalOpenStateChangedEventArgs e)
        {
            ApplyVisibility(e.IsOpen);
        }

        void OnViewChanged(object sender, ViewChangedEventArgs e)
        {
            ApplyBackVisibility();
        }

        void ApplyVisibility(bool isOpen)
        {
            if (_modalController == null || _modalController.Modal == null || _modalController.Modal.header == null)
                return;

            var header = _modalController.Modal.header;

            // Force visibility: certains flows/états peuvent les masquer.
            header.leftSlot.style.visibility = isOpen ? Visibility.Visible : Visibility.Hidden;
            header.rightSlot.style.visibility = isOpen ? Visibility.Visible : Visibility.Hidden;

            InjectButtonsIfNeeded();

            ApplyBackVisibility();
        }

        void ApplyBackVisibility()
        {
            if (_back == null)
                return;

            // Si on est au premier écran, on garde quand même un bouton (il fait Close).
            _back.style.display = DisplayStyle.Flex;
        }
    }
}
