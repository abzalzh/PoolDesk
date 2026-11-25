using System;
using System.Drawing;
using System.Windows.Forms;

class DarkComboBox : ComboBox {
    protected override void WndProc(ref Message m) {
        base.WndProc(ref m);
        if(m.Msg!=0xF) return;
        using(var g=Graphics.FromHwnd(Handle)) {
            var arrow=new Rectangle(Width-25,1,24,Height-2);
            using(var fill=new SolidBrush(BackColor)) g.FillRectangle(fill,arrow);
            using(var line=new Pen(ForeColor,1.5f)) {
                int x=Width-14,y=Height/2;
                g.DrawLines(line,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});
            }
            using(var border=new Pen(Window.Line,2)) g.DrawRectangle(border,1,1,Width-2,Height-2);
        }
    }
}

class GridScroll : Control {
    readonly DataGridView grid;
    bool dragging; int dragY,dragRow;
    int VisibleRows => Math.Max(1,(grid.ClientSize.Height-grid.ColumnHeadersHeight)/Math.Max(1,grid.RowTemplate.Height));
    int MaxRow => Math.Max(0,grid.Rows.Count-VisibleRows);
    int Current => Math.Max(0,grid.FirstDisplayedScrollingRowIndex);
    Rectangle Thumb {
        get { int height=Math.Min(Height,Math.Max(32,Height*VisibleRows/Math.Max(1,grid.Rows.Count)));
            return new Rectangle(4,MaxRow==0?0:(Height-height)*Current/MaxRow,4,height); }
    }
    internal GridScroll(DataGridView grid) {
        this.grid=grid; Width=12; Dock=DockStyle.Right; BackColor=Window.Surface; DoubleBuffered=true;
        grid.Scroll+=(s,e)=>Invalidate(); grid.RowsAdded+=(s,e)=>Invalidate(); grid.RowsRemoved+=(s,e)=>Invalidate(); grid.Resize+=(s,e)=>Invalidate();
        grid.MouseWheel+=(s,e)=>MoveTo(Current-Math.Sign(e.Delta)*3);
    }
    void MoveTo(int row) { if(grid.Rows.Count>0) grid.FirstDisplayedScrollingRowIndex=Math.Clamp(row,0,MaxRow); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); if(MaxRow>0) using(var b=new SolidBrush(Color.FromArgb(91,103,128))) e.Graphics.FillRectangle(b,Thumb); }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e); if(e.Button!=MouseButtons.Left) return;
        if(!Thumb.Contains(e.Location)) MoveTo(Current+(e.Y<Thumb.Top?-VisibleRows:VisibleRows));
        dragging=true; dragY=e.Y; dragRow=Current; Capture=true;
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(dragging) MoveTo(dragRow+(e.Y-dragY)*MaxRow/Math.Max(1,Height-Thumb.Height)); }
    protected override void OnMouseUp(MouseEventArgs e) { dragging=false; Capture=false; base.OnMouseUp(e); }
}
