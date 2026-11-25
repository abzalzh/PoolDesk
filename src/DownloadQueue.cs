using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

class DownloadControl {
    readonly object gate=new object();
    TaskCompletionSource<bool> resume;
    internal bool Paused { get { lock(gate) return resume!=null; } }
    internal void Pause() { lock(gate) if(resume==null) resume=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); }
    internal void Resume() { lock(gate) { var pending=resume; resume=null; pending?.TrySetResult(true); } }
    internal async Task Wait(CancellationToken token) { Task wait; lock(gate) wait=resume?.Task; if(wait!=null) await wait.WaitAsync(token); token.ThrowIfCancellationRequested(); }
}
class PartialDownload { public string Url=""; public string ETag=""; }
class DownloadQueue {
    internal static readonly Dictionary<string,string> Mirrors=new Dictionary<string,string> {
        {"Nerinyan","https://api.nerinyan.moe/d/"},{"Catboy","https://catboy.best/d/"},{"Sayobot","https://dl.sayobot.cn/beatmaps/download/full/"}
    };
    readonly HttpClient client;
    internal DownloadQueue(HttpClient client=null) {
        this.client=client??new HttpClient { Timeout=Timeout.InfiniteTimeSpan };
        if(!this.client.DefaultRequestHeaders.UserAgent.Any()) this.client.DefaultRequestHeaders.UserAgent.ParseAdd("PoolDesk/1.0");
    }
    internal static Dictionary<int,string> Validate(string file,IEnumerable<Chart> requested) {
        var maps=requested.ToList(); var found=Pool.Inspect(file,maps.Select(m=>m.Id));
        foreach(var map in maps) if(!string.IsNullOrEmpty(map.Checksum) && !String.Equals(map.Checksum,found[map.Id],StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Tournament MD5 is unavailable on this mirror: "+map.Slot+" "+map.Url);
        return found;
    }
    internal async Task<Dictionary<int,string>> Fetch(string destination,int set,List<Chart> maps,AppSettings settings,DownloadControl control,CancellationToken token,Action<long,long,string> progress,Action<string> log) {
        if(File.Exists(destination)) {
            try { return Validate(destination,maps); } catch(InvalidDataException) { File.Delete(destination); } catch(IOException) { File.Delete(destination); }
        }
        string last="";
        foreach(string mirror in settings.Mirrors) {
            string endpoint; if(!Mirrors.TryGetValue(mirror,out endpoint)) continue;
            for(int attempt=0;attempt<settings.Attempts;attempt++) {
                await control.Wait(token);
                try {
                    await Download(endpoint+set,destination,control,token,(n,total)=>progress(n,total,mirror));
                    var found=Validate(destination+".part",maps);
                    File.Move(destination+".part",destination,true);
                    if(File.Exists(destination+".part.json")) File.Delete(destination+".part.json");
                    return found;
                } catch(OperationCanceledException) when(token.IsCancellationRequested) { throw; }
                catch(Exception ex) {
                    last=ex.Message; log("MIRROR "+mirror+" attempt="+(attempt+1)+" "+ex.Message);
                    if(ex is InvalidDataException) { DeletePartial(destination); break; }
                    if(ex is HttpRequestException http && (http.StatusCode==HttpStatusCode.NotFound || http.StatusCode==HttpStatusCode.Forbidden || http.StatusCode==HttpStatusCode.Gone)) { DeletePartial(destination); break; }
                    if(attempt+1<settings.Attempts) await Task.Delay(TimeSpan.FromSeconds(Math.Min(20,Math.Pow(2,attempt))+Random.Shared.NextDouble()),token);
                }
            }
        }
        throw new IOException("All mirrors failed for beatmapset "+set+": "+last);
    }
    static void DeletePartial(string destination) { foreach(string suffix in new[]{".part",".part.json"}) if(File.Exists(destination+suffix)) File.Delete(destination+suffix); }
    internal async Task Download(string url,string destination,DownloadControl control,CancellationToken token,Action<long,long> progress) {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        string partial=destination+".part", metadata=partial+".json";
        PartialDownload state;
        try { state=AppPaths.Load<PartialDownload>(metadata); } catch { state=new PartialDownload(); }
        long offset=File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if(state.Url!=url || string.IsNullOrEmpty(state.ETag) || state.ETag.StartsWith("W/")) offset=0;
        using(var request=new HttpRequestMessage(HttpMethod.Get,url)) {
            if(offset>0) { request.Headers.Range=new RangeHeaderValue(offset,null); request.Headers.TryAddWithoutValidation("If-Range",state.ETag); }
            using(var headers=CancellationTokenSource.CreateLinkedTokenSource(token)) {
                headers.CancelAfter(TimeSpan.FromSeconds(45));
                using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,headers.Token)) {
                    if(response.StatusCode==HttpStatusCode.RequestedRangeNotSatisfiable) { DeletePartial(destination); throw new IOException("Partial file expired; restarting."); }
                    if((int)response.StatusCode==429) {
                        var delay=response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date-DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(5);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds,1,60)),token);
                    }
                    response.EnsureSuccessStatusCode();
                    bool append=offset>0 && response.StatusCode==HttpStatusCode.PartialContent && response.Content.Headers.ContentRange?.From==offset && response.Headers.ETag?.ToString()==state.ETag;
                    if(response.StatusCode==HttpStatusCode.PartialContent && !append) { DeletePartial(destination); throw new IOException("Invalid byte range response."); }
                    if(!append) offset=0;
                    AppPaths.Save(metadata,new PartialDownload{Url=url,ETag=response.Headers.ETag?.ToString()??""});
                    long total=offset+(response.Content.Headers.ContentLength??0);
                    if(total>2L*1024*1024*1024) throw new InvalidDataException("Archive exceeds 2 GiB.");
                    using(var input=await response.Content.ReadAsStreamAsync(token))
                    using(var output=new FileStream(partial,append?FileMode.Append:FileMode.Create,FileAccess.Write,FileShare.Read,131072,true)) {
                        byte[] buffer=new byte[131072]; long received=offset;
                        while(true) {
                            await control.Wait(token);
                            int n;
                            using(var read=CancellationTokenSource.CreateLinkedTokenSource(token)) { read.CancelAfter(TimeSpan.FromSeconds(45)); n=await input.ReadAsync(buffer,read.Token); }
                            if(n==0) break;
                            received+=n; if(received>2L*1024*1024*1024) throw new InvalidDataException("Archive exceeds 2 GiB.");
                            await output.WriteAsync(buffer.AsMemory(0,n),token); progress(received,total);
                        }
                        await output.FlushAsync(token);
                        if(total>0 && received!=total) throw new IOException("Incomplete HTTP response.");
                    }
                }
            }
        }
    }
}
