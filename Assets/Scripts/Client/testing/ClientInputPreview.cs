using System.Collections.Generic;
using UnityEngine;

public class ClientInputPreview : MonoBehaviour
{
    [Header("What to test")]
    [SerializeField] private StartingPositionSO startingPosition;
    [SerializeField, Range(1, 4)] private int playerCount = 4;
    
    [Header("Rules")]
    [SerializeField] private bool baseBuildEndsTurn = true;
    
    private const int TestBudget = int.MaxValue;
    
    private static readonly Vector2Int[] Orthogonal4 =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };
    
    private static readonly Vector2Int[] Diagonal8 =
    {
        new(0, 1),
        new(0, -1),
        new(1, 0),
        new(-1, 0),
        new(1, 1),
        new(1, -1),
        new(-1, 1),
        new(-1, -1),
    };
    
    private TileState[] _truth;
    private int _width, _height;
    private ClientBoardCache _board;
    private BoardCoordinateMapper _mapper;
    private IBoardRenderer _renderer;
    private InputHandler _inputHandler;
    private ClientConnectivityMap _connectivity;
    
    private PlayerActionController _actions;
    private byte _viewerId = 1;
    private short _nextTerritoryId = 1;
    private bool _initialised;
    
    private void Start()
    {
        if (InitOnce())
            RebuildForViewer();
    }
    
    [ContextMenu("Restart Test (reload board, viewer 1)")]
    private void Restart()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ClientInputPreview] Enter play mode first.");
            return;
        }
        _viewerId = 1;
        _nextTerritoryId = 1;
        _truth = startingPosition.BuildTileStates();
        RebuildForViewer();
    }
    
    private bool InitOnce()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ClientInputPreview] Enter play mode: needs a live camera and tilemaps.");
            return false;
        }

        var context = ClientSceneContext.Instance;
        if (!context) { Debug.LogError("[ClientInputPreview] No ClientSceneContext in the scene."); return false; }
        if (!startingPosition) { Debug.LogError("[ClientInputPreview] No StartingPositionSO assigned."); return false; }
        if (!context.Grid || !context.BoardCamera || context.Renderer == null || !context.InputHandler)
        {
            Debug.LogError("[ClientInputPreview] ClientSceneContext rig is incomplete (Grid / Camera / BoardView / InputHandler).");
            return false;
        }

        _truth = startingPosition.BuildTileStates();
        _width = startingPosition.Width;
        _height = startingPosition.Height;

        _mapper = new BoardCoordinateMapper(context.Grid, context.BoardCamera, context.BoardOriginCell, _width, _height);
        _board = new ClientBoardCache(_width, _height);
        _renderer = context.Renderer;
        _inputHandler = context.InputHandler;

        _board.Changed += OnBoardChanged;
        _inputHandler.RequestSubmitted += OnRequestSubmitted;
        _inputHandler.HoverChanged += OnHoverChanged;

        _renderer.Initialise(_board, _mapper, _viewerId);

        _initialised = true;
        return true;
    }
    
    private void RebuildForViewer()
    {
        if (!_initialised)
            return;

        _connectivity = new ClientConnectivityMap(_board, _viewerId);
        var legal = new LegalMoveCalculator(_board, _viewerId, _connectivity);
        var scanner = new BaseFormationScanner(_board, _viewerId, _connectivity);

        if (_actions != null)
            _actions.HighlightsInvalidated -= OnHighlightsInvalidated;

        _actions = new PlayerActionController(
            new SoldierMoveMode(legal),
            new BombPlacementMode(legal),
            new BaseBuildMode(scanner));

        _actions.HighlightsInvalidated += OnHighlightsInvalidated;

        _inputHandler.Initialize(_mapper, _actions);

        ProjectAndApply();
        _actions.SetTurnState(true, TestBudget);
    }
    
    private void ProjectAndApply()
    {
        var diffs = StartingPositionProjector.BuildDiffs(_truth, _width, _height, _viewerId);
        _board.Apply(diffs);
    }
    
    private void OnRequestSubmitted(MoveRequest request)
    {
        switch (request.Intent)
        {
            case MoveIntent.MoveSoldier:
                var target = _truth[request.Cell.y * _width + request.Cell.x];
                if (target.Type == TileType.Bomb && target.OwnerId != _viewerId)
                {
                    DetonateBomb(request.Cell);
                }
                else
                {
                    SetTruth(request.Cell, TileState.Soldier(_viewerId));
                    ResolveCaptures();
                    ProjectAndApply();
                }
                break;

            case MoveIntent.PlaceBomb:
                SetTruth(request.Cell, TileState.Bomb(_viewerId));
                ResolveCaptures();
                ProjectAndApply();
                break;

            case MoveIntent.BuildBase:
                ApplyBase(request.Cell);
                ResolveCaptures();
                if (baseBuildEndsTurn) AdvanceViewer();
                else ProjectAndApply();
                break;

            case MoveIntent.Pass:
                AdvanceViewer();
                break;
        }
    }
    
    private void DetonateBomb(Vector2Int epicenter)
    {
        var cleared = new Dictionary<Vector2Int, byte>();
        var detonated = new HashSet<Vector2Int>();
        var queue = new Queue<(Vector2Int cell, byte gen)>();

        queue.Enqueue((epicenter, 1));
        detonated.Add(epicenter);
        Record(cleared, epicenter, 1);

        while (queue.Count > 0)
        {
            var (bombCell, gen) = queue.Dequeue();

            foreach (var off in Diagonal8)
            {
                var n = bombCell + off;
                if (n.x < 0 || n.y < 0 || n.x >= _width || n.y >= _height)
                    continue;

                var s = _truth[n.y * _width + n.x];
                if (!s.IsBlastable)
                    continue;

                Record(cleared, n, gen);

                if (s.Type == TileType.Bomb && detonated.Add(n))
                    queue.Enqueue((n, (byte)(gen + 1)));
            }
        }
        
        var view = TileProjector.Project(TileState.Empty, _viewerId, frozen: false);
        var diffs = new List<CellDiff>(cleared.Count);
        foreach (var kv in cleared)
        {
            var cell = kv.Key;
            _truth[cell.y * _width + cell.x] = TileState.Empty;
            diffs.Add(CellDiff.From(cell, view.VisualType, view.OwnerId, view.Frozen, kv.Value));
        }

        _board.Apply(diffs);
    }
    
    private static void Record(Dictionary<Vector2Int, byte> cleared, Vector2Int cell, byte gen)
    {
        if (!cleared.TryGetValue(cell, out var existing) || existing > gen)
            cleared[cell] = gen;
    }
    
    private void ApplyBase(Vector2Int origin)
    {
        var territoryId = _nextTerritoryId++;
        for (var dy = 0; dy < 2; dy++)
            for (var dx = 0; dx < 2; dx++)
                SetTruth(new Vector2Int(origin.x + 1 + dx, origin.y + 1 + dy),
                    TileState.BaseCell(_viewerId, territoryId));
    }

    private void AdvanceViewer()
    {
        var count = Mathf.Max(1, playerCount);
        _viewerId = (byte)((_viewerId % count) + 1);
        RebuildForViewer();
    }
    
    private void SetTruth(Vector2Int cell, in TileState state)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x >= _width || cell.y >= _height)
            return;
        _truth[cell.y * _width + cell.x] = state;
    }
    
    private void ResolveCaptures()
    {
        var guard = _truth.Length;
        bool capturedAny;
        do
        {
            capturedAny = false;
            var visited = new bool[_truth.Length];

            for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                {
                    var i = y * _width + x;
                    if (visited[i])
                        continue;

                    var state = _truth[i];
                    if (!state.IsTerritory)
                    {
                        visited[i] = true;
                        continue;
                    }
                    
                    var region = GatherRegion(new Vector2Int(x, y), state.Type, state.OwnerId, visited);

                    if (state.OwnerId == _viewerId)
                        continue;
                    if (!RingOwnedByViewer(region))
                        continue;

                    foreach (var cell in region)
                    {
                        var idx = cell.y * _width + cell.x;
                        _truth[idx] = _truth[idx].WithOwner(_viewerId);
                    }
                    capturedAny = true;
                }
        }
        while (capturedAny && guard-- > 0);
    }
    
    private List<Vector2Int> GatherRegion(Vector2Int start, TileType type, byte owner, bool[] visited)
    {
        var region = new List<Vector2Int>();
        var stack = new Stack<Vector2Int>();
        stack.Push(start);
        visited[start.y * _width + start.x] = true;

        while (stack.Count > 0)
        {
            var c = stack.Pop();
            region.Add(c);
            foreach (var off in Orthogonal4)
            {
                var n = c + off;
                if (n.x < 0 || n.y < 0 || n.x >= _width || n.y >= _height)
                    continue;
                var ni = n.y * _width + n.x;
                if (visited[ni])
                    continue;
                var s = _truth[ni];
                if (s.Type == type && s.OwnerId == owner)
                {
                    visited[ni] = true;
                    stack.Push(n);
                }
            }
        }
        
        return region;
    }
    
    private bool RingOwnedByViewer(List<Vector2Int> region)
    {
        var regionSet = new HashSet<Vector2Int>(region);
        var ring = new HashSet<Vector2Int>();

        foreach (var c in region)
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (n.x < 0 || n.y < 0 || n.x >= _width || n.y >= _height)
                        continue; // out-of-bounds ring cell: skipped (see edge note)
                    if (regionSet.Contains(n))
                        continue;
                    ring.Add(n);
                }

        if (ring.Count == 0)
            return false;

        foreach (var cell in ring)
            if (_truth[cell.y * _width + cell.x].OwnerId != _viewerId)
                return false;

        return true;
    }
    
    public void SelectMoveSoldier() => _actions?.SetMode(MoveIntent.MoveSoldier);
    public void SelectPlaceBomb() => _actions?.SetMode(MoveIntent.PlaceBomb);
    public void SelectBuildBase() => _actions?.SetMode(MoveIntent.BuildBase);
    public void PassTurn() => _inputHandler?.SubmitPass();
    
    private void OnBoardChanged(IReadOnlyList<CellDiff> _) => _actions?.OnBoardChanged();
    private void OnHighlightsInvalidated() => _renderer?.SetHighlights(_actions.CurrentHighlights);
    private void OnHoverChanged(Vector2Int? cell) => _renderer?.SetHover(cell);
    
    private void OnDestroy()
    {
        if (_board != null) _board.Changed -= OnBoardChanged;
        if (_inputHandler)
        {
            _inputHandler.RequestSubmitted -= OnRequestSubmitted;
            _inputHandler.HoverChanged -= OnHoverChanged;
        }
        if (_actions != null) _actions.HighlightsInvalidated -= OnHighlightsInvalidated;
    }
}