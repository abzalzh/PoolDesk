using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

class OwnedFile { public string Relative=""; public string Sha256=""; }
class OwnedFolder { public string Path=""; public List<OwnedFile> Files=new List<OwnedFile>(); public List<string> Hashes=new List<string>(); }
class InstallReceipt {
    public string Id=Guid.NewGuid().ToString("N"); public DateTime Date=DateTime.UtcNow;
    public string Pool="",Stage="",Source="",Root="",Songs="",Collection="",State="Running",Log="";
    public bool CollectionCreated,CollectionCommitted,Undone;
    public string CollectionBefore="",CollectionAfter="";
    public List<string> AddedHashes=new List<string>(),Hashes=new List<string>();
    public List<string> Errors=new List<string>();
    public List<OwnedFolder> Folders=new List<OwnedFolder>();
    public int Completed,Skipped,Failed;
    internal string FilePath { get { return System.IO.Path.Combine(AppPaths.History,Id+".json"); } }
    internal void Save() { AppPaths.Save(FilePath,this); }
    public override string ToString() { return Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm")+" · "+Pool+" / "+Stage+" · "+UiText.State(State); }
}
static class InstallHistory {
    internal static List<InstallReceipt> ReadAll() {
        var result=new List<InstallReceipt>(); if(!Directory.Exists(AppPaths.History)) return result;
        foreach(string file in Directory.EnumerateFiles(AppPaths.History,"*.json")) {
            try { var entry=AppPaths.Load<InstallReceipt>(file); if(entry.Id==System.IO.Path.GetFileNameWithoutExtension(file)) result.Add(entry); } catch { }
        }
        return result.OrderByDescending(x=>x.Date).ToList();
    }
    internal static string Sha(string path) { using(var s=File.OpenRead(path)) return Convert.ToHexString(SHA256.HashData(s)); }
    internal static string DbSha(string root) { string file=System.IO.Path.Combine(root,"collection.db"); return File.Exists(file)?Sha(file):""; }
    internal static OwnedFolder Extract(string archive,string songs,int set,List<Chart> requested,InstallReceipt receipt,CancellationToken token,Action assertClosed=null) {
        assertClosed=assertClosed??Stable.Closed;
        string name="PoolDesk-"+set+"-"+Guid.NewGuid().ToString("N");
        string final=System.IO.Path.Combine(songs,name),stage=final+".staging";
        Directory.CreateDirectory(songs);
        var folder=new OwnedFolder{Path=final}; var wanted=new HashSet<int>(requested.Select(m=>m.Id));
        long total=0; var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try {
            Directory.CreateDirectory(stage);
            using(var zip=ZipFile.OpenRead(archive)) {
                if(zip.Entries.Count>20000) throw new InvalidDataException("Archive has too many files.");
                foreach(var entry in zip.Entries) {
                    token.ThrowIfCancellationRequested();
                    string relative=entry.FullName.Replace('/',System.IO.Path.DirectorySeparatorChar);
                    if(string.IsNullOrEmpty(entry.Name)) continue;
                    string dest=System.IO.Path.GetFullPath(System.IO.Path.Combine(stage,relative));
                    if(!AppPaths.Inside(dest,stage) || relative.Split(System.IO.Path.DirectorySeparatorChar).Any(x=>x==".."||x=="."||x.Contains(':')||x.TrimEnd(' ','.')!=x) || ((entry.ExternalAttributes>>16)&0xf000)==0xa000)
                        throw new InvalidDataException("Unsafe archive path: "+entry.FullName);
                    if(!seen.Add(dest)) throw new InvalidDataException("Duplicate archive path.");
                    total+=entry.Length; if(total>2L*1024*1024*1024) throw new InvalidDataException("Expanded archive exceeds 2 GiB.");
                    bool beatmap=relative.EndsWith(".osu",StringComparison.OrdinalIgnoreCase);
                    byte[] mapData=null;
                    if(beatmap) {
                        if(entry.Length>8*1024*1024) throw new InvalidDataException("Beatmap exceeds 8 MiB.");
                        using(var input=entry.Open()) using(var memory=new MemoryStream()) { input.CopyTo(memory); mapData=memory.ToArray(); }
                        if(!wanted.Contains(LocalInventory.Id(mapData))) continue;
                        folder.Hashes.Add(LocalInventory.Md5(mapData));
                    }
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
                    if(mapData!=null) File.WriteAllBytes(dest,mapData);
                    else using(var input=entry.Open()) using(var output=File.Create(dest)) input.CopyTo(output);
                    folder.Files.Add(new OwnedFile{Relative=relative,Sha256=Sha(dest)});
                }
            }
            if(folder.Hashes.Count<requested.Select(x=>x.Id).Distinct().Count()) throw new InvalidDataException("Required difficulties are missing.");
            lock(receipt) { receipt.Folders.Add(folder); receipt.Save(); }
            assertClosed(); token.ThrowIfCancellationRequested(); Directory.Move(stage,final);
            return folder;
        } finally {
            if(Directory.Exists(stage) && AppPaths.Inside(stage,songs) && System.IO.Path.GetFileName(stage).StartsWith("PoolDesk-")) Directory.Delete(stage,true);
        }
    }
    internal static List<string> Undo(InstallReceipt receipt,bool deleteMaps,int backups,Action assertClosed=null) {
        assertClosed=assertClosed??Stable.Closed; assertClosed(); var warnings=new List<string>();
        var others=ReadAll().Where(x=>x.Id!=receipt.Id&&!x.Undone&&String.Equals(x.Root,receipt.Root,StringComparison.OrdinalIgnoreCase)).ToList();
        var shared=new HashSet<string>(others.Where(x=>x.Collection==receipt.Collection).SelectMany(x=>x.Hashes),StringComparer.OrdinalIgnoreCase);
        bool pending= !receipt.CollectionCommitted && receipt.CollectionAfter!="";
        if(pending && DbSha(receipt.Root)!=receipt.CollectionAfter && DbSha(receipt.Root)!=receipt.CollectionBefore) throw new IOException("Interrupted transaction and collection.db changed. Restore the relevant backup manually; automatic removal was stopped.");
        if(!receipt.Undone && (receipt.CollectionCommitted || pending&&DbSha(receipt.Root)==receipt.CollectionAfter)) {
            CollectionStore.Mutate(receipt.Root,backups,doc=>CollectionStore.RemoveAdded(doc,receipt.Collection,receipt.AddedHashes.Where(x=>!shared.Contains(x)),receipt.CollectionCreated),null,assertClosed);
        }
        // Pass ownership to surviving installs before marking this receipt removed.
        foreach(var hash in receipt.AddedHashes.Where(shared.Contains)) {
            var next=others.First(x=>x.Collection==receipt.Collection && x.Hashes.Contains(hash,StringComparer.OrdinalIgnoreCase));
            if(!next.AddedHashes.Contains(hash,StringComparer.OrdinalIgnoreCase)) next.AddedHashes.Add(hash);
            next.CollectionCreated |= receipt.CollectionCreated; next.Save();
        }
        foreach(var folder in receipt.Folders) {
            var next=others.FirstOrDefault(x=>folder.Hashes.Any(h=>x.Hashes.Contains(h,StringComparer.OrdinalIgnoreCase)));
            if(next!=null && !next.Folders.Any(x=>x.Path==folder.Path)) { next.Folders.Add(folder); next.Save(); }
        }
        receipt.Undone=true; receipt.State="Removed"; receipt.Save();
        if(deleteMaps) {
            var needed=new HashSet<string>(others.SelectMany(x=>x.Hashes).Concat(CollectionStore.Load(receipt.Root).Entries.SelectMany(x=>x.Hashes)),StringComparer.OrdinalIgnoreCase);
            foreach(var folder in receipt.Folders) {
                try {
                    if(!Directory.Exists(folder.Path)) continue;
                    if(folder.Hashes.Any(needed.Contains)) { warnings.Add("Shared maps kept: "+folder.Path); continue; }
                    VerifyOwned(folder,receipt.Songs); assertClosed(); Directory.Delete(folder.Path,true);
                } catch(Exception ex) { warnings.Add(ex.Message); }
            }
        }
        return warnings;
    }
    internal static void VerifyOwned(OwnedFolder folder,string songs) {
        if(!AppPaths.Inside(folder.Path,songs) || !System.IO.Path.GetFileName(folder.Path).StartsWith("PoolDesk-",StringComparison.Ordinal) || (File.GetAttributes(folder.Path)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Folder is not owned by PoolDesk: "+folder.Path);
        var files=new List<string>(); var pending=new Stack<string>(); pending.Push(folder.Path);
        while(pending.Count>0) foreach(string entry in Directory.EnumerateFileSystemEntries(pending.Pop())) {
            var attrs=File.GetAttributes(entry);
            if((attrs&FileAttributes.ReparsePoint)!=0) throw new IOException("Linked directory preserved: "+folder.Path);
            if((attrs&FileAttributes.Directory)!=0) pending.Push(entry); else files.Add(entry);
        }
        if(files.Count!=folder.Files.Count || folder.Files.Any(x=>!AppPaths.Inside(System.IO.Path.Combine(folder.Path,x.Relative),folder.Path)||!File.Exists(System.IO.Path.Combine(folder.Path,x.Relative))||Sha(System.IO.Path.Combine(folder.Path,x.Relative))!=x.Sha256))
            throw new IOException("Modified folder preserved: "+folder.Path);
    }
}
