using System;
using System.Collections.Generic;
using UI.Common;
using UI.RoomsList;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.RoomCreation
{
    [RequireComponent(typeof(UIDocument))]
    public class CreditsUIView : BaseOverlayView
    {
        public event Action OnBackRequested;

        private Button _backButton;

        protected override void OnInitializeUI()
        {
            if (Root == null) return;

            _backButton = Root.Q<Button>   (UI_Join_Room_View.back_button);
            
            SetupCallbacks();
        }

        private void SetupCallbacks()
        {
            if (_backButton != null)
            {
                _backButton.clicked += () => OnBackRequested?.Invoke();
            }
            else
            {
                Debug.LogError("[CreditsUIView] Could not find Button named 'back-button' in Credits_View.");
            }
            
            Hide();
        }
    }
}