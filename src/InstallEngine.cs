using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class TransferProgress { public int Done,Total; public long Bytes,Expected; public string Message=""; }
class InstallEngine {
    readonly Action assertClosed; readonly AppSettings settings; readonly DownloadQueue downloads;
    internal InstallEngine(AppSettings settings,DownloadQueue downloads=null,Action assertClosed=null) { this.assertClosed=assertClosed??Stable.Closed; this.settings=settings; this.downloads=downloads??new DownloadQueue(); }
    internal async Task<InstallReceipt> Run(Pool pool,Stage stage,List<Chart> maps,string root,string destination,string collection,LocalInventory inventory,DownloadControl control,CancellationToken token,IProgress<TransferProgress> progress) {
        bool install=!string.IsNullOrEmpty(root); string songs=install?Stable.Songs(root):"";
        var receipt=new InstallReceipt{Pool=pool.Name,Stage=stage.Name,Source=pool.Source,Root=root,Songs=songs,Collection=collection};
        Directory.CreateDirectory(AppPaths.Logs); Directory.CreateDirectory(destination);
        receipt.Log=Path.Combine(AppPaths.Logs,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+receipt.Id+".log");
        using(var file=new StreamWriter(receipt.Log,false,Encoding.UTF8)) {
            file.AutoFlush=true; object logGate=new object();
            Action<string> log=message=> { lock(logGate) file.WriteLine(DateTimeOffset.Now.ToString("O")+" "+message); };
            log("START source="+pool.Source+" stage="+stage.Name+" destination="+destination);
            receipt.Save();
            var groups=maps.Where(x=>x.Available).GroupBy(x=>x.Set).ToList(); int done=0;
            foreach(var missing in maps.Where(x=>!x.Available)) { receipt.Failed++; receipt.Errors.Add(missing.Slot+" · "+missing.Url); log("UNRESOLVED "+missing.Slot+" "+missing.Url); }
            var perSet=new System.Collections.Concurrent.ConcurrentDictionary<int,Tuple<long,long,string>>(); object progressGate=new object();
            Action<int,long,long,string> report=(id,bytes,total,message)=> {
                lock(progressGate) {
                    perSet[id]=Tuple.Create(bytes,total,message);
                    progress?.Report(new TransferProgress{Done=done,Total=groups.Count,Bytes=perSet.Values.Sum(x=>x.Item1),Expected=perSet.Values.Sum(x=>x.Item2),Message=message});
                }
            };
            using(var workers=new SemaphoreSlim(settings.ParallelDownloads)) {
                try {
                    await Task.WhenAll(groups.Select(async group=> {
                        await workers.WaitAsync(token);
                        try {
                            await control.Wait(token);
                            var requested=group.ToList(); var missing=new List<Chart>(); var hashes=new List<string>();
                            foreach(var map in requested) {
                                var local=install?inventory.Exact(map):null;
                                if(local==null) missing.Add(map);
                                else { hashes.Add(local.Hash); log("EXISTING "+map.Slot+" "+map.Url+" "+local.Path); }
                            }
                            if(missing.Count==0) {
                                lock(receipt) { receipt.Skipped++; receipt.Hashes.AddRange(hashes); receipt.Save(); }
                                report(group.Key,0,0,UiText.Pick("Уже установлено: ","Already installed: ")+group.Key); return;
                            }
                            string signature=(settings.IncludeVideo?"video:":"novideo:")+string.Join(";",missing.OrderBy(m=>m.Id).Select(m=>m.Id+":"+m.Checksum));
                            string suffix=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))).Substring(0,12);
                            string target=Path.Combine(destination,group.Key+"-"+suffix+".osz");
                            var found=await downloads.Fetch(target,group.Key,missing,settings,control,token,(bytes,total,mirror)=>report(group.Key,bytes,total,mirror+" · "+group.First().Song),log);
                            await control.Wait(token);
                            if(install) await Task.Run(()=>InstallHistory.Extract(target,songs,group.Key,missing,receipt,token,assertClosed),token);
                            hashes.AddRange(missing.Select(m=>found[m.Id]));
                            lock(receipt) { receipt.Hashes.AddRange(hashes); receipt.Completed++; receipt.Save(); }
                            log("OK set="+group.Key+" maps="+string.Join(",",missing.Select(m=>m.Id)));
                        } catch(OperationCanceledException) when(token.IsCancellationRequested) { throw; }
                        catch(Exception ex) { lock(receipt) { receipt.Failed++; receipt.Errors.Add(string.Join("; ",group.Select(m=>m.Slot+" · "+m.Url))+"\n"+ex.Message); receipt.Save(); } log("ERROR set="+group.Key+" "+ex); foreach(var map in group) log(map.Slot+" "+map.Url); }
                        finally { Interlocked.Increment(ref done); report(group.Key,perSet.TryGetValue(group.Key,out var value)?value.Item1:0,value?.Item2??0,UiText.Pick("Обработано ","Processed ")+done+"/"+groups.Count); workers.Release(); }
                    }));
                    token.ThrowIfCancellationRequested();
                    if(install&&!string.IsNullOrWhiteSpace(collection)&&receipt.Hashes.Count>0) {
                        await Task.Run(()=>CollectionStore.Mutate(root,settings.BackupCount,doc=> {
                            receipt.CollectionBefore=InstallHistory.DbSha(root);
                            var change=CollectionStore.Add(doc,collection,receipt.Hashes);
                            receipt.CollectionCreated=change.Created; receipt.AddedHashes=change.Added;
                        },doc=> { receipt.CollectionAfter=Convert.ToHexString(SHA256.HashData(CollectionStore.Write(doc))); receipt.Save(); },assertClosed),token);
                        receipt.CollectionCommitted=true;
                    }
                    receipt.State=receipt.Failed>0?"Partial":"Completed";
                    log("END installed="+receipt.Completed+" existing="+receipt.Skipped+" failed="+receipt.Failed);
                } catch(OperationCanceledException) { receipt.State="Cancelled"; log("CANCELLED; installed files remain recorded in History"); }
                catch(Exception ex) { receipt.State="Interrupted"; log("FATAL "+ex); throw; }
                finally { receipt.Hashes=receipt.Hashes.Distinct(StringComparer.OrdinalIgnoreCase).ToList(); receipt.Save(); }
            }
            return receipt;
        }
    }
}
