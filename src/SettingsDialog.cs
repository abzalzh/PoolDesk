using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

static class UiText {
    internal static string Pick(string ru,string en) { return L.English?en:ru; }
    internal static string State(string state) { if(L.English) return state; return state switch { "Completed"=>"Завершено", "Partial"=>"Частично", "Cancelled"=>"Отменено", "Interrupted"=>"Прервано", "Removed"=>"Удалено", "Running"=>"В процессе", _=>state }; }
}
class SettingsDialog : DarkForm {
    internal SettingsDialog() {
        Text=UiText.Pick("Настройки · PoolDesk","Settings · PoolDesk"); Size=new Size(780,710); MinimumSize=new Size(740,670); StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Segoe UI",10); ForeColor=Window.Ink;
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,AutoScroll=true,Padding=new Padding(22)};
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52)); Body.Controls.Add(panel);
        int row=0;
        Action<string,Control> add=(label,control)=> { panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(0,10,10,10)},0,row); control.Margin=new Padding(0,6,0,6); control.Dock=DockStyle.Fill; panel.Controls.Add(control,1,row++); };
        var settings=AppSettings.Current;
        var path=new TextBox{Text=settings.OsuFolder,BackColor=Window.Surface,ForeColor=Window.Ink}; add(UiText.Pick("Папка osu!stable","osu!stable folder"),path);
        var choose=new Button{Text=UiText.Pick("Выбрать / определить","Browse / detect"),Height=32}; add("",choose);
        choose.Click+=delegate {
            var found=OsuLocator.Find(path.Text); using(var picker=new FolderBrowserDialog{SelectedPath=found.FirstOrDefault()??path.Text,Description=UiText.Pick("Выбери папку с osu!.exe","Select the folder containing osu!.exe")}) if(picker.ShowDialog(this)==DialogResult.OK) path.Text=picker.SelectedPath;
        };
        Func<int,int,int,NumericUpDown> number=(value,min,max)=>new NumericUpDown{Minimum=min,Maximum=max,Value=value,BackColor=Window.Surface,ForeColor=Window.Ink};
        var threads=number(settings.ParallelDownloads,1,6); add(UiText.Pick("Параллельные загрузки (по умолчанию 3)","Parallel downloads (default 3)"),threads);
        var attempts=number(settings.Attempts,1,5); add(UiText.Pick("Попыток на зеркало (3)","Attempts per mirror (3)"),attempts);
        var backups=number(settings.BackupCount,3,50); add(UiText.Pick("Сохранять резервных копий (10)","Keep collection backups (10)"),backups);
        var cache=number(settings.CacheLimitGB,1,50); add(UiText.Pick("Лимит кеша, ГиБ (5)","Cache limit, GiB (5)"),cache);
        var size=new Button{Text=UiText.Pick("Очистить кеш · ","Clear cache · ")+(AppPaths.CacheSize()/1048576.0).ToString("0.0")+" MiB",Height=32}; add("",size);
        size.Click+=delegate { try { AppPaths.CleanCache(0); size.Text=UiText.Pick("Кеш очищен","Cache cleared"); } catch(Exception ex) { DarkForm.ShowMessage(this,ex.Message); } };
        var mirrors=new ListBox{Height=75,BackColor=Window.Surface,ForeColor=Window.Ink}; mirrors.Items.AddRange(settings.Mirrors.Cast<object>().ToArray()); add(UiText.Pick("Приоритет зеркал","Mirror priority"),mirrors);
        var up=new Button{Text=UiText.Pick("Поднять выбранное зеркало","Move selected mirror up"),Height=30}; add("",up);
        up.Click+=delegate { int index=mirrors.SelectedIndex; if(index>0) { object item=mirrors.Items[index]; mirrors.Items.RemoveAt(index); mirrors.Items.Insert(index-1,item); mirrors.SelectedIndex=index-1; } };
        var theme=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList}; theme.Items.AddRange(new object[]{"Dark","Light"}); theme.SelectedItem=settings.Theme; add(UiText.Pick("Тема","Theme"),theme);
        var sound=new CheckBox{Text=UiText.Pick("Звуки завершения и ошибок","Completion and error sounds"),Checked=settings.Sounds,AutoSize=true}; add("",sound);
        var save=new Button{Text=UiText.Pick("Сохранить","Save"),Height=40,BackColor=Window.Pink,ForeColor=Window.Bg,FlatStyle=FlatStyle.Flat}; add("",save);
        save.Click+=delegate {
            try {
                if(path.Text.Trim().Length>0&&!OsuLocator.Valid(path.Text.Trim())) throw new IOException(UiText.Pick("В выбранной папке нет osu!.exe.","The selected folder does not contain osu!.exe."));
                settings.OsuFolder=path.Text.Trim(); settings.ParallelDownloads=(int)threads.Value; settings.Attempts=(int)attempts.Value;
                settings.BackupCount=(int)backups.Value; settings.CacheLimitGB=(int)cache.Value; settings.Sounds=sound.Checked; settings.Theme=(string)theme.SelectedItem;
                settings.Mirrors=mirrors.Items.Cast<string>().ToList(); settings.Save(); DialogResult=DialogResult.OK; Close();
            } catch(Exception ex) { DarkForm.ShowMessage(this,ex.Message); }
        };
        ThemeManager.Apply(this,settings.Theme=="Light");
    }
}
class HistoryDialog : DarkForm {
    internal HistoryDialog() {
        Text=UiText.Pick("История установок","Installation history"); Font=new Font("Segoe UI",10); Size=new Size(940,550); MinimumSize=new Size(760,420); StartPosition=FormStartPosition.CenterParent;
        var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(20)};
        panel.RowStyles.Add(new RowStyle(SizeType.Percent,100)); for(int i=1;i<4;i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Body.Controls.Add(panel);
        var list=new ListBox{Dock=DockStyle.Fill,BackColor=Window.Surface,ForeColor=Window.Ink,HorizontalScrollbar=true}; panel.Controls.Add(list);
        var note=new Label{AutoSize=true,ForeColor=Window.Muted,Text=UiText.Pick("Удаляются только добавленные этой установкой записи. Изменённые и общие карты сохраняются.","Only entries added by this installation are removed. Changed and shared maps are kept."),Margin=new Padding(0,12,0,8)}; panel.Controls.Add(note);
        var maps=new CheckBox{Text=UiText.Pick("Также удалить неизменённые карты, созданные PoolDesk","Also delete unchanged maps created by PoolDesk"),AutoSize=true,ForeColor=Window.Ink}; panel.Controls.Add(maps);
        var remove=new Button{Text=UiText.Pick("Удалить этот пул","Remove this pool"),Height=40,Dock=DockStyle.Fill,BackColor=Window.Pink,FlatStyle=FlatStyle.Flat}; panel.Controls.Add(remove);
        Action reload=()=> { list.Items.Clear(); list.Items.AddRange(InstallHistory.ReadAll().Where(x=>!string.IsNullOrEmpty(x.Root)).Cast<object>().ToArray()); }; reload();
        remove.Click+=async delegate {
            var receipt=list.SelectedItem as InstallReceipt; if(receipt==null) return;
            try {
                Stable.Closed();
                if(MessageBox.Show(this,UiText.Pick("Удалить добавления этой установки?","Remove this installation's additions?"),"PoolDesk",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
                remove.Enabled=false; bool delete=maps.Checked;
                var warnings=await Task.Run(()=>InstallHistory.Undo(receipt,delete,AppSettings.Current.BackupCount));
                DarkForm.ShowMessage(this,UiText.Pick("Записи пула удалены.","Pool entries removed.")+"\n"+string.Join("\n",warnings)); reload();
            } catch(Exception ex) { DarkForm.ShowMessage(this,ex.Message); }
            finally { remove.Enabled=true; }
        };
        ThemeManager.Apply(this,AppSettings.Current.Theme=="Light");
    }
}
static class SupportTools {
    internal static void OpenLogs() { Directory.CreateDirectory(AppPaths.Logs); Process.Start(new ProcessStartInfo(AppPaths.Logs){UseShellExecute=true}); }
    internal static string Diagnostics() {
        return "PoolDesk 1.0.0\nOS: "+Environment.OSVersion+"\n.NET: "+Environment.Version+"\nArchitecture: "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture+
            "\nLanguage: "+(L.English?"en":"ru")+"\nThreads: "+AppSettings.Current.ParallelDownloads+"\nAttempts: "+AppSettings.Current.Attempts+
            "\nMirrors: "+string.Join(", ",AppSettings.Current.Mirrors)+"\nCache MiB: "+(AppPaths.CacheSize()/1048576.0).ToString("0.0")+"\nosu folder valid: "+OsuLocator.Valid(AppSettings.Current.OsuFolder);
    }
    internal static async Task<string> CheckUpdate() {
        using(var http=new HttpClient{Timeout=TimeSpan.FromSeconds(20)}) {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PoolDesk/1.0");
            using(var doc=JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/abzalzh/PoolDesk/releases?per_page=30"))) {
                foreach(var release in doc.RootElement.EnumerateArray()) {
                    if(release.GetProperty("draft").GetBoolean()||release.GetProperty("prerelease").GetBoolean()) continue;
                    string tag=release.GetProperty("tag_name").GetString(); Version version;
                    if(!tag.StartsWith("v1.",StringComparison.OrdinalIgnoreCase)||!Version.TryParse(tag.TrimStart('v','V'),out version)||version<=new Version(1,0,0)) continue;
                    return "https://github.com/abzalzh/PoolDesk/releases/tag/"+Uri.EscapeDataString(tag);
                }
            }
        }
        return null;
    }
}
