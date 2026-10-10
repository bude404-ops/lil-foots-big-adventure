// BIG-BRIDGE: auto-enable Unity AI MCP server so BudE doesn't have to hunt for toggles.
// Safe by design: only touches types with "Mcp" in the name, every call wrapped in try/catch.
// Writes BIG-BRIDGE-STATUS.txt to the project root with everything it found.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BigBridgeBootstrap
{
    const string kTag = "[BIG-BRIDGE]";
    static bool s_DidRun;

    static BigBridgeBootstrap()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (s_DidRun) return;
        s_DidRun = true;
        var log = new List<string> { "BIG-BRIDGE STATUS @ " + DateTime.Now };
        bool acted = false;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                string fullName;
                try { fullName = t.FullName ?? ""; } catch { continue; }
                if (fullName.IndexOf("Mcp", StringComparison.OrdinalIgnoreCase) < 0) continue;

                log.Add("found type: " + fullName);

                // Flip any static bool "*Enabled*" property on Mcp types to true.
                try
                {
                    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (p.PropertyType != typeof(bool) || !p.CanWrite) continue;
                        if (p.Name.IndexOf("Enabled", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        try
                        {
                            bool cur = (bool)p.GetValue(null, null);
                            if (!cur) { p.SetValue(null, true, null); log.Add("SET TRUE: " + fullName + "." + p.Name); acted = true; }
                            else log.Add("already on: " + fullName + "." + p.Name);
                        }
                        catch (Exception ex) { log.Add("could not set " + fullName + "." + p.Name + ": " + ex.Message); }
                    }
                } catch { }

                // Invoke zero-arg static Start/Enable/TryStart/Initialize methods.
                try
                {
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (m.GetParameters().Length != 0) continue;
                        string mn = m.Name;
                        if (!(mn.Equals("Start", StringComparison.OrdinalIgnoreCase)
                              || mn.Equals("Enable", StringComparison.OrdinalIgnoreCase)
                              || mn.Equals("TryStart", StringComparison.OrdinalIgnoreCase)
                              || mn.Equals("Initialize", StringComparison.OrdinalIgnoreCase)
                              || mn.Equals("StartServer", StringComparison.OrdinalIgnoreCase))) continue;
                        if (m.IsGenericMethod) continue;
                        try { m.Invoke(null, null); log.Add("INVOKED: " + fullName + "." + mn); acted = true; }
                        catch (Exception ex) { log.Add("invoke failed " + fullName + "." + mn + ": " + ex.Message); }
                    }
                } catch { }
            }
        }

        if (!acted) log.Add("No Mcp toggles found yet — the Unity AI package may still be installing. Reopen the project if this file never changes.");
        else log.Add("MCP bootstrap attempted. If the Unity AI window shows an MCP Server toggle, confirm it is ON.");

        try
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            File.WriteAllLines(Path.Combine(root, "BIG-BRIDGE-STATUS.txt"), log.ToArray());
        } catch (Exception ex) { Debug.LogWarning(kTag + " could not write status file: " + ex.Message); }

        foreach (var line in log) Debug.Log(kTag + " " + line);
    }
}
#endif