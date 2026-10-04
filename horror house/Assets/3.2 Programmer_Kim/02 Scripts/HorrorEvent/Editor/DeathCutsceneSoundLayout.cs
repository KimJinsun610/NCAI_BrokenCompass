using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 청각 사망 컷신(<see cref="DeathCutscene"/>)의 <b>소리 트랙만</b> 다시 짠다. 화면(카메라·소년·숨막힘 볼륨) 트랙은 건드리지 않는다.
///
/// <para>시각표는 사운드 담당의 시연 영상(v3) 설명을 옮긴 것이다. 영상은 지금 컷신을 녹화한 것이고
/// <b>잡히는 순간(영상 5.40초) = 컷신 0초</b>다. 아래 시각은 전부 「영상 시각 − 5.40」.</para>
/// <para>2026-10-05(사용자 가이드): 빌드업 끝(4.40)에 <b>「쾅」(CLX-10_2)이 나며 다른 소리가 모두 끊기고</b>, 쾅 울림이 사라진 뒤
/// <b>1.5초 정적</b>(그동안 숙였던 고개를 들어 정면) → 정면을 보는 순간 화면이 확 뒤돈다(<see cref="TurnStart"/>~<see cref="TurnEnd"/>).
/// 그래서 문서의 히트 이후(Hit·Hold·Stretch) 시각에는 <see cref="HitShift"/>를 더한다. 빌드업 시각은 문서 그대로다.</para>
/// <list type="bullet">
/// <item>몸싸움 빌드업 0.00~4.10 — 의자 끼익 · 금속 긁힘 두 겹(속도↑ = 음높이↑) · 심박 13번(0.46 → 0.20초 간격) · 왼쪽 귀 숨 ·
/// 시점이 꺾일 때 사물함 철컹 · 속삭임 세 겹(왼·오·가운데) · CLX-02_3 현악 상승(4.10에 딱 끊김)</item>
/// <item>빨려 듦 3.60~4.40 — CLX-08_3 거꾸로 감은 심벌. 4.10부터 남은 빌드업은 먹먹하게(<see cref="DeathCutscene"/>의 저역 필터)</item>
/// <item>정적 4.40~4.667 — 화면이 어두워지는 동안 아무 소리도 없다</item>
/// <item>히트 4.667 — 음역을 나눠 쌓는다: CLX-04_3(~100Hz) · CLX-01_3(100Hz~2.5kHz) · CLX-05_2(500Hz~) · CLX-03_3(1.5kHz~) · CLX-07_3(2kHz~),
/// 4.677 「헉」, 4.867 CLX-06_2 지직 비명</item>
/// <item>얼굴 유지 4.70~7.467 — 저음 드론(5.55까지 차오름) · 글리치 · 이명, 머리가 크게 꺾이는 8순간마다 짧은 「지직」, 6.20~7.30 왼쪽 속삭임</item>
/// <item>얼굴 늘어남 7.467~7.967 — CLX-03_2 끝부분 · CLX-07_2 「끽」 · 「지지직」. 7.967에 전부 끊김</item>
/// <item>복귀 — 게임으로 돌아오는 순간 「흡」 + 떨리는 숨(<see cref="DeathCutscene"/>의 returnAudio)</item>
/// </list>
///
/// <para>클립은 <b>파일 이름으로 프로젝트 전체에서 찾는다</b>(<c>Assets/_Game/Audio</c>, 진선님 <c>99 Resources/04 Sound</c> 어디든).
/// 없는 클립은 그 자리만 비우고 로그에 남긴다 — 파일을 넣은 뒤 메뉴를 다시 돌리면 채워진다.</para>
/// <para>정렬: 소리마다 WAV를 직접 읽어 <b>소리가 시작되는 지점(onset)</b>이나 <b>가장 큰 지점(peak)</b>을 재고, 그 지점이 문서 시각에 오도록
/// clipIn을 잡는다(압축 클립은 에디터에서 GetData가 0을 돌려줘 파일을 읽는다).</para>
/// </summary>
public static class DeathCutsceneSoundLayout
{
    private const string PrefabPath = "Assets/3.2 Programmer_Kim/03 Prefebs/04 Horror/Resources/" + DeathCutscene.ResourceName + ".prefab";

    private const string TrackPrefix = "SFX ";
    private const string LegacyWhisperTrack = "Whisper";
    private const string SoundsRoot = "Sounds";
    private const string SubAssetPrefix = "SFX_";

