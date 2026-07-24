using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Background "Matrix falling code" effect built from the game's pawn sprite instead of glyphs.
/// Pooled and centrally updated (no per-drop Coroutines, no per-frame allocations) so it stays
/// cheap even with a large number of columns/stripes. Designed to sit behind the board on its
/// own sorting layer.
///
/// Setup: drop this on an empty GameObject in the scene, assign an orthographic camera,
/// the 20x20 pawn sprite, and a color palette. Everything else is tunable in the inspector.
/// </summary>
public class MatrixPawnRain : MonoBehaviour
{
    private enum StripeColorMode
    {
        SolidPerStripe,   // whole stripe shares one random palette color
        RandomPerUnit,    // every pawn in the stripe gets its own random color
        CyclePalette      // pawns cycle through the palette in order along the stripe
    }

    [Header("References")]
    [Tooltip("The 20x20 pawn sprite (16x16 art, transparent padding).")]
    [SerializeField] private Sprite pawnSprite;
    [Tooltip("Optional override material. Leave empty to use the default sprite material.")]
    [SerializeField] private Material spriteMaterial;
    [Tooltip("Camera the effect fills. Must be orthographic.")]
    [SerializeField] private Camera targetCamera;

    [Header("Color Palette")]
    [SerializeField] private Color[] palette = {
        new Color(0.0f, 0.45f, 0.70f), // blue
        new Color(0.90f, 0.62f, 0.0f), // orange
        new Color(0.0f, 0.62f, 0.45f), // bluish green
        new Color(0.80f, 0.47f, 0.65f) // reddish purple
    };
    [SerializeField] private StripeColorMode colorMode = StripeColorMode.SolidPerStripe;
    [Tooltip("The lead pawn of each stripe gets this color instead of a palette color, like the bright head glyph in the Matrix effect. Set alpha to 0 to disable.")]
    [SerializeField] private Color headHighlightColor = new Color(1f, 1f, 1f, 1f);

    [Header("Grid")]
    [Tooltip("If true, cellSize is computed from the sprite's world-space bounds instead of set manually.")]
    [SerializeField] private bool autoComputeCellSizeFromSprite = true;
    [Tooltip("World-space size of one grid step. Ignored if autoComputeCellSizeFromSprite is on.")]
    [SerializeField] private float cellSize = 0.2f;
    [Tooltip("Extra world units above/below/left/right of the camera view where stripes can spawn/despawn off-screen.")]
    [SerializeField] private float boundsPadding = 1f;

    [Header("Timing")]
    [Tooltip("A stripe moves down exactly one grid cell every 'step interval' seconds. This is randomized per-stripe between min and max.")]
    [SerializeField] private float minStepInterval = 0.06f;
    [SerializeField] private float maxStepInterval = 0.18f;
    [Tooltip("If true, pawns snap instantly between grid cells (matches the game's own turn-based movement feel). If false, they glide smoothly between cells.")]
    [SerializeField] private bool snapToGrid = true;
    [Tooltip("Only used when snapToGrid is false. Shapes the glide between cells (e.g. ease-out) instead of a flat linear move. Input/output are both 0-1.")]
    [SerializeField] private AnimationCurve stepEaseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Use unscaled time so the rain keeps animating even if Time.timeScale is 0 (e.g. during a paused turn UI).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Stripes")]
    [SerializeField] private int minStripeLength = 4;
    [SerializeField] private int maxStripeLength = 14;
    [Tooltip("Delay range before a column spawns its next stripe, in seconds.")]
    [SerializeField] private float minSpawnDelay = 0.15f;
    [SerializeField] private float maxSpawnDelay = 1.2f;
    [Tooltip("How many stripes are allowed to fall in the same column at once.")]
    [SerializeField] private int maxActiveStripesPerColumn = 1;

    [Header("Fade")]
    [SerializeField] private bool fadeTail = true;
    [Tooltip("Evaluated 0 (head) to 1 (tail). Output is used as alpha.")]
    [SerializeField] private AnimationCurve tailFadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Flicker")]
    [Tooltip("Each step, every visible pawn has this chance to re-roll its color from the palette (subtle glitch feel).")]
    [SerializeField, Range(0f, 1f)] private float recolorChance = 0f;

    [Header("Pooling")]
    [Tooltip("SpriteRenderers pre-allocated at startup. Should comfortably exceed columns * average stripe length.")]
    [SerializeField] private int poolSize = 400;
    [Tooltip("If the pool runs dry, allow creating extra renderers on demand (small GC cost, only happens rarely).")]
    [SerializeField] private bool growPoolIfNeeded = true;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int orderInLayer = -100;

