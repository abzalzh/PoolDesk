using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

class Window : DarkForm {
    internal static readonly Color Bg = Color.FromArgb(16,19,27), Surface = Color.FromArgb(25,30,41),
        Ink = Color.FromArgb(237,241,249), Muted = Color.FromArgb(151,163,185), Pink = Color.FromArgb(174,158,255),
        Line = Color.FromArgb(43,51,68), Warn = Color.FromArgb(244,188,111);
    static readonly string StateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PoolDesk");
    readonly TextBox link = new TextBox(), collection = new TextBox();
    readonly Label title = new Label(), status = new Label(), count = new Label(), detailTitle = new Label(), detailMeta = new Label();
    readonly LinkLabel detailLink = new LinkLabel(), notice = new LinkLabel();
    readonly ComboBox stages = new DarkComboBox();
    readonly DataGridView list = new DataGridView();
    readonly SmoothProgress bar = new SmoothProgress();
    readonly Button open = ButtonOf(L.T("Открыть пул"),true), download = ButtonOf(L.T("Скачать все карты"),false),
        add = ButtonOf(L.T("Добавить в osu!"),true), start = ButtonOf(L.T("Установить"),true);
    readonly CheckBox create = new CheckBox();
    readonly FlowLayoutPanel options = new FlowLayoutPanel(), stats = new FlowLayoutPanel();
    readonly Label basis = new Label();
    readonly Button recent=ButtonOf("",false), slot=ButtonOf("",false), stagePicker=ButtonOf("",false);
    readonly Button languageButton = new Button();
    readonly Label subtitle;
    Pool pool;
    bool busy;
    readonly Button toolsButton=new Button(), pauseButton=ButtonOf("Pause",false), cancelButton=ButtonOf("Cancel",false);
    CancellationTokenSource transferCancel;
    DownloadControl transferControl;

