using System;
using System.IO;
using System.Media;
class NoticeSound {
    static SoundPlayer player;
    static MemoryStream audio;
    internal static void Play(bool error) {
        if(!AppSettings.Current.Sounds) return;
        try {
            if(player!=null) { player.Stop(); player.Dispose(); audio.Dispose(); }
            int rate=22050, count=(int)(rate*0.65);
            audio=new MemoryStream(); var w=new BinaryWriter(audio);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36+count*2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate*2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(count*2);
            for(int i=0;i<count;i++) {
                double t=(double)i/rate, f=error ? (t<0.22 ? 523.25 : 440) : (t<0.22 ? 659.25 : 880);
                double local=t<0.22 ? t : t-0.22;
                double envelope=Math.Min(1,local/0.025)*Math.Exp(-local*9)*Math.Min(1,(0.65-t)/0.05);
                w.Write((short)(Math.Sin(2*Math.PI*f*local)*envelope*4200));
            }
            audio.Position=0; player=new SoundPlayer(audio); player.Play();
        } catch { /* Audio failure must not affect installation. */ }
    }
}
