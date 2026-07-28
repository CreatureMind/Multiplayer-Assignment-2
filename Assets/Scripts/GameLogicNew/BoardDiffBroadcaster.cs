using System.Collections.Generic;
using UnityEngine;

// Server-side transport. One job: turn authoritative cells into per-viewer projected diff RPCs, chunked.
// No rules, no validation. Enemy bombs are hidden here by TileProjector, before serialization.
public sealed class BoardDiffBroadcaster
{
    private const int FullBoardPulseSize = 16;

    private readonly BoardManager _board;
    private readonly IReadOnlyList<ClientManager> _clients; // reference to ServerGameManager's live list, populated after construction

    public BoardDiffBroadcaster(BoardManager board, IReadOnlyList<ClientManager> clients)
    {
        _board = board;
        _clients = clients;
    }
    
    // Send a set of changed cells to every client, each viewer projected independently.
    public void Broadcast(IReadOnlyList<Vector2Int> changedCells)
    {
        if (changedCells == null || changedCells.Count == 0)
            return;
        foreach (var client in _clients)
            SendCells(client, changedCells);
    }
    
    // Send the entire authored board to one client (used on bootstrap).
    public void SendFullBoard(ClientManager client)
    {
        var all = new List<Vector2Int>(_board.TileCount);
        for (var y = 0; y < _board.Height; y++)
            for (var x = 0; x < _board.Width; x++)
                all.Add(new Vector2Int(x, y));
        SendCells(client, all, FullBoardPulseSize);
    }
    
    // Project first so chunk boundaries and the final flag are unambiguous.
    private void SendCells(ClientManager client, IReadOnlyList<Vector2Int> cells, int maxPerRpc = ClientManager.MaxDiffsPerRpc)
    {
        var viewerId = client.PlayerId;
        var projected = new List<CellDiff>(cells.Count);
        foreach (var cell in cells)
        {
            if (!_board.TryGetTile(cell.x, cell.y, out var state))
                continue;
            // Frozen derivation is TBD (BoardGraph); false is safe because the server revalidates every request.
            var view = TileProjector.Project(state, viewerId, frozen: false);
            projected.Add(CellDiff.From(cell, view.VisualType, view.OwnerId, view.Frozen));
        }
        if (projected.Count == 0)
            return;

        var max = Mathf.Max(1, maxPerRpc);
        for (var start = 0; start < projected.Count; start += max)
        {
            var count = Mathf.Min(max, projected.Count - start);
            var chunk = new CellDiff[count];
            projected.CopyTo(start, chunk, 0, count);
            var isFinal = start + count >= projected.Count;
            client.RPC_ApplyDiffs(chunk, count, isFinal);
        }
    }
}