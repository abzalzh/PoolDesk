using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
class TestRunner {
 static int passed; static string root;
 static void Check(bool yes,string text) { if(!yes) throw new Exception(text); }
 static void Test(string name,Action test) { test(); passed++; Console.WriteLine("PASS "+name); }
 static void Fails(Action test) { bool failed=false; try { test(); } catch { failed=true; } Check(failed,"Expected failure"); }
 static string Dir(string name) { string p=Path.Combine(root,name); Directory.CreateDirectory(p); return p; }
 static byte[] Map(int id) => Encoding.UTF8.GetBytes("osu file format v14\n\n[General]\nAudioFilename: audio.wav\nMode: 0\n[Metadata]\nTitle: Test\nArtist: PoolDesk\nCreator: Tests\nVersion: Offline\nBeatmapID: "+id+"\nBeatmapSetID: 1\n[Difficulty]\nHPDrainRate:5\nCircleSize:4\nOverallDifficulty:7\nApproachRate:9\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,2,1,100,1,0\n[HitObjects]\n64,64,1000,1,0,0:0:0:0:\n448,320,1250,1,0,0:0:0:0:\n64,64,1500,1,0,0:0:0:0:\n448,320,1750,1,0,0:0:0:0:\n");
 static Chart Chart(int id) => new Chart{Id=id,Set=id,Slot="NM"+id,Song="Test",Difficulty="Offline",Checksum=LocalInventory.Md5(Map(id))};
 static byte[] Zip(int id,string filename="map.osu") { using(var memory=new MemoryStream()) { using(var z=new ZipArchive(memory,ZipArchiveMode.Create,true)) { using(var stream=z.CreateEntry(filename).Open()) stream.Write(Map(id)); } return memory.ToArray(); } }
 class Handler:HttpMessageHandler {
  internal new Func<HttpRequestMessage,Task<HttpResponseMessage>> Send; internal int Requests;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) { Interlocked.Increment(ref Requests); return Send(request); }
 }
 static HttpResponseMessage Reply(byte[] bytes,HttpStatusCode code=HttpStatusCode.OK) { var r=new HttpResponseMessage(code){Content=new ByteArrayContent(bytes)}; r.Headers.ETag=new EntityTagHeaderValue("\"fixture\""); return r; }
 static DownloadQueue Queue(Handler h) => new DownloadQueue(new HttpClient(h));
 static void Run(Task task) => task.GetAwaiter().GetResult();
 static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();
 [STAThread] static int Main(string[] args) {
  root=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"PoolDesk-tests-"+Guid.NewGuid().ToString("N")));
  Directory.CreateDirectory(root); AppPaths.Root=Dir("state"); Ratings.CacheDirectory=Dir("rating-cache");
  try {
   Test("collection Unicode / empty / multiple round trip",()=> { var d=new CollectionDocument(); CollectionStore.Add(d,"Турнир ★",new[]{"abc","def"}); CollectionStore.Add(d,"",Array.Empty<string>()); byte[] b=CollectionStore.Write(d); Check(b.SequenceEqual(CollectionStore.Write(CollectionStore.Read(b))),"roundtrip"); });
   Test("reject truncated and corrupt DB",()=> { byte[] b=CollectionStore.Write(new CollectionDocument()); for(int i=0;i<b.Length;i++) Fails(()=>CollectionStore.Read(b.Take(i).ToArray())); Fails(()=>CollectionStore.Read(b.Concat(new byte[]{1}).ToArray())); Fails(()=>CollectionStore.Read(new byte[]{1,0,0,0,255,255,255,255})); });
   Test("atomic write, backups rotate, preserve old collection",()=> { string d=Dir("db"); for(int i=0;i<8;i++) { int j=i; CollectionStore.Mutate(d,3,x=>CollectionStore.Add(x,"test",new[]{j.ToString()}),null,()=>{}); } Check(Directory.GetFiles(d,"*.bak").Length==3,"retention"); Check(CollectionStore.Load(d).Entries[0].Hashes.Count==8,"merge"); });
   Test("failed transaction leaves original intact",()=> { string d=Dir("db"); byte[] old=File.ReadAllBytes(Path.Combine(d,"collection.db")); Fails(()=>CollectionStore.Mutate(d,3,x=>CollectionStore.Add(x,"x",new[]{"x"}),x=>throw new IOException("injected"),()=>{})); Check(old.SequenceEqual(File.ReadAllBytes(Path.Combine(d,"collection.db"))),"original changed"); });
   Test("concurrent DB change prevents replacement",()=> { string d=Dir("db-conflict"); CollectionStore.Mutate(d,3,x=>CollectionStore.Add(x,"a",new[]{"a"}),null,()=>{}); var other=CollectionStore.Write(new CollectionDocument()); Fails(()=>CollectionStore.Mutate(d,3,x=>CollectionStore.Add(x,"b",new[]{"b"}),x=>File.WriteAllBytes(Path.Combine(d,"collection.db"),other),()=>{})); Check(File.ReadAllBytes(Path.Combine(d,"collection.db")).SequenceEqual(other),"overwrote external change"); });
   Test("game guard blocks collection writes",()=> { string d=Dir("guard"); Fails(()=>CollectionStore.Mutate(d,3,x=>{},null,()=>throw new IOException("running"))); Check(!File.Exists(Path.Combine(d,"collection.db")),"guard"); });
   Test("settings clamp and persist",()=> { var s=new AppSettings{ParallelDownloads=999,Attempts=0,BackupCount=0,Mirrors=null}; s.Save(); AppSettings.Load(); Check(AppSettings.Current.ParallelDownloads==6&&AppSettings.Current.Attempts==1&&AppSettings.Current.BackupCount==3&&AppSettings.Current.Mirrors.Count==3,"clamp"); AppSettings.Current=new AppSettings(); });
   Test("local .osu and .osz scan / exact MD5",()=> { string d=Dir("songs-scan"); File.WriteAllBytes(Path.Combine(d,"a.osu"),Map(1)); File.WriteAllBytes(Path.Combine(d,"b.osz"),Zip(2)); var inv=LocalInventory.Scan(d,CancellationToken.None,_=>{}); Check(inv.Exact(Chart(1))!=null&&inv.Exact(Chart(2))!=null,"missing maps"); var map=Chart(1); map.Checksum=new string('0',32); Check(inv.Different(map),"mismatch"); });
   Test("mod parsing / conflicting mods",()=> { Check(Ratings.ModsForSlot("HDDT1")=="HDDT"&&Ratings.ModsForSlot("HRDT2")=="HRDT"&&Ratings.ModsForSlot("HR+EZ1")==null&&Ratings.ModsForSlot("TB1")=="NM","mods"); });
   Test("native NM and DT calculation",()=> { double nm=Native.Calculate(Map(1),"NM"),dt=Native.Calculate(Map(1),"DT"); Check(nm>0&&dt>nm,"native rating"); });
   Test("API tournament offline fixture",()=> { var p=Pool.ParseTournament(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","tournament73.json"))); Check(p.Stages.Count>1&&p.Stages.Any(s=>s.Name.ToLowerInvariant().Contains("qual"))&&p.Stages.SelectMany(s=>s.Maps).All(m=>m.Id>0),"parse"); });
   Test("HTTP fallback after missing mirror",()=> { var h=new Handler{Send=r=>Task.FromResult(r.RequestUri.Host.Contains("nerinyan")?Reply(Array.Empty<byte>(),HttpStatusCode.NotFound):Reply(Zip(1)))}; var found=Run(Queue(h).Fetch(Path.Combine(root,"fallback.osz"),1,new List<Chart>{Chart(1)},new AppSettings{Attempts=1},new DownloadControl(),CancellationToken.None,(a,b,c)=>{},_=>{})); Check(found.ContainsKey(1)&&h.Requests==2,"fallback"); });
   Test("checksum mismatch never accepted",()=> { var h=new Handler{Send=r=>Task.FromResult(Reply(Zip(1)))}; var m=Chart(1); m.Checksum=new string('0',32); Fails(()=>Run(Queue(h).Fetch(Path.Combine(root,"wrong.osz"),1,new List<Chart>{m},new AppSettings{Attempts=1},new DownloadControl(),CancellationToken.None,(a,b,c)=>{},_=>{}))); Check(!File.Exists(Path.Combine(root,"wrong.osz")),"wrong checksum accepted"); });
   Test("resume validates ETag and Content-Range",()=> { string d=Path.Combine(root,"resume"); byte[] bytes=Zip(1); File.WriteAllBytes(d+".part",bytes.Take(10).ToArray()); AppPaths.Save(d+".part.json",new PartialDownload{Url="https://test/map",ETag="\"fixture\""}); var h=new Handler{Send=r=>{ Check(r.Headers.Range.Ranges.First().From==10,"range"); var response=Reply(bytes.Skip(10).ToArray(),HttpStatusCode.PartialContent); response.Content.Headers.ContentRange=new ContentRangeHeaderValue(10,bytes.Length-1,bytes.Length); return Task.FromResult(response); }}; Run(Queue(h).Download("https://test/map",d,new DownloadControl(),CancellationToken.None,(a,b)=>{})); Check(bytes.SequenceEqual(File.ReadAllBytes(d+".part")),"resumed bytes"); });
   Test("200 on resume restarts file safely",()=> { string d=Path.Combine(root,"restart"); File.WriteAllBytes(d+".part",new byte[10]); AppPaths.Save(d+".part.json",new PartialDownload{Url="https://test/map",ETag="\"fixture\""}); var h=new Handler{Send=r=>Task.FromResult(Reply(Zip(1)))}; Run(Queue(h).Download("https://test/map",d,new DownloadControl(),CancellationToken.None,(a,b)=>{})); Check(File.ReadAllBytes(d+".part").SequenceEqual(Zip(1)),"restart"); });
   Test("pause / resume / cancel",()=> { var c=new DownloadControl(); c.Pause(); var task=c.Wait(CancellationToken.None); Check(!task.IsCompleted,"not paused"); c.Resume(); Run(task); c.Pause(); using(var token=new CancellationTokenSource()) { token.Cancel(); Fails(()=>Run(c.Wait(token.Token))); } });
   Test("archive traversal blocked",()=> { string a=Path.Combine(root,"unsafe.osz"),songs=Dir("unsafe-songs"); File.WriteAllBytes(a,Zip(1,"../escape.osu")); var receipt=new InstallReceipt(); Fails(()=>InstallHistory.Extract(a,songs,1,new List<Chart>{Chart(1)},receipt,CancellationToken.None,()=>{})); Check(!File.Exists(Path.Combine(root,"escape.osu")),"escaped"); });
   Test("install continues after error / history / skip / shared undo",()=> {
    string d=Dir("game"),songs=Path.Combine(d,"Songs"); File.WriteAllBytes(Path.Combine(d,"osu!.exe"),Array.Empty<byte>());
    var h=new Handler{Send=r=>Task.FromResult(r.RequestUri.AbsolutePath.EndsWith("/2")?Reply(Array.Empty<byte>(),HttpStatusCode.NotFound):Reply(Zip(int.Parse(r.RequestUri.Segments.Last()))))};
    var settings=new AppSettings{Attempts=1}; var engine=new InstallEngine(settings,Queue(h),()=>{}); var stage=new Stage{Name="Qualifiers",Maps=new List<Chart>{Chart(1),Chart(2),Chart(3)}}; var pool=new Pool{Name="Offline"};
    var receipt=Run(engine.Run(pool,stage,stage.Maps,d,Dir("cache"),"pool",new LocalInventory(),new DownloadControl(),CancellationToken.None,null));
    Check(receipt.Completed==2&&receipt.Failed==1&&receipt.State=="Partial","continue"); Check(CollectionStore.Load(d).Entries.Single().Hashes.Count==2,"partial collection"); Check(File.ReadAllText(receipt.Log).Contains("ERROR set=2"),"error log");
    int requests=h.Requests; var inv=LocalInventory.Scan(songs,CancellationToken.None,_=>{}); var selected=new List<Chart>{Chart(1),Chart(3)};
    var second=Run(engine.Run(pool,stage,selected,d,Dir("cache"),"pool",inv,new DownloadControl(),CancellationToken.None,null)); Check(second.Skipped==2&&h.Requests==requests,"downloaded existing");
    InstallHistory.Undo(receipt,true,3,()=>{}); Check(Directory.GetDirectories(songs).Length==2,"removed shared files");
    second=InstallHistory.ReadAll().Single(x=>x.Id==second.Id); InstallHistory.Undo(second,true,3,()=>{}); Check(!CollectionStore.Load(d).Entries.Any()&&Directory.GetDirectories(songs).Length==0,"last shared owner cleanup");
   });
   Test("modified installed folder preserved",()=> { string songs=Dir("modified"),a=Path.Combine(root,"safe.osz"); File.WriteAllBytes(a,Zip(1)); var receipt=new InstallReceipt(); var folder=InstallHistory.Extract(a,songs,1,new List<Chart>{Chart(1)},receipt,CancellationToken.None,()=>{}); InstallHistory.VerifyOwned(folder,songs); File.AppendAllText(Path.Combine(folder.Path,"map.osu"),"changed"); Fails(()=>InstallHistory.VerifyOwned(folder,songs)); Check(Directory.Exists(folder.Path),"deleted modified"); });
   Test("parallel worker limit",()=> { int active=0,max=0; object gate=new object(); var h=new Handler{Send=async r=> { int n=Interlocked.Increment(ref active); lock(gate) max=Math.Max(max,n); await Task.Delay(40); Interlocked.Decrement(ref active); return Reply(Zip(int.Parse(r.RequestUri.Segments.Last()))); }}; var stage=new Stage{Name="parallel",Maps=Enumerable.Range(10,8).Select(Chart).ToList()}; Run(new InstallEngine(new AppSettings{ParallelDownloads=3,Attempts=1},Queue(h),()=>{}).Run(new Pool{Name="parallel"},stage,stage.Maps,"",Dir("parallel"),"",new LocalInventory(),new DownloadControl(),CancellationToken.None,null)); Check(max==3,"parallel limit "+max); });
   Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
   Test("borderless window / RU EN / light theme / render",()=> {
    using(var w=new Window()) { w.StartPosition=FormStartPosition.Manual; w.Location=new Point(-16000,-16000); w.Show(); Application.DoEvents(); Check(w.FormBorderStyle==FormBorderStyle.None,"frame");
     var pool=new Pool{Name="PoolDesk 1.0 · Demo"}; var stage=new Stage{Name="Qualifiers",Maps=Enumerable.Range(1,12).Select(i=>new Chart{Id=i,Set=i,Slot=(i<5?"NM":i<9?"HD":"DT")+i,Song="PoolDesk — Interface preview",Difficulty="Expert",Creator="PoolDesk",Mode="osu",Status="Demo",CS=4,AR=9,OD=8,HP=5,BPM=180,Length=125,BaseStars=4.75,ModStars=i<9?4.75:6.80}).ToList()}; pool.Stages.Add(stage);
     typeof(Window).GetField("pool",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(w,pool);
     var stages=(ComboBox)typeof(Window).GetField("stages",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w); stages.Items.Add(stage); stages.SelectedIndex=0;
     ((Label)typeof(Window).GetField("title",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w)).Text=pool.Name;
     typeof(Window).GetMethod("Toggle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
     var grid=(DataGridView)typeof(Window).GetField("list",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w); grid.CurrentCell=grid.Rows[0].Cells[0]; grid.Focus();
     foreach(bool en in new[]{false,true}) { L.English=en; w.ApplyLanguage(); Application.DoEvents(); using(var b=new Bitmap(w.Width,w.Height)) { w.DrawToBitmap(b,new Rectangle(Point.Empty,w.Size)); b.Save(Path.Combine(root,en?"preview-en.png":"preview-ru.png")); } }
     ThemeManager.Apply(w,true); Application.DoEvents(); using(var b=new Bitmap(w.Width,w.Height)) { w.DrawToBitmap(b,new Rectangle(Point.Empty,w.Size)); b.Save(Path.Combine(root,"preview-light.png")); }
     w.Close(); }
   });
   Test("resize hit corners",()=> { Check(DarkForm.ResizeHit(new Point(0,0),new Size(100,100),6)==13&&DarkForm.ResizeHit(new Point(99,99),new Size(100,100),6)==17,"resize"); });
   Console.WriteLine("PASS TOTAL: "+passed); return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
 }
}
