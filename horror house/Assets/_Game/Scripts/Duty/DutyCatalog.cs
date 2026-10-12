using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>[근무 지시]의 종류(2026-10-07 3차 회의 1차 묶음, 54차).</summary>
    public enum DutyKind
    {
        /// <summary>W1 — CCTV 채널을 모두 2초 이상 본다.</summary>
        CctvSweep = 0,

        /// <summary>W2 — 도서관 숨은 지점 네 곳을 지나 나온다.</summary>
        Patrol = 1,

        /// <summary>W3 「교실의 상태를 확인해주십시오.」 — 1-3 교실 안에 <see cref="DutyCatalog.ClassroomCheckSeconds"/>초 머무른다(88차 — 옛: 문간에서 천장 등 1초 응시).</summary>
        LightsOutCheck = 2,

        /// <summary>W4 — 플레이어가 열어 둔 문을 모두 닫는다.</summary>
        DoorTidy = 3,

        /// <summary>W5 — 이완 구간에 근무일지에 서명한다.</summary>
        LogSign = 4,

        /// <summary>W6 — 복도 끝 유도등을 1초 보고 경비실로 돌아온다.</summary>
        ExitSignCheck = 5,

        /// <summary>57차 — 비품 하나(<see cref="DutyDef.Target"/>, 그날 점검에 없는 점검 대상)를 1초 본다. 보고는 없다.</summary>
        GazeCheck = 6,

        /// <summary>65차 — 물건 하나(<see cref="DutyDef.Target"/>)를 [E]로 줍는다(민: 「반납 상자 → 도서관 안쪽 책상의 biology 책 습득, 클릭해서 습득하면 완료」).</summary>
        Pickup = 7,

        /// <summary>80차 W21 — 복도 끝 비상구 앞(<see cref="DutyCatalog.ExitZone"/>)에서 손전등을 세 번 깜빡인다(<see cref="DutyCatalog.FlashWindowSeconds"/>초 안에 켜기 세 번).</summary>
        ExitFlash = 8,

        /// <summary>80차 W19 — 경비실 전화가 울리면 받는다(<see cref="SignalKind.PhoneAnswered"/>). 지시를 받으면 잠시 뒤 전화가 울린다(연출 <c>PhoneDuty</c>).</summary>
        PhoneAnswer = 9,

        /// <summary>80차 W16·W17 — 출입 카드(<see cref="DutyDef.Target"/>)를 [E]로 집어 경비실 열쇠함(LibraryCardCatalogB의 열린 서랍)에 [E]로 넣는다(연출 <c>DutyCards</c>).</summary>
        CardDelivery = 10
    }

    /// <summary>[근무 지시] 한 종류의 정의. 문구는 「~바랍니다」(수칙 「하십시오」 · 점검 「있습니다」 · 역설 「지금 하십시오」와 갈린다).</summary>
    public sealed class DutyDef
    {
        /// <summary>ID(W1~W6).</summary>
        public readonly string Id;

        /// <summary>종류.</summary>
        public readonly DutyKind Kind;

        /// <summary>일하는 공간(거리 계산·문자 공간).</summary>
        public readonly SpaceId Space;

        /// <summary>완료하면 내려가는 축(맞물린 수칙의 축).</summary>
        public readonly FearAxis Axis;

        /// <summary>지시 본문(머리글 뒤).</summary>
        public readonly string Order;

        /// <summary>완료 답장(뒤에 시각이 붙는다).</summary>
        public readonly string Done;

        /// <summary>미완료 답장.</summary>
        public readonly string Missed;

        /// <summary>기한(실제 초). 0이면 제한 없음(67차 ②: W2·W4·W5·W15). 조우 중에도 흐른다(태블릿 시계 마감과 맞게).</summary>
        public readonly float Seconds;

        private readonly int[] _days;

        /// <summary>57차 <see cref="DutyKind.GazeCheck"/>의 응시 대상(<c>inspect.H-1</c>). 그 밖은 빈 문자열.</summary>
        public string Target { get; private set; } = string.Empty;

        /// <summary>대상이 점검 항목이면 그 ID(<c>H-1</c>) — 그날 점검에 있으면 내지 않는다.</summary>
        public string Item { get; private set; } = string.Empty;

        /// <summary>비품 확인 지시를 만든다(57차).</summary>
        public static DutyDef Gaze(string id, string item, FearAxis axis, string order, string done, string missed, float seconds, params int[] days)
        {
            InspectionItem it = InspectionCatalog.Find(item);
            DutyDef d = new DutyDef(id, DutyKind.GazeCheck, it != null ? it.Space : SpaceId.Corridor, axis, order, done, missed, seconds, days);
            d.Item = item;
            d.Target = InspectionCatalog.TargetPrefix + item;
            return d;
        }

        /// <summary>줍기 지시를 만든다(65차). <paramref name="target"/> = 연출 쪽이 세우는 줍는 물건 ID.</summary>
        public static DutyDef Pickup(string id, string target, SpaceId space, FearAxis axis, string order, string done, string missed, float seconds, params int[] days)
        {
            DutyDef d = new DutyDef(id, DutyKind.Pickup, space, axis, order, done, missed, seconds, days);
            d.Target = target;
            return d;
        }

        /// <summary>80차: 출입 카드 반납 지시를 만든다. <paramref name="card"/> = 연출 쪽이 세우는 카드 ID.</summary>
        public static DutyDef Card(string id, string card, SpaceId space, FearAxis axis, string order, string done, string missed, float seconds, params int[] days)
        {
            DutyDef d = new DutyDef(id, DutyKind.CardDelivery, space, axis, order, done, missed, seconds, days);
            d.Target = card;
            return d;
        }

        /// <summary>만든다.</summary>
        public DutyDef(string id, DutyKind kind, SpaceId space, FearAxis axis, string order, string done, string missed, float seconds, params int[] days)
        {
            Id = id;
            Kind = kind;
            Space = space;
            Axis = axis;
            Order = order;
            Done = done;
            Missed = missed;
            Seconds = seconds;
            _days = days ?? new int[0];
        }

        /// <summary>그 일차에 낼 수 있는지.</summary>
        public bool OnDay(int day)
        {
            return Array.IndexOf(_days, day) >= 0;
        }
    }

    /// <summary>
    /// [근무 지시] 목록(54차 — 3차 회의 제안서 「최종 채택 지시」 1차 묶음 W1~W6, 민 질문 Q1·Q2는 제안 기본값: 「~바랍니다」, 진행 표시 없음).
    /// 판정 지점 ID는 연출 쪽(<c>DutyStage</c>)이 런타임 구역·응시 대상으로 세운다.
    /// </summary>
    public static class DutyCatalog
    {
        /// <summary>옛 머리글(88차부터 지시 문자에 붙이지 않는다 — 민: 「그냥 지시 문구만 나오게」). 로그·문서용.</summary>
        public const string Header = "[근무 지시] ";

        /// <summary>도서관 순찰 지점 구역 ID 접두어(뒤에 1~4).</summary>
        public const string LibraryZonePrefix = "duty.library.";

        /// <summary>도서관 순찰 지점 수(입구 · 열람석 · 서가 안 · 창가).</summary>
        public const int LibraryZones = 3;   // 57차(민: 「도서관 순찰이 완료가 잘 안 된다」): 네 곳 중 셋이면 된다

        /// <summary>1-3 교실 문간 구역.</summary>
        public const string ClassroomDoorZone = "duty.c13.door";

        /// <summary>1-3 교실 천장 등(응시 대상).</summary>
        public const string ClassroomLightTarget = "duty.c13.light";

        /// <summary>복도 끝 구역.</summary>
        public const string ExitZone = "duty.exit.end";

        /// <summary>복도 끝 유도등(응시 대상).</summary>
        public const string ExitSignTarget = "duty.exit.sign";

        /// <summary>65차: 도서관 안쪽 책상 위 biology 책(줍기 대상, 씬 <c>Interior/Library/Book16 (2)</c>).</summary>
        public const string LibraryBookTarget = "duty.library.book";

        /// <summary>80차 W16: 과학실에 두고 간 출입 카드(과학실 책상 위에 선다).</summary>
        public const string ScienceCardTarget = "duty.card.science";

        /// <summary>80차 W17: 교실(1-1) 교탁 서랍 속 출입 카드.</summary>
        public const string ClassroomCardTarget = "duty.card.classroom";

        /// <summary>80차 W21: 손전등 세 번 깜빡임을 한 번으로 치는 시간(초).</summary>
        public const float FlashWindowSeconds = 8f;

        /// <summary>80차 W21: 깜빡임 수.</summary>
        public const int FlashCount = 3;

        /// <summary>CCTV 채널 하나를 본 것으로 치는 초.</summary>
        public const float ChannelSeconds = 2f;

        /// <summary>88차 W3: 교실 상태 확인으로 치는 교실 안 누적 초.</summary>
        public const float ClassroomCheckSeconds = 2f;

        /// <summary>등·유도등을 본 것으로 치는 연속 응시 초.</summary>
        public const float GazeSeconds = 1f;

        /// <summary>완료하면 그 축에서 빼는 양(점검 정확 보고 −5보다 작게 — 판단 행위의 값을 지킨다).</summary>
        public const int Relief = 3;

        /// <summary>103차(축 균형): 지시 완료로 한 축에서 하룻밤 빠지는 양의 상한 — 청각 지시가 많아 청각만 계속 내려가던 것을 막는다.</summary>
        public const int ReliefCapPerAxis = 6;

        private static readonly List<DutyDef> s_all = new List<DutyDef>
        {
            // 77차(민): W1 CCTV 순회 폐기(K-1 「CCTV 모든 채널」 점검과 겹침). ID W1은 다시 쓰지 않는다.
            new DutyDef("W2", DutyKind.Patrol, SpaceId.Library, FearAxis.Auditory,
                "도서관을 순찰해주십시오.", "도서관 순찰이 기록되었습니다.", "도서관 순찰 기록이 없습니다.", 0f, 1, 2, 3, 4, 5),   // 57차: 120 → 150초 · 67차(민): 제한 없음
            new DutyDef("W3", DutyKind.LightsOutCheck, SpaceId.Classroom_1_3, FearAxis.Illuminance,   // 103차 축 균형: 청각 → 조도(소등 확인)
                "교실의 상태를 확인해주십시오.", "1-3 교실 이상 없음.", "1-3 교실 확인 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),
            new DutyDef("W4", DutyKind.DoorTidy, SpaceId.Corridor, FearAxis.Layout,
                "열어 둔 문들을 정리해주십시오.", "문 정리가 기록되었습니다.", "문 정리 기록이 없습니다.", 0f, 2, 3, 4, 5),   // 67차(민): 제한 없음 · 103차 띄어쓰기: 정리 해주십시오 → 정리해주십시오
            new DutyDef("W5", DutyKind.LogSign, SpaceId.SecurityRoom, FearAxis.Auditory,
                "경비실 근무일지에 서명해주십시오.", "근무일지 서명이 기록되었습니다.", "근무일지 서명 기록이 없습니다.", 0f, 1, 2, 3, 4, 5),
            // 92차(민: 「복도 끝 비상구를 응시 후 경비실에 복귀하십시오 <- 이건 폐지해줘」): W6 비상구 응시 폐기. ID W6은 다시 쓰지 않는다(ExitSignCheck 처리 코드는 남겨 둠).

            // 77차(민: 「지시 문구들은 점검 지시들과 너무 겹쳐서」): 57차 비품 확인 W7~W14(점검 대상을 1초 보기) 폐기. ID W7~W14는 다시 쓰지 않는다.

            // 65차(민: 「반납 상자는 book16(2) 관련 지시로 — 도서관 안쪽 책상에 있는 biology 책을 습득하세요. 클릭해서 습득하면 완료」). 문구는 민 그대로(「~바랍니다」 예외).
            // 80차(민: W19 「경비실 전화가 울리면 받아주십시오.」 채택 — 문구 민 그대로). 지시 뒤 전화가 울린다 — K5(세 번 울린 뒤 받기)와 맞물린다.
            new DutyDef("W19", DutyKind.PhoneAnswer, SpaceId.SecurityRoom, FearAxis.Auditory,
                "경비실 전화가 울리면 받아주십시오.", "통화가 기록되었습니다.", "통화 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),

            // 80차(민: W16 「과학실에 두고 간 출입 카드를 경비실 열쇠함에 넣어주십시오.」 · W17 「교실 교탁 서랍의 출입 카드를 가져와 주십시오.」 ·
            // 「열쇠함은 경비실의 LibraryCardCatalogB에 열려있는 서랍에 두는 걸로 · 카드키 모델 · 문구도 출입 카드로」). 둘 다 열쇠함에 넣으면 완료.
            DutyDef.Card("W16", ScienceCardTarget, SpaceId.ScienceRoom, FearAxis.Layout, "과학실에 두고 간 출입 카드를 경비실 열쇠함에 넣어주십시오.",
                "출입 카드 반납이 기록되었습니다.", "출입 카드 반납 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),
            // 103차(민: 「교실 카드키 회수에 경비실 열쇠함에 넣으라는 것을 명시」): 옛 「교실 교탁 서랍의 출입 카드를 가져와 주십시오.」 — 어디로 가져오는지 없었다.
            DutyDef.Card("W17", ClassroomCardTarget, SpaceId.Classroom, FearAxis.Layout, "교실 교탁 서랍 출입 카드를 경비실 열쇠함에 넣어주십시오.",
                "출입 카드 회수가 기록되었습니다.", "출입 카드 회수 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),

            // 80차(민: D1 채택 「유도등이 아니라 비상구라고 명시」). W18·W20은 폐기한 제안 번호라 쓰지 않는다.
            new DutyDef("W21", DutyKind.ExitFlash, SpaceId.Corridor, FearAxis.Illuminance,
                "복도 끝 비상구 앞에서 손전등을 세 번 깜빡여주십시오.", "비상구 신호가 기록되었습니다.", "비상구 신호 기록이 없습니다.", ExitFlashSeconds, 1, 2, 3, 4, 5),   // 103차: 기한 90초(시간 제한 지시)

            DutyDef.Pickup("W15", LibraryBookTarget, SpaceId.Library, FearAxis.Auditory, "도서관 안쪽 책상에 있는 biology 책을 습득하세요.", "biology 책 습득이 기록되었습니다.", "biology 책 습득 기록이 없습니다.", 0f, 1, 2, 3, 4, 5),   // 67차(민): 제한 없음
        };

        /// <summary>전부(정의 순).</summary>
        public static IReadOnlyList<DutyDef> All
        {
            get { return s_all; }
        }

        /// <summary>ID로 찾는다. 없으면 null.</summary>
        public static DutyDef Find(string id)
        {
            for (int i = 0; i < s_all.Count; i++)
            {
                if (s_all[i].Id == id) return s_all[i];
            }

            return null;
        }

        /// <summary>
        /// 하루에 낼 수 있는 지시 수(근무일지 W5 제외). 54차 플레이 점검: 제안서의 2~3개·일차별 짧은 목록으로는 01:00 뒤가 다시 비었다 — 날마다 4,
        /// 모든 지시를 모든 날에(W4는 2일차부터). 더 늘리면 심부름 게임이 된다(제안서 「위험 ①」).
        /// </summary>
        public static int DailyCap(int day)
        {
            // 57차(민: 「근무 지시가 많아서 귀찮다」): 날마다 4 → 1·2일차 2 · 3일차부터 3. 대신 종류를 늘려 같은 지시가 덜 겹친다.
            // 67차(민: 「물품 점검이 비중이 더 높은데, 지시 사항과 비중이 균일했으면」): 1·2일차 3 · 3일차부터 4 — 점검 4·5·5·6·6항목(지시 3~4통)과 번갈아 낸다.
            return day <= 2 ? 3 : 4;
        }

        /// <summary>
        /// 67차 ②(민: 「시간 제한이 있는 모든 점검 지시는 120초로 고정 — W2·W4·W5·W15는 제한 없음」): 제한이 있는 지시의 기한(실제 초).
        /// 기한은 태블릿 시계로 「HH:MM까지」(<see cref="DeadlineText"/>) — 게임 시계가 24배라 120초 = 48분.
        /// </summary>
        public const float LimitSeconds = 120f;

        /// <summary>
        /// 76차(민: 「지시 사항들의 제한 시간도 폐기해줘」): 기본 꺼짐 — 근무 지시·점검 지시 모두 기한·「HH:MM까지」·기한 지남 경고가 없다.
        /// 67차 제한 방식을 시험하는 옛 시험만 켠다. 켜지 않는 한 <see cref="DutyDef.Seconds"/>는 읽지 않고 <see cref="EffectiveSeconds"/>를 쓴다.
        /// </summary>
        public static bool TimeLimitsEnabled { get; set; }

        /// <summary>
        /// 103차(민: 「전화 받기와 근무일지 서명, 비상구 손전등 3번만 시간제한」): 76차에 끈 제한을 이 지시들에만 다시 건다 — 기한이 상황에서 나오는 것들.
        /// W19 = 전화가 울리는 동안(재시도 포함 <see cref="LimitSeconds"/>초) · W21 = <see cref="ExitFlashSeconds"/>초 · W5 = 서명 시간(이완 구간, 호출 2까지 — 기한 초 대신 시간대).
        /// 기한을 넘겨도 경고는 없다 — 신뢰 보상(<see cref="TrustFor"/>)만 받지 못한다. 조우 중에는 기한이 멈춘다.
        /// </summary>
        public static readonly string[] TimedDuties = { "W19", "W21" };

        /// <summary>103차: W21 비상구 손전등 기한(실제 초).</summary>
        public const float ExitFlashSeconds = 90f;

        /// <summary>103차: 시간 제한 지시인지(W19 · W21 · W5 서명 시간).</summary>
        public static bool IsTimed(DutyDef def)
        {
            return def != null && (def.Kind == DutyKind.LogSign || Array.IndexOf(TimedDuties, def.Id) >= 0);
        }

        /// <summary>76차: 지금 적용할 그 지시의 기한(실제 초). 제한을 껐으면(기본) 0 = 제한 없음. 103차: <see cref="TimedDuties"/>는 늘 제 기한.</summary>
        public static float EffectiveSeconds(DutyDef def)
        {
            if (def == null) return 0f;
            if (TimeLimitsEnabled) return def.Seconds;
            return Array.IndexOf(TimedDuties, def.Id) >= 0 ? def.Seconds : 0f;
        }

        /// <summary>103차(민: 「지시 사항을 잘 수행해도 신뢰가 오르게」): 지시를 어기지 않고 마치면 오르는 신뢰 — 시간 제한 지시 2 · 그 밖 1.</summary>
        public static int TrustFor(DutyDef def)
        {
            if (def == null) return 0;
            return IsTimed(def) ? TrustTimed : TrustPlain;
        }

        /// <summary>103차: 일반 지시 완료 신뢰.</summary>
        public const int TrustPlain = 1;

        /// <summary>103차: 시간 제한 지시를 기한 안에 마친 신뢰.</summary>
        public const int TrustTimed = 2;

        /// <summary>103차: 하룻밤 지시로 오르는 신뢰 상한 — 심부름만으로 변조 구간에 닿지 않게.</summary>
        public const int TrustCapPerNight = 4;

        /// <summary>103차: 완료 답장 끝에 붙는 근무 평가 줄(신뢰가 올랐을 때).</summary>
        public const string TrustLine = "근무 평가에 반영되었습니다.";

        /// <summary><see cref="LimitSeconds"/>를 게임 분으로(48분).</summary>
        public static float LimitGameMinutes
        {
            get { return LimitSeconds * NightClock.GameSecondsPerRealSecond / 60f; }
        }

        /// <summary>그 게임 분에 받은 지시의 마감 「HH:MM까지」(근무 시작 00:00 기준 = 태블릿 시계).</summary>
        public static string DeadlineText(float issuedMinute, float seconds = LimitSeconds)
        {
            float due = issuedMinute + seconds * NightClock.GameSecondsPerRealSecond / 60f;
            return Clock((int)Math.Ceiling(due - 0.0001f)) + "까지";
        }

        /// <summary>67차(민: 「제한시간이 존재하고, 명시되면 좋겠어」): 「제한 1분 15초」. 0 이하면 빈 문자열.</summary>
        public static string LimitText(float seconds)
        {
            if (seconds <= 0f) return string.Empty;
            int s = (int)Math.Round(seconds);
            int m = s / 60;
            int r = s % 60;
            if (m == 0) return "제한 " + r + "초";
            return r == 0 ? "제한 " + m + "분" : "제한 " + m + "분 " + r + "초";
        }

        /// <summary>지시 문자 끝에 붙는 마감 줄(67차 ②) — 제한이 있는 지시만 「HH:MM까지」. 받은 시각을 모르면 빈 문자열.</summary>
        public static string LimitLine(DutyDef def, float issuedMinute)
        {
            float seconds = EffectiveSeconds(def);   // 76차: 제한을 껐으면 마감 줄이 없다
            if (seconds <= 0f || issuedMinute < 0f) return string.Empty;
            return "\n" + DeadlineText(issuedMinute, seconds);
        }

        /// <summary>
        /// 그날의 지시 문자 — 88차(민: 「지시 메시지의 양식을 고쳐야 해. 그냥 지시 문구만 나오게 해줘」): 지시 문장만(옛 머리글 「[근무 지시] 」 없음).
        /// </summary>
        public static string OrderText(DutyDef def, int day)
        {
            if (def == null) return string.Empty;
            // 77차(민): W2 4일차 「다른 근무자」 변주 폐기, W3 2일차부터 둘째 줄(「1-3 교실은 소등되어 있습니다.」)도 새 문구에 없어 뺐다.
            return def.Order;
        }

        /// <summary>
        /// 태블릿에 실리는 지시 문자 = 지시 문장만(88차). 103차: 시간 제한 지시 중 W21·W5만 둘째 줄에 마감 「HH:MM까지」(W19는 벨이 기한이라 붙이지 않는다).
        /// </summary>
        public static string MessageText(DutyDef def, int day, float issuedMinute)
        {
            string text = OrderText(def, day);
            if (def == null || issuedMinute < 0f || TimeLimitsEnabled) return text;
            if (def.Kind == DutyKind.LogSign) return text + "\n" + Clock(NightClock.Call2) + "까지";
            if (def.Id == "W21") return text + "\n" + DeadlineText(issuedMinute, EffectiveSeconds(def));
            return text;
        }

        /// <summary>완료 답장(「~이 기록되었습니다. 00:42」). 「확인되었습니다.」는 역설 답장 전용이라 쓰지 않는다.</summary>
        public static string DoneText(DutyDef def, int day, int minute)
        {
            if (def == null) return string.Empty;
            string text = def.Done;   // 77차: 4일차 W2 변주 폐기
            return minute >= 0 ? text + " " + Clock(minute) : text;
        }

        /// <summary>미완료 답장.</summary>
        public static string MissedText(DutyDef def)
        {
            return def != null ? def.Missed : string.Empty;
        }

        /// <summary>밤 시계 분 → 「HH:MM」(근무 시작 00:00 기준).</summary>
        public static string Clock(int minute)
        {
            int m = Math.Max(0, minute);
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }
    }
}