    // ── 문서 시각(컷신 초 = 영상 초 − 5.40) ──
    // ── 연출 뼈대(컷신 초) — 빌더(카메라·소년·화면)도 이 값을 쓴다 ──
    // 빌드업(소리가 요란한 구간) — 문서는 4.40초. 2026-10-05 사용자 요청으로 2초 늘림(4.40 → 5.40 → 6.40): 빌드업 안의 소리·카메라 시각을 모두 BuildupScale배
    private const float DocBuildupEnd = 4.40f;
    public const float BuildupEnd = 6.40f;                         // 쾅 — 다른 소리가 모두 끊김
    public const float BuildupScale = BuildupEnd / DocBuildupEnd;  // 문서 빌드업 시각 → 컷신 시각
    public const float BangTail = 0.8f;                            // 쾅 울림이 사라지는 데 걸리는 시간(실측 0.75초에 −53dB)
    public const float QuietSeconds = 1.5f;                        // 쾅이 사라진 뒤 정적(고개를 들어 정면을 봄)
    public const float FrontHold = 0.5f;                           // 고개를 다 든 뒤 정면을 본 채 멈춰 있는 시간(2026-10-05 사용자 요청 +1초 → 길어서 +0.5초) — 그만큼 정적도 길어짐
    public const float TurnStart = BuildupEnd + BangTail + QuietSeconds + FrontHold;   // 9.20 — 화면이 확 뒤돌기 시작
    public const float TurnSeconds = 0.35f;
    public const float TurnEnd = TurnStart + TurnSeconds;          // 9.05 — 소년과 마주함
    public const float NewHitAt = TurnEnd - 0.05f;                 // 얼굴이 시야에 들어오는 순간
    public const float HitShift = NewHitAt - 4.667f;               // 문서 히트(4.667) 이후 소리를 이만큼 뒤로
    public const float EndAt = CutAt + HitShift + 0.03f;          // Timeline 길이
    private const string BangClip = "CLX-10_2";   // 소년의 「거대한 쿵」 — 가장 큰 한 방(원본 1.50초)만 잘라 쓴다. 0.75초 안에 −53dB로 줄어 뒤 정적이 산다

    private const float SilenceAt = DocBuildupEnd; // 빌드업·빨려 듦이 뚝 끊김(문서 시각) — 여기서 쾅
    private const float HitAt = 4.667f;         // 얼굴이 뜨는 순간
    // 비명·얼굴 유지 구간을 ScreamTrim만큼 잘라냄(2026-10-05 사용자 요청 0.7초 → 0.5초 더 = 1.2초) — 얼굴 늘어남(0.5초)은 길이 그대로 앞당김
    private const float ScreamTrim = 1.2f;
    private const float HoldEnd = 7.467f - ScreamTrim;    // 얼굴 늘어남 시작
    private const float CutAt = 7.967f - ScreamTrim;      // 모든 소리 끊김
    private const float MuffleFrom = 4.10f;     // 남은 빌드업을 먹먹하게
    private const float MuffleTo = 4.30f;

    private enum Align { Onset, Peak, Start }

    /// <summary>한 소리 배치. at = 맞출 컷신 시각, until = 끝 시각.</summary>
    private struct Cue
    {
        public string clip;
        public float at;
        public float until;
        public Align align;
        public float offset;      // Start 정렬일 때 clipIn, 그 밖엔 맞춤 지점 앞쪽 여유(초)
        public float volume;
        public float speed;
        public float easeIn;
        public float easeOut;
    }

    private static Cue C(string clip, float at, float until, float volume, Align align = Align.Onset, float offset = 0f, float easeIn = 0f, float easeOut = 0f, float speed = 1f)
    {
        return new Cue { clip = clip, at = at, until = until, volume = volume, align = align, offset = offset, easeIn = easeIn, easeOut = easeOut, speed = speed };
    }

    /// <summary>소리 한 갈래 = AudioSource 하나 + 트랙 하나(겹치는 소리는 갈래를 나눈다).</summary>
    private class Lane
    {
        public string name;
        public string group;          // Buildup · Hit · Hold · Stretch
        public float pan;
        public float highPass;        // 0이면 없음
        public float lowPass;         // 0이면 없음(빌드업은 먹먹하게용 필터를 따로 단다)
        public List<Cue> cues = new List<Cue>();
    }

    [MenuItem("Tools/Programmer_Kim/Horror/Rebuild Death Cutscene Sounds (Auditory)")]
    public static void RebuildMenu()
    {
        Debug.Log(Rebuild());
    }

