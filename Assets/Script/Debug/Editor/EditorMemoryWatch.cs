using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

// Finds what makes the editor's memory jump. Every 5 s (and on play mode / domain reload events) it appends one line to
// Logs/MemoryWatch.csv: process private memory, the Mono heap (Boehm never gives memory back to Windows, so one big
// managed spike stays for the whole session), Unity's native reserve, play state and the focused window.
// A jump of 300 MB+ is also written to the Console. Delete this file when the cause is found.
[InitializeOnLoad]
static class EditorMemoryWatch
{
    const double Interval = 5;
    const long JumpMB = 300;
    static readonly string Path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs", "MemoryWatch.csv");
    static double nextTime;
    static long lastPrivateMB = -1;

    static EditorMemoryWatch()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += s => Write("playmode " + s);
        AssemblyReloadEvents.beforeAssemblyReload += () => Write("before domain reload");
        Write("domain loaded");
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextTime) return;
        nextTime = EditorApplication.timeSinceStartup + Interval;
        Write("");
    }

    static void Write(string evt)
    {
        try
        {
            const long mb = 1024 * 1024;
            long privateMB = PrivateBytes() / mb;
            var win = EditorWindow.focusedWindow;
            string line = string.Format("{0:HH:mm:ss},{1},{2},{3},{4},{5},{6},{7}",
                DateTime.Now, privateMB, Profiler.GetMonoHeapSizeLong() / mb, Profiler.GetMonoUsedSizeLong() / mb,
                Profiler.GetTotalReservedMemoryLong() / mb, EditorApplication.isPlaying ? "play" : "edit",
                win != null ? win.titleContent.text.Replace(',', ' ') : "-", evt);

            bool newFile = !File.Exists(Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            File.AppendAllText(Path, (newFile ? "time,privateMB,monoHeapMB,monoUsedMB,nativeReservedMB,state,focusedWindow,event\n" : "") + line + "\n");

            if (lastPrivateMB >= 0 && privateMB - lastPrivateMB >= JumpMB)
                Debug.LogWarning("[MemoryWatch] +" + (privateMB - lastPrivateMB) + " MB: " + line);
            lastPrivateMB = privateMB;
        }
        catch (Exception) { }
    }

    // Process.PrivateMemorySize64 is always 0 under the editor's Mono, so ask Windows directly (Task Manager's number).
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct ProcessMemoryCounters
    {
        public uint cb, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
            QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }

    [System.Runtime.InteropServices.DllImport("psapi.dll")]
    static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, uint size);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    static long PrivateBytes()
    {
        var c = new ProcessMemoryCounters();
        uint size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(ProcessMemoryCounters));
        c.cb = size;
        return GetProcessMemoryInfo(GetCurrentProcess(), out c, size) ? (long)c.PrivateUsage.ToUInt64() : 0;
    }
}
