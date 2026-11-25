using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class DarkForm : Form {
    internal readonly Panel Body = new Panel { Dock = DockStyle.Fill };
    internal readonly FlowLayoutPanel CaptionActions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
    readonly Label caption = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10,0,0,0) };
    readonly CaptionButton minimize = new CaptionButton(0), maximize = new CaptionButton(1), close = new CaptionButton(2);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    public DarkForm() {
        FormBorderStyle = FormBorderStyle.None; Padding = new Padding(4); BackColor = Color.FromArgb(16,19,27);
        DoubleBuffered = true;
        var bar = new Panel { Height = 36, Dock = DockStyle.Top, BackColor = Color.FromArgb(20,24,34) };
        caption.ForeColor = Color.FromArgb(159,172,196); caption.Font = new Font("Segoe UI",9);
        bar.Controls.Add(caption); bar.Controls.Add(CaptionActions);
        CaptionActions.Controls.Add(minimize); CaptionActions.Controls.Add(maximize); CaptionActions.Controls.Add(close);
        Controls.Add(Body); Controls.Add(bar);
        caption.MouseDown += BeginDrag; bar.MouseDown += BeginDrag;
        caption.DoubleClick += delegate { ToggleMaximize(); }; bar.DoubleClick += delegate { ToggleMaximize(); };
        minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
        maximize.Click += delegate { ToggleMaximize(); }; close.Click += delegate { Close(); };
        TextChanged += delegate { caption.Text = Text; };
        RefreshChromeLanguage();
    }
    internal void RefreshChromeLanguage() {
        minimize.AccessibleName = L.T("Свернуть"); maximize.AccessibleName = L.T("Развернуть / восстановить"); close.AccessibleName = L.T("Закрыть");
    }
    internal void ToggleMaximize() {
        if(!MaximizeBox) return;
        MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }
    void BeginDrag(object sender,MouseEventArgs e) {
        if(e.Button != MouseButtons.Left) return;
        ReleaseCapture(); SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);
    }
    protected override CreateParams CreateParams {
        get { var cp=base.CreateParams; cp.Style = (cp.Style & ~0x00C40000) | 0x80000 | 0x20000 | 0x10000; return cp; }
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        int dark=1,round=2,border=0x00534037;
        DwmSetWindowAttribute(Handle,20,ref dark,4);
        DwmSetWindowAttribute(Handle,33,ref round,4);
        DwmSetWindowAttribute(Handle,34,ref border,4);
    }
    internal static int ResizeHit(Point p,Size size,int grip) {
        bool left=p.X<grip, right=p.X>=size.Width-grip, top=p.Y<grip, bottom=p.Y>=size.Height-grip;
        if(top) return left ? 13 : right ? 14 : 12;
        if(bottom) return left ? 16 : right ? 17 : 15;
        return left ? 10 : right ? 11 : 1;
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x83) { m.Result=IntPtr.Zero; return; }
        if(m.Msg==0x85) { m.Result=IntPtr.Zero; return; }
        if(m.Msg==0x86) { m.Result=(IntPtr)1; return; }
        base.WndProc(ref m);
        if(m.Msg==0x84 && WindowState==FormWindowState.Normal) {
            long xy=m.LParam.ToInt64(); var point=PointToClient(new Point((short)(xy&0xffff),(short)((xy>>16)&0xffff)));
            int hit=ResizeHit(point,ClientSize,Math.Max(6,DeviceDpi*6/96));
            if(hit!=1) m.Result=(IntPtr)hit;
        }
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        using(var pen=new Pen(Color.FromArgb(55,64,83))) e.Graphics.DrawRectangle(pen,0,0,ClientSize.Width-1,ClientSize.Height-1);
    }
    internal static void ShowMessage(IWin32Window owner,string message) {
        using(var dialog=new DarkForm { Text="PoolDesk", Size=new Size(680,360), MinimumSize=new Size(540,280),
            StartPosition=FormStartPosition.CenterParent, Font=new Font("Segoe UI",10) }) {
            var text=new RichTextBox { Dock=DockStyle.Fill, ReadOnly=true, DetectUrls=true, BorderStyle=BorderStyle.None,
                BackColor=Color.FromArgb(25,30,41), ForeColor=Color.FromArgb(237,241,249), Font=dialog.Font, Text=L.Translate(message) };
            var ok=new Button { Text=L.T("Хорошо"), Dock=DockStyle.Bottom, Height=42, BackColor=Color.FromArgb(174,158,255), ForeColor=Color.FromArgb(16,19,27), FlatStyle=FlatStyle.Flat };
            ok.FlatAppearance.BorderSize=0; ok.Click+=delegate { dialog.Close(); };
            dialog.Body.Padding=new Padding(16); dialog.Body.Controls.Add(text); dialog.Body.Controls.Add(ok); dialog.AcceptButton=ok; dialog.CancelButton=ok;
            dialog.ShowDialog(owner);
        }
    }
}
class CaptionButton : Button {
    readonly int kind;
    internal CaptionButton(int kind) {
        this.kind=kind; Width=46; Height=36; Margin=Padding.Empty; FlatStyle=FlatStyle.Flat;
        FlatAppearance.BorderSize=0; BackColor=Color.FromArgb(20,24,34); ForeColor=Color.FromArgb(215,224,240);
        FlatAppearance.MouseOverBackColor=kind==2 ? Color.FromArgb(184,54,72) : Color.FromArgb(45,53,70);
        TabStop=true;
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        float scale=DeviceDpi/96f; float x=Width/2f,y=Height/2f,r=5*scale;
        using(var pen=new Pen(ForeColor,Math.Max(1,scale))) {
            if(kind==0) e.Graphics.DrawLine(pen,x-r,y+3*scale,x+r,y+3*scale);
            if(kind==1) e.Graphics.DrawRectangle(pen,x-r,y-r,2*r,2*r);
            if(kind==2) { e.Graphics.DrawLine(pen,x-r,y-r,x+r,y+r); e.Graphics.DrawLine(pen,x+r,y-r,x-r,y+r); }
        }
    }
}
