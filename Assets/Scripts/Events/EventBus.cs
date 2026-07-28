using System;
using System.Collections.Generic;

public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> Events = new();

    public static void Subscribe<T>(Action<T> listener)
    {
        var key = typeof(T);
        if (Events.TryGetValue(key, out var existing))
            Events[key] = Delegate.Combine(existing, listener);
        else
            Events[key] = listener;
    }

    public static void Unsubscribe<T>(Action<T> listener)
    {
        var key = typeof(T);
        if (Events.TryGetValue(key, out var existing))
        {
            var result = Delegate.Remove(existing, listener);
            if (result == null)
                Events.Remove(key);
            else
                Events[key] = result;
        }
    }

    public static void Raise<T>(T args)
    {
        if (Events.TryGetValue(typeof(T), out var existing))
            ((Action<T>)existing).Invoke(args);
    }
}