using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Win32;

static class OsuLocator {
    internal static bool Valid(string folder) { return !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder,"osu!.exe")); }
    internal static List<string> Find(string saved) {
        var candidates=new List<string>{saved,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"osu!")};
        foreach(var process in Process.GetProcessesByName("osu!")) using(process) {
            try { candidates.Add(Path.GetDirectoryName(process.MainModule.FileName)); } catch { }
        }
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine}) {
            foreach(string key in new[]{@"Software\Microsoft\Windows\CurrentVersion\Uninstall\osu!",@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\osu!"})
                try { using(var registry=hive.OpenSubKey(key)) { candidates.Add(registry?.GetValue("InstallLocation") as string); AddCommand(candidates,registry?.GetValue("UninstallString") as string); } } catch { }
        }
        foreach(string key in new[]{@"osu\shell\open\command",@"osu!\shell\open\command",@"osu!\DefaultIcon",@"osu!beatmap\shell\open\command",@"osustable\shell\open\command"})
            try { using(var registry=Registry.ClassesRoot.OpenSubKey(key)) AddCommand(candidates,registry?.GetValue("") as string); } catch { }
        return candidates.Where(Valid).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    internal static void AddCommand(List<string> folders,string command) {
        if(string.IsNullOrEmpty(command)) return;
        var match=Regex.Match(Environment.ExpandEnvironmentVariables(command),"(?:\"([^\"]+\\.exe)\"|^(.+?\\.exe))",RegexOptions.IgnoreCase);
        if(match.Success) folders.Add(Path.GetDirectoryName(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value));
    }
}
