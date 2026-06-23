// ============================================================
//  UxmlNameGenerator.cs
//
//  SETUP: Place this file inside any folder named "Editor"
//         e.g.  Assets/Editor/UxmlNameGenerator.cs
//
//  Auto-triggers when you save a .uxml file inside SourceFolders.
//  For existing files: Tools → UXML → Regenerate All Enums
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class UxmlEnumGenerator : AssetPostprocessor
    {
        // ── Configuration — edit these to match your project ──────

        /// <summary>
        /// Only .uxml files inside these folders will be processed.
        /// Paths are relative to the project root (same format as AssetDatabase).
        /// Add as many as you need.
        /// </summary>
        private static readonly string[] SourceFolders =
        {
            "Assets/UI Toolkit/UXMLs"
        };

        /// <summary>Output folder for all generated .cs files.</summary>
        private const string OutputFolder = "Assets/Generated/UxmlNames";

        // ──────────────────────────────────────────────────────────

        // ── Auto-trigger on import ────────────────────────────────
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var uxmlPaths = importedAssets
                .Where(p => p.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase)
                            && IsInSourceFolder(p)
                            && !p.StartsWith(OutputFolder, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (uxmlPaths.Count == 0) return;

            foreach (string path in uxmlPaths)
                GenerateForUxml(path);

            // Deferred — calling Refresh() directly inside this callback can
            // destabilise the import pipeline.
            EditorApplication.delayCall += AssetDatabase.Refresh;
        }

        // ── Manual trigger ────────────────────────────────────────
        [MenuItem("Tools/UXML/Regenerate All Enums")]
        public static void RegenerateAll()
        {
            // Pass SourceFolders directly into FindAssets — no whole-project scan
            string[] guids = AssetDatabase.FindAssets("t:VisualTreeAsset", SourceFolders);

            if (guids.Length == 0)
            {
                Debug.LogWarning(
                    $"[UxmlNameGenerator] No .uxml assets found in: {string.Join(", ", SourceFolders)}\n" +
                    "Check that SourceFolders in UxmlNameGenerator.cs matches your project layout.");
                return;
            }

            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) { GenerateForUxml(path); count++; }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[UxmlNameGenerator] Done — {count} file(s) processed. Output → {OutputFolder}");
        }

        // ── Menu item to print the current config (useful for debugging) ──
        [MenuItem("Tools/UXML/Show Config")]
        public static void ShowConfig()
        {
            Debug.Log(
                $"[UxmlNameGenerator] Config\n" +
                $"  SourceFolders : {string.Join(", ", SourceFolders)}\n" +
                $"  OutputFolder  : {OutputFolder}");
        }

        // ── Core logic ────────────────────────────────────────────
        static void GenerateForUxml(string uxmlPath)
        {
            try
            {
                XDocument doc = XDocument.Parse(File.ReadAllText(uxmlPath));

                var entries         = new List<(string identifier, string original)>();
                var seenIdentifiers = new HashSet<string>();

                foreach (XElement el in doc.Descendants())
                {
                    string original = el.Attribute("name")?.Value;
                    if (string.IsNullOrWhiteSpace(original)) continue;

                    string identifier = ToIdentifier(original);

                    // Two different UXML names sanitise identically → suffix with counter
                    if (seenIdentifiers.Contains(identifier))
                    {
                        int suffix = 2;
                        while (seenIdentifiers.Contains($"{identifier}_{suffix}")) suffix++;
                        identifier = $"{identifier}_{suffix}";
                    }

                    seenIdentifiers.Add(identifier);
                    entries.Add((identifier, original));
                }

                if (entries.Count == 0)
                {
                    Debug.LogWarning($"[UxmlNameGenerator] No named elements in: {uxmlPath}");
                    return;
                }

                string baseName   = Path.GetFileNameWithoutExtension(uxmlPath);
                string enumName   = $"UI_{baseName}";
                string outputPath = Path.Combine(OutputFolder, $"{enumName}.cs");
                string content    = BuildSourceFile(enumName, uxmlPath, entries);

                EnsureDirectory(OutputFolder);
                WriteIfChanged(outputPath, content);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UxmlNameGenerator] Failed on {uxmlPath}:\n{e}");
            }
        }

        static string BuildSourceFile(
            string enumName,
            string sourcePath,
            List<(string identifier, string original)> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED — DO NOT EDIT");
            sb.AppendLine($"// Source : {sourcePath}");
            sb.AppendLine($"// Updated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();

            // ── Enum ──────────────────────────────────────────────
            sb.AppendLine($"public enum {enumName}");
            sb.AppendLine("{");
            foreach (var (identifier, _) in entries)
                sb.AppendLine($"    {identifier},");
            sb.AppendLine("}");

            // ── Companion map ─────────────────────────────────────
            // Maps each enum value → original UXML name string.
            // Needed because UXML names with hyphens/spaces get sanitised to
            // underscores in the identifier, so .ToString() would return the
            // wrong string. VisualElementExtensions reads this map at runtime.
            sb.AppendLine();
            sb.AppendLine($"internal static class {enumName}_Map");
            sb.AppendLine("{");
            sb.AppendLine($"    internal static readonly Dictionary<{enumName}, string> Names =");
            sb.AppendLine($"        new Dictionary<{enumName}, string>");
            sb.AppendLine("        {");
            foreach (var (identifier, original) in entries)
                sb.AppendLine($"            {{ {enumName}.{identifier}, \"{EscapeString(original)}\" }},");
            sb.AppendLine("        };");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ── Helpers ───────────────────────────────────────────────

        /// <summary>Returns true if <paramref name="assetPath"/> lives inside one of SourceFolders.</summary>
        static bool IsInSourceFolder(string assetPath) =>
            SourceFolders.Any(folder =>
                assetPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase));

        static string ToIdentifier(string raw)
        {
            string clean = Regex.Replace(raw, @"[^a-zA-Z0-9_]", "_");
            if (clean.Length > 0 && char.IsDigit(clean[0])) clean = "_" + clean;
            return clean;
        }

        static string EscapeString(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        static void WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content) return;
            File.WriteAllText(path, content);
            Debug.Log($"[UxmlNameGenerator] Generated → {path}");
        }
    }
}