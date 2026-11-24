using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

static class Ratings {
    static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
    static readonly SemaphoreSlim limit = new SemaphoreSlim(2);
    internal static string CacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PoolDesk","DifficultyMaps");
    static Ratings() { client.DefaultRequestHeaders.UserAgent.ParseAdd("PoolDesk/2.2"); }
    internal static string ModsForSlot(string slot) {
        string code = Regex.Replace((slot ?? "").ToUpperInvariant().Trim(),@"\d+$","").Replace("+","").Replace(" ","");
        if(code == "" || code == "NM" || code == "FM" || code == "TB") return "NM";
        if(!Regex.IsMatch(code,@"^(HD|HR|DT|NC|HT|EZ|FL|NF|SD|PF|SO)+$")) return null;
        var mods = Regex.Matches(code,@"HD|HR|DT|NC|HT|EZ|FL|NF|SD|PF|SO").Cast<Match>().Select(m=>m.Value).ToArray();
        if(mods.Distinct().Count()!=mods.Length || (mods.Contains("HR") && mods.Contains("EZ")) ||
            (mods.Contains("HT") && (mods.Contains("DT") || mods.Contains("NC")))) return null;
        return code;
    }
    internal static void Validate(byte[] bytes,Chart map) {
        if(bytes.Length > 8*1024*1024) throw new IOException(L.T("Слишком большой файл .osu."));
        string text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        if(!text.StartsWith("osu file format v",StringComparison.Ordinal) || !text.Contains("[HitObjects]")) throw new IOException(L.T("Вместо карты получен неверный файл."));
        var match = Regex.Match(text,@"(?m)^BeatmapID\s*:\s*(\d+)\s*$"); int id;
        if(!int.TryParse(match.Groups[1].Value,out id) || id != map.Id) throw new IOException(L.T("ID скачанного файла не совпадает с картой."));
        if(Regex.IsMatch(map.Checksum ?? "",@"^[a-fA-F0-9]{32}$")) using(var md5 = MD5.Create()) {
            string hash = BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-","");
            if(!hash.Equals(map.Checksum,StringComparison.OrdinalIgnoreCase)) throw new IOException(L.T("Версия .osu отличается от osu!Collector. Старрейт не подменён; обнови пул позже."));
        }
    }
    internal static async Task<byte[]> GetMap(Chart map,CancellationToken token) {
        using(var deadline = CancellationTokenSource.CreateLinkedTokenSource(token)) {
        deadline.CancelAfter(TimeSpan.FromSeconds(30)); token = deadline.Token;
        bool cacheable = Regex.IsMatch(map.Checksum ?? "",@"^[a-fA-F0-9]{32}$");
        string path = Path.Combine(CacheDirectory,map.Id + "-" + (cacheable ? map.Checksum.ToLowerInvariant() : "unknown") + ".osu");
        if(cacheable && File.Exists(path)) {
            byte[] cached = File.ReadAllBytes(path);
            try { Validate(cached,map); return cached; } catch(IOException) { File.Delete(path); }
        }
        using(var response = await client.GetAsync("https://osu.ppy.sh/osu/" + map.Id,HttpCompletionOption.ResponseHeadersRead,token)) {
            response.EnsureSuccessStatusCode();
            using(var stream = await response.Content.ReadAsStreamAsync()) using(var memory = new MemoryStream()) {
                byte[] buffer = new byte[32768]; int read;
                while((read = await stream.ReadAsync(buffer,0,buffer.Length,token))>0) {
                    if(memory.Length+read>8*1024*1024) throw new IOException(L.T("Слишком большой файл .osu."));
                    memory.Write(buffer,0,read);
                }
                byte[] bytes = memory.ToArray(); Validate(bytes,map);
                if(cacheable) {
                    Directory.CreateDirectory(CacheDirectory); string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try { File.WriteAllBytes(temp,bytes); if(!File.Exists(path)) File.Move(temp,path); }
                    finally { if(File.Exists(temp)) File.Delete(temp); }
                }
                return bytes;
            }
        }
        }
    }
    internal static async Task Update(Stage stage,CancellationToken token,Action<Chart> changed) {
        await Task.WhenAll(stage.Maps.Where(m=>m.Available).Select(async map=> {
            if(map.ModStars.HasValue) { changed(map); return; }
            await limit.WaitAsync(token);
            try {
                token.ThrowIfCancellationRequested(); map.RatingLoading = true; map.RatingError = null; changed(map);
                byte[] bytes = await GetMap(map,token);
                string mods = map.ModCode;
                var result = await Task.Run(delegate {
                    double normal = Native.Calculate(bytes,"NM");
                    double? modified = mods == null ? (double?)null : mods == "NM" ? normal : Native.Calculate(bytes,mods);
                    return Tuple.Create(normal,modified);
                },token);
                token.ThrowIfCancellationRequested(); map.BaseStars=result.Item1; map.ModStars=result.Item2;
                if(mods == null) map.RatingError = L.T("Моды слота не распознаны; старрейт с модами неизвестен.");
            } catch(OperationCanceledException) { if(token.IsCancellationRequested) throw; map.RatingError=L.T("Сервер не ответил вовремя. Переключи стадию для повтора."); }
            catch(Exception e) { map.RatingError=L.T("Не удалось рассчитать: ") + e.Message + L.T(" Переключи стадию для повтора."); }
            finally { map.RatingLoading=false; limit.Release(); if(!token.IsCancellationRequested) changed(map); }
        }));
    }
}
