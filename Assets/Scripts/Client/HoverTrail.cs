using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class HoverTrail
{
    private readonly Tilemap _map;
    private readonly BoardCoordinateMapper _mapper;
    private readonly TileBase _overlayTile;
    private readonly Color _baseColor;
    private readonly float _fadeSeconds;

    private Vector2Int? _current;

    private readonly Dictionary<Vector2Int, float> _fading = new();
    private readonly List<Vector2Int> _scratch = new();
    
    public HoverTrail(Tilemap map, BoardCoordinateMapper mapper, TileBase overlayTile, Color baseColor, float fadeSeconds)
    {
        _map = map;
        _mapper = mapper;
        _overlayTile = overlayTile;
        _baseColor = baseColor;
        _fadeSeconds = Mathf.Max(0f, fadeSeconds);
    }
    
    public void SetCurrent(Vector2Int? cell)
    {
        if (!_map || _mapper == null || cell.Equals(_current))
            return;
        
        if (_current.HasValue)
        {
            if (_fadeSeconds > 0f)
                _fading[_current.Value] = _fadeSeconds;
            else
                ClearCell(_current.Value);
        }

        _current = cell;
        if (!_current.HasValue)
            return;
        
        _fading.Remove(_current.Value);
        Paint(_current.Value, 1f);
    }
    
    public void Tick(float deltaTime)
    {
        if (!_map || _fadeSeconds <= 0f || _fading.Count == 0)
            return;

        _scratch.Clear();
        _scratch.AddRange(_fading.Keys);

        foreach (var cell in _scratch)
        {
            var remaining = _fading[cell] - deltaTime;

            if (remaining <= 0f)
            {
                _fading.Remove(cell);
                ClearCell(cell);
            }
            else
            {
                _fading[cell] = remaining;
                Paint(cell, remaining / _fadeSeconds);
            }
        }
    }
    
    public void Clear()
    {
        if (!_map)
            return;
        if (_current.HasValue)
            ClearCell(_current.Value);
        _current = null;
        foreach (var cell in _fading.Keys)
            ClearCell(cell);
        _fading.Clear();
    }
    
    private void Paint(Vector2Int boardCell, float t01)
    {
        if (!_overlayTile)
            return;
        var cell = _mapper.BoardToCell(boardCell);
        _map.SetTile(cell, _overlayTile);
        _map.SetTileFlags(cell, TileFlags.None);
        var c = _baseColor;
        c.a = _baseColor.a * Mathf.Clamp01(t01);
        _map.SetColor(cell, c);
    }
    
    private void ClearCell(Vector2Int boardCell)
        => _map.SetTile(_mapper.BoardToCell(boardCell), null);
}