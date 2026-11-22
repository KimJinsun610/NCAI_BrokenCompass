using System.Text;
using NightDuty;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DT 3단계 Fx: 연출 SO 에셋에 Fx_Table 값을 굽는다(결정 5 — 런타임은 SO만 읽는다). 정본은 xlsx — 구운 값을 인스펙터에서 고치면 다음 굽기 때 덮어쓴다.
/// <list type="bullet">
/// <item>조도 화면(420101~420105) → <c>IlluminanceTone</c> 구간 톤 5줄</item>
/// <item>조도 조명(420201~420205) 「화장실 조명 붉음」「과학실 앞 복도 소등」 → 처음 1이 되는 구간</item>
/// <item>청각 바탕(420301~420305) 원샷 간격 → <c>AmbienceConfig</c>, 「룸톤 판: High」가 처음 나오는 구간 → 강한 판 구간</item>
/// <item>CCTV 카메라(421301~) → <c>CctvConfig</c> 채널(카메라 번호 순)</item>
/// </list>
/// xlsx를 고친 뒤 순서: Tools/DT/내보내기 (Export) → Tools/DT/Fx 굽기 (Bake).
/// </summary>
public static class DTFxBaker
{
    [MenuItem("Tools/DT/Fx 굽기 (Bake)")]
    public static void BakeMenu()
    {
        var log = new StringBuilder();
        int n = Run(true, log);
        Debug.Log("[DT Fx 굽기] 바뀐 칸 " + n + "개\n" + log);
    }

    [MenuItem("Tools/DT/Fx 대조 (Compare)")]
    public static void CompareMenu()
    {
        var log = new StringBuilder();
        int n = Run(false, log);
        if (n == 0) Debug.Log("[DT Fx 대조] SO 에셋 = 표");
        else Debug.LogWarning("[DT Fx 대조] 표와 다른 칸 " + n + "개 — Tools/DT/Fx 굽기 (Bake)\n" + log);
    }

    /// <summary>표와 SO가 다른 칸 수. apply면 표 값으로 고치고 저장한다.</summary>
    public static int Run(bool apply, StringBuilder log)
    {
        DesignData.Ensure();
        int n = 0;
        n += Tone(apply, log);
        n += Ambience(apply, log);
        n += Cctv(apply, log);
        if (apply && n > 0) AssetDatabase.SaveAssets();
        return n;
    }

    // ── 조도 ──

    private static int Tone(bool apply, StringBuilder log)
    {
        IlluminanceToneSO so = Resources.Load<IlluminanceToneSO>("IlluminanceTone");
        if (so == null) { log.Append("IlluminanceTone 에셋 없음\n"); return 0; }
        var s = new SerializedObject(so);
        int n = 0;
        SerializedProperty rows = s.FindProperty("rows");
        if (rows.arraySize != 5) { n++; log.Append("tone rows " + rows.arraySize + " → 5\n"); if (apply) rows.arraySize = 5; }
        for (int b = 0; b < 5 && b < rows.arraySize; b++)
        {
            uint id = 420101u + (uint)b;
            SerializedProperty r = rows.GetArrayElementAtIndex(b);
            n += F(r.FindPropertyRelative("temperature"), DesignData.Fx(id, "색온도"), apply, log, "tone" + b + ".색온도");
            n += F(r.FindPropertyRelative("tint"), DesignData.Fx(id, "틴트"), apply, log, "tone" + b + ".틴트");
            n += F(r.FindPropertyRelative("saturation"), DesignData.Fx(id, "채도"), apply, log, "tone" + b + ".채도");
            n += C(r.FindPropertyRelative("colorFilter"), DesignData.FxColor(id, "컬러 필터"), apply, log, "tone" + b + ".컬러 필터");
            n += F(r.FindPropertyRelative("vignette"), DesignData.Fx(id, "비네팅"), apply, log, "tone" + b + ".비네팅");
            n += C(r.FindPropertyRelative("vignetteColor"), ColorOrBlack(id, "비네팅 색"), apply, log, "tone" + b + ".비네팅 색");
        }

        n += E(s.FindProperty("toiletRedFrom"), FirstOn("화장실 조명 붉음(1=예)"), apply, log, "toiletRedFrom");
        n += E(s.FindProperty("scienceFrontOffFrom"), FirstOn("과학실 앞 복도 소등(1=예)"), apply, log, "scienceFrontOffFrom");
        if (apply) s.ApplyModifiedPropertiesWithoutUndo();
        if (apply && n > 0) EditorUtility.SetDirty(so);
        return n;
    }

    /// <summary>비네팅 색 칸이 비면 검정(표 설명: 비우면 검정).</summary>
    private static Color ColorOrBlack(uint id, string prefix)
    {
        InstructionDT.FxRow row = DesignData.FxRow(id);
        for (int i = 1; i <= 12; i++)
        {
            string name = (string)typeof(InstructionDT.FxRow).GetField("Param_" + i + "_Name").GetValue(row);
            if (name == prefix + " R" && row.IsBlank("Param_" + i + "_Value")) return Color.black;
        }

        return DesignData.FxColor(id, prefix);
    }

    /// <summary>조도 조명 줄(420201~420205)에서 그 칸이 처음 1이 되는 구간. 없으면 구간 4 다음(끔).</summary>
    private static int FirstOn(string name)
    {
        for (int b = 0; b < 5; b++)
            if (DesignData.FxOn(420201u + (uint)b, name)) return b;
        return 5;
    }

    // ── 앰비언스 ──

