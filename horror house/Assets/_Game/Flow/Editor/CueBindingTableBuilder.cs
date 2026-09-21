using System.Collections.Generic;
using NightDuty;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 큐 바인딩 표(<see cref="CueBindingTableSO"/>) 에셋을 만들고 검수하는 에디터 도구.
///
/// <para><b>이 파일이 <c>Assets/_Game/Scripts/Editor/</c>가 아니라 <c>Assets/_Game/Flow/Editor/</c>에 있는 이유.</b>
/// <c>Assets/_Game/Scripts/Editor/</c>는 <c>NightDuty.Editor</c> asmdef 안이고, 그 어셈블리는 <c>NightDuty.Core</c>만 참조한다.
/// <b><c>Assembly-CSharp</c>을 볼 수 없다</b>(CLAUDE.md §5.1-3). 아래 「큐 바인딩 표 검사」는 씬의
/// <c>AnomalyCueDirector</c>·<c>SpaceZones</c>(둘 다 Flow = <c>Assembly-CSharp</c>)를 직접 들여다보므로
/// 그쪽에 두면 컴파일되지 않는다. <c>Flow/Editor/</c>는 asmdef가 없어 <c>Assembly-CSharp-Editor</c>에 들어가고,
/// 이 어셈블리는 <c>Assembly-CSharp</c>과 <c>NightDuty.Core</c>를 <b>둘 다</b> 본다.</para>
///
/// <para><b>표에 없는 것을 지어내지 않았다.</b> 큐 ID는 <c>SpaceAnomalyTable.asset</c> 실측값이고,
/// 판정 ID는 24장 카드 에셋의 <c>TriggerId</c>/<c>TargetIds</c> 실측값이다.
/// 기획서 H절에 시점 문장이 없는 줄은 <see cref="CueMoment.None"/>으로 두고 <c>Notes</c>에 이유를 적었다.</para>
/// </summary>
public static class CueBindingTableBuilder
{
    /// <summary>
    /// 만들 자리. <c>Resources</c>에 두는 이유는 <c>AnomalyCueDirector</c>가 인스펙터 참조가 비었을 때
    /// <c>Resources.Load</c>로 물러설 수 있게 하기 위해서다(<c>BandTable</c>·<c>NightDeckTable</c>과 같은 자리).
    /// </summary>
    private const string AssetPath = "Assets/_Game/Resources/CueBindingTable.asset";

    private const string AnomalyPath = "Assets/_Game/ScriptableObjects/SpaceAnomalyTable.asset";