    private class Stripe
    {
        public bool active;
        public int column;
        public int headRow;      // current integer row of the head (row 0 = just above top bound)
        public float timer;
        public float stepInterval;
        public int length;
        public SpriteRenderer[] units; // fixed-size buffer, only [0, length) in use
        public Color stripeColor;
    }

    private readonly List<Stripe> _stripePool = new List<Stripe>();
    private readonly List<Stripe> _activeStripes = new List<Stripe>();
    private readonly Stack<SpriteRenderer> _rendererPool = new Stack<SpriteRenderer>();

    private float[] _columnSpawnTimer;
    private int[] _columnActiveCount;
    private int _columnCount;

    private float _left, _right, _top, _bottom;
    private Transform _poolRoot;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        if (autoComputeCellSizeFromSprite && pawnSprite != null)
        {
            cellSize = pawnSprite.bounds.size.y;
        }

        _poolRoot = new GameObject("MatrixPawnRain_Pool").transform;
        _poolRoot.SetParent(transform, false);

        PreWarmRendererPool(poolSize);
        RecalculateGrid();

        int maxLen = Mathf.Max(1, maxStripeLength);
        for (int i = 0; i < _columnCount * Mathf.Max(1, maxActiveStripesPerColumn); i++)
        {
            _stripePool.Add(new Stripe { units = new SpriteRenderer[maxLen], active = false });
        }
    }

    private void OnValidate()
    {
        minStepInterval = Mathf.Max(0.001f, minStepInterval);
        maxStepInterval = Mathf.Max(minStepInterval, maxStepInterval);
        minSpawnDelay = Mathf.Max(0f, minSpawnDelay);
        maxSpawnDelay = Mathf.Max(minSpawnDelay, maxSpawnDelay);
        minStripeLength = Mathf.Max(1, minStripeLength);
        maxStripeLength = Mathf.Max(minStripeLength, maxStripeLength);
        poolSize = Mathf.Max(1, poolSize);
        maxActiveStripesPerColumn = Mathf.Max(1, maxActiveStripesPerColumn);
    }

    /// <summary>
    /// Recomputes column count and world bounds from the current camera. Call this manually
    /// if you change resolution/orthographic size at runtime; it is only done once at Awake
    /// otherwise, since resizing mid-effect would orphan active stripes' column indices.
    /// </summary>
    public void RecalculateGrid()
    {
        RecalculateBounds();
        _columnCount = Mathf.Max(1, Mathf.CeilToInt((_right - _left) / cellSize));
        _columnSpawnTimer = new float[_columnCount];
        _columnActiveCount = new int[_columnCount];
        for (int c = 0; c < _columnCount; c++)
        {
            _columnSpawnTimer[c] = Random.Range(0f, maxSpawnDelay);
        }
    }

    private void RecalculateBounds()
    {
        float halfHeight = targetCamera.orthographicSize + boundsPadding;
        float halfWidth = halfHeight * targetCamera.aspect;
        Vector3 camPos = targetCamera.transform.position;
        _left = camPos.x - halfWidth;
        _right = camPos.x + halfWidth;
        _top = camPos.y + halfHeight;
        _bottom = camPos.y - halfHeight;
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        TickSpawning(dt);
        TickActiveStripes(dt);
    }

    private void TickSpawning(float dt)
    {
        for (int c = 0; c < _columnCount; c++)
        {
            if (_columnActiveCount[c] >= maxActiveStripesPerColumn) continue;

            _columnSpawnTimer[c] -= dt;
            if (_columnSpawnTimer[c] > 0f) continue;

            TrySpawnStripe(c);
            _columnSpawnTimer[c] = Random.Range(minSpawnDelay, maxSpawnDelay);
        }
    }

    private void TrySpawnStripe(int column)
    {
        Stripe stripe = GetPooledStripe();
        if (stripe == null) return; // pool exhausted this cycle, try again next spawn tick

        int length = Random.Range(minStripeLength, maxStripeLength + 1);
        length = Mathf.Min(length, stripe.units.Length);

        for (int i = 0; i < length; i++)
        {
            SpriteRenderer sr = GetPooledRenderer();
            if (sr == null)
            {
                // Ran out of renderers mid-assembly; release what we grabbed and abort this stripe.
                for (int j = 0; j < i; j++) ReleaseRenderer(stripe.units[j]);
                ReleaseStripe(stripe);
                return;
            }
            stripe.units[i] = sr;
            sr.gameObject.SetActive(true);
        }

        stripe.active = true;
        stripe.column = column;
        stripe.headRow = 0;
        stripe.timer = 0f;
        stripe.stepInterval = Random.Range(minStepInterval, maxStepInterval);
        stripe.length = length;
        stripe.stripeColor = palette.Length > 0 ? palette[Random.Range(0, palette.Length)] : Color.white;

        ApplyStripeVisuals(stripe);
        PositionStripe(stripe, 0f);

        _activeStripes.Add(stripe);
        _columnActiveCount[column]++;
    }

    private void TickActiveStripes(float dt)
    {
        for (int i = _activeStripes.Count - 1; i >= 0; i--)
        {
            Stripe stripe = _activeStripes[i];
            stripe.timer += dt;

            float progress = Mathf.Clamp01(stripe.timer / stripe.stepInterval);

            if (stripe.timer >= stripe.stepInterval)
            {
                stripe.headRow++;
                stripe.timer -= stripe.stepInterval;
                stripe.stepInterval = Random.Range(minStepInterval, maxStepInterval);
                progress = 0f;

                if (recolorChance > 0f) MaybeRecolor(stripe);
            }

            float easedProgress = snapToGrid ? 0f : stepEaseCurve.Evaluate(progress);
            PositionStripe(stripe, easedProgress);

            // Despawn once the tail (trailing/topmost pawn, which exits last) has fully
            // passed the bottom bound.
            float tailY = _top - stripe.headRow * cellSize + (stripe.length - 1) * cellSize;
            if (tailY < _bottom - cellSize)
            {
                for (int u = 0; u < stripe.length; u++) ReleaseRenderer(stripe.units[u]);
                _columnActiveCount[stripe.column]--;
                ReleaseStripe(stripe);
                _activeStripes.RemoveAt(i);
            }
        }
    }

    private void PositionStripe(Stripe stripe, float subStepProgress)
    {
        float x = _left + stripe.column * cellSize + cellSize * 0.5f;
        float headYExact = _top - (stripe.headRow + subStepProgress) * cellSize;

        for (int i = 0; i < stripe.length; i++)
        {
            // i = 0 is the leading pawn (bottom, direction of travel). Higher indices trail
            // upward behind it, which is why they extend with a POSITIVE offset here.
            float y = headYExact + i * cellSize;
            stripe.units[i].transform.position = new Vector3(x, y, 0f);

            if (fadeTail)
            {
                float t = stripe.length <= 1 ? 0f : (float)i / (stripe.length - 1);
                Color baseColor = (i == 0 && headHighlightColor.a > 0f) ? headHighlightColor : stripe.units[i].color;
                baseColor.a = tailFadeCurve.Evaluate(t);
                stripe.units[i].color = baseColor;
            }
        }
    }

    private void ApplyStripeVisuals(Stripe stripe)
    {
        for (int i = 0; i < stripe.length; i++)
        {
            Color c = ResolveUnitColor(stripe, i);
            if (i == 0 && headHighlightColor.a > 0f) c = headHighlightColor;
            stripe.units[i].color = c;
        }
    }

    private Color ResolveUnitColor(Stripe stripe, int index)
    {
        if (palette.Length == 0) return Color.white;

        switch (colorMode)
        {
            case StripeColorMode.RandomPerUnit:
                return palette[Random.Range(0, palette.Length)];
            case StripeColorMode.CyclePalette:
                return palette[index % palette.Length];
            case StripeColorMode.SolidPerStripe:
            default:
                return stripe.stripeColor;
        }
    }

    private void MaybeRecolor(Stripe stripe)
    {
        for (int i = 0; i < stripe.length; i++)
        {
            if (Random.value <= recolorChance)
            {
                Color c = ResolveUnitColor(stripe, i);
                c.a = stripe.units[i].color.a; // keep current fade alpha
                stripe.units[i].color = c;
            }
        }
    }

    private Stripe GetPooledStripe()
    {
        for (int i = 0; i < _stripePool.Count; i++)
        {
            if (!_stripePool[i].active) return _stripePool[i];
        }

        if (growPoolIfNeeded)
        {
            var s = new Stripe { units = new SpriteRenderer[Mathf.Max(1, maxStripeLength)], active = false };
            _stripePool.Add(s);
            return s;
        }

        return null;
    }

    private void ReleaseStripe(Stripe stripe)
    {
        stripe.active = false;
    }

    private void PreWarmRendererPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _rendererPool.Push(CreateRenderer());
        }
    }

    private SpriteRenderer CreateRenderer()
    {
        var go = new GameObject("Pawn");
        go.transform.SetParent(_poolRoot, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pawnSprite;
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = orderInLayer;
        go.SetActive(false);
        return sr;
    }

    private SpriteRenderer GetPooledRenderer()
    {
        if (_rendererPool.Count > 0) return _rendererPool.Pop();
        if (growPoolIfNeeded) return CreateRenderer();
        return null;
    }

    private void ReleaseRenderer(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(false);
        _rendererPool.Push(sr);
    }
}