    private static int Ambience(bool apply, StringBuilder log)
    {
        AmbienceConfigSO so = Resources.Load<AmbienceConfigSO>(AmbienceConfigSO.ResourceName);
        if (so == null) { log.Append("AmbienceConfig 에셋 없음\n"); return 0; }
        var s = new SerializedObject(so);
        int n = 0;
        SerializedProperty oi = s.FindProperty("oneShotInterval");
        if (oi.arraySize != 5) { n++; log.Append("oneShotInterval " + oi.arraySize + " → 5\n"); if (apply) oi.arraySize = 5; }
        int high = 5;
        for (int b = 0; b < 5; b++)
        {
            uint id = 420301u + (uint)b;
            if (b < oi.arraySize)
            {
                var want = new Vector2(DesignData.Fx(id, "원샷 간격 최소(초)"), DesignData.Fx(id, "원샷 간격 최대(초)"));
                SerializedProperty v = oi.GetArrayElementAtIndex(b);
                if ((v.vector2Value - want).sqrMagnitude > 1e-8f) { n++; log.Append("원샷 간격 " + b + ": " + v.vector2Value + " → " + want + "\n"); if (apply) v.vector2Value = want; }
            }

            string text = DesignData.FxRow(id).Text ?? string.Empty;
            if (high == 5 && text.Contains("High")) high = b;
        }

        SerializedProperty hb = s.FindProperty("highFromBand");
        if (high <= 4 && hb.intValue != high) { n++; log.Append("highFromBand " + hb.intValue + " → " + high + "\n"); if (apply) hb.intValue = high; }
        if (apply) s.ApplyModifiedPropertiesWithoutUndo();
        if (apply && n > 0) EditorUtility.SetDirty(so);
        return n;
    }

    // ── CCTV ──

    private static int Cctv(bool apply, StringBuilder log)
    {
        CctvConfigSO so = Resources.Load<CctvConfigSO>(CctvConfigSO.ResourceName);
        if (so == null) { log.Append("CctvConfig 에셋 없음\n"); return 0; }
        var s = new SerializedObject(so);
        int n = 0;
        SerializedProperty ch = s.FindProperty("channels");
        foreach (InstructionDT.FxRow row in InstructionDT.DT.Fx.All)
        {
            if (row.ID < 421301 || row.ID > 421399) continue;
            int i = DesignData.FxInt(row.ID, "카메라 번호") - 1;
            if (i < 0 || i >= ch.arraySize) { n++; log.Append("CCTV 카메라 " + (i + 1) + " — 채널 없음(" + ch.arraySize + "개)\n"); continue; }
            SerializedProperty c = ch.GetArrayElementAtIndex(i);
            string k = "CCTV " + (i + 1) + ".";
            n += V(c.FindPropertyRelative("position"), new Vector3(DesignData.Fx(row.ID, "카메라 위치 X"), DesignData.Fx(row.ID, "카메라 위치 Y"), DesignData.Fx(row.ID, "카메라 위치 Z")), apply, log, k + "위치");
            n += V(c.FindPropertyRelative("lookAt"), new Vector3(DesignData.Fx(row.ID, "바라보는 점 X"), DesignData.Fx(row.ID, "바라보는 점 Y"), DesignData.Fx(row.ID, "바라보는 점 Z")), apply, log, k + "바라보는 점");
            n += F(c.FindPropertyRelative("fieldOfView"), DesignData.Fx(row.ID, "화각(°)"), apply, log, k + "화각");
            n += F(c.FindPropertyRelative("irIntensity"), DesignData.Fx(row.ID, "적외선 세기"), apply, log, k + "적외선 세기");
            n += F(c.FindPropertyRelative("irRange"), DesignData.Fx(row.ID, "적외선 거리"), apply, log, k + "적외선 거리");
            n += F(c.FindPropertyRelative("irSpotAngle"), DesignData.Fx(row.ID, "적외선 범위"), apply, log, k + "적외선 범위");
        }

        if (apply) s.ApplyModifiedPropertiesWithoutUndo();
        if (apply && n > 0) EditorUtility.SetDirty(so);
        return n;
    }

    // ── 칸 비교·쓰기 ──

    private static int F(SerializedProperty p, float want, bool apply, StringBuilder log, string key)
    {
        if (Mathf.Abs(p.floatValue - want) <= 1e-4f) return 0;
        log.Append(key + ": " + p.floatValue + " → " + want + "\n");
        if (apply) p.floatValue = want;
        return 1;
    }

    private static int C(SerializedProperty p, Color want, bool apply, StringBuilder log, string key)
    {
        Color now = p.colorValue;
        want.a = now.a;   // 표에는 알파가 없다 — 에셋 값 유지
        if (Mathf.Abs(now.r - want.r) <= 1e-4f && Mathf.Abs(now.g - want.g) <= 1e-4f && Mathf.Abs(now.b - want.b) <= 1e-4f) return 0;
        log.Append(key + ": " + now + " → " + want + "\n");
        if (apply) p.colorValue = want;
        return 1;
    }

    private static int V(SerializedProperty p, Vector3 want, bool apply, StringBuilder log, string key)
    {
        if ((p.vector3Value - want).sqrMagnitude <= 1e-8f) return 0;
        log.Append(key + ": " + p.vector3Value + " → " + want + "\n");
        if (apply) p.vector3Value = want;
        return 1;
    }

    private static int E(SerializedProperty p, int want, bool apply, StringBuilder log, string key)
    {
        if (p.intValue == want) return 0;
        log.Append(key + ": " + p.intValue + " → " + want + "\n");
        if (apply) p.intValue = want;
        return 1;
    }
}
