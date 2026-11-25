using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

class LocalMap { public int Id; public string Hash=""; public string Path=""; public long Length; public long Ticks; }
class LocalInventory {
    internal List<LocalMap> Maps=new List<LocalMap>();
    internal LocalMap Exact(Chart map) { return Maps.FirstOrDefault(x=>x.Id==map.Id && (string.IsNullOrEmpty(map.Checksum)||x.Hash.Equals(map.Checksum,StringComparison.OrdinalIgnoreCase))); }
    internal bool Different(Chart map) { return !string.IsNullOrEmpty(map.Checksum) && Maps.Any(x=>x.Id==map.Id) && Exact(map)==null; }
    internal static string Md5(byte[] data) { return Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant(); }
    internal static int Id(byte[] data) { var match=Regex.Match(Encoding.UTF8.GetString(data),@"(?m)^BeatmapID\s*:\s*(\d+)\s*$"); int id; return int.TryParse(match.Groups[1].Value,out id)?id:0; }
    internal static LocalInventory Scan(string songs,CancellationToken token,Action<string> log) {
        var inventory=new LocalInventory(); if(!Directory.Exists(songs)) return inventory;
        string cache=Path.Combine(AppPaths.Root,"index-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(songs).ToUpperInvariant()))).Substring(0,12)+".json");
        List<LocalMap> saved; try { saved=AppPaths.Load<List<LocalMap>>(cache); } catch { saved=new List<LocalMap>(); }
        var byPath=saved.GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
        var options=new EnumerationOptions { RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint };
        foreach(string path in Directory.EnumerateFiles(songs,"*",options)) {
            token.ThrowIfCancellationRequested();
            string ext=Path.GetExtension(path);
            if(!ext.Equals(".osu",StringComparison.OrdinalIgnoreCase) && !ext.Equals(".osz",StringComparison.OrdinalIgnoreCase)) continue;
            try {
                var file=new FileInfo(path);
                if(ext.Equals(".osz",StringComparison.OrdinalIgnoreCase)) {
                    foreach(var pair in Pool.Inspect(path,Array.Empty<int>())) inventory.Maps.Add(new LocalMap{Id=pair.Key,Hash=pair.Value,Path=path,Length=file.Length,Ticks=file.LastWriteTimeUtc.Ticks});
                } else {
                    if(file.Length>8*1024*1024) continue;
                    LocalMap old;
                    if(byPath.TryGetValue(path,out old) && old.Length==file.Length && old.Ticks==file.LastWriteTimeUtc.Ticks) inventory.Maps.Add(old);
                    else { byte[] data=File.ReadAllBytes(path); int id=Id(data); if(id>0) inventory.Maps.Add(new LocalMap{Id=id,Hash=Md5(data),Path=path,Length=file.Length,Ticks=file.LastWriteTimeUtc.Ticks}); }
                }
            } catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) { log("INDEX "+path+" "+ex.Message); }
        }
        AppPaths.Save(cache,inventory.Maps.Where(x=>x.Path.EndsWith(".osu",StringComparison.OrdinalIgnoreCase)).ToList()); return inventory;
    }
}
