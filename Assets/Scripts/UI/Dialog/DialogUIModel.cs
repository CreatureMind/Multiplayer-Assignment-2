using System;
using Events;

namespace UI.Dialog
{
    public class DialogUIModel
    {
        public string Title { get; private set; }
        public string Message { get; private set; }
        public DialogType Type { get; private set; }

        public string PrimaryText { get; private set; }
        public Action OnPrimary { get; private set; }

        public string SecondaryText { get; private set; }
        public Action OnSecondary { get; private set; }

        public string TertiaryText { get; private set; }
        public Action OnTertiary { get; private set; }

        public void SetData(ShowDialogEvent data)
        {
            Title = data.Title;
            Message = data.Message;
            Type = data.Type;

            PrimaryText = data.PrimaryText;
            OnPrimary = data.OnPrimary;

            SecondaryText = data.SecondaryText;
            OnSecondary = data.OnSecondary;

            TertiaryText = data.TertiaryText;
            OnTertiary = data.OnTertiary;
        }

        public void Clear()
        {
            Title = string.Empty;
            Message = string.Empty;
            OnPrimary = null;
            OnSecondary = null;
            OnTertiary = null;
        }
    }
}