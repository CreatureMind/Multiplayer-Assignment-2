using UnityEngine;

[System.Serializable]
public abstract class Position
{
    public enum SpaceType { World, Local }
    
    // These fields will be drawn by our custom property drawer
    [SerializeField] protected SpaceType space = SpaceType.World;

    public abstract Vector3 Get();

    /// <summary>
    /// If this Position is backed by a live object (Transform/Component/GameObject) and the space is World,
    /// expose its transform so systems (like audio) can follow it over time.
    /// </summary>
    public virtual bool TryGetFollowTarget(out Transform target)
    {
        target = null;
        return false;
    }

    // Implicit conversion allows direct use as a Vector3
    public static implicit operator Vector3(Position pos)
    {
        return pos != null ? pos.Get() : Vector3.zero;
    }

    public static Position From<T>(T target, SpaceType space = SpaceType.World) where T : Object
    {
        Position pos = null;
        if (target is Component comp) pos = new ComponentPosition(comp);
        else if (target is GameObject go) pos = new GameObjectPosition(go);
        
        if (pos != null) pos.space = space;
        return pos;
    }
}

[System.Serializable]
public class ComponentPosition : Position
{
    [SerializeField] private Component target;

    private Vector3 cachedValue;
    private int lastCachedFrame = -1;

    public ComponentPosition(Component target) => this.target = target;
    
    public override Vector3 Get()
    {
        if (target == null) return Vector3.zero;

        // If we already calculated the position on THIS frame, return the cached version!
        if (Time.frameCount == lastCachedFrame)
        {
            return cachedValue;
        }

        // Otherwise, fetch it from Unity's core and update the cache
        cachedValue = space == SpaceType.World ? target.transform.position : target.transform.localPosition;
        lastCachedFrame = Time.frameCount;

        return cachedValue;
    }

    public override bool TryGetFollowTarget(out Transform followTarget)
    {
        // "Local" positions are relative to a parent, so following-by-transform doesn't make sense unless we re-parent.
        // We only expose follow in world space.
        followTarget = (space == SpaceType.World && target != null) ? target.transform : null;
        return followTarget != null;
    }
}

[System.Serializable]
public class GameObjectPosition : Position
{
    [SerializeField] private GameObject target;

    private Vector3 cachedValue;
    private int lastCachedFrame = -1;

    public GameObjectPosition(GameObject target) => this.target = target;
    
    public override Vector3 Get()
    {
        if (target == null) return Vector3.zero;

        // If we already calculated the position on THIS frame, return the cached version!
        if (Time.frameCount == lastCachedFrame)
        {
            return cachedValue;
        }

        // Otherwise, fetch it from Unity's core and update the cache
        cachedValue = space == SpaceType.World ? target.transform.position : target.transform.localPosition;
        lastCachedFrame = Time.frameCount;

        return cachedValue;
    }

    public override bool TryGetFollowTarget(out Transform followTarget)
    {
        followTarget = (space == SpaceType.World && target != null) ? target.transform : null;
        return followTarget != null;
    }
}
