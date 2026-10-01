using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>조우가 시작될 수 있는 플레이어 상태(기획서 「발동 조건」의 장소 부분).</summary>
    public enum EncounterTrigger
    {
        /// <summary>그 공간에 들어가는 순간.</summary>
        EnterSpace = 0,

        /// <summary>그 공간에 <see cref="EncounterScript.Dwell"/>초 머문 뒤.</summary>
        DwellInSpace = 1,

        /// <summary>복도를 <see cref="EncounterScript.Dwell"/>m 이상 걷는 중(달려오는 발소리).</summary>
        CorridorWalk = 2,

        /// <summary>CCTV를 <see cref="EncounterScript.Dwell"/>초 보고 있을 때.</summary>
        ViewingCctv = 3
    }

    /// <summary>
    /// 조우 한 개의 대본(2026-10-01, 7단계). 방아쇠·전조·대면·대응 창·끝 단서를 정한다. 단서 이름은 <see cref="FinalCues"/>와 같다.
    /// 「안 볼 때 순간이동」 몹이라 움직임은 없고, 대역 자리(<see cref="Placement"/>)만 정한다.
    /// </summary>
    public sealed class EncounterScript
    {
        /// <summary>조우 ID.</summary>
        public string Id;

        /// <summary>방아쇠.</summary>
        public EncounterTrigger Trigger;

        /// <summary>방아쇠 공간(<see cref="EncounterTrigger.CorridorWalk"/>는 복도, CCTV는 경비실).</summary>
        public SpaceId Space;

        /// <summary>머무름(초) 또는 걸은 거리(m).</summary>
        public float Dwell;

        /// <summary>대면 때 판정 책에 넣는 단서. 비면 판정 단서 없음(사람 나무 — 근접 기준점만).</summary>
        public string Cue = string.Empty;

        /// <summary>
        /// 대응 창이 닫힐 때 <b>시작</b> 신호로 보낼 단서(종소리 — C3는 종이 울릴 때까지). 비면 <see cref="Cue"/>의 끝 신호만 보낸다.
        /// </summary>
        public string ReleaseCue = string.Empty;

        /// <summary>대응 창(초). 음수면 슬롯이 끝날 때까지 머무는 존재형(사람 나무·천장 다리·없던 문).</summary>
        public float Window = 10f;

        /// <summary>끝 단서 뒤 결과까지(초). 발소리(H3)는 끝나고 2초 더 방에 머물러야 한다.</summary>
        public float After;

        /// <summary>대역·소리 자리.</summary>
        public CuePlacement Placement;

        /// <summary>자리 거리(m).</summary>
        public float Distance;

        /// <summary>대역 ID(<c>mob.boy</c> — <c>Resources/StandIns/&lt;ID&gt;</c>가 있으면 그 프리팹, 없으면 대역). 비면 대역 없음.</summary>
        public string StandIn = string.Empty;

        /// <summary>대역에 붙일 판정 기준점 ID(<c>rule.L3.face</c>). 비면 없음.</summary>
        public string AnchorId = string.Empty;

        /// <summary>소등할 공간(None이면 소등 없음).</summary>
        public SpaceId LightsOff = SpaceId.None;

        /// <summary>대면 동안 열리는 신호 구역(<c>science.dark</c> — 소등한 방 전체, <c>classroom.phantomdoor</c> — 없던 문 앞). 비면 없음.</summary>
        public string Zone = string.Empty;

        /// <summary>함께 세우는 두 번째 대역(소년 머리 박기의 천장 다리). 비면 없음. 자리는 플레이어 앞 천장.</summary>
        public string ExtraStandIn = string.Empty;

        /// <summary>두 번째 대역의 기준점 ID.</summary>
        public string ExtraAnchorId = string.Empty;

        /// <summary>대면 때 함께 보내는 두 번째 단서(끝 단서도 같이). 비면 없음.</summary>
        public string ExtraCue = string.Empty;

        /// <summary>
        /// 고정 자리 ID(씬의 <c>StageAnchor</c>). 등록돼 있으면 <see cref="Placement"/> 대신 그 자리·방향에 세운다
        /// (2026-10-01 민 지정: 소년 = 1-3 교실 맨 뒤 줄 오른쪽에서 둘째 책상, 천장 다리 = 뒤 통로 너머 창고 천장, 창밖 남자 = 도서관 북쪽 창 밖). 비면 없음.
        /// </summary>
        public string StageAnchor = string.Empty;

        /// <summary>두 번째 대역의 고정 자리 ID(소년 머리 박기의 천장 다리). 비면 플레이어 앞 천장.</summary>
        public string ExtraStageAnchor = string.Empty;

        /// <summary>
        /// 방아쇠를 이 방(정확한 <see cref="SpaceId"/>)으로만 좁힌다. 고정 자리가 한 교실에 있으면 다른 교실에서 걸리면 안 된다. None이면 <see cref="Space"/>(정규화)만 본다.
        /// </summary>
        public SpaceId ExactSpace = SpaceId.None;

        /// <summary>메모(왜 이렇게 정했는지).</summary>
        public string Note = string.Empty;

        /// <summary>존재형(슬롯 끝까지 머묾)인지.</summary>
        public bool IsPresence
        {
            get { return Window < 0f; }
        }
    }

    /// <summary>씬의 고정 연출 자리 ID(<c>StageAnchor</c> 오브젝트의 ID와 같다).</summary>
    public static class StageAnchors
    {
        /// <summary>앉은 소년 — 1-3 교실(Classroom02) 맨 뒤 줄, 학생 기준 오른쪽에서 둘째 책상 의자.</summary>
        public const string BoySeat = "stage.boy.seat";

        /// <summary>천장 다리 — 1-3 교실 뒤 통로 너머 창고 천장(사다리 바로 위).</summary>
        public const string LegsCeiling = "stage.legs.ceiling";

        /// <summary>창밖 남자 — 도서관 북쪽 창 밖.</summary>
        public const string WindowMan = "stage.window.man";
    }

    /// <summary>조우 15개의 대본 표. 수치는 이 파일 한 곳.</summary>
    public static class EncounterScripts
    {
        private static readonly Dictionary<string, EncounterScript> s_byId = Build();

        /// <summary>모든 대본.</summary>
        public static IEnumerable<EncounterScript> All
        {
            get { return s_byId.Values; }
        }

        /// <summary>그 조우의 대본. 없으면 null.</summary>
        public static EncounterScript Find(string encounterId)
        {
            EncounterScript s;
            return encounterId != null && s_byId.TryGetValue(encounterId, out s) ? s : null;
        }

        private static Dictionary<string, EncounterScript> Build()
        {
            List<EncounterScript> list = new List<EncounterScript>
            {
                new EncounterScript
                {
                    Id = ProgramCatalog.BoySeated, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.Classroom, Dwell = 3f,
                    Cue = FinalCues.BoySeated, ReleaseCue = FinalCues.Bell, Window = 15f,
                    Placement = CuePlacement.AheadOfPlayer, Distance = 3f, StandIn = "mob.boy",
                    StageAnchor = StageAnchors.BoySeat, ExactSpace = SpaceId.Classroom_1_3,
                    Note = "기획서: 교실 점검 항목을 처음 비출 때. 비춤 대신 교실 3초 체류로 시작한다(점검 대상이 교실 깊숙이 있어 거의 같은 순간)."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.BoyBang, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.Classroom, Dwell = 3f,
                    Cue = FinalCues.BoySeated, ReleaseCue = FinalCues.Bell, Window = 18f,
                    Placement = CuePlacement.AheadOfPlayer, Distance = 3f, StandIn = "mob.boy",
                    ExtraStandIn = "mob.legs", ExtraAnchorId = FinalCues.LegsTarget, ExtraCue = FinalCues.Legs,
                    StageAnchor = StageAnchors.BoySeat, ExtraStageAnchor = StageAnchors.LegsCeiling, ExactSpace = SpaceId.Classroom_1_3,
                    Note = "교차(청각 2 + 배치 2). 소년과 천장 다리가 함께 — C2(다리 3초 응시)를 어기면 머리 박기 소리가 복도까지."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.ToiletGirl, Trigger = EncounterTrigger.EnterSpace, Space = SpaceId.Toilet,
                    Cue = FinalCues.GirlStall, Window = 4f,
                    Placement = CuePlacement.AheadOfPlayer, Distance = 3f, StandIn = "mob.girl",
                    Note = "칸으로 들어가는 것을 보여 주고 역보고(T-1)를 건다."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.Footsteps, Trigger = EncounterTrigger.CorridorWalk, Space = SpaceId.Corridor, Dwell = 10f,
                    Cue = FinalCues.Footsteps, Window = 8f, After = 2.2f,
                    Placement = CuePlacement.BehindPlayer, Distance = 12f,
                    Note = "H3: 6초 안에 방으로, 끝나고 2초 더."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.CallingVoice, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.Corridor, Dwell = 5f,
                    Cue = FinalCues.Voice, Window = 6f,
                    Placement = CuePlacement.BehindPlayer, Distance = 6f,
                    Note = "H4: 소리 쪽으로 돌아보면 위반."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.YellowFace, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.Library, Dwell = 8f,
                    Cue = FinalCues.YellowFace, Window = 8f,
                    Placement = CuePlacement.AheadOfPlayer, Distance = 4f, StandIn = "mob.duck", AnchorId = FinalCues.FaceTarget,
                    Note = "기획서: 도서관 점검 2개 뒤 출입구를 등질 때. 우선 도서관 8초 체류."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.HallEndFigure, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.ScienceRoom, Dwell = 10f,
                    Cue = FinalCues.HallEnd, Window = 10f,
                    Placement = CuePlacement.AheadOfPlayer, Distance = 7f, StandIn = "mob.dummy.stand",
                    Note = "기획서 방아쇠는 「과학실 퇴실」이지만 S5는 과학실 안에서 판정한다 — 과학실 10초 체류 뒤 문밖에 선다."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.ModelRush, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.ScienceRoom, Dwell = 10f,
                    Cue = FinalCues.HallEnd, Window = 10f,
                    Placement = CuePlacement.AheadOfPlayer, Distance = 5f, StandIn = "mob.dummy",
                    Note = "S5 — 지키면 암전 속에서 스쳐 지나감."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.ScienceBlackout, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.ScienceRoom, Dwell = 5f,
                    Cue = FinalCues.ScienceBlackout, Window = 12f, LightsOff = SpaceId.ScienceRoom, Zone = FinalCues.ScienceDarkZone,
                    Note = "S4: 소등은 금지 — 어두운 구역(과학실 전체)에서 손전등을 켜 둔다."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.ToiletBlackout, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.Toilet, Dwell = 4f,
                    Cue = FinalCues.ToiletBlackout, Window = 10f, LightsOff = SpaceId.Toilet,
                    Note = "T3: 불을 끄고 기다린다."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.PeopleTree, Trigger = EncounterTrigger.EnterSpace, Space = SpaceId.Corridor,
                    Window = -1f, Placement = CuePlacement.AheadOfPlayer, Distance = 8f, StandIn = "mob.tree", AnchorId = FinalCues.H1Object,
                    Note = "존재형. H1은 근접 기준점만 본다(단서 없음)."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.CeilingLegs, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.Classroom, Dwell = 2f,
                    Cue = FinalCues.Legs, Window = -1f,
                    Placement = CuePlacement.CeilingAhead, Distance = 3f, StandIn = "mob.legs", AnchorId = FinalCues.LegsTarget,
                    StageAnchor = StageAnchors.LegsCeiling, ExactSpace = SpaceId.Classroom_1_3,
                    Note = "존재형. C2: 3초 응시하면 위반."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.PhantomDoor, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.Classroom, Dwell = 3f,
                    Cue = FinalCues.PhantomDoor, Window = -1f,
                    Placement = CuePlacement.AheadOfPlayer, Distance = 3f, StandIn = "prop.phantomdoor", Zone = FinalCues.PhantomDoorZone,
                    Note = "존재형. C5: 없던 문 앞 구역에 들어가면 위반."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.SuitMan, Trigger = EncounterTrigger.DwellInSpace, Space = SpaceId.Library, Dwell = 6f,
                    Cue = FinalCues.WindowKnock, Window = 10f,
                    Placement = CuePlacement.AheadOfPlayer, Distance = 5f, StandIn = "mob.windowman", AnchorId = FinalCues.ManTarget,
                    StageAnchor = StageAnchors.WindowMan,
                    Note = "창 두드림으로 시작. L5: 먼저 인사(비춤·2초 응시)하지 않는다. 도서관 북쪽 창(WallOutside_4m_WindowDouble) 밖 — 모델은 DUCK(민 지정)."
                },
                new EncounterScript
                {
                    Id = ProgramCatalog.CctvPerson, Trigger = EncounterTrigger.ViewingCctv, Space = SpaceId.SecurityRoom, Dwell = 2f,
                    Cue = FinalCues.CctvPerson, Window = 10f,
                    Note = "K1: 지나갈 때까지 채널을 넘기지 않는다. 화면에만 보인다(CctvOnlyVisible)."
                }
            };

            Dictionary<string, EncounterScript> map = new Dictionary<string, EncounterScript>(StringComparer.Ordinal);
            foreach (EncounterScript s in list) map[s.Id] = s;
            return map;
        }
    }

    /// <summary>조우에 묶이지 않은 수칙의 단서 대본(분필·물 내림 등). 그 공간에 머물면 그 밤 한 번 울린다.</summary>
    public sealed class RuleTriggerScript
    {
        /// <summary>수칙 ID.</summary>
        public string RuleId;

        /// <summary>단서 ID. <c>@</c> 뒤 대상이 필요하면 <see cref="Target"/>.</summary>
        public string Cue;

        /// <summary>단서 뒤에 붙일 대상(<c>toilet.stall.inner.inside</c>). <c>cctv</c>면 디렉터가 다른 채널을 고른다.</summary>
        public string Target = string.Empty;

        /// <summary>조건 공간.</summary>
        public SpaceId Space;

        /// <summary>그 공간이 아니라 이 신호 구역에 들어갈 때(C1 — 교실 문 밖). 비면 공간 체류.</summary>
        public string Zone = string.Empty;

        /// <summary>CCTV를 보는 중일 때(K2).</summary>
        public bool WhileViewingCctv;

        /// <summary>머무름 최소~최대(초) — 밤마다 그 사이에서 하나를 뽑는다.</summary>
        public float DwellMin = 4f;

        /// <summary>머무름 최대(초).</summary>
        public float DwellMax = 10f;

        /// <summary>단서 길이(초). 0이면 순간(시작·끝을 바로 이어 보낸다).</summary>
        public float Duration;

        /// <summary>단서 길이 대신 그 공간을 나갈 때 끝낸다(T5 — 나갈 때까지 불을 켜 둔다).</summary>
        public bool EndOnExit;
    }

    /// <summary>수칙 단서 대본 표.</summary>
    public static class RuleTriggers
    {
        /// <summary>CCTV 채널 수(0~4). 연출 쪽 채널 ID는 <c>cctv.ch&lt;n&gt;</c>.</summary>
        public const int CctvChannels = 5;

        private static readonly Dictionary<string, RuleTriggerScript> s_byRule = Build();

        /// <summary>모든 대본.</summary>
        public static IEnumerable<RuleTriggerScript> All
        {
            get { return s_byRule.Values; }
        }

        /// <summary>그 수칙의 대본. 없으면 null(단서가 필요 없는 수칙 — 근접·통로·달리기·체류·조우 묶음).</summary>
        public static RuleTriggerScript Find(string ruleId)
        {
            RuleTriggerScript s;
            return ruleId != null && s_byRule.TryGetValue(ruleId, out s) ? s : null;
        }

        private static Dictionary<string, RuleTriggerScript> Build()
        {
            List<RuleTriggerScript> list = new List<RuleTriggerScript>
            {
                new RuleTriggerScript { RuleId = "H2", Cue = "cue.door.autoopen", Space = SpaceId.Corridor, DwellMin = 8f, DwellMax = 20f },
                new RuleTriggerScript { RuleId = "C1", Cue = FinalCues.Chalk, Space = SpaceId.Corridor, Zone = "cls11.door.outside", DwellMin = 0.5f, DwellMax = 1.5f, Duration = 5f },
                new RuleTriggerScript { RuleId = "C4", Cue = FinalCues.RedLight, Space = SpaceId.Classroom, DwellMin = 5f, DwellMax = 12f, Duration = 15f },
                new RuleTriggerScript { RuleId = "S2", Cue = FinalCues.Glass, Space = SpaceId.ScienceRoom, DwellMin = 5f, DwellMax = 12f },
                new RuleTriggerScript { RuleId = "T1", Cue = FinalCues.Flush, Space = SpaceId.Toilet, DwellMin = 3f, DwellMax = 8f, Duration = 8f },
                new RuleTriggerScript { RuleId = "T2", Cue = FinalCues.StallOccupied, Target = "toilet.stall.inner.inside", Space = SpaceId.Toilet, DwellMin = 1f, DwellMax = 4f, Duration = 20f },
                new RuleTriggerScript { RuleId = "T5", Cue = FinalCues.StallLit, Space = SpaceId.Toilet, DwellMin = 4f, DwellMax = 9f, EndOnExit = true },
                new RuleTriggerScript { RuleId = "L2", Cue = FinalCues.Pages, Space = SpaceId.Library, DwellMin = 6f, DwellMax = 14f, Duration = 3f },
                new RuleTriggerScript { RuleId = "K2", Cue = FinalCues.EmptyRoom, Target = "cctv", Space = SpaceId.SecurityRoom, WhileViewingCctv = true, DwellMin = 2f, DwellMax = 5f, Duration = 15f }
            };

            Dictionary<string, RuleTriggerScript> map = new Dictionary<string, RuleTriggerScript>(StringComparer.Ordinal);
            foreach (RuleTriggerScript s in list) map[s.RuleId] = s;
            return map;
        }
    }
}
