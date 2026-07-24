using System;
using UnityEngine;

// THE single screen <-> board-cell conversion point on the client.
// Grid.WorldToCell returns a Vector3Int that can be negative, while the board is indexed from (0,0).
// The origin offset is applied here and nowhere else.
// Assumes an orthographic camera looking down +Z at the grid plane.
public sealed class BoardCoordinateMapper
{
    private readonly Grid _grid;
    private readonly Camera _camera;
    private readonly Vector3Int _originCell; // the grid cell that IS board (0,0)
    private readonly int _width;
    private readonly int _height;

    public BoardCoordinateMapper(Grid grid, Camera camera, Vector3Int originCell, int width, int height)
    {
        if (!grid)
            throw new ArgumentNullException(nameof(grid));
        if (!camera)
            throw new ArgumentNullException(nameof(camera));
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Board dimensions must be positive.");
        
        _grid = grid;
        _camera = camera;
        _originCell = originCell;
        _width = width;
        _height = height;
    }

    // False when the pointer is off the board.
    // Client-side courtesy only - server revalidates - but it stops us sending junk RPCs.
    public bool TryScreenToBoard(Vector2 screenPosition, out Vector2Int boardCell)
    {
        // Distance from camera plane to board plane. DO NOT leave z at 0
        var planeDistance = _grid.transform.position.z - _camera.transform.position.z;

        var world = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, planeDistance));

        var cell = _grid.WorldToCell(world);
        boardCell = new Vector2Int(cell.x - _originCell.x, cell.y - _originCell.y);
        return Contains(boardCell);
    }
    
    public Vector3Int BoardToCell(Vector2Int boardCell)
        => new Vector3Int(boardCell.x + _originCell.x, boardCell.y + _originCell.y, 0);
    
    public Vector3 BoardToWorldCenter(Vector2Int boardCell)
        => _grid.GetCellCenterWorld(BoardToCell(boardCell));
    
    public bool Contains(Vector2Int cell)
        => cell is { x: >= 0, y: >= 0 } && cell.x < _width && cell.y < _height;
}