    CancellationTokenSource ratingCancel;
    static Button ButtonOf(string label,bool accent) {
        var b = new ReadableButton { Text = label, AutoSize = true, MinimumSize = new Size(110,40), FlatStyle = FlatStyle.Flat,
            Padding = new Padding(14,6,14,6), Margin = new Padding(0,0,10,0), BackColor = accent ? Pink : Surface,
            ForeColor = accent ? Bg : Ink, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        b.FlatAppearance.BorderSize = accent ? 0 : 1; b.FlatAppearance.BorderColor = Line;
        b.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(194,181,255) : Color.FromArgb(38,45,60);
        return b;
    }
    FlowLayoutPanel Row() { return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = Padding.Empty }; }
    void StyleInput(TextBox t) { t.BackColor = Surface; t.ForeColor = Ink; t.BorderStyle = BorderStyle.FixedSingle; t.Margin = new Padding(0,8,12,8); }
    static Label LabelOf(string text, Color color, float size) {
        return new Label { Text = text, ForeColor = color, AutoSize = true, Font = new Font("Segoe UI Variable Text",size), Margin = Padding.Empty };
    }
    public Window() {
        Text = "PoolDesk 1.1.2 · osu!stable"; AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(780,640); Size = new Size(1120,880);
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI Variable Text",10); BackColor = Bg; ForeColor = Ink;
        DoubleBuffered = true;
        Shown += delegate {
            var area=Screen.FromControl(this).WorkingArea;
            MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height));
            Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
        };
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28,22,28,20), ColumnCount = 1, RowCount = 10 };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Body.Controls.Add(page);
        for(int i=0;i<10;i++) page.RowStyles.Add(i == 5 ? new RowStyle(SizeType.Percent,100) : new RowStyle(SizeType.AutoSize));
        var brand = Row(); brand.Margin = new Padding(0,0,0,20);
        var mark = new PictureBox { Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(34,34), Margin = new Padding(0,0,10,0) };
        brand.Controls.Add(mark); var brandName = LabelOf("PoolDesk",Ink,17); brandName.Font = new Font("Segoe UI Semibold",17); brand.Controls.Add(brandName);
        subtitle = LabelOf(L.T("НЕОФИЦИАЛЬНЫЙ · OSU!STABLE"),Muted,9); subtitle.Margin = new Padding(16,10,0,0); brand.Controls.Add(subtitle); page.Controls.Add(brand,0,0);
        var source = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); source.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        StyleInput(link); link.Dock = DockStyle.Fill; link.AccessibleName = L.T("Ссылка на osu!Collector");
        link.PlaceholderText = "https://osucollector.com/tournaments/73/6-Digit-World-Cup";
        source.ColumnCount=3; source.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        source.Controls.Add(recent,2,0);
        recent.Click+=delegate {
            var entries=RecentPool.Read();
            if(entries.Count==0) { DarkForm.ShowMessage(this,UiText.Pick("Здесь появятся открытые пулы.","Opened pools will appear here.")); return; }
            int i=Choice.Pick(this,recent.Text,entries.Select(x=>x.ToString()).ToArray());
            if(i>=0) { link.Text=entries[i].Url; open.PerformClick(); }
        };
        source.Controls.Add(link,0,0); open.Margin = Padding.Empty; source.Controls.Add(open,1,0); page.Controls.Add(source,0,1);
        title.Text = L.T("Твой следующий маппул"); title.Font = new Font("Segoe UI Semibold",24);
        title.AutoSize = true; title.Margin = new Padding(0,20,0,16); title.MaximumSize = new Size(1000,0); page.Controls.Add(title,0,2);
        var toolbar = Row(); toolbar.Margin = new Padding(0,0,0,12);
        stages.DropDownStyle = ComboBoxStyle.DropDownList; stages.Width = 230; stages.FlatStyle = FlatStyle.Flat;
        stages.BackColor = Surface; stages.ForeColor = Ink; stages.AccessibleName = L.T("Стадия турнира");
        stages.DrawMode = DrawMode.OwnerDrawFixed;
        stages.DrawItem += delegate(object sender,DrawItemEventArgs e) {
            using(var brush = new SolidBrush(Surface)) e.Graphics.FillRectangle(brush,e.Bounds);
            if(e.Index >= 0) TextRenderer.DrawText(e.Graphics,stages.Items[e.Index].ToString(),Font,
                new Rectangle(e.Bounds.X+6,e.Bounds.Y,e.Bounds.Width-6,e.Bounds.Height),Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
        stages.Margin = new Padding(0,5,16,0); stages.Visible=false; toolbar.Controls.Add(stages); toolbar.Controls.Add(stagePicker);
        stagePicker.Click+=delegate {
            int i=Choice.Pick(this,L.T("Стадия турнира"),stages.Items.Cast<object>().Select(x=>x.ToString()).ToArray(),stages.SelectedIndex);
            if(i>=0) stages.SelectedIndex=i;
        };
        toolbar.Controls.Add(download); toolbar.Controls.Add(add); toolbar.Controls.Add(slot);
        slot.Click+=async delegate {
            if(pool == null) return;
            var slots = pool.SlotNames();
            if(slots.Length == 0) { DarkForm.ShowMessage(this,UiText.Pick("В этом пуле нет слотов.","This pool has no slots.")); return; }
            int i = Choice.Pick(this,UiText.Pick("Слот из всех стадий", "Slot from all stages"),slots);
            if(i < 0) return;
            string name = Choice.CollectionName(this,pool.Name + " · " + slots[i],pool.SelectSlot(slots[i]).Maps.Count);
            if(name != null) await Transfer(true,slots[i],name);
        };
        count.AutoSize = true; count.ForeColor = Muted; count.Margin = new Padding(4,10,0,0); toolbar.Controls.Add(count); page.Controls.Add(toolbar,0,3);
        notice.AutoSize = true; notice.LinkColor = Warn; notice.ActiveLinkColor = Ink; notice.VisitedLinkColor = Warn;
        notice.Margin = new Padding(0,0,0,12); notice.Visible = false;
        notice.LinkClicked += delegate { if(pool != null) ShowMissing(pool.Stages); }; page.Controls.Add(notice,0,4);
        SetupGrid(); var gridPanel=new Panel { Dock=DockStyle.Fill, Margin=Padding.Empty }; list.ScrollBars=ScrollBars.None; gridPanel.Controls.Add(list); gridPanel.Controls.Add(new GridScroll(list)); page.Controls.Add(gridPanel,0,5);
        var detail = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, BackColor = Surface,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, RowCount = 5,
            Padding = new Padding(18,14,18,14), Margin = new Padding(0,12,0,14) };
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<5;i++) detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailTitle.Text = L.T("Выбери карту в таблице"); detailTitle.Font = new Font("Segoe UI Semibold",12); detailTitle.AutoSize = true;
        detailTitle.Margin = new Padding(0,0,0,3); detail.Controls.Add(detailTitle);
        detailMeta.AutoSize = true; detailMeta.ForeColor = Muted; detailMeta.Margin = new Padding(0,0,0,10); detail.Controls.Add(detailMeta);
        stats.AutoSize = true; stats.WrapContents = false; stats.AutoSizeMode = AutoSizeMode.GrowAndShrink; stats.Dock = DockStyle.Fill; stats.Margin = Padding.Empty; detail.Controls.Add(stats);
        basis.Text = L.T("Параметры и старрейт из osu!Collector · без модов"); basis.ForeColor = Muted; basis.AutoSize = true; basis.Font = new Font("Segoe UI Variable Text",9);
        basis.Margin = new Padding(0,8,0,4); detail.Controls.Add(basis);
        detailLink.AutoSize = true; detailLink.LinkColor = Pink; detailLink.ActiveLinkColor = Ink; detailLink.VisitedLinkColor = Pink;
        detailLink.Margin = Padding.Empty; detail.Controls.Add(detailLink);
        detailLink.LinkClicked += delegate { if(detailLink.Tag != null) OpenUrl((string)detailLink.Tag); };
        detail.Resize += delegate { int width = Math.Max(100, detail.ClientSize.Width - detail.Padding.Horizontal); detailTitle.MaximumSize = detailMeta.MaximumSize = basis.MaximumSize = new Size(width,0); };
        page.Controls.Add(detail,0,6);
        options.AutoSize = true; options.AutoSizeMode = AutoSizeMode.GrowAndShrink; options.Dock = DockStyle.Fill; options.Visible = false; options.Margin = new Padding(0,0,0,12);
        create.Text = L.T("Создать коллекцию в osu!"); create.AutoSize = true; create.Checked = true;
        create.Margin = new Padding(0,10,16,0); options.Controls.Add(create);
        StyleInput(collection); collection.Width = 290; collection.AccessibleName = L.T("Название коллекции"); options.Controls.Add(collection); options.Controls.Add(start);
        page.Controls.Add(options,0,7);
        bar.Dock = DockStyle.Fill; bar.Height = 5; bar.Margin = new Padding(0,0,0,10); page.Controls.Add(bar,0,8);
        status.Text = L.T("Вставь ссылку на турнир или коллекцию и нажми «Открыть пул».");
        status.Dock = DockStyle.Fill; status.AutoSize = true; status.ForeColor = Muted; status.Margin = Padding.Empty; page.Controls.Add(status,0,9);
        page.Resize += delegate { title.MaximumSize = new Size(Math.Max(100,page.ClientSize.Width-page.Padding.Horizontal),0); };
        open.Click += async delegate { await Run(async delegate {
            if(ratingCancel != null) ratingCancel.Cancel();
            pool = null; stages.Items.Clear(); list.Rows.Clear(); count.Text = ""; title.Text = L.T("Загрузка пула…"); options.Visible = false;
            notice.Visible = false; ClearDetails(); bar.Value = 0;
            status.Text = L.T("Получаю карты с osu!Collector…");
            pool = await Pool.Load(link.Text); title.Text = pool.Name; try { RecentPool.Record(pool); } catch(Exception ex) { CrashLog.Write(ex); }
            foreach(var stage in pool.Stages) stages.Items.Add(stage);
            stages.SelectedIndex = 0; UpdateNotice(); status.Text = L.T("Выбери стадию. Нажми на карту, чтобы посмотреть параметры.");
        }); };
        stages.SelectedIndexChanged += delegate { stagePicker.Text=stages.SelectedItem?.ToString()??L.T("Стадия турнира"); ShowStage(); };
        add.Click += delegate { options.Visible = true; stages.Focus(); status.Text = L.T("Выбери стадию выше и проверь название коллекции."); };
        create.CheckedChanged += delegate { collection.Enabled = create.Checked; };
        download.Click += async delegate { await Transfer(false); };
        start.Click += async delegate { await Transfer(true); };
        link.KeyDown += delegate(object sender,KeyEventArgs e) { if(e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; open.PerformClick(); } };
        FormClosing += delegate(object sender,FormClosingEventArgs e) { if(busy) { e.Cancel = true; status.Text = L.T("Дождись завершения операции."); } };
        FormClosed += delegate { if(ratingCancel != null) ratingCancel.Cancel(); };
        languageButton.Size = new Size(100,36); languageButton.FlatStyle = FlatStyle.Flat;
        languageButton.FlatAppearance.BorderSize = 0; languageButton.BackColor = Color.FromArgb(20,24,34);
        languageButton.ForeColor = Pink; languageButton.Margin = new Padding(0,0,8,0); languageButton.Cursor = Cursors.Hand;
        languageButton.Click += delegate { L.English = !L.English; ApplyLanguage(); if(!L.Save()) status.Text = L.T("Не удалось сохранить язык. Выбор действует до закрытия программы."); };
        CaptionActions.Controls.Add(languageButton); CaptionActions.Controls.SetChildIndex(languageButton,0);
        toolsButton.Text="☰"; toolsButton.Size=new Size(46,36); toolsButton.FlatStyle=FlatStyle.Flat;
        toolsButton.FlatAppearance.BorderSize=0; toolsButton.BackColor=Color.FromArgb(20,24,34); toolsButton.ForeColor=Pink; toolsButton.Margin=Padding.Empty;
        CaptionActions.Controls.Add(toolsButton); CaptionActions.Controls.SetChildIndex(toolsButton,0);
        toolsButton.Click+=delegate { OpenTools(); };
        pauseButton.Visible=cancelButton.Visible=false; toolbar.Controls.Add(pauseButton); toolbar.Controls.Add(cancelButton);
        pauseButton.Click+=delegate {
            if(transferControl==null) return;
            if(transferControl.Paused) transferControl.Resume(); else transferControl.Pause();
            pauseButton.Text=transferControl.Paused?UiText.Pick("Продолжить","Resume"):UiText.Pick("Пауза","Pause");
        };
        cancelButton.Click+=delegate { transferCancel?.Cancel(); transferControl?.Resume(); };
        ApplyLanguage(); Toggle();
    }
    internal void ApplyLanguage() {
        SuspendLayout();
        languageButton.Text = L.English ? "English" : "Русский";
        recent.Text=UiText.Pick("Недавние","Recent pools"); slot.Text=UiText.Pick("Установить слот","Install slot"); stagePicker.Text=stages.SelectedItem?.ToString()??L.T("Стадия турнира");
        languageButton.AccessibleName = L.English ? "Switch to Russian" : "Переключить на английский";
        RefreshChromeLanguage();
        open.Text=L.T("Открыть пул"); download.Text=L.T("Скачать все карты"); add.Text=L.T("Добавить в osu!"); start.Text=L.T("Установить");
        create.Text=L.T("Создать коллекцию в osu!"); subtitle.Text=L.T("НЕОФИЦИАЛЬНЫЙ · OSU!STABLE");
        link.AccessibleName=L.T("Ссылка на osu!Collector"); stages.AccessibleName=L.T("Стадия турнира"); collection.AccessibleName=L.T("Название коллекции");
        string[] headers={"Слот","Исполнитель / трек","Сложность","★ с модами"};
        for(int i=0;i<headers.Length;i++) list.Columns[i].HeaderText=L.T(headers[i]);
        if(pool==null) title.Text=L.T("Твой следующий маппул");
        else UpdateNotice();
        var stage=stages.SelectedItem as Stage;
        if(stage!=null) count.Text=stage.Maps.Count + L.T(" карт");
        foreach(DataGridViewRow row in list.Rows) {
            var map=row.Tag as Chart; if(map==null) continue;
            row.Cells[3].Value=map.StarText; row.Cells[3].ToolTipText=map.RatingDetails;
            if(!map.Available) { row.Cells[1].Value=L.Translate(map.Song); row.Cells[2].Value=L.T("Карта не найдена"); }
        }
        stages.Invalidate(); ShowDetails();
        status.Text=pool==null ? L.T("Вставь ссылку на турнир или коллекцию и нажми «Открыть пул».") : L.T("Выбери стадию. Нажми на карту, чтобы посмотреть параметры.");
        ResumeLayout(true); ThemeManager.Apply(this,AppSettings.Current.Theme=="Light");
    }
    async void OpenTools() {
        if(busy) return;
        var items=new[]{UiText.Pick("Настройки","Settings"),UiText.Pick("История установок","Installation history"),UiText.Pick("Открыть логи","Open logs"),UiText.Pick("Скопировать диагностику","Copy diagnostics"),UiText.Pick("Проверить обновления","Check for updates"),UiText.Pick("Журнал сбоев","Crash logs")};
        try {
            switch(Choice.Pick(this,UiText.Pick("Меню","Menu"),items)) {
                case 0: using(var dialog=new SettingsDialog()) dialog.ShowDialog(this); ThemeManager.Apply(this,AppSettings.Current.Theme=="Light"); break;
                case 1: using(var dialog=new HistoryDialog()) dialog.ShowDialog(this); break;
                case 2: SupportTools.OpenLogs(); break;
                case 3: Clipboard.SetText(SupportTools.Diagnostics()); status.Text=UiText.Pick("Диагностика скопирована","Diagnostics copied"); break;
                case 4: var url=await SupportTools.CheckUpdate(); if(url!=null) OpenUrl(url); else DarkForm.ShowMessage(this,UiText.Pick("Установлена актуальная версия.","You are up to date.")); break;
                case 5: Directory.CreateDirectory(CrashLog.Folder); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(CrashLog.Folder){UseShellExecute=true}); break;
            }
        } catch(Exception ex) { CrashLog.Write(ex); DarkForm.ShowMessage(this,ex.Message); }
    }
    void SetupGrid() {
        list.Dock = DockStyle.Fill; list.Margin = Padding.Empty; list.BackgroundColor = Surface; list.BorderStyle = BorderStyle.None;
        list.ReadOnly = true; list.AllowUserToAddRows = false; list.AllowUserToDeleteRows = false; list.AllowUserToResizeRows = false;
        list.RowHeadersVisible = false; list.MultiSelect = false; list.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        list.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; list.EnableHeadersVisualStyles = false;
        list.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; list.GridColor = Line;
        list.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; list.ColumnHeadersHeight = 42; list.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        list.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(31,37,50), ForeColor = Muted,
            Font = new Font("Segoe UI Semibold",9), Padding = new Padding(12,0,0,0), SelectionBackColor = Surface };
        list.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface, ForeColor = Ink, SelectionBackColor = Color.FromArgb(53,48,78),
            SelectionForeColor = Ink, Padding = new Padding(12,0,6,0), Font = new Font("Segoe UI Variable Text",10) };
        list.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(28,33,45); list.RowTemplate.Height = 44;
        string[] names = {L.T("Слот"),L.T("Исполнитель / трек"),L.T("Сложность"),L.T("★ с модами")}; int[] weights = {10,43,27,20};
        for(int i=0;i<names.Length;i++) list.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = names[i], FillWeight = weights[i],
            MinimumWidth = i == 1 ? 180 : i == 3 ? 140 : 85, SortMode = DataGridViewColumnSortMode.NotSortable });
        list.Columns[3].DefaultCellStyle.ForeColor = Pink;
        list.CurrentCellChanged += delegate { ShowDetails(); };
    }
    void ClearDetails() {
        detailTitle.Text = L.T("Выбери карту в таблице"); detailTitle.ForeColor = Ink; detailMeta.Text = L.T("CS, AR, OD, HP, BPM и длительность появятся здесь.");
        ClearStats(); detailLink.Text = ""; detailLink.Tag = null; basis.Visible = false;
    }
    void ClearStats() { while(stats.Controls.Count > 0) stats.Controls[0].Dispose(); }
    void Metric(string label, string value) {
        var card = new Panel { Size = new Size(96,60), BackColor = Bg, Margin = new Padding(0,0,8,0) };
        var key = LabelOf(label,Muted,8); key.Location = new Point(10,6); card.Controls.Add(key);
        var val = LabelOf(value,Ink,13); val.Font = new Font("Segoe UI Semibold",13); val.Location = new Point(10,22); card.Controls.Add(val); stats.Controls.Add(card);
    }
    void ShowDetails() {
        var map = list.CurrentRow == null ? null : list.CurrentRow.Tag as Chart;
        if(map == null) { ClearDetails(); return; }
        detailTitle.Text = (String.IsNullOrEmpty(map.Slot) ? "" : map.Slot + "  ·  ") + (map.Available ? map.Song : L.Translate(map.Song));
        detailTitle.ForeColor = map.Available ? Ink : Warn;
        detailMeta.Text = map.Available ? map.Difficulty + "  /  " + (String.IsNullOrEmpty(map.Creator) ? L.T("автор неизвестен") : map.Creator) +
            "  /  " + map.Mode + "  /  " + map.Status : L.T("osu!Collector пока не определил эту карту. Остальные карты будут установлены.");
        ClearStats(); basis.Visible = map.Available; basis.Text = map.RatingDetails;
        if(map.Available) {
            Metric("CS",Chart.Display(map.CS)); Metric("AR",Chart.Display(map.AR)); Metric("OD",Chart.Display(map.OD));
            Metric("HP",Chart.Display(map.HP)); Metric("BPM",Chart.Display(map.BPM));
            Metric(L.T("ВРЕМЯ"),map.Length.HasValue ? ((int)map.Length.Value/60) + ":" + ((int)map.Length.Value%60).ToString("00") : "—");
        }
        detailLink.Tag = map.Id > 0 ? map.Url : null;
        detailLink.Text = map.Id > 0 ? map.Url : L.T("Ссылка недоступна: источник не передал ID карты.");
        ThemeManager.Apply(this,AppSettings.Current.Theme=="Light");
    }
    async void ShowStage() {
        var stage = stages.SelectedItem as Stage; if(stage == null) return;
        list.Rows.Clear();
        foreach(var map in stage.Maps) {
            int index = list.Rows.Add(map.Slot,map.Available ? map.Song : L.Translate(map.Song),map.Available ? map.Difficulty : L.T("Карта не найдена"),map.StarText);
            list.Rows[index].Tag = map;
            list.Rows[index].Cells[0].Style.ForeColor = map.ModCode=="HR" ? Color.FromArgb(247,153,167) : map.ModCode=="HD" ? Color.FromArgb(230,202,125) : map.ModCode=="DT" || map.ModCode=="NC" ? Pink : Color.FromArgb(120,211,205);
            if(!map.Available) { list.Rows[index].DefaultCellStyle.ForeColor = Warn; list.Rows[index].Cells[1].ToolTipText = map.Url; }
        }
        list.ClearSelection(); list.CurrentCell = null; ClearDetails();
        count.Text = stage.Maps.Count + L.T(" карт");
        collection.Text = pool.Name + (pool.Tournament ? " — " + stage.Name : "");
        if(ratingCancel != null) ratingCancel.Cancel();
        var request = new CancellationTokenSource(); ratingCancel = request;
        try {
            await Ratings.Update(stage,request.Token,delegate(Chart map) {
                if(IsDisposed || request.IsCancellationRequested || stages.SelectedItem != stage) return;
                foreach(DataGridViewRow row in list.Rows) if(row.Tag == map) {
                    row.Cells[3].Value = map.StarText;
                    row.Cells[3].ToolTipText = map.RatingDetails;
                }
                if(list.CurrentRow != null && list.CurrentRow.Tag == map) ShowDetails();
            });
        } catch(OperationCanceledException) { }
        catch(Exception e) { if(!IsDisposed && !request.IsCancellationRequested) status.Text = L.T("Расчёт звёзд: ") + e.Message; }
        finally { if(ratingCancel == request) ratingCancel = null; request.Dispose(); }

    }
    void UpdateNotice() {
        int missing = pool.Stages.Sum(s=>s.Maps.Count(m=>!m.Available));
        notice.Text = L.T("Не удалось определить карты: ") + missing + L.T("  ·  Показать слоты и ссылки"); notice.Visible = missing > 0;
    }
    void ShowMissing(IEnumerable<Stage> selected) {
        using(var dialog = new DarkForm { Text = L.T("Неопределённые карты"), Size = new Size(760,440), MinimumSize = new Size(600,300),
            StartPosition = FormStartPosition.CenterParent, BackColor = Bg, ForeColor = Ink, Font = Font, Icon = Icon }) {
            var text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = true, BorderStyle = BorderStyle.None,
                BackColor = Surface, ForeColor = Ink, Font = Font };
            text.Text = L.T("osu!Collector пока не определил следующие карты.\nПроверь ссылки или повтори загрузку пула позже.\n\n") +
                String.Join("\n\n",selected.SelectMany(s=>s.Maps.Where(m=>!m.Available).Select(m=>s.Name + " / " +
                    (String.IsNullOrEmpty(m.Slot) ? L.T("Без слота") : m.Slot) + L.T(" — карта не найдена\n") + m.Url)));
            text.LinkClicked += delegate(object sender,LinkClickedEventArgs e) { OpenUrl(e.LinkText); };
            dialog.Body.Padding = new Padding(20); dialog.Body.Controls.Add(text); dialog.ShowDialog(this);
        }
    }
    void OpenUrl(string url) {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch(Exception e) { status.Text = L.T("Не удалось открыть браузер: ") + e.Message; }
    }
    void Toggle() {
        recent.Enabled = open.Enabled = link.Enabled = languageButton.Enabled = !busy;
        slot.Enabled = stagePicker.Enabled = stages.Enabled = download.Enabled = add.Enabled = options.Enabled = !busy && pool != null;
    }
    async Task Run(Func<Task> action) {
        if(busy) return; busy = true; Toggle();
        try { await action(); }
        catch(OperationCanceledException) { status.Text=UiText.Pick("Операция отменена.","Operation cancelled."); }
        catch(Exception e) {
            if(pool == null) title.Text = L.T("Твой следующий маппул");
            status.Text = L.T("Операция прервана. ") + e.Message.Split('\n')[0];
            NoticeSound.Play(true); DarkForm.ShowMessage(this,e.Message);
        } finally { busy = false; Toggle(); }
    }
    async Task Transfer(bool install,string selectedSlot=null,string selectedCollectionName=null) {
        await Run(async delegate {
            var stage=selectedSlot == null ? stages.SelectedItem as Stage : pool?.SelectSlot(selectedSlot); if(stage==null) return;
            var settings=AppSettings.Current;
            string name=collection.Text.Trim(); bool merge=install&&(create.Checked||selectedSlot!=null);
            if(selectedSlot!=null) name=(selectedCollectionName ?? "").Trim();
            if(merge&&name.Length==0) throw new IOException(L.T("Введи название коллекции."));
            var maps=install?stage.Maps:pool.Stages.SelectMany(x=>x.Maps).ToList();
            string root="",destination="";
            if(install) {
                Stable.Closed();
                var found=OsuLocator.Find(settings.OsuFolder);
                if(OsuLocator.Valid(settings.OsuFolder)) root=settings.OsuFolder;
                else if(found.Count==1) root=found[0];
                else using(var picker=new FolderBrowserDialog{Description=L.T("Выбери папку osu!stable с файлом osu!.exe"),SelectedPath=found.FirstOrDefault()??""}) {
                    if(picker.ShowDialog(this)!=DialogResult.OK) return; root=picker.SelectedPath;
                }
                Stable.Songs(root); settings.OsuFolder=root; settings.Save(); destination=AppPaths.Cache;
            } else using(var picker=new FolderBrowserDialog{Description=L.T("Куда скачать все карты?")}) {
                if(picker.ShowDialog(this)!=DialogResult.OK) return; destination=picker.SelectedPath;
            }
            transferCancel=new CancellationTokenSource(); transferControl=new DownloadControl();
            cancelButton.Text=UiText.Pick("Отмена","Cancel"); pauseButton.Text=UiText.Pick("Пауза","Pause");
            cancelButton.Visible=true;
            try {
                var token=transferCancel.Token; LocalInventory inventory=new LocalInventory();
                if(install) {
                    status.Text=UiText.Pick("Проверяю установленные карты…","Scanning installed maps…");
                    inventory=await Task.Run(()=>LocalInventory.Scan(Stable.Songs(root),token,_=>{}),token);
                    var different=maps.Where(m=>m.Available&&inventory.Different(m)).ToList();
                    if(different.Count>0) {
                        string message=UiText.Pick("Установлены другие версии карт: ","Different map versions installed: ")+different.Count+"\n"+
                            string.Join("\n",different.Take(12).Select(m=>m.Slot+" · "+m.Song))+"\n\n"+
                            UiText.Pick("Попробовать скачать версии с MD5 из турнира? Старые файлы останутся. Недоступные точные версии будут пропущены.","Try downloading the tournament MD5 versions? Existing files will be preserved. Unavailable exact versions will be skipped.");
                        if(MessageBox.Show(this,message,"PoolDesk",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
                    }
                }
                pauseButton.Visible=true;
                long last=0; int lastDone=-1;
                var progress=new Progress<TransferProgress>(p=> {
                    if(IsDisposed) return;
                    long now=Environment.TickCount64; if(now-last<120&&p.Done==lastDone) return; last=now; lastDone=p.Done;
                    bar.Maximum=Math.Max(1,p.Total); bar.Value=p.Done;
                    status.Text=p.Done+"/"+p.Total+" · "+(p.Bytes/1048576.0).ToString("0.0")+" MiB · "+p.Message;
                });
                var receipt=await new InstallEngine(settings).Run(pool,stage,maps,root,destination,merge?name:"",inventory,transferControl,token,progress);
                status.Text=UiText.Pick("Готово: ","Completed: ")+receipt.Completed+UiText.Pick(" · уже установлено: "," · already installed: ")+receipt.Skipped+
                    UiText.Pick(" · ошибок: "," · errors: ")+receipt.Failed+" · "+UiText.State(receipt.State);
                NoticeSound.Play(receipt.Failed>0||receipt.State!="Completed");
                DarkForm.ShowMessage(this,status.Text+"\n"+string.Join("\n\n",receipt.Errors.Take(20))+"\n"+UiText.Pick("Журнал: ","Log: ")+receipt.Log+"\n"+
                    UiText.Pick("История и удаление пула доступны в меню ☰. После установки открой выбор песни; при необходимости нажми F5.","History and pool removal are in the ☰ menu. After installation open song selection; press F5 if needed."));
                try { AppPaths.CleanCache((long)settings.CacheLimitGB*1024*1024*1024); } catch(Exception ex) { status.Text+=" · "+ex.Message; }
            } finally {
                pauseButton.Visible=cancelButton.Visible=false; transferControl?.Resume(); transferCancel.Dispose(); transferCancel=null; transferControl=null;
            }
        });
    }
    [STAThread] static void Main() {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(s,e)=>{ CrashLog.Write(e.Exception); DarkForm.ShowMessage(null,e.Exception.Message); };
        AppDomain.CurrentDomain.UnhandledException+=(s,e)=>CrashLog.Write(e.ExceptionObject as Exception??new Exception(e.ExceptionObject?.ToString()));
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        AppSettings.Load();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        L.Load();
        bool first;
        using(var mutex = new Mutex(true,@"Local\PoolDeskOsuStable",out first)) {
            if(!first) { DarkForm.ShowMessage(null,L.T("Pool Desk уже открыт.")); return; }
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.Run(new Window());
        }
    }
}
