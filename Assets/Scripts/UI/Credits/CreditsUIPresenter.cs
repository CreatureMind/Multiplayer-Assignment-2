using System;
using System.Collections.Generic;
using System.Linq;
using Events;
using UnityEngine;

namespace UI.RoomCreation
{
    public class CreditsUIPresenter
    {
        private readonly CreditsUIModel _model;
        private readonly CreditsUIView _view;

        public CreditsUIPresenter(CreditsUIModel model, CreditsUIView view)
        {
            _model = model;
            _view = view;

            SubscribeToViewEvents();
        }

        private void SubscribeToViewEvents() => _view.OnBackRequested += HandleBackRequested;

        public void UnsubscribeFromEvents() => _view.OnBackRequested -= HandleBackRequested;

        private void HandleBackRequested() => _view.Hide();
    }
}