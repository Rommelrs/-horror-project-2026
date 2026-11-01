using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;

// Works around a memory blow-up in the Unity AI Assistant package (com.unity.ai.assistant 2.19.0-pre.2).
//
// When an MCP client (Claude Code) connects, the package's ExecutableIdentityCollector checks the client's executable:
// a SHA256 hash plus X509Certificate.CreateFromSignedFile for the Authenticode signature. Under the editor's Mono the
// signature call reads the whole file into managed arrays - for the ~300 MB claude.exe that is ~1.4 GB of garbage per
// call, it fails anyway ("Input data cannot be coded as a valid certificate"), and the first connection runs it about
// three times at once. The Mono heap grows to ~7 GB and never shrinks, so the editor sits at ~11 GB all session.
//
// The package caches identities per executable path + modification time (in memory, saved to SessionState). This script
// puts claude.exe into that cache before it connects, with exactly what the package would end up with: the streamed
// SHA256 (cheap) and "unsigned" (what the failed signature check produces). Approval prompts and trust matching are
// unchanged - the package keys unsigned clients by hash either way. If the package's internals change this does nothing.
// Remove once a package update stops reading big executables into memory.
[InitializeOnLoad]
static class McpIdentityCacheSeed
{
    const string ClientProcess = "claude";
    const double PollSeconds = 3;
    const long MinBytes = 50L * 1024 * 1024; // only big executables are worth it

    static readonly HashSet<string> seeded = new HashSet<string>();
    static double nextPoll;

    static McpIdentityCacheSeed()
    {
        EditorApplication.update += Poll;
    }

    // Claude Code can start before or after the editor, so keep looking (cheap: a process list every few seconds)
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + PollSeconds;

        foreach (var p in Process.GetProcessesByName(ClientProcess))
        {
            using (p)
            {
                string path = ImagePath(p.Id);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                string key = path + "|" + File.GetLastWriteTime(path).Ticks;
                if (!seeded.Add(key)) continue;
                if (new FileInfo(path).Length < MinBytes) continue;
                Task.Run(() => Seed(path));
            }
        }
    }

    static void Seed(string path)
    {
        try
        {
            var collector = FindType("Unity.AI.MCP.Editor.Security.ExecutableIdentityCollector");
            var identityType = FindType("Unity.AI.MCP.Editor.Models.ExecutableIdentity");
            if (collector == null || identityType == null) return;
            const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
            var cache = collector.GetField("identityCache", S)?.GetValue(null) as IDictionary;
            var cacheLock = collector.GetField("cacheLock", S)?.GetValue(null);
            var cachedType = collector.GetNestedType("CachedIdentity", BindingFlags.NonPublic);
            if (cache == null || cacheLock == null || cachedType == null) return;

            DateTime modTime = File.GetLastWriteTime(path);
            lock (cacheLock)
                if (cache.Contains(path)) return; // already done by the package (or restored after a domain reload)

            string hash;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();

            var identity = Activator.CreateInstance(identityType, true);
            identityType.GetField("Path").SetValue(identity, path);
            identityType.GetField("SHA256Hash").SetValue(identity, hash);
            identityType.GetField("LastModified").SetValue(identity, modTime);
            identityType.GetField("IsSigned").SetValue(identity, false);
            identityType.GetField("SignatureValid").SetValue(identity, false);

            var cached = Activator.CreateInstance(cachedType, true);
            cachedType.GetField("Identity").SetValue(cached, identity);
            cachedType.GetField("FileModTime").SetValue(cached, modTime);

            lock (cacheLock)
                if (!cache.Contains(path)) cache[path] = cached;

            // let the package write it to SessionState too, so it survives domain reloads (SessionState = main thread)
            var save = collector.GetMethod("SaveCacheToSessionState", S);
            if (save != null) EditorApplication.delayCall += () => save.Invoke(null, null);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[McpIdentityCacheSeed] skipped: " + e.Message);
        }
    }

    static Type FindType(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);

    // Process.MainModule is unreliable under Mono, so ask Windows for the image path
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr process, int flags, System.Text.StringBuilder path, ref int size);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    static string ImagePath(int pid)
    {
        const uint QueryLimitedInformation = 0x1000;
        IntPtr h = OpenProcess(QueryLimitedInformation, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new System.Text.StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }
}