    /// <summary>프리팹을 열어 소리만 다시 짜고 저장한다. 결과 요약을 돌려준다.</summary>
    public static string Rebuild()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null) return "[DeathCutsceneSoundLayout] 프리팹이 없습니다: " + PrefabPath;
        try
        {
            var director = root.GetComponent<PlayableDirector>();
            var timeline = director != null ? director.playableAsset as TimelineAsset : null;
            if (timeline == null) return "[DeathCutsceneSoundLayout] 프리팹에 Timeline이 없습니다.";

            string report = Apply(root, director, timeline);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            return report;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 컷신 루트에 소리를 짠다(<see cref="DeathCutsceneBuilder"/>도 부른다). 옛 소리 트랙·소스는 지우고 새로 만든다.
    /// </summary>
    public static string Apply(GameObject root, PlayableDirector director, TimelineAsset timeline)
    {
        var cutscene = root.GetComponent<DeathCutscene>();
        rampCursor.Clear();
        var missing = new List<string>();

        // ── 옛 것 지우기 ──
        foreach (TrackAsset t in new List<TrackAsset>(timeline.GetOutputTracks()))
        {
            if (t is AudioTrack && (t.name.StartsWith(TrackPrefix) || t.name == LegacyWhisperTrack))
            {
                director.ClearGenericBinding(t);
                timeline.DeleteTrack(t);
            }
        }
        Transform oldWhisper = root.transform.Find(LegacyWhisperTrack);
        if (oldWhisper != null) UnityEngine.Object.DestroyImmediate(oldWhisper.gameObject);
        Transform oldSounds = root.transform.Find(SoundsRoot);
        if (oldSounds != null) UnityEngine.Object.DestroyImmediate(oldSounds.gameObject);

        var soundsGo = new GameObject(SoundsRoot);
        soundsGo.transform.SetParent(root.transform, false);

        // ── 배치 ──
        List<Lane> lanes = BuildLanes(root);
        foreach (Lane lane in lanes)
        {
            for (int i = 0; i < lane.cues.Count; i++)
            {
                Cue c = lane.cues[i];
                if (lane.group == "Buildup")   // 빌드업은 문서 시각을 BuildupScale배로 늘림
                {
                    c.at *= BuildupScale;
                    c.until *= BuildupScale;
                    lane.cues[i] = c;
                    continue;
                }
                c.at += HitShift;
                c.until += HitShift;
                lane.cues[i] = c;
            }
        }
        // 빌드업 끝: 쾅 — 다른 소리는 모두 여기서 끊기고, 쾅 울림이 사라진 뒤 1.5초는 아무 소리도 없다
        var bang = new Lane { group = "Silence", name = "Bang" };
        bang.cues.Add(C(BangClip, BuildupEnd, BuildupEnd + BangTail + 0.4f, 1f, Align.Start, 1.40f, 0f, 0.6f));   // 원본 1.40초부터(1.50초가 쿵)
        lanes.Add(bang);

        // 머리가 크게 꺾이는 순간(머리 흔들기의 Head 각속도 상위 8)마다 짧은 「지직」 — 이미 컷신 시각이라 옮기지 않는다
        Lane flash = lanes.Find(l => l.name == "Static flashes");
        float[] jerks = HeadJerkTimes(root, 8);
        float[] staticIns = { 2.6f, 3.1f, 2.9f, 3.4f, 2.7f, 3.3f, 3.0f, 3.6f };
        for (int i = 0; flash != null && i < jerks.Length; i++)
        {
            flash.cues.Add(C("CAP-COM-05_2", jerks[i] - 0.02f, jerks[i] + 0.08f, 0.5f, Align.Start, staticIns[i % staticIns.Length]));
        }
        var own = new List<AudioSource>();
        var muffles = new List<AudioLowPassFilter>();
        int clipCount = 0;

        foreach (Lane lane in lanes)
        {
            Transform group = soundsGo.transform.Find(lane.group);
            if (group == null)
            {
                group = new GameObject(lane.group).transform;
                group.SetParent(soundsGo.transform, false);
            }
            var go = new GameObject(lane.name);
            go.transform.SetParent(group, false);
            AudioSource src = NewSource(go, lane.pan);
            if (lane.highPass > 0f) go.AddComponent<AudioHighPassFilter>().cutoffFrequency = lane.highPass;
            if (lane.lowPass > 0f) go.AddComponent<AudioLowPassFilter>().cutoffFrequency = lane.lowPass;
            if (lane.group == "Buildup") muffles.Add(go.AddComponent<AudioLowPassFilter>());
            own.Add(src);

            var track = timeline.CreateTrack<AudioTrack>(null, TrackPrefix + lane.group + " · " + lane.name);
            SetTrackProperties(track, lane.pan);
            director.SetGenericBinding(track, src);

            foreach (Cue cue in lane.cues)
            {
                AudioClip clip = FindClip(cue.clip);
                if (clip == null)
                {
                    if (!missing.Contains(cue.clip)) missing.Add(cue.clip);
                    continue;
                }
                if (Place(track, clip, cue)) clipCount++;
            }
        }

        // ── 복귀 소리(Timeline 밖 — 컷신이 끝나고 게임으로 돌아올 때) ──
        var returnGroup = new GameObject("Return").transform;
        returnGroup.SetParent(soundsGo.transform, false);
        var returns = new List<AudioSource>();
        foreach (var r in new[] { new { clip = "CAP-COM-09_2", vol = 0.8f }, new { clip = "RST-04_2", vol = 0.9f } })
        {
            AudioClip clip = FindClip(r.clip);
            if (clip == null) { missing.Add(r.clip); continue; }
            var go = new GameObject("Return " + r.clip);
            go.transform.SetParent(returnGroup, false);
            AudioSource src = NewSource(go, 0f);
            src.clip = clip;
            src.volume = r.vol;
            src.ignoreListenerPause = false;   // 게임 소리와 함께 돌아오는 소리 — 일시정지하면 같이 멈춘다
            returns.Add(src);
        }

        // ── 컷신 연결 ──
        if (cutscene != null)
        {
            var so = new SerializedObject(cutscene);
            SetArray(so.FindProperty("ownAudio"), own);
            SetArray(so.FindProperty("muffleFilters"), muffles);
            SetArray(so.FindProperty("returnAudio"), returns);
            so.FindProperty("muffleFrom").floatValue = MuffleFrom * BuildupScale;
            so.FindProperty("muffleTo").floatValue = MuffleTo * BuildupScale;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return "[DeathCutsceneSoundLayout] 소리 갈래 " + lanes.Count + " · 클립 " + clipCount + " · 복귀 " + returns.Count +
               (missing.Count > 0 ? " · <b>없는 클립: " + string.Join(", ", missing) + "</b>(파일을 넣고 메뉴를 다시 실행)" : " · 빠진 클립 없음");
    }

    // ─────────────────────────────── 시각표 ───────────────────────────────

    private static List<Lane> BuildLanes(GameObject root)
    {
        var lanes = new List<Lane>();
        Lane L(string group, string name, float pan = 0f, float hp = 0f, float lp = 0f)
        {
            var l = new Lane { group = group, name = name, pan = pan, highPass = hp, lowPass = lp };
            lanes.Add(l);
            return l;
        }

        // ── 1. 몸싸움 빌드업 (0.00~4.40) ──
        // 쾅쾅 — 0.2초(실제 초) 간격으로 빌드업 내내 두드린다(2026-10-05 사용자 요청). 소년 「머리 박기」(HeadThud)는
        // 0.08초에 바로 터지고 0.2초 안에 −20dB로 빠져 0.2초마다 반복해도 한 번 한 번 또렷하다.
        // 뒤로 갈수록 크게, 음높이·세기를 조금씩 흔들어 기계처럼 들리지 않게(고정 시드라 매번 같음).
        {
            const string bangName = "SFX_CLASS_HeadThud";
            const float interval = 0.2f;                                  // 컷신 초
            float stepDoc = interval / BuildupScale;                      // 배치 때 BuildupScale배 → 0.2초
            Lane bangs = L("Buildup", "Pounding");
            var rng = new System.Random(11);
            int count = Mathf.FloorToInt(SilenceAt / stepDoc);
            for (int i = 0; i < count; i++)
            {
                float k = i / (float)Mathf.Max(1, count - 1);
                float t = i * stepDoc;
                Cue cue = C(bangName, t, t + stepDoc, Mathf.Lerp(0.35f, 0.85f, k) * (0.85f + 0.15f * (float)rng.NextDouble()),
                            Align.Start, 0.06f, 0f, 0.03f);
                cue.speed = 0.94f + 0.12f * (float)rng.NextDouble();
                bangs.cues.Add(cue);
            }
        }

        // 의자 끼익 — 원본(4.4초)이 늘어난 빌드업보다 짧아, 끝나기 0.3초 전에 다음 판을 겹쳐 쾅 직전까지 반복한다(두 갈래 번갈아).
        {
            const string chairName = "CAP-C3-03_3";
            const float overlap = 0.3f;                                   // 컷신 초
            AudioClip chair = FindClip(chairName);
            float usable = chair != null ? chair.length - 0.23f : 4.4f;   // onset(0.24초)부터 끝까지
            float stepDoc = (usable - overlap) / BuildupScale;            // 문서 시각 간격(배치 때 BuildupScale배)
            Lane[] chairLanes = { L("Buildup", "Chair creak (behind)"), L("Buildup", "Chair creak 2 (behind)") };
            int n = 0;
            for (float t = 0f; t < SilenceAt - 0.05f && n < 8; t += stepDoc, n++)
            {
                chairLanes[n % 2].cues.Add(C(chairName, t, SilenceAt, 0.8f, Align.Onset, 0f, n == 0 ? 0f : overlap, overlap));
            }
        }

        // 금속 긁힘 두 겹 — 구간마다 속도를 올려 크기·음높이가 함께 오른다
        AddRamp(L("Buildup", "Metal scrape A"), "CAP-COM-03_1", 0f, SilenceAt, 0.3f, 0.85f, 1.0f, 1.45f);
        AddRamp(L("Buildup", "Metal scrape B"), "CAP-COM-03_3", 1.60f, SilenceAt, 0.2f, 0.7f, 1.05f, 1.6f);

        // 심박 13번: 0.39 → 4.42, 간격 0.46 → 0.20, 점점 크게. 박 하나(원본 2.14초 근처)를 잘라 쓴다.
        // 간격이 짧아 바로 뒤 박과 겹치므로 갈래 둘에 번갈아 놓는다.
        Lane hbA = L("Buildup", "Heartbeat A");
        Lane hbB = L("Buildup", "Heartbeat B");
        const int beats = 13;
        const float hbFirst = 0.39f, hbLast = 4.42f;
        float span = hbLast - hbFirst, raw = 0f;
        for (int i = 0; i < beats - 1; i++) raw += Mathf.Lerp(0.46f, 0.20f, i / (float)(beats - 2));
        float tBeat = hbFirst;
        for (int i = 0; i < beats; i++)
        {
            float k = i / (float)(beats - 1);
            float next = i < beats - 1 ? Mathf.Lerp(0.46f, 0.20f, i / (float)(beats - 2)) * span / raw : 0.3f;
            (i % 2 == 0 ? hbA : hbB).cues.Add(C("CAP-COM-08_1", tBeat, Mathf.Min(tBeat + Mathf.Min(0.42f, next + 0.15f), SilenceAt),Mathf.Lerp(0.45f, 1f, k), Align.Start, 2.06f, 0f, 0.05f));
            tBeat += next;
        }

        L("Buildup", "Child breath (left ear)", -0.85f).cues.Add(C("CAP-C3-02_2", 0.40f, 3.30f, 1f, Align.Start, 0f, 0.2f, 0.3f));

        // 시점이 꺾일 때마다 사물함 철컹 — 소리의 가장 큰 지점을 꺾이는 순간에
        Lane locker = L("Buildup", "Locker rattle");
        locker.cues.Add(C("SFX_HALL_LockerRattle_3", 1.70f, 2.40f, 0.65f, Align.Peak, 0.18f));
        locker.cues.Add(C("SFX_HALL_LockerRattle_1", 2.90f, 3.40f, 0.75f, Align.Peak, 0.06f));
        locker.cues.Add(C("SFX_HALL_LockerRattle_3", 3.50f, 4.10f, 0.9f, Align.Peak, 0.18f));

        // 속삭임 세 겹 — 「수업이 안 끝났는데 어디 가니」 녹음이 들어갈 자리
        L("Buildup", "Whisper L", -0.8f).cues.Add(C("CAP-C3-01_2", 2.00f, 4.08f, 1f, Align.Start, 0f, 0.25f, 0.1f));
        L("Buildup", "Whisper R", 0.8f).cues.Add(C("CAP-C3-01_1", 2.12f, 4.08f, 1f, Align.Start, 0f, 0.25f, 0.1f));
        L("Buildup", "Whisper C").cues.Add(C("CAP-C3-01_3", 2.24f, 4.08f, 1f, Align.Start, 0f, 0.25f, 0.1f));

        // 현악 상승 3.4초 — 가장 큰 지점이 4.10에 오게, 거기서 딱 끊는다
        L("Buildup", "String rise").cues.Add(C("CLX-02_3", 4.10f, 4.10f, 0.8f, Align.Peak, 3.415f));

        // ── 2. 빨려 듦 (3.60~4.40) — 거꾸로 감은 심벌의 끝(가장 큰 지점)이 4.40에 오게 ──
        L("Buildup", "Reverse suck").cues.Add(C("CLX-08_3", SilenceAt, SilenceAt, 0.9f, Align.Peak, SilenceAt - 3.60f));

        // ── 3. 히트 (4.667) — 음역을 나눠 서로 묻히지 않게 ──
        L("Hit", "Sub 0-100Hz", 0f, 0f, 100f).cues.Add(C("CLX-04_3", HitAt, CutAt, 1f));
        L("Hit", "Body 100Hz-2.5kHz", 0f, 100f, 2500f).cues.Add(C("CLX-01_3", HitAt, CutAt, 0.9f));
        L("Hit", "Strings 500Hz+", 0f, 500f).cues.Add(C("CLX-05_2", HitAt, CutAt, 0.6f, Align.Onset, 0f, 0f, 0.4f));
        L("Hit", "Scream 1.5kHz+", 0f, 1500f).cues.Add(C("CLX-03_3", HitAt, CutAt, 0.75f, Align.Onset, 0f, 0f, 0.3f));
        L("Hit", "Chalk 2kHz+", 0f, 2000f).cues.Add(C("CLX-07_3", HitAt, CutAt, 0.45f, Align.Onset, 0f, 0f, 0.3f));
        L("Hit", "Gasp").cues.Add(C("CAP-COM-09_3", HitAt + 0.01f, HitAt + 0.5f, 0.8f));
        L("Hit", "Glitch scream").cues.Add(C("CLX-06_2", HitAt + 0.20f, CutAt, 0.7f, Align.Onset, 0f, 0f, 0.3f));

        // ── 4. 얼굴 유지 (4.70~7.467) ──
        L("Hold", "Drone").cues.Add(C("CAP-COM-07_1", 4.70f, HoldEnd, 0.6f, Align.Start, 0f, 5.55f - 4.70f, 0f));
        L("Hold", "Glitch bed").cues.Add(C("CAP-COM-13_1", 4.70f, HoldEnd, 0.35f, Align.Start, 0f, 0.2f, 0f));
        L("Hold", "Tinnitus").cues.Add(C("CAP-COM-11_2", 4.70f, HoldEnd, 0.22f, Align.Start, 0f, 0.4f, 0f));

        L("Hold", "Static flashes");   // 「지직」은 머리 흔들기 순간을 재서 Apply에서 채운다

        L("Hold", "Whisper again (left ear)", -0.85f).cues.Add(C("CAP-C3-01_2", 6.20f - ScreamTrim, 7.30f - ScreamTrim, 1f, Align.Start, 0.5f, 0.15f, 0.2f));

        // ── 5. 얼굴 늘어남 (7.467~7.967) — 두 번째 정점 ──
        L("Stretch", "Scream tail").cues.Add(C("CLX-03_2", HoldEnd, CutAt, 0.75f, Align.Start, -0.5f));
        L("Stretch", "Chalk squeak").cues.Add(C("CLX-07_2", HoldEnd + 0.1f, CutAt, 0.6f, Align.Peak, 0.1f));
        L("Stretch", "Static burst").cues.Add(C("CAP-COM-05_2", HoldEnd + 0.15f, CutAt, 0.6f, Align.Peak, 0.15f));

        return lanes;
    }

    /// <summary>한 소리를 구간 여럿으로 잘라 뒤로 갈수록 빠르고(= 높고) 크게.</summary>
    private static void AddRamp(Lane lane, string clip, float from, float to, float vol0, float vol1, float speed0, float speed1)
    {
        const int steps = 5;
        float seg = (to - from) / steps;
        for (int i = 0; i < steps; i++)
        {
            float k = i / (float)(steps - 1);
            var cue = C(clip, from + seg * i, from + seg * (i + 1), Mathf.Lerp(vol0, vol1, k), Align.Start, -1f);   // -1 = 앞 구간에 이어서
            cue.speed = Mathf.Lerp(speed0, speed1, k);
            if (i == 0) cue.easeIn = Mathf.Min(0.3f, seg);
            lane.cues.Add(cue);
        }
    }

    // ─────────────────────────────── 배치 ───────────────────────────────

    private static readonly Dictionary<string, double> rampCursor = new Dictionary<string, double>();

    /// <summary>cue 하나를 트랙에 놓는다. 맞춤 지점(onset/peak)이 cue.at에 오도록 clipIn을 잡는다.</summary>
    private static bool Place(AudioTrack track, AudioClip clip, Cue cue)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        WavInfo info = WavInfo.Read(path);
        double clipIn, start;

        switch (cue.align)
        {
            case Align.Peak:
                // offset = 가장 큰 지점 앞에서 얼마나 일찍 시작할지
                clipIn = Math.Max(0.0, info.peak - cue.offset);
                start = cue.at - (info.peak - clipIn);
                break;
            case Align.Onset:
                clipIn = Math.Max(0.0, info.onset - 0.01);
                start = cue.at;
                break;
            default:
                string key = track.name + "|" + clip.name;
                if (cue.offset < 0f && cue.offset > -0.75f)        // 끝에서 거꾸로 셈(CLX-03_2 「끝부분」)
                {
                    clipIn = Math.Max(0.0, clip.length + cue.offset);
                }
                else if (cue.offset <= -1f)                         // 앞 구간에 이어서(속도 램프)
                {
                    clipIn = rampCursor.TryGetValue(key, out double c) ? c : Math.Max(0.0, info.onset - 0.01);
                }
                else
                {
                    clipIn = cue.offset;
                }
                start = cue.at;
                break;
        }

        double end = Math.Min(cue.until > cue.at ? cue.until : cue.at, CutAt + HitShift);
        if (start < 0) { clipIn -= start; start = 0; }
        double duration = Math.Min(end - start, (clip.length - clipIn) / cue.speed);
        if (duration <= 0.01) return false;

        TimelineClip tc = track.CreateClip(clip);
        tc.start = start;
        tc.clipIn = clipIn;
        tc.timeScale = cue.speed;
        tc.duration = duration;
        tc.easeInDuration = Math.Min(cue.easeIn, duration * 0.9);
        tc.easeOutDuration = Math.Min(cue.easeOut, duration - tc.easeInDuration);
        tc.displayName = clip.name;

        var asset = tc.asset as AudioPlayableAsset;
        if (asset != null)
        {
            var so = new SerializedObject(asset);
            SerializedProperty vol = so.FindProperty("m_ClipProperties.volume");
            if (vol != null) vol.floatValue = cue.volume;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        rampCursor[track.name + "|" + clip.name] = clipIn + duration * cue.speed;
        return true;
    }

    private static AudioSource NewSource(GameObject go, float pan)
    {
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;              // 귀 바로 옆 — 2D
        src.panStereo = pan;
        src.volume = 1f;
        src.ignoreListenerPause = true;     // 모든 소리를 끊은 뒤에도 컷신 소리는 들린다
        return src;
    }

    private static void SetTrackProperties(AudioTrack track, float pan)
    {
        var so = new SerializedObject(track);
        SerializedProperty p = so.FindProperty("m_TrackProperties.stereoPan");
        if (p != null) p.floatValue = pan;
        p = so.FindProperty("m_TrackProperties.spatialBlend");
        if (p != null) p.floatValue = 0f;
        p = so.FindProperty("m_TrackProperties.volume");
        if (p != null) p.floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray<T>(SerializedProperty prop, List<T> items) where T : UnityEngine.Object
    {
        if (prop == null) return;
        prop.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    /// <summary>파일 이름(확장자 없이)이 정확히 같은 오디오 클립을 프로젝트 전체에서 찾는다. 여럿이면 _Game/Audio를 먼저.</summary>
    private static AudioClip FindClip(string fileName)
    {
        string best = null;
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:AudioClip"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) != fileName) continue;
            if (best == null || p.StartsWith("Assets/_Game/Audio")) best = p;
        }
        return best != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(best) : null;
    }

    /// <summary>
    /// 소년 머리 흔들기에서 Head가 가장 빨리 꺾이는 순간들(컷신 시각). 서로 0.18초 이상 떨어진 것만.
    /// 소년 트랙(덮어쓰기 트랙은 부모의 바인딩을 쓴다)의 클립 중 Head가 가장 크게 움직이는 것을 흔들기로 본다 — idle은 2° 남짓이다.
    /// </summary>
    private static float[] HeadJerkTimes(GameObject root, int count)
    {
        var director = root.GetComponent<PlayableDirector>();
        var timeline = director.playableAsset as TimelineAsset;
        TimelineClip brrr = null;
        GameObject boy = null;
        float bestSpeed = 0f;
        // GetOutputTracks는 덮어쓰기(하위) 트랙을 부모 출력에 합쳐 돌려주지 않으므로 하위 트랙까지 직접 모은다.
        var tracks = new List<TrackAsset>();
        foreach (TrackAsset r in timeline.GetRootTracks())
        {
            tracks.Add(r);
            tracks.AddRange(r.GetChildTracks());
        }
        foreach (TrackAsset t in tracks)
        {
            if (!(t is AnimationTrack)) continue;
            var anim = director.GetGenericBinding(t) as Animator;
            if (anim == null && t.parent is TrackAsset parentTrack) anim = director.GetGenericBinding(parentTrack) as Animator;
            if (anim == null || anim.transform.Find("CamTarget") != null || FindChild(anim.transform, "Head") == null) continue;
            foreach (TimelineClip c in t.GetClips())
            {
                if (c.animationClip == null) continue;
                float speed = MaxHeadSpeed(anim.gameObject, c.animationClip);
                if (speed > bestSpeed) { bestSpeed = speed; brrr = c; boy = anim.gameObject; }
            }
        }
        if (brrr == null) return new float[0];

        GameObject copy = UnityEngine.Object.Instantiate(boy);
        copy.SetActive(true);
        Transform head = FindChild(copy.transform, "Head");
        var speeds = new List<KeyValuePair<float, float>>();
        Quaternion prev = Quaternion.identity;
        const float step = 1f / 60f;
        for (float t = 0f; t <= brrr.animationClip.length; t += step)
        {
            brrr.animationClip.SampleAnimation(copy, t);
            if (t > 0f) speeds.Add(new KeyValuePair<float, float>(t, Quaternion.Angle(prev, head.localRotation) / step));
            prev = head.localRotation;
        }
        UnityEngine.Object.DestroyImmediate(copy);

        speeds.Sort((a, b) => b.Value.CompareTo(a.Value));
        var picked = new List<float>();
        foreach (var s in speeds)
        {
            float at = (float)brrr.start + s.Key;
            if (at - HitShift > HoldEnd) continue;   // 얼굴 늘어남 앞까지만
            bool far = true;
            foreach (float p in picked) if (Mathf.Abs(p - at) < 0.18f) far = false;
            if (far) picked.Add(at);
            if (picked.Count >= count) break;
        }
        picked.Sort();
        return picked.ToArray();
    }

    private static float MaxHeadSpeed(GameObject boy, AnimationClip clip)
    {
        GameObject copy = UnityEngine.Object.Instantiate(boy);
        copy.SetActive(true);
        Transform head = FindChild(copy.transform, "Head");
        float max = 0f;
        Quaternion prev = head.localRotation;
        const float step = 1f / 30f;
        for (float t = 0f; t <= clip.length; t += step)
        {
            clip.SampleAnimation(copy, t);
            if (t > 0f) max = Mathf.Max(max, Quaternion.Angle(prev, head.localRotation) / step);
            prev = head.localRotation;
        }
        UnityEngine.Object.DestroyImmediate(copy);
        return max;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    /// <summary>WAV를 직접 읽어 소리가 시작되는 지점과 가장 큰 지점(10ms RMS)을 잰다. 읽지 못하면 둘 다 0.</summary>
    private struct WavInfo
    {
        public double onset;
        public double peak;

        public static WavInfo Read(string assetPath)
        {
            var info = new WavInfo();
            try
            {
                if (!assetPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return info;
                byte[] b = File.ReadAllBytes(assetPath);
                int pos = 12, sr = 0, ch = 1, bits = 16;
                while (pos + 8 <= b.Length)
                {
                    string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
                    int size = BitConverter.ToInt32(b, pos + 4);
                    if (id == "fmt ")
                    {
                        ch = BitConverter.ToInt16(b, pos + 10);
                        sr = BitConverter.ToInt32(b, pos + 12);
                        bits = BitConverter.ToInt16(b, pos + 22);
                    }
                    else if (id == "data" && sr > 0)
                    {
                        int bps = bits / 8;
                        int frames = Math.Min(size, b.Length - pos - 8) / (bps * ch);
                        int win = sr / 100;
                        var env = new List<float>();
                        float max = 0f;
                        int maxAt = 0;
                        for (int f0 = 0; f0 + win <= frames; f0 += win)
                        {
                            double sum = 0;
                            for (int f = f0; f < f0 + win; f++)
                            {
                                int q = pos + 8 + f * bps * ch;
                                float v = bits == 16 ? BitConverter.ToInt16(b, q) / 32768f
                                        : bits == 24 ? ((b[q] | (b[q + 1] << 8) | ((sbyte)b[q + 2] << 16)) / 8388608f)
                                        : BitConverter.ToSingle(b, q);
                                sum += v * v;
                            }
                            float rms = (float)Math.Sqrt(sum / win);
                            env.Add(rms);
                            if (rms > max) { max = rms; maxAt = env.Count - 1; }
                        }
                        int on = 0;
                        while (on < env.Count && env[on] < max * 0.1f) on++;
                        info.onset = on * 0.01;
                        info.peak = maxAt * 0.01;
                        break;
                    }
                    pos += 8 + size + (size & 1);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DeathCutsceneSoundLayout] WAV를 읽지 못했습니다: " + assetPath + " — " + e.Message);
            }
            return info;
        }
    }
}
