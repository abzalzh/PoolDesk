using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows.Forms;

class Chart {
    public int Id, Set;
    public string Slot, Song, Difficulty;
    public double? Stars, CS, AR, OD, HP, BPM, Length;
    public string Creator, Mode, Status, Checksum;
    public double? BaseStars, ModStars;
    public string RatingError;
    public bool RatingLoading;
    public string ModCode { get { return Ratings.ModsForSlot(Slot); } }
    public bool FreeMods { get { return System.Text.RegularExpressions.Regex.IsMatch(Slot ?? "", @"^(FM|TB)\d*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase); } }
    public static string FormatStars(double? value) { return value.HasValue ? value.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ★" : "—"; }
    public string RatingDetails { get {
        return L.T("Без модов: ") + FormatStars(BaseStars) + "     |     " + (ModCode ?? L.T("Неизвестные моды")) + ": " + FormatStars(ModStars) +
            (FreeMods ? L.T(" (FM/TB: набор модов не задан)") : "") + "\n" +
            (RatingLoading ? L.T("Рассчитываю по файлу карты…") : !String.IsNullOrEmpty(RatingError) ? L.Translate(RatingError) : L.T("rosu-pp 4.0.1 · stable · остальные параметры — без модов"));
    } }
    public bool Available { get { return Id > 0 && Set > 0 && !String.IsNullOrWhiteSpace(Difficulty); } }
    public string Url { get { return Id > 0 ? "https://osu.ppy.sh/beatmaps/" + Id : L.T("Источник не передал ID карты — ссылка неизвестна."); } }
    public static string Display(double? value) { return value.HasValue ? value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "—"; }
    public string StarText { get { return ModStars.HasValue ? FormatStars(ModStars) + " " + ModCode + (FreeMods ? "*" : "") : RatingLoading ? L.T("Расчёт…") : "—"; } }
}
class Stage {
    public string Name;
    public List<Chart> Maps = new List<Chart>();
    public override string ToString() { return Name == "Все карты" || Name == "All maps" ? L.T("Все карты") : Name; }
}
class Pool {
    public string Name;
    public string Source="";
    public bool Tournament;
    public List<Stage> Stages = new List<Stage>();
    static HttpClient http = new HttpClient { Timeout = TimeSpan.FromMinutes(4) };
    static Pool() { http.DefaultRequestHeaders.UserAgent.ParseAdd("PoolDesk/1.0"); }
    static async Task<Dictionary<string,object>> Read(string url) {
        string json = await http.GetStringAsync(url);
        using(var document=JsonDocument.Parse(json)) return (Dictionary<string,object>)JsonValue(document.RootElement);
    }
    internal static Pool ParseTournament(string json) {
        using(var document=JsonDocument.Parse(json)) return TournamentData((Dictionary<string,object>)JsonValue(document.RootElement));
    }
    static Pool TournamentData(Dictionary<string,object> data) {
        var pool=new Pool{Name=Field(data,"name"),Tournament=true};
        foreach(Dictionary<string,object> round in (System.Collections.IEnumerable)data["rounds"]) {
            var stage=new Stage{Name=Field(round,"round")};
            foreach(Dictionary<string,object> mod in (System.Collections.IEnumerable)round["mods"]) {
                int index=0;
                foreach(var map in (System.Collections.IEnumerable)mod["maps"]) stage.Maps.Add(Parse(map,Field(mod,"mod").ToUpperInvariant()+(++index),null));
            }
            pool.Stages.Add(stage);
        }
        return pool;
    }
    static object JsonValue(JsonElement value) {
        switch(value.ValueKind) {
            case JsonValueKind.Object: return value.EnumerateObject().ToDictionary(x=>x.Name,x=>JsonValue(x.Value));
            case JsonValueKind.Array: return value.EnumerateArray().Select(JsonValue).ToArray();
            case JsonValueKind.String: return value.GetString();
            case JsonValueKind.Number: return value.GetDouble();
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            default: return null;
        }
    }
    static string Field(Dictionary<string,object> obj,string key) {
        object v;
        return obj != null && obj.TryGetValue(key,out v) ? Convert.ToString(v) : "";
    }
    static int Number(Dictionary<string,object> obj,string key) {
        int n; return int.TryParse(Field(obj,key),out n) ? n : 0;
    }
    static double? Decimal(Dictionary<string,object> obj,string key) {
        object value; double parsed;
        if(obj == null || !obj.TryGetValue(key,out value) || value == null) return null;
        return double.TryParse(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out parsed)
            && !double.IsNaN(parsed) && !double.IsInfinity(parsed) ? (double?)parsed : null;
    }
    static Chart Parse(object value,string slot, Dictionary<int,Dictionary<string,object>> sets) {
        var map = value as Dictionary<string,object>;
        if(map == null) {
            int id; int.TryParse(Convert.ToString(value),out id);
            return new Chart { Id = id, Slot = slot, Song = L.T("Карта не найдена в osu!Collector") };
        }
        var sid = Number(map,"beatmapset_id");
        var set = map.ContainsKey("beatmapset") ? map["beatmapset"] as Dictionary<string,object> : null;
        if(set == null && sets != null) sets.TryGetValue(sid,out set);
        if(set != null && sid == 0) sid = Number(set,"id");
        return new Chart { Id = Number(map,"id"), Set = sid, Slot = slot,
            Song = set == null ? "Beatmap " + Field(map,"id") : Field(set,"artist") + " — " + Field(set,"title"),
            Difficulty = Field(map,"version"), Stars = Decimal(map,"difficulty_rating"),
            CS = Decimal(map,"cs"), AR = Decimal(map,"ar"), OD = Decimal(map,"accuracy"),
            HP = Decimal(map,"drain"), BPM = Decimal(map,"bpm"), Length = Decimal(map,"hit_length"),
            Checksum = Field(map,"checksum"), Creator = Field(set,"creator"), Mode = Field(map,"mode"), Status = Field(map,"status") };
    }
    public static async Task<Pool> Load(string link) {
        Uri uri;
        if(!Uri.TryCreate(link.Trim(),UriKind.Absolute,out uri) || uri.Scheme != "https" ||
            (uri.Host != "osucollector.com" && uri.Host != "www.osucollector.com"))
            throw new Exception(L.T("Вставь HTTPS-ссылку на osu!Collector."));
        var match = Regex.Match(uri.AbsolutePath,@"^/(tournaments|collections)/([0-9]+)(?:/|$)");
        if(!match.Success) throw new Exception(L.T("Нужна ссылка на турнир или коллекцию."));
        string url = "https://osucollector.com/api/" + match.Groups[1] + "/" + match.Groups[2];
        bool tournament = match.Groups[1].Value == "tournaments";
        var data = await Read(url + (tournament ? "" : "?withBeatmapsets=false"));
        var pool = new Pool { Name = Field(data,"name"), Tournament = tournament, Source = link.Trim() };
        if(tournament) {
            pool=TournamentData(data); pool.Source=link.Trim();
        } else {
            var all = await Read(url + "/beatmapsv3?perPage=50000");
            var sets = ((System.Collections.IEnumerable)all["beatmapsets"]).Cast<Dictionary<string,object>>().ToDictionary(x=>Number(x,"id"));
            var stage = new Stage { Name = L.T("Все карты") };
            foreach(var m in (System.Collections.IEnumerable)all["beatmaps"]) stage.Maps.Add(Parse(m,"",sets));
            pool.Stages.Add(stage);
        }
        if(!pool.Stages.Any(s=>s.Maps.Any())) throw new Exception(L.T("В пуле пока нет карт."));
        return pool;
    }
    public static Dictionary<int,string> Inspect(string path,IEnumerable<int> required) {
        var found = new Dictionary<int,string>();
        using(var zip = ZipFile.OpenRead(path))
        using(var md5 = MD5.Create()) {
            foreach(var file in zip.Entries.Where(e=>e.Name.EndsWith(".osu",StringComparison.OrdinalIgnoreCase))) {
                if(file.Length > 64*1024*1024) throw new IOException(L.T("Слишком большой файл сложности."));
                using(var input = file.Open())
                using(var bytes = new MemoryStream()) {
                    input.CopyTo(bytes);
                    byte[] raw = bytes.ToArray();
                    var match = Regex.Match(Encoding.UTF8.GetString(raw),@"(?m)^BeatmapID\s*:\s*(\d+)\s*$");
                    int id;
                    if(int.TryParse(match.Groups[1].Value,out id))
                        found[id] = BitConverter.ToString(md5.ComputeHash(raw)).Replace("-","").ToLowerInvariant();
                }
            }
        }
        if(required.Any(id=>!found.ContainsKey(id))) throw new IOException(L.T("В архиве отсутствует нужная сложность."));
        return found;
    }
    public static async Task<Dictionary<int,string>> Fetch(string dest,int set,int[] required) {
        if(File.Exists(dest)) return Inspect(dest,required);
        string last = "";
        foreach(string mirror in new[]{"https://api.nerinyan.moe/d/","https://catboy.best/d/"}) {
            string temp = dest + "." + Guid.NewGuid().ToString("N") + ".part";
            try {
                using(var response = await http.GetAsync(mirror + set,HttpCompletionOption.ResponseHeadersRead)) {
                    response.EnsureSuccessStatusCode();
                    using(var stream = await response.Content.ReadAsStreamAsync())
                    using(var file = File.Create(temp)) {
                        byte[] buffer = new byte[131072];
                        int n;
                        using(var deadline = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(10))) {
                            while((n = await stream.ReadAsync(buffer,0,buffer.Length,deadline.Token)) > 0)
                                await file.WriteAsync(buffer,0,n,deadline.Token);
                        }
                    }
                }
                var hashes = Inspect(temp,required);
                File.Move(temp,dest);
                return hashes;
            } catch(Exception e) { last = e.Message; }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        throw new IOException(L.T("Набор ") + set + ": " + last);
    }
}

class Stable {
    public static void Closed() {
        if(Process.GetProcessesByName("osu!").Length != 0) throw new IOException(L.T("Закрой osu!stable и повтори установку."));
    }
    public static string Songs(string root) {
        if(!File.Exists(Path.Combine(root,"osu!.exe"))) throw new IOException(L.T("Выбери папку с osu!.exe."));
        string songs = "Songs";
        string cfg = Path.Combine(root,"osu!." + Environment.UserName + ".cfg");
        if(File.Exists(cfg)) foreach(string line in File.ReadLines(cfg)) {
            var m = Regex.Match(line,@"^\s*BeatmapDirectory\s*=\s*(.+?)\s*$",RegexOptions.IgnoreCase);
            if(m.Success) songs = m.Groups[1].Value;
        }
        return Path.GetFullPath(Path.Combine(root,songs));
    }
    static string Str(BinaryReader r) {
        int tag = r.ReadByte();
        if(tag == 0) return "";
        if(tag != 11) throw new InvalidDataException(L.T("Неизвестный формат collection.db."));
        return r.ReadString();
    }
    static void Str(BinaryWriter w,string s) { w.Write((byte)(s.Length == 0 ? 0 : 11)); if(s.Length != 0) w.Write(s); }
    static int Count(BinaryReader r) {
        int n = r.ReadInt32();
        if(n < 0 || n > r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException(L.T("Повреждён collection.db."));
        return n;
    }
    public static void Merge(string root,string name,IEnumerable<string> hashes) {
        CollectionStore.Mutate(root,AppSettings.Current.BackupCount,doc=>CollectionStore.Add(doc,name,hashes));
    }
}
