using System;
using Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Loading
{
    public class LoadingUIPresenter : MonoBehaviour
    {
        private readonly LoadingUIModel _model = new();
        private LoadingUIView _view;
        
        private bool _isVisible = false;

        private void Awake()
        {
            _view = GetComponent<LoadingUIView>();
            
            SubscribeToEventBus();
        }

        private void SubscribeToEventBus()
        {
            EventBus.Subscribe<ShowLoadingScreenEvent>(OnShowLoadingScreen);
            EventBus.Subscribe<SceneLoadStartedEvent> (OnShowLoadingScreen);
            EventBus.Subscribe<HideLoadingScreenEvent>(OnHideLoadingScreen);
            EventBus.Subscribe<SceneLoadDoneEvent>    (OnHideLoadingScreen);
        }

        private void UnsubscribeFromEvents()
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

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
    }
}