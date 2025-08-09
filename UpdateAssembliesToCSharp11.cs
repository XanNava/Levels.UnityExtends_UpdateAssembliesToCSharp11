#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Levels.UnityExtends {
    public static class UpdateAssembliesToCSharp11 {
        // Unity 2022/2023 generally needs "preview" for C# 11 features.
        // If your Unity accepts "-langVersion:11", you can change this.
        private const string DesiredLangArg = "-langVersion:preview";
        private const string RspFileName = "csc.rsp";

        // Treat these as "already C# 11+"
        private static readonly string[] AcceptableLangArgs =
        {
            "-langVersion:preview",
            "-langVersion:11",
            "-langVersion:latest"
        };

        [MenuItem("Assets/Create/Scripting/Update All Assemblies To C#11", false, 2000)]
        private static void UpdateAllAssembliesToCSharp11_Menu() {
            var targets = Selection.objects;

            if (targets == null || targets.Length == 0) {
                EditorUtility.DisplayDialog(
                    "Update All Assemblies To C#11",
                    "Select a folder (recursively scanned) or an .asmdef asset, then try again.",
                    "OK"
                );
                return;
            }

            // Gather asmdef paths from selection (folders recurse; direct .asmdef supported)
            var asmdefPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var obj in targets) {
                var path = AssetDatabase.GetAssetPath(obj);

                if (string.IsNullOrEmpty(path))
                    continue;

                if (Directory.Exists(path)) {
                    foreach (var asmPath in FindAsmdefsUnder(path))
                        asmdefPaths.Add(asmPath);
                } else if (path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)) {
                    asmdefPaths.Add(path);
                } else {
                    // If a file inside a folder is selected, optionally scan its folder
                    var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');

                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) {
                        foreach (var asmPath in FindAsmdefsUnder(dir))
                            asmdefPaths.Add(asmPath);
                    }
                }
            }

            if (asmdefPaths.Count == 0) {
                EditorUtility.DisplayDialog(
                    "Update All Assemblies To C#11",
                    "No assembly definition (.asmdef) files found under the selection.",
                    "OK"
                );
                return;
            }

            int created = 0, updated = 0, skipped = 0;

            foreach (var asmdefPath in asmdefPaths) {
                var dir = Path.GetDirectoryName(asmdefPath);

                if (string.IsNullOrEmpty(dir)) {
                    skipped++;
                    continue;
                }

                var rspPath = Path.Combine(dir, RspFileName);

                if (!File.Exists(rspPath)) {
                    // Create new RSP with desired settings
                    try {
                        File.WriteAllText(rspPath, DesiredLangArg + Environment.NewLine + "-nullable" + Environment.NewLine);
                        created++;
                    }
                    catch (Exception ex) {
                        Debug.LogError($"[C#11] Failed to create {rspPath}: {ex.Message}");
                        skipped++;
                    }
                } else {
                    // Update existing file only if needed
                    try {
                        var original = File.ReadAllText(rspPath);
                        var modified = EnsureLangVersion(original, DesiredLangArg, AcceptableLangArgs);

                        if (!string.Equals(original, modified, StringComparison.Ordinal)) {
                            File.WriteAllText(rspPath, modified);
                            updated++;
                        } else {
                            skipped++;
                        }
                    }
                    catch (Exception ex) {
                        Debug.LogError($"[C#11] Failed to update {rspPath}: {ex.Message}");
                        skipped++;
                    }
                }
            }

            AssetDatabase.Refresh();

            Debug.Log($"[C#11] Update complete. asmdefs: {asmdefPaths.Count} | created RSP: {created}, updated: {updated}, skipped: {skipped}");

            EditorUtility.DisplayDialog(
                "Update All Assemblies To C#11",
                $"Assemblies found: {asmdefPaths.Count}\nCreated RSP: {created}\nUpdated: {updated}\nSkipped: {skipped}",
                "OK"
            );
        }

        private static IEnumerable<string> FindAsmdefsUnder(string folderAssetPath) {
            // Use Unity's GUID search so it respects packages/filters
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { folderAssetPath });

            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    yield return path;
            }
        }

        private static string EnsureLangVersion(string content, string desired, string[] acceptable) {
            // Normalize line endings
            var text = content.Replace("\r\n", "\n");

            // If already acceptable, leave as-is
            if (acceptable.Any(a => text.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0))
                return content;

            // Replace any existing -langVersion:* token
            var rx = new Regex(@"-langVersion:[^\s]+", RegexOptions.IgnoreCase);

            if (rx.IsMatch(text)) {
                text = rx.Replace(text, desired);
            } else {
                // Append the desired arg at the end with a newline
                if (!text.EndsWith("\n")) text += "\n";
                text += desired + "\n";
            }

            // Preserve original line ending style on write
            return text.Replace("\n", Environment.NewLine);
        }
    }
}
#endif
