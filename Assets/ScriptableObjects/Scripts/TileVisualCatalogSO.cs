using System;
using UnityEngine;
using UnityEngine.Tilemaps;

// Maps a projected TileView to a sprite + color. Owned by BoardView, edited in the inspector.
// Kept out of BoardView so the renderer holds no art decisions - swap the catalog to reskin.
[CreateAssetMenu(fileName = "TileVisualCatalog", menuName = "ScriptableObjects/TileVisualCatalog")]
public class TileVisualCatalogSO : ScriptableObject
{
    [Serializable]
    private struct TypeVisual
    {
        public TileType type;
        public TileBase tile; // null renders as an empty cell
        public bool tintByOwner; // soldiers/bases follow the owner palette; blocked/empty do not
    }

    [SerializeField] private TypeVisual[] typeVisuals;

    [Header("Owner palette - index IS the ownerId (0 = neutral / unowned")]
    [SerializeField] private Color[] ownerColors = { Color.grey };
    
    [Header("Frozen Cells")]
    [SerializeField, Range(0f, 1f)] private float frozenDim = 0.45f;

    private TileBase[] _tileByType;
    private bool[] _tintByType;

    private void OnEnable()
    {
        var count = Enum.GetValues(typeof(TileType)).Length;
        _tileByType = new TileBase[count];
        _tintByType = new bool[count];

        if (typeVisuals == null)
            return;

        foreach (var visual in typeVisuals)
        {
            var i = (int)visual.type;
            if (i < 0 || i >= count)
                continue;
            _tileByType[i] = visual.tile;
            _tintByType[i] = visual.tintByOwner;
        }
    }

    public TileBase GetTile(TileType type)
    {
        var i = (int)type;
        return _tileByType != null && i < _tileByType.Length ? _tileByType[i] : null;
    }

    public Color GetColor(in TileView view)
    {
        var color = Color.white;

        var i = (int)view.VisualType;
        if (_tintByType != null && i >= 0 && i < _tintByType.Length && _tintByType[i])
            color = OwnerColor(view.OwnerId);
        
        // Frozen is a render hint from the server; it never lives in TileState.
        if (view.Frozen)
            color = new Color(color.r * frozenDim, color.g * frozenDim, color.b * frozenDim, color.a);

        return color;
    }
    
    private Color OwnerColor(byte ownerId)
        => ownerColors != null && ownerId < ownerColors.Length ? ownerColors[ownerId] : Color.white;
}