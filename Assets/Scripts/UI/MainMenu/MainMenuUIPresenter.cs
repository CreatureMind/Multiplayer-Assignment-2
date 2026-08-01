using System;
using Events;
using UnityEngine;

namespace UI.MainMenu
{
    public class MainMenuUIPresenter
    {
        private readonly MainMenuUIModel _model;
        private readonly MainMenuUIView _view;
        private readonly Action _onOptionsRequested;
        private readonly Action _onCreditsRequested;

        public MainMenuUIPresenter(
            MainMenuUIModel model,
            MainMenuUIView view,
            Action onOptionsRequested = null,
            Action onCreditsRequested = null)
        {
            _model = model;
            _view = view;
            _onOptionsRequested = onOptionsRequested;
            _onCreditsRequested = onCreditsRequested;

            SubscribeToViewEvents();
        }

        private void SubscribeToViewEvents()
        {
            _view.OnPlayClicked    += HandlePlayClicked;
            _view.OnOptionsClicked += HandleOptionsClicked;
            _view.OnCreditsClicked += HandleCreditsClicked;
        }
        
        public void UnsubscribeFromEvents()
        {
            _view.OnPlayClicked    -= HandlePlayClicked;
            _view.OnOptionsClicked -= HandleOptionsClicked;
            _view.OnCreditsClicked -= HandleCreditsClicked;
        }

        private void HandlePlayClicked()
        {
            _model.EnterGlobalLobby();
        }

        private void HandleOptionsClicked()
        {
            _onOptionsRequested?.Invoke();
        }

        private void HandleCreditsClicked()
        {
            Debug.Log("Credits button clicked");
            _onCreditsRequested?.Invoke();
        }
    }
}