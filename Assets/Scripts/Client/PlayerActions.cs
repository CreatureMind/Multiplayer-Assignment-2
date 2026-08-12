using System.Collections.Generic;
using UnityEngine;

// One selectable client action mode. swapping the mode changes both what a click means and which cells light up.
// The controller delegates instead of branching on an enum in five places.
public interface IPlayerActionMode
{
    MoveIntent Intent { get; }

    // Budget consumed. BuildBase is 0 because is costs the entire turn.
    int BudgetCost { get; }
    
    // Cells to highlight while this mode is active.
    IReadOnlyCollection<Vector2Int> Highlights { get; }
    
    // Refresh cached candidates. Called on board change and on mode entry.
    void Refresh();

    bool IsAffordable(int remainingBudget);

    bool TryCreateRequest(Vector2Int clickedCell, out MoveRequest request);
}

public sealed class SoldierMoveMode : IPlayerActionMode
{
    private readonly LegalMoveCalculator _legal;
    
    public SoldierMoveMode(LegalMoveCalculator legal) => _legal = legal;
    
    public MoveIntent Intent => MoveIntent.MoveSoldier;
    public int BudgetCost => 1;
    public IReadOnlyCollection<Vector2Int> Highlights => _legal.MoveTargets;
    
    public void Refresh() => _legal.Recompute();
    public bool IsAffordable(int remainingBudget) => remainingBudget >= BudgetCost;
    
    public bool TryCreateRequest(Vector2Int cell, out MoveRequest request)
    {
        if (!_legal.IsMoveTarget(cell))
        {
            request = default;
            return false;
        }
        
        request = new MoveRequest(MoveIntent.MoveSoldier, cell);
        return true;
    }
}

public sealed class BombPlacementMode : IPlayerActionMode
{
    public const int Cost = 3;
    
    private readonly LegalMoveCalculator _legal;
    
    public BombPlacementMode(LegalMoveCalculator legal) => _legal = legal;

    public MoveIntent Intent => MoveIntent.PlaceBomb;
    public int BudgetCost => Cost;
    public IReadOnlyCollection<Vector2Int> Highlights => _legal.BombTargets;
    
    public void Refresh() => _legal.Recompute();
    public bool IsAffordable(int remainingBudget) => remainingBudget >= Cost;

    public bool TryCreateRequest(Vector2Int cell, out MoveRequest request)
    {
        if (!_legal.IsBombTarget(cell))
        {
            request = default;
            return false;
        }

        request = new MoveRequest(MoveIntent.PlaceBomb, cell);
        return true;
    }
}

public sealed class BaseBuildMode : IPlayerActionMode
{
    private readonly BaseFormationScanner _scanner;
    
    public BaseBuildMode(BaseFormationScanner scanner) => _scanner = scanner;
    
    public MoveIntent Intent => MoveIntent.BuildBase;
    public int BudgetCost => 0;
    public IReadOnlyCollection<Vector2Int> Highlights => _scanner.HighlightCells;
    
    public void Refresh() => _scanner.Recompute();
    public bool IsAffordable(int remainingBudget) => _scanner.Origins.Count > 0;

    public bool TryCreateRequest(Vector2Int cell, out MoveRequest request)
    {
        if (!_scanner.TryGetOriginForCell(cell, out var origin))
        {
            request = default;
            return false;
        }
        // The RPC carries the WINDOW ORIGIN, not the clicked cell, so the server never has to guess which of several overlapping windows was meant
        request = new MoveRequest(MoveIntent.BuildBase, origin);
        return true;
    }
}