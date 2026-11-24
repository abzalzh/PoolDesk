using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

class SmoothProgress : Control {
    int current, maximum = 100;
    public int Maximum { get { return maximum; } set { maximum = Math.Max(1,value); Invalidate(); } }
    public int Value { get { return current; } set { current = Math.Max(0,value); Invalidate(); } }
    public SmoothProgress() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw,true); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(Window.Line);
        int width = (int)(ClientSize.Width * Math.Min(1,(double)current / maximum));
        if(width > 0) using(var brush = new LinearGradientBrush(ClientRectangle,Window.Pink,Color.FromArgb(109,207,219),0f))
            e.Graphics.FillRectangle(brush,0,0,width,Height);
    }
}
