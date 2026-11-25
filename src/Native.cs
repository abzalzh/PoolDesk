using System;
using System.Runtime.InteropServices;
class Native {
    const string Lib = "rosu_pp_ffi.dll";
    [StructLayout(LayoutKind.Sequential)]
    internal struct Attributes {
        public int mode;
        public double stars;
        public uint max_combo;
        public double aim;
        public double speed;
        public double flashlight;
        public double stamina;
        public double rhythm;
        public double color;
        public double reading;
        public double ar;
        public double hp;
        public double great_hit_window;
        public double ok_hit_window;
        public double meh_hit_window;
        public uint n_circles;
        public uint n_sliders;
        public uint n_large_ticks;
        public uint n_spinners;
        public uint n_objects;
        public double aim_difficult_slider_count;
        public double slider_factor;
        public double aim_top_weighted_slider_factor;
        public double speed_top_weighted_slider_factor;
        public double speed_note_count;
        public double aim_difficult_strain_count;
        public double speed_difficult_strain_count;
        public double nested_score_per_object;
        public double legacy_score_base_multiplier;
        public double maximum_legacy_combo_score;
        public double mono_stamina_factor;
        public double mechanical_difficulty;
        public double consistency_factor;
        public double preempt;
        public uint n_fruits;
        public uint n_droplets;
        public uint n_tiny_droplets;
        public uint n_hold_notes;
        [MarshalAs(UnmanagedType.I1)]
        public bool is_convert;
    }
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int rosu_pp_beatmap_from_bytes(byte[] bytes, UIntPtr length, out IntPtr map);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void rosu_pp_beatmap_free(IntPtr map);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr rosu_pp_difficulty_new();
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void rosu_pp_difficulty_free(IntPtr diff);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int rosu_pp_difficulty_lazer(IntPtr diff, [MarshalAs(UnmanagedType.I1)] bool lazer);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int rosu_pp_mods_from_acronym([MarshalAs(UnmanagedType.LPStr)] string mods, out IntPtr handle);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void rosu_pp_mods_free(IntPtr mods);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int rosu_pp_difficulty_mods(IntPtr diff, IntPtr mods);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int rosu_pp_difficulty_checked_calculate(IntPtr diff, IntPtr map, out Attributes attributes);
    static void Check(int code) { if(code != 0) throw new InvalidOperationException(L.T("Ошибка расчёта rosu-pp: ") + code); }
    internal static double Calculate(byte[] bytes,string mods) {
        IntPtr map=IntPtr.Zero, diff=IntPtr.Zero, mod=IntPtr.Zero;
        try {
            Check(rosu_pp_beatmap_from_bytes(bytes,(UIntPtr)bytes.Length,out map));
            diff = rosu_pp_difficulty_new();
            if(diff == IntPtr.Zero) throw new InvalidOperationException(L.T("Не удалось создать калькулятор."));
            Check(rosu_pp_difficulty_lazer(diff,false));
            Check(rosu_pp_mods_from_acronym(mods,out mod));
            Check(rosu_pp_difficulty_mods(diff,mod));
            Attributes result; Check(rosu_pp_difficulty_checked_calculate(diff,map,out result));
            if(double.IsNaN(result.stars) || double.IsInfinity(result.stars) || result.stars < 0) throw new InvalidOperationException(L.T("Некорректный результат расчёта."));
            return result.stars;
        } finally {
            rosu_pp_mods_free(mod); rosu_pp_difficulty_free(diff); rosu_pp_beatmap_free(map);
        }
    }
}
