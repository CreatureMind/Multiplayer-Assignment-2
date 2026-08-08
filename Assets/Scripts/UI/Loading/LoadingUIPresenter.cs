using Events;
using UnityEngine;

namespace UI.Loading
{
    public class LoadingUIPresenter
    {
        private readonly LoadingUIModel _model;
        private readonly LoadingUIView _view;
        
        private bool _isVisible = false;

        public LoadingUIPresenter(LoadingUIModel model, LoadingUIView view)
        {
            _model = model;
            _view = view;

            SubscribeToEventBus();
        }

        private void SubscribeToEventBus()
        {
            EventBus.Subscribe<ShowLoadingScreenEvent>(OnShowLoadingScreen);
            EventBus.Subscribe<SceneLoadStartedEvent> (OnShowLoadingScreen);
            EventBus.Subscribe<HideLoadingScreenEvent>(OnHideLoadingScreen);
            EventBus.Subscribe<SceneLoadDoneEvent>    (OnHideLoadingScreen);
        }

        public void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe<ShowLoadingScreenEvent>(OnShowLoadingScreen);
            EventBus.Unsubscribe<SceneLoadStartedEvent> (OnShowLoadingScreen);
            EventBus.Unsubscribe<HideLoadingScreenEvent>(OnHideLoadingScreen);
            EventBus.Unsubscribe<SceneLoadDoneEvent>    (OnHideLoadingScreen);
        }

        private void OnShowLoadingScreen<T>(T e)
        {
            if (_isVisible) return;
            _isVisible = true;

            _view.Show();
            _model.PlaySound();
        }

        private void OnHideLoadingScreen<T>(T e)
        {
            if (!_isVisible) return;
            _isVisible = false;

            _view.Hide();
            _model.EndSound();
        }
    }
}