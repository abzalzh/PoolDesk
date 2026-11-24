using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Windows.Forms;
class Tests21 {
    static int checks;
    static void Check(bool ok,string description) { if(!ok) throw new Exception(description); checks++; Console.WriteLine("PASS " + description); }
    static object Field(Window w,string name) { return typeof(Window).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w); }
    [STAThread] static void Main(string[] args) {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Ratings.CacheDirectory=Path.GetFullPath(".test-cache");
        var chart=new Chart {Id=568565,Set=244799,Slot="DT1",Song="Memme — Chinese Restaurant",Difficulty="Hyper",Checksum="039f937f789273cd6d87a8035895081d"};
        byte[] bytes=Ratings.GetMap(chart,CancellationToken.None).GetAwaiter().GetResult();
        Ratings.Validate(bytes,chart); Check(true,"real file ID and checksum");
        double nm=Native.Calculate(bytes,"NM"), dt=Native.Calculate(bytes,"DT"), hr=Native.Calculate(bytes,"HR"), hd=Native.Calculate(bytes,"HD"), combo=Native.Calculate(bytes,"HDDT");
        Console.WriteLine("NM="+nm+" DT="+dt+" HR="+hr+" HD="+hd+" HDDT="+combo);
        Check(nm>0 && dt>nm && hr>nm,"native NM/DT/HR calculation from real hit objects");
        Check(Math.Abs(Native.Calculate(bytes,"NM")-nm)<1e-12,"repeat calculation deterministic");
        Check(Math.Abs(Native.Calculate(bytes,"NC")-dt)<1e-10,"NC has DT clock rate");
        Check(Ratings.ModsForSlot("HDHR2")=="HDHR" && Ratings.ModsForSlot("DT1")=="DT","combined and single slot mods");
        Check(Ratings.ModsForSlot("NM1")=="NM" && Ratings.ModsForSlot("")=="NM","NM and plain collections");
        Check(Ratings.ModsForSlot("FM1")=="NM" && new Chart{Slot="TB1"}.FreeMods,"FM/TB explicitly use NM baseline");
        Check(Ratings.ModsForSlot("HRDTHT1")==null && Ratings.ModsForSlot("SPEED1")==null,"conflicting or unknown mods are not guessed");
        Check(chart.StarText=="—","no NM fallback masquerading as DT");
        chart.BaseStars=nm; chart.ModStars=dt;
        Check(chart.StarText.Contains("DT") && chart.RatingDetails.Contains(Chart.FormatStars(nm)) && chart.RatingDetails.Contains(Chart.FormatStars(dt)),"table uses mod rating and details show both");
        chart.Checksum=new string('0',32);
        try { Ratings.Validate(bytes,chart); Check(false,"reject wrong checksum"); } catch(IOException) { Check(true,"reject wrong checksum"); }
        chart.Checksum=null; chart.Id=1;
        try { Ratings.Validate(bytes,chart); Check(false,"reject wrong map"); } catch(IOException) { Check(true,"reject wrong map ID"); }
        try { Ratings.Validate(new byte[]{1,2,3},chart); Check(false,"reject non-map response"); } catch(IOException) { Check(true,"reject non-map response"); }
        var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        try { Ratings.Update(new Stage{Maps={new Chart{Id=1,Set=1,Difficulty="x"}}},cancelled.Token,m=>{}).GetAwaiter().GetResult(); Check(false,"cancelled"); } catch(OperationCanceledException) { Check(true,"cancel queued stage calculation"); }
        if(args.Contains("--live")) {
            
            var pool=Pool.Load("https://osucollector.com/tournaments/73/6-Digit-World-Cup").GetAwaiter().GetResult();
            var stage=pool.Stages[0];
            Ratings.Update(stage,CancellationToken.None,m=> { if(!m.RatingLoading) Console.WriteLine(m.Slot+" "+m.StarText+" "+m.RatingError); }).GetAwaiter().GetResult();
            Check(stage.Maps.All(m=>m.BaseStars.HasValue && m.ModStars.HasValue),"all 10 qualifier maps calculated from live .osu files");
            var dtMap=stage.Maps.First(m=>m.ModCode=="DT"); Check(dtMap.ModStars>dtMap.BaseStars,"live tournament DT slot raises stars");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using(var w=new Window()) {
                typeof(Window).GetField("pool",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(w,pool);
                ((Label)Field(w,"title")).Text=pool.Name;
                var stages=(ComboBox)Field(w,"stages"); stages.Items.Add(stage); stages.SelectedIndex=0;
                w.StartPosition=FormStartPosition.Manual; w.Location=new Point(-20000,-20000); w.ShowInTaskbar=false; w.Show(); Application.DoEvents();
                typeof(Window).GetMethod("Toggle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
                ((Label)Field(w,"status")).Text="Выбери карту: ниже появятся звёзды без модов и с модами.";
                var grid=(DataGridView)Field(w,"list"); grid.CurrentCell=grid.Rows[stage.Maps.IndexOf(dtMap)].Cells[0]; grid.Focus(); grid.FirstDisplayedScrollingRowIndex=Math.Max(0,stage.Maps.IndexOf(dtMap)-3);
                Check(((Label)Field(w,"basis")).Text.Contains(Chart.FormatStars(dtMap.ModStars)),"selected card shows computed DT metadata");
                using(var bmp=new Bitmap(w.Width,w.Height)) { w.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size)); bmp.Save(".test-cache/preview.png"); }
            }
        }
        Console.WriteLine("PASSED "+checks);
    }
}
