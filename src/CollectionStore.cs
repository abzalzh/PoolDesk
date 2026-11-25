using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

class CollectionEntry { public string Name=""; public List<string> Hashes=new List<string>(); }
class CollectionDocument { public int Version=20191106; public List<CollectionEntry> Entries=new List<CollectionEntry>(); }
class CollectionChange { public bool Created; public List<string> Added=new List<string>(); }
static class CollectionStore {
    internal static CollectionDocument Read(byte[] bytes) {
        if(bytes.Length>128*1024*1024) throw new InvalidDataException("collection.db exceeds 128 MiB.");
        using(var stream=new MemoryStream(bytes)) using(var r=new BinaryReader(stream,Encoding.UTF8)) {
            var doc=new CollectionDocument { Version=r.ReadInt32() }; int count=Count(r);
            for(int i=0;i<count;i++) {
                var group=new CollectionEntry { Name=Text(r) }; int length=Count(r);
                for(int j=0;j<length;j++) group.Hashes.Add(Text(r)); doc.Entries.Add(group);
            }
            if(stream.Position!=stream.Length) throw new InvalidDataException("Unexpected trailing collection.db data.");
            return doc;
        }
    }
    static int Count(BinaryReader r) {
        int n=r.ReadInt32(); if(n<0 || n>1000000 || n>r.BaseStream.Length-r.BaseStream.Position) throw new InvalidDataException("Invalid collection count."); return n;
    }
    static string Text(BinaryReader r) {
        byte marker=r.ReadByte(); if(marker==0) return ""; if(marker!=11) throw new InvalidDataException("Invalid string marker.");
        uint size=0; int shift=0;
        while(true) { byte b=r.ReadByte(); if(shift>=28 && (b&0xf0)!=0) throw new InvalidDataException("Invalid string length."); size|=(uint)(b&127)<<shift; if((b&128)==0) break; shift+=7; if(shift>28) throw new InvalidDataException("Invalid string length."); }
        if(size>1024*1024 || size>r.BaseStream.Length-r.BaseStream.Position) throw new InvalidDataException("Truncated collection string.");
        return new UTF8Encoding(false,true).GetString(r.ReadBytes((int)size));
    }
    internal static byte[] Write(CollectionDocument doc) {
        using(var stream=new MemoryStream()) {
            using(var w=new BinaryWriter(stream,Encoding.UTF8,true)) {
                w.Write(doc.Version); w.Write(doc.Entries.Count);
                foreach(var group in doc.Entries) { Put(w,group.Name); w.Write(group.Hashes.Count); foreach(var hash in group.Hashes) Put(w,hash); }
            }
            return stream.ToArray();
        }
    }
    static void Put(BinaryWriter w,string value) { w.Write((byte)(value.Length==0 ? 0 : 11)); if(value.Length>0) w.Write(value); }
    internal static CollectionDocument Load(string root) { string path=Path.Combine(root,"collection.db"); return File.Exists(path) ? Read(File.ReadAllBytes(path)) : new CollectionDocument(); }
    internal static CollectionChange Add(CollectionDocument doc,string name,IEnumerable<string> hashes) {
        var change=new CollectionChange(); var group=doc.Entries.FirstOrDefault(x=>x.Name==name);
        if(group==null) { group=new CollectionEntry{Name=name}; doc.Entries.Add(group); change.Created=true; }
        var known=new HashSet<string>(group.Hashes,StringComparer.OrdinalIgnoreCase);
        foreach(string hash in hashes) if(known.Add(hash)) { group.Hashes.Add(hash); change.Added.Add(hash); }
        return change;
    }
    internal static void RemoveAdded(CollectionDocument doc,string name,IEnumerable<string> added,bool created) {
        var group=doc.Entries.FirstOrDefault(x=>x.Name==name); if(group==null) return;
        var remove=new HashSet<string>(added,StringComparer.OrdinalIgnoreCase); group.Hashes.RemoveAll(remove.Contains);
        if(created && group.Hashes.Count==0) doc.Entries.Remove(group);
    }
    internal static void Mutate(string root,int backupCount,Action<CollectionDocument> action,Action<CollectionDocument> beforeCommit=null,Action assertClosed=null) {
        assertClosed=assertClosed??Stable.Closed; assertClosed();
        string path=Path.Combine(root,"collection.db"); Directory.CreateDirectory(root);
        using(var guard=new FileStream(Path.Combine(root,"collection.db.pooldesk.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
            byte[] original=File.Exists(path) ? File.ReadAllBytes(path) : null;
            var doc=original==null ? new CollectionDocument() : Read(original); action(doc);
            byte[] bytes=Write(doc); Read(bytes);
            if(original!=null && original.SequenceEqual(bytes)) return;
            beforeCommit?.Invoke(doc);
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                assertClosed();
                byte[] current=File.Exists(path) ? File.ReadAllBytes(path) : null;
                if((original==null)!=(current==null) || original!=null&&!original.SequenceEqual(current)) throw new IOException("collection.db changed during installation; no replacement was performed.");
                if(original==null) File.Move(temp,path);
                else File.Replace(temp,path,path+".pooldesk."+DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff")+".bak");
                Rotate(root,backupCount);
            } finally { if(File.Exists(temp)) File.Delete(temp); }
        }
    }
    internal static void Rotate(string root,int keep) {
        foreach(var file in new DirectoryInfo(root).GetFiles("collection.db.pooldesk.*.bak").OrderByDescending(f=>f.Name).Skip(Math.Max(3,keep))) {
            try { file.Delete(); } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
    }
}
