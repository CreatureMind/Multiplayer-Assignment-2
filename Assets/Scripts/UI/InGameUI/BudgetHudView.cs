using Events;
using UnityEngine.UIElements;

public sealed class BudgetHudView
{
    private readonly Label _valueLabel;
    private bool _subscribed;

    public BudgetHudView(Label valueLabel) => _valueLabel = valueLabel;

    // Start mirroring. No-ops if the label was missing, so a bad UXML id can't throw at runtime.
    public void Enable()
    {
        if (_subscribed || _valueLabel == null)
            return;
        EventBus.Subscribe<LocalTurnStateChangedEvent>(OnTurnStateChanged);
        _subscribed = true;
    }

    public void Disable()
    {
        if (!_subscribed)
            return;
        EventBus.Unsubscribe<LocalTurnStateChangedEvent>(OnTurnStateChanged);
        _subscribed = false;
    }

    private void OnTurnStateChanged(LocalTurnStateChangedEvent e)
        => _valueLabel.text = e.CurrentBudget.ToString();
}