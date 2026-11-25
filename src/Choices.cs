using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

class RecentPool {
    public string Name="", Url="";
    public override string ToString() => Name+"   ·   "+Url;
    internal static string FileName => Path.Combine(AppPaths.Root,"recent-pools.json");
    internal static List<RecentPool> Read() {
        try { return AppPaths.Load<List<RecentPool>>(FileName); }
        catch(Exception e) { CrashLog.Write(e); return new List<RecentPool>(); }
    }
    internal static void Record(Pool pool) {
        var entries=Read();
        entries.RemoveAll(x=>x.Url==pool.Source);
        entries.Insert(0,new RecentPool{Name=pool.Name,Url=pool.Source});
        AppPaths.Save(FileName,entries.Take(100).ToList());
    }
}

static class CrashLog {
    internal static string Folder => Path.Combine(AppPaths.Root,"CrashLogs");
    internal static void Write(Exception error) {
        try {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder,DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N")+".log"),
                DateTimeOffset.Now+"  PoolDesk 1.1  "+Environment.OSVersion+Environment.NewLine+error);
        } catch { }
    }
}

static class Choice {
    internal static int Pick(Form owner,string title,string[] items,int current=0) {
        using var form=new DarkForm {Text=title,StartPosition=FormStartPosition.CenterParent,Size=new Size(610,400),Font=new Font("Segoe UI Variable Text",10),ForeColor=Window.Ink};
        var list=new ListBox {Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=Window.Surface,ForeColor=Window.Ink,IntegralHeight=false,HorizontalScrollbar=true,ItemHeight=32,DrawMode=DrawMode.OwnerDrawFixed};
        list.DrawItem+=(s,e)=> {
            if(e.Index<0) return;
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using var brush=new SolidBrush(selected?Color.FromArgb(67,62,98):list.BackColor);
            e.Graphics.FillRectangle(brush,e.Bounds);
            TextRenderer.DrawText(e.Graphics,items[e.Index],list.Font,Rectangle.Inflate(e.Bounds,-10,0),selected?Color.White:list.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        };
        list.Items.AddRange(items);
        if(items.Length>0) list.SelectedIndex=Math.Clamp(current,0,items.Length-1);
        var ok=new Button {Text=UiText.Pick("Выбрать","Select"),Dock=DockStyle.Bottom,Height=40,FlatStyle=FlatStyle.Flat,BackColor=Window.Pink,ForeColor=Window.Bg,DialogResult=DialogResult.OK};
        list.DoubleClick+=(s,e)=>form.DialogResult=DialogResult.OK;
        form.Body.Padding=new Padding(16); form.Body.Controls.Add(list); form.Body.Controls.Add(ok); form.AcceptButton=ok;
        ThemeManager.Apply(form,AppSettings.Current.Theme=="Light");
        return form.ShowDialog(owner)==DialogResult.OK?list.SelectedIndex:-1;
    }
}

class ReadableButton : Button {
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if(Enabled) return;
        using var brush=new SolidBrush(BackColor);
        var rect=Rectangle.Inflate(ClientRectangle,-2,-2);
        e.Graphics.FillRectangle(brush,rect);
        TextRenderer.DrawText(e.Graphics,Text,Font,rect,BackColor.GetBrightness()<0.5?Color.FromArgb(172,183,205):Color.FromArgb(58,63,78),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
}
