using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 엠비언트 에디터 도구.
/// <list type="bullet">
/// <item>소리 파일 가져오기 규칙 — <c>Resources/Ambience/</c>, <c>Resources/Cctv/</c> 아래 .ogg/.wav를 넣으면 알아서 맞춘다.
/// 루프(룸톤·불안 레이어·CCTV 험/지직)는 메모리에 압축된 채로(40초 스테레오를 풀면 한 개에 15MB), 짧은 원샷은 불러올 때 푼다.</item>
/// <item>메뉴 — 설정 에셋 만들기 · 소리 파일 검사.</item>
/// </list>
/// </summary>
public sealed class AmbienceAudioImportRules : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        string p = assetPath.Replace('\\', '/');
        bool ambience = p.Contains("/Resources/Ambience/");
        bool cctv = p.Contains("/Resources/Cctv/");
        if (!ambience && !cctv)
        {
            return;
        }

        AudioImporter imp = (AudioImporter)assetImporter;
        AudioImporterSampleSettings s = imp.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;

        bool loop = p.Contains("/Rooms/") || p.Contains("/Dread/") || p.EndsWith("cctv_hum.ogg") || p.EndsWith("cctv_static.ogg");
        if (loop)
        {
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.quality = 0.6f;
            imp.loadInBackground = true;
        }
        else
        {
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.quality = 0.7f;
            imp.loadInBackground = false;
        }

        imp.defaultSampleSettings = s;
    }
}

public static class AmbienceMenu
{
    private const string AssetPath = "Assets/_Game/Resources/AmbienceConfig.asset";

    [MenuItem("NightDuty/엠비언트 설정 에셋 만들기")]
    private static void CreateConfig()
    {
        AmbienceConfigSO existing = AssetDatabase.LoadAssetAtPath<AmbienceConfigSO>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("[Ambience] 이미 있습니다: " + AssetPath);
            return;
        }

        AmbienceConfigSO so = ScriptableObject.CreateInstance<AmbienceConfigSO>();
        so.FillDefaults();
        AssetDatabase.CreateAsset(so, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = so;
        Debug.Log("[Ambience] 만들었습니다: " + AssetPath);
    }

    [MenuItem("NightDuty/엠비언트 소리 파일 검사")]
    private static void CheckClips()
    {
        Debug.Log(Report());
    }

    /// <summary>설정이 가리키는 소리 파일 중 없는 것을 센다. 메뉴와 테스트가 같이 쓴다.</summary>
    public static string Report()
    {
        AmbienceConfigSO cfg = AmbienceConfigSO.Load();
        List<string> paths = new List<string>();
        if (!string.IsNullOrEmpty(cfg.BaseClip)) paths.Add(cfg.BaseClip);
        Add(paths, cfg.DefaultZone);
        for (int i = 0; i < cfg.Zones.Count; i++)
        {
            Add(paths, cfg.Zones[i]);
        }

        for (int i = 0; i < cfg.DreadClips.Count; i++)
        {
            paths.Add(cfg.DreadClips[i]);
        }

        foreach (string s in new[] { "stinger_hit", "stinger_riser", "stinger_breath", "stinger_whisper" })
        {
            paths.Add("Ambience/Stingers/" + s);
        }

        foreach (string s in new[] { "cctv_hum", "cctv_switch", "cctv_static" })
        {
            paths.Add("Cctv/" + s);
        }

        int ok = 0;
        StringBuilder missing = new StringBuilder();
        HashSet<string> seen = new HashSet<string>();
        foreach (string p in paths)
        {
            if (!seen.Add(p))
            {
                continue;
            }

            if (Resources.Load<AudioClip>(p) != null)
            {
                ok++;
            }
            else
            {
                missing.Append("\n  - Resources/").Append(p);
            }
        }

        return missing.Length == 0
            ? "[Ambience] 소리 파일 " + ok + "개 전부 있습니다."
            : "[Ambience] 있음 " + ok + "개, 없음 " + (seen.Count - ok) + "개:" + missing;
    }

    private static void Add(List<string> into, AmbienceConfigSO.Zone z)
    {
        if (z == null)
        {
            return;
        }

        into.Add(z.roomClip);
        if (!string.IsNullOrEmpty(z.roomClipHigh)) into.Add(z.roomClipHigh);
        if (z.oneShots != null)
        {
            into.AddRange(z.oneShots);
        }
    }
}
