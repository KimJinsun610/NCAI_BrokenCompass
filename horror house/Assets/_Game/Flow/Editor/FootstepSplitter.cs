using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 여러 걸음이 든 발소리 파일(전달본 <c>SFX_STEP_LAB_*_1~3</c> · <c>SFX_STEP_GUARD_*_1~3</c>)을 한 걸음씩 잘라
/// <c>SFX_STEP_LAB_Walk_01~05</c> · <c>_Run_01~04</c> 식으로 쓴다(사운드 전달본 안내 「한 걸음씩 잘라 …로 쓴다」).
/// 소리 크기의 포락선에서 걸음이 시작되는 곳(조용하다가 커지는 곳)을 찾아 그 앞 10ms부터 다음 걸음 직전(최대 0.45초)까지 자르고 끝 30ms를 줄인다.
/// 같은 이름이 있으면 덮어쓴다. 메뉴 「야간근무/소리/여러 걸음 발소리 자르기」.
/// </summary>
public static class FootstepSplitter
{
    public const string Folder = "Assets/_Game/Audio/footsteps/";

    /// <summary>여러 걸음이 든 원본(전달본 그대로)을 두는 곳.</summary>
    public const string SourceFolder = Folder + "source/";

    private static readonly string[,] Jobs =
    {
        { "SFX_STEP_LAB_Walk", "5" },
        { "SFX_STEP_LAB_Run", "4" },
        { "SFX_STEP_GUARD_Walk", "5" },
        { "SFX_STEP_GUARD_Run", "4" },
    };

    [MenuItem("야간근무/소리/여러 걸음 발소리 자르기")]
    public static void SplitMenu()
    {
        Debug.Log(SplitAll());
    }

    public static string SplitAll()
    {
        StringBuilder sb = new StringBuilder();
        for (int j = 0; j < Jobs.GetLength(0); j++)
        {
            string stem = Jobs[j, 0];
            int want = int.Parse(Jobs[j, 1]);
            List<float[]> steps = new List<float[]>();
            int rate = 48000;
            for (int v = 1; v <= 3 && steps.Count < want * 2; v++)
            {
                string path = SourceFolder + stem + "_" + v + ".wav";
                if (!File.Exists(path)) continue;
                float[] mono;
                if (!ReadWav(path, out mono, out rate)) continue;
                steps.AddRange(Cut(mono, rate));
            }

            // 가장 또렷한(최대 크기가 큰) 걸음부터 고른다.
            steps.Sort((a, b) => Peak(b).CompareTo(Peak(a)));
            int made = 0;
            for (int i = 0; i < steps.Count && made < want; i++)
            {
                made++;
                WriteWav(Folder + stem + "_" + made.ToString("00") + ".wav", Normalize(steps[i], 0.7f), rate);
            }

            sb.Append(stem).Append(": 걸음 ").Append(steps.Count).Append("개 찾음 → ").Append(made).Append("개 씀").AppendLine();
        }

        AssetDatabase.Refresh();
        return sb.ToString();
    }

    /// <summary>걸음 하나하나로 자른 조각들.</summary>
    public static List<float[]> Cut(float[] x, int rate)
    {
        int win = Mathf.Max(1, rate / 200);   // 5ms
        int n = x.Length / win;
        float[] env = new float[n];
        float max = 0f;
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            for (int k = 0; k < win; k++) s += x[i * win + k] * x[i * win + k];
            env[i] = (float)Math.Sqrt(s / win);
            if (env[i] > max) max = env[i];
        }

        List<int> onsets = new List<int>();
        List<float[]> parts = new List<float[]>();
        if (max <= 0f) return parts;
        float hi = max * 0.22f;
        float lo = max * 0.08f;
        int quietNeed = 120 / 5;   // 조용함 120ms
        int quiet = quietNeed;
        for (int i = 0; i < n; i++)
        {
            if (env[i] < lo) quiet++;
            else if (env[i] >= hi && quiet >= quietNeed)
            {
                onsets.Add(i);
                quiet = 0;
            }
            else if (env[i] >= lo) quiet = 0;
        }

        int maxLen = (int)(rate * 0.45f);
        int pre = rate / 100;
        int fade = (int)(rate * 0.03f);
        for (int o = 0; o < onsets.Count; o++)
        {
            int start = Mathf.Max(0, onsets[o] * win - pre);
            int end = o + 1 < onsets.Count ? onsets[o + 1] * win - 2 * pre : x.Length;
            end = Mathf.Min(end, start + maxLen, x.Length);
            int len = end - start;
            if (len < rate / 10) continue;   // 100ms보다 짧으면 버린다
            float[] part = new float[len];
            Array.Copy(x, start, part, 0, len);
            for (int k = 0; k < Mathf.Min(fade, len); k++) part[len - 1 - k] *= k / (float)fade;
            parts.Add(part);
        }

        return parts;
    }

    private static float Peak(float[] a)
    {
        float p = 0f;
        for (int i = 0; i < a.Length; i++) p = Mathf.Max(p, Mathf.Abs(a[i]));
        return p;
    }

    private static float[] Normalize(float[] a, float target)
    {
        float p = Peak(a);
        if (p <= 0f) return a;
        float g = target / p;
        float[] b = new float[a.Length];
        for (int i = 0; i < a.Length; i++) b[i] = a[i] * g;
        return b;
    }

    /// <summary>PCM 16·24·32비트 WAV를 모노 float로 읽는다.</summary>
    public static bool ReadWav(string path, out float[] mono, out int rate)
    {
        mono = null;
        rate = 0;
        byte[] d = File.ReadAllBytes(path);
        if (d.Length < 44 || Encoding.ASCII.GetString(d, 0, 4) != "RIFF" || Encoding.ASCII.GetString(d, 8, 4) != "WAVE") return false;
        int pos = 12;
        int channels = 1, bits = 16, format = 1;
        int dataPos = -1, dataLen = 0;
        while (pos + 8 <= d.Length)
        {
            string id = Encoding.ASCII.GetString(d, pos, 4);
            int size = BitConverter.ToInt32(d, pos + 4);
            if (id == "fmt ")
            {
                format = BitConverter.ToInt16(d, pos + 8);
                channels = BitConverter.ToInt16(d, pos + 10);
                rate = BitConverter.ToInt32(d, pos + 12);
                bits = BitConverter.ToInt16(d, pos + 22);
            }
            else if (id == "data")
            {
                dataPos = pos + 8;
                dataLen = Math.Min(size, d.Length - dataPos);
                break;
            }

            pos += 8 + size + (size & 1);
        }

        if (dataPos < 0 || (format != 1 && format != 3 && format != -2) || channels < 1) return false;
        int bytes = bits / 8;
        int frames = dataLen / (bytes * channels);
        mono = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                int p = dataPos + (f * channels + ch) * bytes;
                float v;
                if (bits == 16) v = BitConverter.ToInt16(d, p) / 32768f;
                else if (bits == 24) v = ((d[p] | (d[p + 1] << 8) | ((sbyte)d[p + 2] << 16))) / 8388608f;
                else if (format == 3) v = BitConverter.ToSingle(d, p);
                else v = BitConverter.ToInt32(d, p) / 2147483648f;
                sum += v;
            }

            mono[f] = sum / channels;
        }

        return true;
    }

    public static void WriteWav(string path, float[] x, int rate)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            int dataLen = x.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            for (int i = 0; i < x.Length; i++) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(x[i] * 32767f), -32768, 32767));
        }
    }
}
