using System;
using System.Collections.Generic;
using UnityEngine;

// Owns the current action mode and the client's mirror of turn state.
// Budget is MIRRORED, never computed.
public sealed class PlayerActionController
{
    private readonly Dictionary<MoveIntent, IPlayerActionMode> _modes = new Dictionary<MoveIntent, IPlayerActionMode>();
    
    private IPlayerActionMode _current;
    
    // Fired when highlights may have changed: mode switch, board diff, or turn change.
    // The view redraws its overlay from CurrentHighlights.
    public event Action HighlightsInvalidated;
    
    public bool IsMyTurn { get; private set; }
    public int RemainingBudget { get; private set; }

    public MoveIntent CurrentIntent => _current?.Intent ?? MoveIntent.MoveSoldier;
    
    public IReadOnlyCollection<Vector2Int> CurrentHighlights
        => IsMyTurn && _current != null && _current.IsAffordable(RemainingBudget)
        ? _current.Highlights
        : Array.Empty<Vector2Int>();

    public PlayerActionController(params IPlayerActionMode[] modes)
    {
        foreach (var mode in modes)
            _modes[mode.Intent] = mode;
        
        _modes.TryGetValue(MoveIntent.MoveSoldier, out _current);
    }

    public bool CanAfford(MoveIntent intent)
        => _modes.TryGetValue(intent, out var mode) && mode.IsAffordable(RemainingBudget);

    public void SetMode(MoveIntent intent)
    {
        if (!_modes.TryGetValue(intent, out var mode))
        {
            Debug.LogWarning($"[PlayerActionController] No mode registered for {intent}.");
            return;
        }
        _current = mode;
        _current.Refresh();
        HighlightsInvalidated?.Invoke();
    }
    
    // Called from the server's turn broadcast
    public void SetTurnState(bool isMyTurn, int remainingBudget)
    {
        IsMyTurn = isMyTurn;
        RemainingBudget = Mathf.Max(0, remainingBudget);

        // Dropping below a mode's cost should not leave a stale highlight up
        if (_current != null && !_current.IsAffordable(RemainingBudget))
            _modes.TryGetValue(MoveIntent.MoveSoldier, out _current);

        _current?.Refresh();
        HighlightsInvalidated?.Invoke();
    }
    
    // Called whenever the board cache applies a diff
    public void OnBoardChanged()
    {
        _current?.Refresh();
        HighlightsInvalidated?.Invoke();
    }
    
    // Returns false for illegal clicks so InputHandler can stay silent rather than sending a request the server would only reject
    public bool TryHandleClick(Vector2Int cell, out MoveRequest request)
    {
        request = default;
        if (_current == null)
            return false;
        return _current.IsAffordable(RemainingBudget) && _current.TryCreateRequest(cell, out request);
    }
}