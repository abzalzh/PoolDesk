using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
static class ThemeManager {
    class Colors { public Color Back,Fore; }
    static readonly ConditionalWeakTable<Control,Colors> original=new ConditionalWeakTable<Control,Colors>();
    internal static void Apply(Control root,bool light) {
        var colors=original.GetValue(root,c=>new Colors{Back=c.BackColor,Fore=c.ForeColor});
        root.BackColor=light && colors.Back.GetBrightness()<0.3 ? Color.FromArgb(241,244,250) : colors.Back;
        root.ForeColor=light && colors.Fore.GetBrightness()>0.45 ? Color.FromArgb(30,40,62) : colors.Fore;
        if(root is DataGridView grid) {
            grid.BackgroundColor=light?Color.White:Window.Surface;
            grid.DefaultCellStyle.BackColor=light?Color.White:Window.Surface;
            grid.DefaultCellStyle.ForeColor=light?Color.FromArgb(30,40,62):Window.Ink;
            grid.AlternatingRowsDefaultCellStyle.BackColor=light?Color.FromArgb(235,239,247):Color.FromArgb(28,33,45);
            grid.ColumnHeadersDefaultCellStyle.BackColor=light?Color.FromArgb(223,230,242):Color.FromArgb(31,37,50);
            grid.ColumnHeadersDefaultCellStyle.ForeColor=light?Color.FromArgb(45,55,75):Window.Muted;
        }
        foreach(Control child in root.Controls) Apply(child,light);
        root.Invalidate();
    }
}
