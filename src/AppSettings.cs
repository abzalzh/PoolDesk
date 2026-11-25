using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

static class AppPaths {
    internal static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PoolDesk");
    internal static string Cache { get { return Path.Combine(Root,"Maps"); } }
    internal static string Logs { get { return Path.Combine(Root,"Logs"); } }
    internal static string History { get { return Path.Combine(Root,"History"); } }
    internal static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields=true, WriteIndented=true };
    internal static void Save<T>(string path,T value) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                JsonSerializer.Serialize(file,value,Json); file.Flush(true);
            }
            File.Move(temp,path,true);
        } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    internal static T Load<T>(string path) where T:new() {
        if(!File.Exists(path)) return new T();
        using(var stream=File.OpenRead(path)) return JsonSerializer.Deserialize<T>(stream,Json) ?? new T();
    }
    internal static bool Inside(string path,string root) {
        return Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    }
    internal static long CacheSize() {
        return new[]{Cache,Path.Combine(Root,"DifficultyMaps")}.Where(Directory.Exists).Sum(d=>Directory.EnumerateFiles(d,"*",SearchOption.TopDirectoryOnly).Sum(f=>new FileInfo(f).Length));
    }
    internal static void CleanCache(long keepBytes) {
        var files=new[]{Cache,Path.Combine(Root,"DifficultyMaps")}.Where(Directory.Exists)
            .SelectMany(d=>Directory.EnumerateFiles(d,"*",SearchOption.TopDirectoryOnly)).Select(f=>new FileInfo(f)).OrderBy(f=>f.LastWriteTimeUtc).ToList();
        long total=files.Sum(f=>f.Length);
        foreach(var file in files) {
            if(total<=keepBytes) break;
            if((file.Attributes&FileAttributes.ReparsePoint)!=0) continue;
            long length=file.Length; file.Delete(); total-=length;
        }
    }
}
class AppSettings {
    internal static AppSettings Current = new AppSettings();
    public string OsuFolder="";
    public int ParallelDownloads=3;
    public int Attempts=3;
    public int BackupCount=10;
    public int CacheLimitGB=5;
    public bool Sounds=true;
    public bool IncludeVideo=false;
    public string Theme="Dark";
    public List<string> Mirrors=new List<string>{"Nerinyan","Catboy","Sayobot"};
    internal void Validate() {
        ParallelDownloads=Math.Clamp(ParallelDownloads,1,6); Attempts=Math.Clamp(Attempts,1,5);
        BackupCount=Math.Clamp(BackupCount,3,50); CacheLimitGB=Math.Clamp(CacheLimitGB,1,50);
        if(Theme!="Dark" && Theme!="Light") Theme="Dark";
        Mirrors=(Mirrors??new List<string>()).Where(x=>DownloadQueue.Mirrors.ContainsKey(x)).Distinct().ToList();
        foreach(var key in DownloadQueue.Mirrors.Keys) if(!Mirrors.Contains(key)) Mirrors.Add(key);
    }
    internal static void Load() {
        try { Current=AppPaths.Load<AppSettings>(Path.Combine(AppPaths.Root,"settings.json")); Current.Validate(); }
        catch(Exception) { Current=new AppSettings(); }
        string legacy=Path.Combine(AppPaths.Root,"stable-folder.txt");
        if(string.IsNullOrEmpty(Current.OsuFolder) && File.Exists(legacy)) Current.OsuFolder=File.ReadAllText(legacy).Trim();
    }
    internal void Save() { Validate(); AppPaths.Save(Path.Combine(AppPaths.Root,"settings.json"),this); }
}