    [MenuItem("NightDuty/큐 바인딩 표 에셋 생성", false, 40)]
    public static void Build()
    {
        CueBindingTableSO table = AssetDatabase.LoadAssetAtPath<CueBindingTableSO>(AssetPath);
        bool created = false;

        if (table == null)
        {
            table = ScriptableObject.CreateInstance<CueBindingTableSO>();
            string dir = System.IO.Path.GetDirectoryName(AssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Debug.LogError("[큐 바인딩] 폴더가 없습니다: " + dir);
                return;
            }

            AssetDatabase.CreateAsset(table, AssetPath);
            created = true;
        }

        table.SetBindings(BuildRows().ToArray());
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[큐 바인딩] " + (created ? "생성" : "갱신") + ": " + AssetPath +
                  " (" + table.Bindings.Count + "줄)");
        Selection.activeObject = table;
        Validate();
    }

    [MenuItem("NightDuty/검수/큐 바인딩 표 검사", false, 41)]
    public static void Validate()
    {
        CueBindingTableSO table = AssetDatabase.LoadAssetAtPath<CueBindingTableSO>(AssetPath);
        if (table == null)
        {
            Debug.LogWarning("[큐 바인딩] 표가 없습니다. 먼저 「NightDuty ▸ 큐 바인딩 표 에셋 생성」을 실행하십시오.");
            return;
        }

        SpaceAnomalyTableSO anomaly = AssetDatabase.LoadAssetAtPath<SpaceAnomalyTableSO>(AnomalyPath);
        List<string> errors = new List<string>();
        table.Validate(errors, anomaly);

        // ① 표 자체의 모순.
        for (int i = 0; i < errors.Count; i++)
        {
            Debug.LogWarning("[큐 바인딩] " + errors[i], table);
        }

        // ② 이상현상 표의 큐 중 바인딩 줄이 아예 없는 것. (빠뜨린 큐를 찾는다.)
        if (anomaly != null)
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (SpaceAnomalyTableSO.Cell c in anomaly.Cells)
            {
                if (c == null || c.Cues == null) continue;
                for (int i = 0; i < c.Cues.Length; i++)
                {
                    string key = c.Space + "|" + c.Cues[i];
                    if (!seen.Add(key)) continue;
                    if (table.Find(c.Cues[i], c.Space) == null)
                    {
                        Debug.LogWarning("[큐 바인딩] 바인딩 줄이 없는 큐: " + key +
                                         " — 연출 전용이면 Send=None 줄을 명시적으로 만드십시오(§2.7).", table);
                    }
                }
            }
        }

        // ③ 카드가 기다리는 판정 ID 중 아무도 보내 주지 않는 것.
        ReportUnservedCards(table);

        // ④ 씬 상태. 여기가 Flow/Editor여야 하는 이유다 — 아래 두 타입은 Assembly-CSharp에 있다.
        AnomalyCueDirector director = Object.FindAnyObjectByType<AnomalyCueDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            Debug.LogWarning("[큐 바인딩] 열린 씬에 AnomalyCueDirector가 없습니다. README 2절을 보십시오.");
        }

        SpaceZones zones = Object.FindAnyObjectByType<SpaceZones>(FindObjectsInactive.Include);
        if (zones == null)
        {
            Debug.LogWarning("[큐 바인딩] 열린 씬에 SpaceZones가 없습니다. 공간·구역·점검 시점 큐가 전부 죽습니다.");
        }

        Debug.Log("[큐 바인딩] 검사 완료. 경고 " + errors.Count + "건(표) + 위 목록.");
    }

    private static void ReportUnservedCards(CueBindingTableSO table)
    {
        HashSet<string> served = new HashSet<string>();
        foreach (CueBindingTableSO.Binding b in table.Bindings)
        {
            if (b != null && b.Sends)
            {
                served.Add(b.Send + "|" + b.JudgeId);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:NightDuty.RuleSO", new[] { "Assets/_Game/ScriptableObjects" });
        for (int i = 0; i < guids.Length; i++)
        {
            RuleSO card = AssetDatabase.LoadAssetAtPath<RuleSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (card == null) continue;

            CueSend need;
            if (card.TriggerKind == SignalKind.ClueDelivered) need = CueSend.ClueDelivered;
            else if (card.TriggerKind == SignalKind.ClueIdentified) need = CueSend.ClueIdentified;
            else continue;

            // 트리거 ID가 비어 있으면 카드 대상 목록 중 하나와 일치해야 한다(TargetMatch 규칙).
            List<string> wanted = new List<string>();
            if (!string.IsNullOrEmpty(card.TriggerId)) wanted.Add(card.TriggerId);
            else foreach (string t in card.TargetIds) wanted.Add(t);

            bool ok = false;
            for (int k = 0; k < wanted.Count; k++)
            {
                if (served.Contains(need + "|" + wanted[k])) { ok = true; break; }
            }

            if (!ok)
            {
                Debug.LogWarning("[큐 바인딩] " + card.CardId + "의 시작 신호 " + need + "(" +
                                 string.Join(" 또는 ", wanted) + ")를 보내는 줄이 없습니다.", card);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 표 본문
    // ────────────────────────────────────────────────────────────────────────

    private static List<CueBindingTableSO.Binding> BuildRows()
    {
        List<CueBindingTableSO.Binding> rows = new List<CueBindingTableSO.Binding>();

        // ═══ 복도 · 청각 ═══════════════════════════════════════════════════
        rows.Add(Mute("amb.base", SpaceId.Corridor, FearAxis.Auditory,
            "기본 환경음. 분위기 효과음은 카드 단서 ID를 보내지 않는다(§2.7)."));

        rows.Add(Mute("door.close.front", SpaceId.Corridor, FearAxis.Auditory,
            "앞쪽 지정 문 닫힘. 기획서 J절에 이 소리로 시작하는 카드가 없다. H2는 뒤쪽(door.close.back)이다."));

        CueBindingTableSO.Binding h2 = Clue("door.close.back", SpaceId.Corridor, FearAxis.Auditory,
            "corridor.door.back", CueMoment.OnPassageEntered, "H2");
        h2.Delivery = CueDelivery.InListenZone;
        h2.ZoneId = "corridor.passage";
        h2.DelaySeconds = 0f;   // 「앞쪽 닫힘 뒤」의 간격은 음원 발주값. 지금은 미정이라 0.
        h2.Notes = "기획서 H절 「앞쪽 닫힘 뒤 지나온 문 닫힘 1회」 → 통행 중. " +
                   "앞쪽 닫힘과의 간격(DelaySeconds)은 음원 발주값이라 지금은 0이다(빈칸).";
        rows.Add(h2);

        rows.Add(Mute("handle.back", SpaceId.Corridor, FearAxis.Auditory,
            "뒤쪽 닫힘 앞의 손잡이 소리. 대응 카드 없음 — 분위기 보강."));

        rows.Add(Mute("door.open.silent", SpaceId.Corridor, FearAxis.Auditory,
            "Band4의 무음 개방. H1의 자동 개방은 Band0의 door.auto_open이고 신호는 DoorRelay가 보낸다."));

        // ═══ 복도 · 배치 ═══════════════════════════════════════════════════
        rows.Add(Mute("door.auto_open", SpaceId.Corridor, FearAxis.Layout,
            "H1의 자동 개방. DoorAutoOpenObserved는 DoorRelay가 보낸다(「열렸다」가 아니라 「보았다」). " +
            "이 발신기는 연출 개방을 예약할 때 DoorRelay.BeginDirectionMove()를 먼저 불러 출처를 Direction으로 만든다."));

        rows.Add(Identify("box.center", SpaceId.Corridor, "corridor.box",
            new[] { "corridor.box" }, "corridor.passage", "H4",
            "H4는 장기 카드(AtNightEnd). 상자는 상태 큐라 방문 내내 식별을 감시한다."));

        rows.Add(Identify("debris.fall", SpaceId.Corridor, "corridor.debris",
            new[] { "corridor.debris" }, "corridor.passage", "H5",
            "Band3~4. 낙하 자체는 연출이고, 판정은 낙하 조각을 식별했을 때 시작한다."));

        rows.Add(Identify("tree.grass", SpaceId.Corridor, "corridor.tree",
            new[] { "corridor.tree" }, "corridor.passage", "H6",
            "Band4 전용. H6은 반경 2m(다른 카드는 1.5m)."));

        // ═══ 교실 1-1·1-3 · 청각 ═══════════════════════════════════════════
        CueBindingTableSO.Binding c1 = Clue("chalk.3", SpaceId.Classroom_1_1, FearAxis.Auditory,
            "cls11.chalk3", CueMoment.OnZoneEntered, "C1");
        c1.Repeat = CueRepeat.OncePerNight;     // 기획서 H절 「하루 1회」
        c1.Delivery = CueDelivery.InListenZone;
        c1.ZoneId = "cls11.door.outside";       // [제안] 씬에 아직 없다. README 3절.
        c1.ShotCount = 3;                       // 「정확히 세 번」 — 단발 3연타(§5.4-15)
        c1.ShotIntervalSeconds = 0.6f;          // [발주 대기] 임시값
        c1.Notes = "기획서 H절 「1-1 문밖에서 분필 3획, 하루 1회」. " +
                   "C1의 위반 조건이 SpaceEntered@1-1이라 반드시 1-1 '밖'에서 전달돼야 한다 — " +
                   "청취 구역 cls11.door.outside는 [제안] ID이며 씬 작업이 필요하다(README 3절). " +
                   "간격 0.6초는 임시값. 루프 클립이면 횟수를 셀 수 없다(§5.4-15).";
        rows.Add(c1);

        rows.Add(Mute("eraser", SpaceId.Classroom_1_1, FearAxis.Auditory,
            "실내 지우개 털기 1회. 대응 카드 없음."));

        CueBindingTableSO.Binding c3 = Clue("desk.hit", SpaceId.Classroom_1_1, FearAxis.Auditory,
            "cls11.desk.back", CueMoment.OnInspectionCompleted, "C3");
        c3.Delivery = CueDelivery.InListenZone;
        c3.Notes = "기획서 H절 「점검 후 퇴실 때 뒷줄 책상 타격음 1회」. " +
                   "신호 순서가 점검 완료 → 공간 이탈이므로 점검 완료 시점에 쏴야 아직 교실 안이다. " +
                   "C3 성공은 SpaceExited@1-1이라 곧바로 이어진다 — 금지 응시 3초는 퇴실 전에만 성립한다.";
        rows.Add(c3);

        rows.Add(Mute("board.scratch", SpaceId.Classroom_1_1, FearAxis.Auditory,
            "칠판 긁기. §2.7 「C-B 칠판음은 C4를 시작하지 않는다」 — 카드 단서 ID를 보내지 않는다. " +
            "C4를 여는 것은 교탁 의자 마찰(chair.scrape)이다."));

        CueBindingTableSO.Binding c4 = Clue("chair.scrape", SpaceId.Classroom_1_1, FearAxis.Auditory,
            "cls11.lectern.noise", CueMoment.OnSpaceEntered, "C4");
        c4.Visit = CueVisit.SeparateVisit;
        c4.Delivery = CueDelivery.InListenZone;
        c4.SequenceId = "cls11.lectern.noise";
        c4.SequenceSeconds = 0f;   // [발주 대기] 칠판 + 교탁 두 음원의 전체 길이. 빈칸.
        c4.Notes = "기획서 H절 「별도 방문의 입구에서 칠판 긁기 + 교탁 의자 마찰」. " +
                   "C4 성공은 AnyOf(SequenceEnded 'cls11.lectern.noise', SpaceExited)이므로 " +
                   "SequenceEnded를 보내는 유일한 줄이다. " +
                   "SequenceSeconds는 두 음원 전체 길이 = 미정(빈칸) — 0인 동안 C4는 퇴실로만 성공한다.";
        rows.Add(c4);

        rows.Add(Mute("chair.scrape.phrase", SpaceId.Classroom_1_1, FearAxis.Auditory,
            "Band4에서 교탁 마찰음 한 구절 추가. 같은 시퀀스의 일부라 신호는 chair.scrape 줄이 대표로 보낸다. " +
            "Band4에서는 chair.scrape의 SequenceSeconds가 더 길어져야 한다(발주 대기)."));

        // ═══ 교실 · 배치 ═══════════════════════════════════════════════════
        rows.Add(Mute("desks.normal", SpaceId.Classroom_1_1, FearAxis.Layout, "정상 배치 상태. 신호 없음."));

        rows.Add(Identify("desks.turned.one", SpaceId.Classroom_1_1, "cls11.desk.turned",
            new[] { "cls11.desk.turned" }, string.Empty, "C2",
            "Band1~4. C2는 「회전 좌석 1.5m 밖에서 점검」이라 식별은 멀리서 해야 한다 — " +
            "안전 관찰 지점을 구역으로 강제하지 않고 열어 둔다(기획서에 구역 지정이 없다)."));

        rows.Add(Mute("chairs.turned.row", SpaceId.Classroom_1_1, FearAxis.Layout,
            "Band3의 열 단위 방향 변경. 대응 카드 없음."));

        rows.Add(Mute("chairs.turned.others", SpaceId.Classroom_1_1, FearAxis.Layout,
            "Band4의 다른 열 방향 변경. 대응 카드 없음."));

        // ═══ 교실 · 조도 (표에 큐가 없다) ═══════════════════════════════════
        CueBindingTableSO.Binding c5 = Identify("bind.cls.lights", SpaceId.Classroom_1_1, "cls11.lights",
            new[] { "cls11.lights", "cls13.lights" }, string.Empty, "C5",
            "기획서 H절의 조도 칸에는 큐가 없다. C5의 시작 신호는 ClueIdentified(cls11.lights 또는 cls13.lights)인데 " +
            "기획서 J절·P11에 「무엇을 보면 식별인가」가 적혀 있지 않다 — 빈칸. " +
            "잠정안: 그 교실 천장등 묶음 표식을 0.2초 식별 = 「등이 줄어든 것을 알아본 순간」. 기획 확인 대기(README 4절).");
        c5.Axis = FearAxis.Illuminance;
        c5.FromAnomalyTable = false;
        c5.BandFrom = Band.Band2;   // 카드 에셋 실측: C5 = Illuminance Band2~Band4 (J절 「조도 50↑」)
        c5.BandTo = Band.Band4;
        c5.Delivery = CueDelivery.GazeIdentifyAny;   // 1-1이든 1-3이든 그 교실의 등 하나면 된다
        c5.SendIdentifiedTargetId = true;            // 1-1이면 cls11.lights, 1-3이면 cls13.lights를 그대로 보낸다
        c5.RequireLitCountMatch = true;              // §2.4
        rows.Add(c5);

        // ═══ 과학실 · 청각 ═════════════════════════════════════════════════
        CueBindingTableSO.Binding s2 = Clue("glass.break", SpaceId.ScienceRoom, FearAxis.Auditory,
            "science.glass.break", CueMoment.OnInspectionCompleted, "S2");
        s2.Delivery = CueDelivery.InListenZone;
        s2.Notes = "기획서 H절 「유효 점검 후 퇴실할 때 유리 파손음 1회」. Band0~4 전 구간에 있다. " +
                   "S2는 장기 카드이고 위반이 SpaceEntered@과학실(재진입 금지)이다.";
        rows.Add(s2);

        CueBindingTableSO.Binding s3 = Clue("glass.touch", SpaceId.ScienceRoom, FearAxis.Auditory,
            "science.bench.glass.clink", CueMoment.OnSpaceEntered, "S3");
        s3.Delivery = CueDelivery.InListenZone;
        s3.Notes = "기획서 H절은 「지정 유리 기구 접촉음 추가」라고만 적고 시점을 적지 않았다 — " +
                   "OnSpaceEntered는 잠정값이다(기획 확인 대기). " +
                   "S3 성공이 InspectionCompleted@과학실이라 점검 전에 전달돼야 한다는 점만은 확실하다.";
        rows.Add(s3);

        rows.Add(Mute("glass.scrape", SpaceId.ScienceRoom, FearAxis.Auditory,
            "Band2의 짧은 긁힘. 대응 카드 없음."));

        CueBindingTableSO.Binding s3u = Clue("glass.touch.under", SpaceId.ScienceRoom, FearAxis.Auditory,
            "science.bench.glass.clink", CueMoment.OnSpaceEntered, "S3");
        s3u.Delivery = CueDelivery.InListenZone;
        s3u.Notes = "Band3~4에서 glass.touch를 대신하는 같은 접촉음(위치만 실험대 아래로). 같은 판정 ID를 보낸다. " +
                    "한 방문에서 둘 다 무장되는 구간은 없다(표가 배타적으로 채워져 있다).";
        rows.Add(s3u);

        rows.Add(Mute("glass.scrape.under", SpaceId.ScienceRoom, FearAxis.Auditory,
            "Band3~4의 아래쪽 긁힘. 대응 카드 없음."));

        rows.Add(Mute("heavy.drop", SpaceId.ScienceRoom, FearAxis.Auditory,
            "Band4의 무거운 물체 놓는 소리. 대응 카드 없음."));

        // ═══ 과학실 · 배치 ═════════════════════════════════════════════════
        rows.Add(Mute("lab.normal", SpaceId.ScienceRoom, FearAxis.Layout,
            "실험대·기구 정상 배치. 신호 없음. " +
            "※ 이 칸은 기획서 H절(Band1 의자 하나 → Band4 반원 + 바깥 의자)과 어긋난 채 에셋에 들어 있다 — " +
            "표 수정은 이 작업의 범위가 아니다(다음작업_결정 §7)."));

        // ═══ 과학실 · 조도 (표에 큐가 없다) ═════════════════════════════════
        CueBindingTableSO.Binding s4 = Identify("bind.science.light.last", SpaceId.ScienceRoom, "science.light.last",
            new[] { "science.light.last" }, string.Empty, "S4",
            "기획서 P15 「조도 75~89, 남은 등을 식별한 직후」 → 식별 대상은 '남은(켜져 있는) 등'이다. " +
            "식별 0.2초는 S4의 금지 응시 2초에 포함하지 않는다(§2.6) — 코어의 GazeCondition이 카드 시작 뒤 따로 센다.");
        s4.Axis = FearAxis.Illuminance;
        s4.FromAnomalyTable = false;
        s4.BandFrom = Band.Band3;   // 카드 에셋 실측: S4 = Illuminance Band3~Band3 (90~99는 대상이 없어 미배정)
        s4.BandTo = Band.Band3;
        s4.RequireLitCountMatch = true;
        rows.Add(s4);

        // ═══ 화장실 · 청각 ═════════════════════════════════════════════════
        rows.Add(Mute("amb.base", SpaceId.Toilet, FearAxis.Auditory, "기본 환경음. 신호 없음(§2.7)."));

        CueBindingTableSO.Binding t2 = Clue("flush.inner", SpaceId.Toilet, FearAxis.Auditory,
            "toilet.flush", CueMoment.OnSpaceEntered, "T2");
        t2.Delivery = CueDelivery.InListenZone;
        t2.SequenceSeconds = 12f;   // 클립 길이 = 판정값(§5.4-16)
        t2.SequenceId = string.Empty;
        t2.Notes = "기획서 H절 「안쪽 칸 물 내림 1회」 · P19 「물 내림이 시작될 때」 → 전달은 시퀀스 '시작'에 보낸다. " +
                   "SequenceEnded는 보내지 않는다 — T2의 실패는 ElapsedCondition(12)이라 코어의 Tick이 직접 센다(실측 확인). " +
                   "SequenceSeconds 12는 음원 발주 길이이며 판정값과 같은 수여야 한다(§5.4-16). 둘이 어긋나면 화면과 판정이 갈라진다.";
        rows.Add(t2);

        CueBindingTableSO.Binding t4 = Clue("call.worker", SpaceId.Toilet, FearAxis.Auditory,
            "toilet.sink.call", CueMoment.OnSpaceEntered, "T4");
        t4.Visit = CueVisit.SeparateVisit;
        t4.Delivery = CueDelivery.InListenZone;
        t4.Notes = "기획서 H절 「별도 방문에서 세면대 쪽 '근무자님' 호칭」. Band2 칸에만 있다.";
        rows.Add(t4);

        rows.Add(Mute("handle.after_flush", SpaceId.Toilet, FearAxis.Auditory,
            "물 내림 끝의 손잡이 소리. 대응 카드 없음."));

        rows.Add(Mute("breath.closed_stall", SpaceId.Toilet, FearAxis.Auditory,
            "Band4의 들숨. 닫힌 칸 음원 대상이 없으면 생략. 대응 카드 없음."));

        // ═══ 화장실 · 배치 ═════════════════════════════════════════════════
        rows.Add(Mute("stalls.base", SpaceId.Toilet, FearAxis.Layout, "칸 기본 상태. 신호 없음."));

        rows.Add(Mute("stall.entry.auto_open", SpaceId.Toilet, FearAxis.Layout,
            "T1의 칸 문 자동 개방. DoorAutoOpenObserved(toilet.stall.outer)는 DoorRelay가 보낸다. " +
            "연출 개방 전에 DoorRelay.BeginDirectionMove()를 먼저 부른다."));

        rows.Add(Mute("stall.inner.open", SpaceId.Toilet, FearAxis.Layout,
            "안쪽 칸이 이미 열린 상태. 단독으로는 신호가 없다 — T6은 두 칸이 모두 열린 구간에서만 성립하므로 " +
            "stall.entry.open 줄이 두 큐를 함께 본다."));

        CueBindingTableSO.Binding t6 = Identify("stall.entry.open", SpaceId.Toilet, "toilet.stalls.bothopen",
            new[] { "toilet.stall.outer", "toilet.stall.inner" }, string.Empty, "T6",
            "기획서 H절 Band3~4 「개폐형 점검칸 두 곳 모두 열린 상태」. " +
            "판정 ID(toilet.stalls.bothopen)와 식별 대상(두 칸 문)이 다른 유일한 줄이다 — " +
            "바인딩 표가 있어야 표현되는 모양이고, toilet.stalls.bothopen에 콜라이더가 필요 없다는 씬 지침과도 맞는다. " +
            "잠정안: 칸 두 개를 '각각' 0.2초 식별(GazeIdentifyAll). " +
            "한 화면에 둘이 함께 들어오면 성립으로 볼지는 기획 확인 대기(README 4절).");
        t6.Delivery = CueDelivery.GazeIdentifyAll;
        t6.RequiresCues = new[] { "stall.inner.open" };
        rows.Add(t6);

        // ═══ 화장실 · 조도 (표에 큐가 없다) ═════════════════════════════════
        CueBindingTableSO.Binding t5 = Identify("bind.toilet.stall.light", SpaceId.Toilet, "toilet.stall.light",
            new[] { "toilet.stall.light" }, string.Empty, "T5",
            "기획서 P22 「조도 50 이상, 새는 빛을 식별한 직후」 → 식별 대상은 '닫힌 칸 아래로 새는 빛'이다. " +
            "씬 배치 지침의 toilet.stall.light 자리(닫힌 칸 아래)와 일치한다.");
        t5.Axis = FearAxis.Illuminance;
        t5.FromAnomalyTable = false;
        t5.BandFrom = Band.Band2;   // 카드 에셋 실측: T5 = Illuminance Band2~Band4 (J절 「조도 50↑」)
        t5.BandTo = Band.Band4;
        t5.RequireLitCountMatch = true;
        rows.Add(t5);

        return rows;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 줄 만들기 도우미
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>연출 전용 줄. 카드 단서 ID를 보내지 않는다는 것을 <b>명시</b>한다(§2.7).</summary>
    private static CueBindingTableSO.Binding Mute(string cueId, SpaceId space, FearAxis axis, string notes)
    {
        return new CueBindingTableSO.Binding
        {
            CueId = cueId,
            Space = space,
            Axis = axis,
            Send = CueSend.None,
            Moment = CueMoment.None,
            Notes = notes
        };
    }

    /// <summary>청각 단서 줄.</summary>
    private static CueBindingTableSO.Binding Clue(string cueId, SpaceId space, FearAxis axis,
        string judgeId, CueMoment moment, string card)
    {
        return new CueBindingTableSO.Binding
        {
            CueId = cueId,
            Space = space,
            Axis = axis,
            Send = CueSend.ClueDelivered,
            JudgeId = judgeId,
            Moment = moment,
            Delivery = CueDelivery.Immediate,
            Repeat = CueRepeat.OncePerVisit,
            Cards = new[] { card }
        };
    }

    /// <summary>시각 단서(식별 0.2초) 줄.</summary>
    private static CueBindingTableSO.Binding Identify(string cueId, SpaceId space, string judgeId,
        string[] identifyTargets, string zoneId, string card, string notes)
    {
        return new CueBindingTableSO.Binding
        {
            CueId = cueId,
            Space = space,
            Axis = FearAxis.Layout,
            Send = CueSend.ClueIdentified,
            JudgeId = judgeId,
            Moment = CueMoment.WhileInSpace,
            Delivery = CueDelivery.GazeIdentifyAny,
            Repeat = CueRepeat.OncePerVisit,
            IdentifyTargetIds = identifyTargets,
            IdentifySeconds = 0.2f,
            ZoneId = zoneId,
            Cards = new[] { card },
            Notes = notes
        };
    }
}
