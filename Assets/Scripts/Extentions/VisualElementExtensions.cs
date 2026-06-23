// ============================================================
//  VisualElementExtensions.cs
//
//  Place anywhere that is NOT inside an Editor folder —
//  these extensions are needed at runtime.
//  e.g.  Assets/Scripts/UI/VisualElementExtensions.cs
//
//  USAGE:
//      root.Q<Button>(UI_MyScreen.okButton)      // typed
//      root.Q(UI_MyScreen.titleLabel)            // → VisualElement
//      root.Q<Label>(UI_MyScreen.headerLabel)    // any VisualElement subtype
//
//  WHY System.Enum INSTEAD OF TEnum : Enum?
//      With a two-type-parameter signature Q<T, TEnum>, C# requires you
//      to spell out BOTH when you specify T:
//
//          root.Q<Button, UI_MyScreen>(UI_MyScreen.okButton)   ← ugly
//
//      Using the non-generic Enum base class means only T needs to be
//      stated, and the enum argument is matched by overload resolution:
//
//          root.Q<Button>(UI_MyScreen.okButton)                ← clean
//
//  HOW THE NAME LOOKUP WORKS:
//      Enum member names must be valid C# identifiers, so "ok-button"
//      becomes member [ok_button]. Calling .ToString() on that gives
//      "ok_button" — not "ok-button" — so the Q<>() lookup would fail.
//      The generated *_Map companion class holds the original strings;
//      UxmlName() retrieves the correct value via reflection (cached).
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;

public static class VisualElementExtensions
{
    // ── Typed query ───────────────────────────────────────────

    /// <summary>
    /// Finds the first child of type <typeparamref name="T"/> whose UXML
    /// <c>name</c> attribute matches the enum value.
    /// </summary>
    public static T Q<T>(this VisualElement root, Enum elementName)
        where T : VisualElement
        => root.Q<T>(UxmlName(elementName));

    // ── Untyped query ─────────────────────────────────────────

    /// <summary>
    /// Finds the first child <see cref="VisualElement"/> whose UXML
    /// <c>name</c> attribute matches the enum value.
    /// </summary>
    public static VisualElement Q(this VisualElement root, Enum elementName)
        => root.Q(UxmlName(elementName));

    // ── Name resolution ───────────────────────────────────────

    /// <summary>
    /// Returns the original UXML name string for an enum value,
    /// using the generated *_Map companion class when available,
    /// otherwise falling back to <c>.ToString()</c>.
    /// </summary>
    public static string UxmlName(Enum value)
    {
        int key = BuildCacheKey(value);

        if (s_cache.TryGetValue(key, out string cached))
            return cached;

        string resolved = ResolveFromMap(value) ?? value.ToString();
        s_cache[key]    = resolved;
        return resolved;
    }

    // ── Internal cache & reflection ───────────────────────────

    // int key = (enum Type hash) XOR (int value) — fast, no allocations
    private static readonly Dictionary<int, string> s_cache = new();

    private static int BuildCacheKey(Enum value)
        => value.GetType().GetHashCode() ^ Convert.ToInt32(value);

    /// <summary>
    /// Looks for a  {EnumTypeName}_Map  class in the same assembly
    /// and reads its  Names  dictionary. This is the companion class
    /// the generator emits alongside each enum.
    /// </summary>
    private static string ResolveFromMap(Enum value)
    {
        Type enumType = value.GetType();
        string mapTypeName = $"{enumType.Name}_Map";

        // Search in the same assembly as the enum type
        Type mapType = enumType.Assembly.GetType(mapTypeName);
        if (mapType == null) return null;

        FieldInfo field = mapType.GetField(
            "Names",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        if (field?.GetValue(null) is IDictionary dict)
        {
            object result = dict[value];
            if (result is string s) return s;
        }

        return null;
    }
}
