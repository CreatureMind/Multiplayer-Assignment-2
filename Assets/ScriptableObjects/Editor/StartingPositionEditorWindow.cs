using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class StartingPositionEditorWindow : EditorWindow
{
    private const int MinGridSize = 1;
    private const int MaxGridSize = 50;
    private const float TileMinSize = 2f;
    private const float TileMaxSize = 64f;
    private const float TileSpacing = 1f;

    private readonly Color _neutralColor = new Color(0.25f, 0.25f, 0.25f);
    private readonly Color _blockedColor = new Color(0.45f, 0.45f, 0.45f);
    private readonly Color _motherloadColor = new Color(0.58f, 0.28f, 0.82f);
    private readonly Color _bombCenterColor = new Color(0.96f, 0.54f, 0.16f);

    private StartingPositionSO _targetAsset;
    private TileType _selectedType = TileType.Soldier;
    private byte _selectedOwner = 1;

    private FieldInfo _widthField;
    private FieldInfo _heightField;
    private FieldInfo _startingPositionField;
    private GUIStyle _tileStyle;

    public static void Open(StartingPositionSO startingPosition)
    {
        var window = GetWindow<StartingPositionEditorWindow>("Starting Position Editor");
        window.minSize = new Vector2(420f, 320f);
        window.SetTarget(startingPosition);
        window.Show();
    }

    [OnOpenAsset(1)]
    public static bool OnOpenAsset(int instanceId, int _)
    {
        var asset = EditorUtility.InstanceIDToObject(instanceId) as StartingPositionSO;
        if (asset == null)
        {
            return false;
        }

        Open(asset);
        return true;
    }

    private void OnEnable()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        _widthField = typeof(StartingPositionSO).GetField("width", flags);
        _heightField = typeof(StartingPositionSO).GetField("height", flags);
        _startingPositionField = typeof(StartingPositionSO).GetField("startingPosition", flags);
    }

    private void OnGUI()
    {
        EnsureEditorCache();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _targetAsset = (StartingPositionSO)EditorGUILayout.ObjectField(_targetAsset, typeof(StartingPositionSO), false, GUILayout.MinWidth(180f));
        GUILayout.FlexibleSpace();
        if (_targetAsset != null && GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(42f)))
        {
            EditorGUIUtility.PingObject(_targetAsset);
            Selection.activeObject = _targetAsset;
        }
        EditorGUILayout.EndHorizontal();

        if (_targetAsset == null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Assign or open a StartingPositionSO asset to edit its grid.");
            return;
        }

        DrawEditorForAsset(_targetAsset);
    }

    private void SetTarget(StartingPositionSO startingPosition)
    {
        _targetAsset = startingPosition;
        Repaint();
    }

    private void DrawEditorForAsset(StartingPositionSO so)
    {
        var width = Mathf.Clamp(GetWidth(so), MinGridSize, MaxGridSize);
        var height = Mathf.Clamp(GetHeight(so), MinGridSize, MaxGridSize);
        var tiles = GetTiles(so);

        if (tiles == null || tiles.Length != width * height)
        {
            Undo.RecordObject(so, "Normalize Grid");
            ResizeGrid(so, width, height);
            EditorUtility.SetDirty(so);
            tiles = GetTiles(so);
        }
        else if (SanitizeOwners(tiles))
        {
            Undo.RecordObject(so, "Sanitize Tile Owners");
            SetTiles(so, tiles);
            EditorUtility.SetDirty(so);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var newWidth = EditorGUILayout.IntSlider("Width", width, MinGridSize, MaxGridSize);
        var newHeight = EditorGUILayout.IntSlider("Height", height, MinGridSize, MaxGridSize);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(so, "Resize Grid");
            ResizeGrid(so, newWidth, newHeight);
            EditorUtility.SetDirty(so);
            width = newWidth;
            height = newHeight;
            tiles = GetTiles(so);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
        _selectedType = (TileType)EditorGUILayout.EnumPopup("Tile Type", _selectedType);
        if (RequiresOwner(_selectedType))
        {
            _selectedOwner = (byte)EditorGUILayout.IntSlider("Owner (1-4)", _selectedOwner, 1, 4);
        }
        else
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntSlider("Owner (forced)", TileState.NoOwner, 0, 0);
            EditorGUI.EndDisabledGroup();
            _selectedOwner = TileState.NoOwner;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Editor", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Click a tile to apply the selected TileType + Owner.");

        EditorGUILayout.Space();
        if (GUILayout.Button("Clear Board To Empty"))
        {
            Undo.RecordObject(so, "Clear Board");
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i] = BuildConstrainedTile(TileType.Empty, TileState.NoOwner, TileState.NoTerritory);
            }

            SetTiles(so, tiles);
            EditorUtility.SetDirty(so);
        }

        EditorGUILayout.Space();
        var boardArea = GUILayoutUtility.GetRect(10f, 10000f, 10f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawBoard(so, tiles, width, height, boardArea);
    }

    private void EnsureEditorCache()
    {
        if (_tileStyle != null)
        {
            return;
        }

        _tileStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            wordWrap = true
        };
        _tileStyle.normal.textColor = Color.white;
    }

    private int GetWidth(StartingPositionSO so)
    {
        return _widthField != null ? (int)_widthField.GetValue(so) : MinGridSize;
    }

    private int GetHeight(StartingPositionSO so)
    {
        return _heightField != null ? (int)_heightField.GetValue(so) : MinGridSize;
    }

    private AuthoredTile[] GetTiles(StartingPositionSO so)
    {
        return _startingPositionField != null ? (AuthoredTile[])_startingPositionField.GetValue(so) : null;
    }

    private void SetTiles(StartingPositionSO so, AuthoredTile[] tiles)
    {
        _startingPositionField.SetValue(so, tiles);
    }

    private void ResizeGrid(StartingPositionSO so, int newWidth, int newHeight)
    {
        var clampedWidth = Mathf.Clamp(newWidth, MinGridSize, MaxGridSize);
        var clampedHeight = Mathf.Clamp(newHeight, MinGridSize, MaxGridSize);

        var oldWidth = Mathf.Clamp(GetWidth(so), MinGridSize, MaxGridSize);
        var oldHeight = Mathf.Clamp(GetHeight(so), MinGridSize, MaxGridSize);
        var oldTiles = GetTiles(so);

        var newTiles = new AuthoredTile[clampedWidth * clampedHeight];
        for (var i = 0; i < newTiles.Length; i++)
        {
            newTiles[i] = BuildConstrainedTile(TileType.Empty, TileState.NoOwner, TileState.NoTerritory);
        }

        if (oldTiles != null && oldTiles.Length > 0)
        {
            var copyWidth = Mathf.Min(oldWidth, clampedWidth);
            var copyHeight = Mathf.Min(oldHeight, clampedHeight);
            for (var y = 0; y < copyHeight; y++)
            {
                for (var x = 0; x < copyWidth; x++)
                {
                    var oldIndex = y * oldWidth + x;
                    if (oldIndex < oldTiles.Length)
                    {
                        var newIndex = y * clampedWidth + x;
                        var oldTile = oldTiles[oldIndex];
                        newTiles[newIndex] = BuildConstrainedTile(oldTile.type, oldTile.ownerId, oldTile.territoryId);
                    }
                }
            }
        }

        _widthField.SetValue(so, clampedWidth);
        _heightField.SetValue(so, clampedHeight);
        _startingPositionField.SetValue(so, newTiles);
    }

    private Color ColorForOwner(byte ownerId)
    {
        switch (ownerId)
        {
            case 1:
                return new Color(0.92f, 0.30f, 0.30f);
            case 2:
                return new Color(0.30f, 0.53f, 0.92f);
            case 3:
                return new Color(0.28f, 0.77f, 0.39f);
            case 4:
                return new Color(0.88f, 0.73f, 0.22f);
            default:
                return _neutralColor;
        }
    }

    private string LabelForTile(AuthoredTile tile)
    {
        switch (tile.type)
        {
            case TileType.None:
                return "None";
            case TileType.Empty:
                return "Empty";
            case TileType.Soldier:
                return "Soldier";
            case TileType.Bomb:
                return "Bomb";
            case TileType.Base:
                return "Base";
            case TileType.Blocked:
                return "Blocked";
            case TileType.Motherload:
                return "Motherload";
            default:
                return tile.type.ToString();
        }
    }

    private static string ShortLabelForTile(AuthoredTile tile)
    {
        switch (tile.type)
        {
            case TileType.None:
                return "N";
            case TileType.Empty:
                return "E";
            case TileType.Soldier:
                return "S";
            case TileType.Bomb:
                return "Bo";
            case TileType.Base:
                return "Ba";
            case TileType.Blocked:
                return "Bl";
            case TileType.Motherload:
                return "M";
            default:
                return "?";
        }
    }

    private void DrawBoard(StartingPositionSO so, AuthoredTile[] tiles, int width, int height, Rect boardArea)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var tileSize = GetTileSize(width, height, boardArea);
        _tileStyle.fontSize = tileSize >= 24f ? 10 : tileSize >= 16f ? 8 : 6;

        var boardWidth = (tileSize * width) + (TileSpacing * Mathf.Max(0, width - 1));
        var boardHeight = (tileSize * height) + (TileSpacing * Mathf.Max(0, height - 1));
        var startX = boardArea.x + Mathf.Max(0f, (boardArea.width - boardWidth) * 0.5f);
        var startY = boardArea.y + Mathf.Max(0f, (boardArea.height - boardHeight) * 0.5f);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var tile = tiles[index];
                var rectX = startX + x * (tileSize + TileSpacing);
                var rectY = startY + (height - 1 - y) * (tileSize + TileSpacing);
                var tileRect = new Rect(rectX, rectY, tileSize, tileSize);

                DrawTileVisual(tile, tileRect);
                var label = tileSize >= 14f ? LabelForTile(tile) : ShortLabelForTile(tile);
                GUI.Label(tileRect, label, _tileStyle);
                if (GUI.Button(tileRect, GUIContent.none, GUIStyle.none))
                {
                    Undo.RecordObject(so, "Paint Tile");
                    tiles[index] = BuildConstrainedTile(_selectedType, _selectedOwner, tile.territoryId);
                    SetTiles(so, tiles);
                    EditorUtility.SetDirty(so);
                }
            }
        }
    }

    private static float GetTileSize(int width, int height, Rect boardArea)
    {
        var byWidth = (boardArea.width - TileSpacing * Mathf.Max(0, width - 1)) / width;
        var byHeight = (boardArea.height - TileSpacing * Mathf.Max(0, height - 1)) / height;
        var computed = Mathf.Min(byWidth, byHeight);
        return Mathf.Clamp(computed, TileMinSize, TileMaxSize);
    }

    private static bool RequiresOwner(TileType type)
    {
        return type == TileType.Soldier || type == TileType.Bomb;
    }

    private static AuthoredTile BuildConstrainedTile(TileType type, byte ownerId, short previousTerritoryId)
    {
        var constrainedOwner = RequiresOwner(type) ? (byte)Mathf.Clamp(ownerId, 1, 4) : TileState.NoOwner;

        if (type == TileType.Base || type == TileType.Motherload)
        {
            return new AuthoredTile(type, constrainedOwner, previousTerritoryId);
        }

        return new AuthoredTile(type, constrainedOwner, TileState.NoTerritory);
    }

    private static bool SanitizeOwners(AuthoredTile[] tiles)
    {
        var changed = false;
        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            var constrained = BuildConstrainedTile(tile.type, tile.ownerId, tile.territoryId);
            if (tile.type != constrained.type || tile.ownerId != constrained.ownerId || tile.territoryId != constrained.territoryId)
            {
                tiles[i] = constrained;
                changed = true;
            }
        }

        return changed;
    }

    private void DrawTileVisual(AuthoredTile tile, Rect tileRect)
    {
        switch (tile.type)
        {
            case TileType.Motherload:
                EditorGUI.DrawRect(tileRect, _motherloadColor);
                break;
            case TileType.Blocked:
                EditorGUI.DrawRect(tileRect, _blockedColor);
                break;
            case TileType.Base:
            {
                var owner = ColorForOwner(tile.ownerId);
                EditorGUI.DrawRect(tileRect, Darken(owner, 0.55f));
                break;
            }
            case TileType.Bomb:
            {
                var owner = ColorForOwner(tile.ownerId);
                EditorGUI.DrawRect(tileRect, owner);
                var inset = Mathf.Max(1f, tileRect.width * 0.25f);
                var middleRect = new Rect(
                    tileRect.x + inset,
                    tileRect.y + inset,
                    Mathf.Max(1f, tileRect.width - inset * 2f),
                    Mathf.Max(1f, tileRect.height - inset * 2f));
                EditorGUI.DrawRect(middleRect, _bombCenterColor);
                break;
            }
            default:
                EditorGUI.DrawRect(tileRect, ColorForOwner(tile.ownerId));
                break;
        }

        Handles.color = new Color(0f, 0f, 0f, 0.35f);
        Handles.DrawAAPolyLine(1f,
            new Vector3(tileRect.xMin, tileRect.yMin),
            new Vector3(tileRect.xMax, tileRect.yMin),
            new Vector3(tileRect.xMax, tileRect.yMax),
            new Vector3(tileRect.xMin, tileRect.yMax),
            new Vector3(tileRect.xMin, tileRect.yMin));
    }

    private static Color Darken(Color color, float factor)
    {
        return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}
