using Events;
using UnityEngine;

namespace UI.Loading
{
    public class LoadingUIPresenter
    {
        private readonly LoadingUIModel _model;
        private readonly LoadingUIView _view;

        public LoadingUIPresenter(LoadingUIModel model, LoadingUIView view)
        {
            _model = model;
            _view = view;

            SubscribeToEventBus();
        }

        private void SubscribeToEventBus()
        {
            EventBus.Subscribe<ShowLoadingScreenEvent>(OnShowLoadingScreen);
            EventBus.Subscribe<HideLoadingScreenEvent>(OnHideLoadingScreen);
        }

        public void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe<ShowLoadingScreenEvent>(OnShowLoadingScreen);
            EventBus.Unsubscribe<HideLoadingScreenEvent>(OnHideLoadingScreen);
        }

        private void OnShowLoadingScreen(ShowLoadingScreenEvent e)
        {
            _view.Show();
            _model.PlayStartSound();
        }

        private void OnHideLoadingScreen(HideLoadingScreenEvent e)
        {
            _view.Hide();
            _model.PlayEndSound();
        }
    }
}