using System;
using System.Collections;
using Events;
using UnityEngine;

namespace UI.NameEntry
{
    public class NameEntryUIPresenter
    {
        public enum State { Hidden, EnteringName, Confirmed }

        private readonly NameEntryUIModel _model;
        private readonly NameEntryUIView _view;
        private readonly Func<IEnumerator, Coroutine> _coroutineRunner;

        private State _state = State.Hidden;
        private string _confirmedName;

        public NameEntryUIPresenter(NameEntryUIModel model, NameEntryUIView view, Func<IEnumerator, Coroutine> coroutineRunner)
        {
            _model = model;
            _view = view;
            _coroutineRunner = coroutineRunner;

            SubscribeToViewEvents();
            SubscribeToEventBus();
        }

        public void Initialize()
        {
            if (_model.IsReturningFromMatch)
            {
                _state = State.Confirmed;
                _confirmedName = _model.SavedConfirmedName;
                _view.Hide();
                return;
            }

            StartNameEntry();
        }

        private void SubscribeToViewEvents()
        {
            _view.OnConfirmClicked += HandleConfirmClicked;
            _view.OnRandomizeClicked += HandleRandomizeClicked;
        }

        private void SubscribeToEventBus()
        {
            EventBus.Subscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        }

        public void UnsubscribeFromEvents()
        {
            _view.OnConfirmClicked -= HandleConfirmClicked;
            _view.OnRandomizeClicked -= HandleRandomizeClicked;

            EventBus.Unsubscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        }

        private void StartNameEntry()
        {
            _state = State.EnteringName;
            _view.SetInputValue(string.Empty);
            _view.ClearError();
            _view.Show();
        }

        private void OnPlayerListChanged(PlayerListChangedEvent e)
        {
            if (_state == State.EnteringName)
            {
                string networkName = _model.GetCurrentNetworkDisplayName();
                if (!string.IsNullOrEmpty(networkName))
                {
                    _view.SetInputValue(networkName);
                }
                return;
            }

            if (_state == State.Confirmed)
            {
                _model.TryApplyConfirmedName(_confirmedName);
            }
        }

        private void HandleConfirmClicked()
        {
            if (_state == State.Confirmed)
                return;

            string trimmed = _view.GetInputValue().Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                _view.ShowError("Please enter a name.");
                return;
            }

            if (trimmed.Length > 32)
            {
                _view.ShowError("Name must be 32 characters or fewer.");
                return;
            }

            if (_model.IsNameAlreadyTaken(trimmed))
            {
                _view.ShowError($"The name \"{trimmed}\" is already taken. Please choose another.");
                return;
            }

            _state = State.Confirmed;
            _confirmedName = trimmed;
            _model.SaveConfirmedName(trimmed);

            _model.TryApplyConfirmedName(_confirmedName);

            _view.Hide();
            EventBus.Raise(new PlayerNameConfirmedEvent { PlayerName = trimmed });
        }

        private void HandleRandomizeClicked()
        {
            _coroutineRunner?.Invoke(_model.FetchRandomNameRoutine(randomName =>
            {
                _view.SetInputValue(randomName);
                _view.ClearError();
            }));
        }
    }
}