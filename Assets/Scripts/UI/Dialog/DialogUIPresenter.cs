using Events;

namespace UI.Dialog
{
    public class DialogUIPresenter
    {
        private readonly DialogUIModel _model;
        private readonly DialogUIView  _view;

        public DialogUIPresenter(DialogUIModel model, DialogUIView view)
        {
            _model = model;
            _view = view;

            SubscribeToViewEvents();
            SubscribeToEventBus();
        }

        private void SubscribeToViewEvents()
        {
            _view.OnPrimaryClicked   += HandlePrimaryClicked;
            _view.OnSecondaryClicked += HandleSecondaryClicked;
            _view.OnTertiaryClicked  += HandleTertiaryClicked;
        }

        private void SubscribeToEventBus()
        {
            EventBus.Subscribe<ShowDialogEvent>(OnShowDialog);
        }

        public void UnsubscribeFromEvents()
        {
            _view.OnPrimaryClicked   -= HandlePrimaryClicked;
            _view.OnSecondaryClicked -= HandleSecondaryClicked;
            _view.OnTertiaryClicked  -= HandleTertiaryClicked;

            EventBus.Unsubscribe<ShowDialogEvent>(OnShowDialog);
        }

        private void OnShowDialog(ShowDialogEvent e)
        {
            EventBus.Raise(new HideLoadingScreenEvent());
            
            _model.SetData(e);

            _view.SetContent(_model.Title, _model.Message, _model.Type);
            _view.SetButtons(
                _model.PrimaryText,
                _model.SecondaryText,
                _model.TertiaryText
            );

            _view.Show();
        }

        private void HandlePrimaryClicked()
        {
            _view.Hide();
            var action = _model.OnPrimary;
            _model.Clear();
            action?.Invoke();
        }

        private void HandleSecondaryClicked()
        {
            _view.Hide();
            var action = _model.OnSecondary;
            _model.Clear();
            action?.Invoke();
        }

        private void HandleTertiaryClicked()
        {
            _view.Hide();
            var action = _model.OnTertiary;
            _model.Clear();
            action?.Invoke();
        }
    }
}