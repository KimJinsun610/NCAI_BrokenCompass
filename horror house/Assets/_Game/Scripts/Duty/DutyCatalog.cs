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

        /// <summary>W3 — 1-3 교실 문간에 서서 천장 등을 1초 본다.</summary>
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
        Pickup = 7
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
        /// <summary>머리글.</summary>
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

        /// <summary>CCTV 채널 하나를 본 것으로 치는 초.</summary>
        public const float ChannelSeconds = 2f;

        /// <summary>등·유도등을 본 것으로 치는 연속 응시 초.</summary>
        public const float GazeSeconds = 1f;

        /// <summary>완료하면 그 축에서 빼는 양(점검 정확 보고 −5보다 작게 — 판단 행위의 값을 지킨다).</summary>
        public const int Relief = 3;

        private static readonly List<DutyDef> s_all = new List<DutyDef>
        {
            new DutyDef("W1", DutyKind.CctvSweep, SpaceId.SecurityRoom, FearAxis.Layout,
                "CCTV 전 채널 순회 바랍니다.", "CCTV 순회가 기록되었습니다.", "CCTV 순회 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),
            new DutyDef("W2", DutyKind.Patrol, SpaceId.Library, FearAxis.Auditory,
                "도서관 순찰 바랍니다.", "도서관 순찰이 기록되었습니다.", "도서관 순찰 기록이 없습니다.", 0f, 1, 2, 3, 4, 5),   // 57차: 120 → 150초 · 67차(민): 제한 없음
            new DutyDef("W3", DutyKind.LightsOutCheck, SpaceId.Classroom_1_3, FearAxis.Auditory,
                "교실 소등 확인", "1-3 교실 이상 없음.", "1-3 교실 확인 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),
            new DutyDef("W4", DutyKind.DoorTidy, SpaceId.Corridor, FearAxis.Layout,
                "열어 둔 문 정리 바랍니다.", "문 정리가 기록되었습니다.", "문 정리 기록이 없습니다.", 0f, 2, 3, 4, 5),   // 67차(민): 제한 없음
            new DutyDef("W5", DutyKind.LogSign, SpaceId.SecurityRoom, FearAxis.Auditory,
                "경비실 근무일지 서명", "근무일지 서명이 기록되었습니다.", "근무일지 서명 기록이 없습니다.", 0f, 1, 2, 3, 4, 5),
            new DutyDef("W6", DutyKind.ExitSignCheck, SpaceId.Corridor, FearAxis.Auditory,
                "복도 끝 비상구 응시 후 경비실 복귀", "유도등 확인이 기록되었습니다.", "유도등 확인 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),

            // 57차(민: 「근무 지시 풀을 늘려서 다양하게」): 비품 확인 — 그날 점검에 없는 점검 대상을 1초 본다(보고 없음, 응시 대상은 씬에 이미 있다).
            DutyDef.Gaze("W7", "H-1", FearAxis.Illuminance, "복도 소화기 압력 확인 바랍니다.", "소화기 확인이 기록되었습니다.", "소화기 확인 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),
            DutyDef.Gaze("W8", "S-2", FearAxis.Illuminance, "과학실 현미경 확인 바랍니다.", "현미경 전원 확인이 기록되었습니다.", "현미경 확인 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),
            DutyDef.Gaze("W9", "C-1", FearAxis.Layout, "교실 화분 상태 확인 바랍니다.", "화분 확인이 기록되었습니다.", "화분 확인 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),
            DutyDef.Gaze("W10", "H-2", FearAxis.Layout, "복도 식수대 누수 확인 바랍니다.", "식수대 확인이 기록되었습니다.", "식수대 확인 기록이 없습니다.", LimitSeconds, 1, 2, 3, 4, 5),
            DutyDef.Gaze("W11", "L-2", FearAxis.Illuminance, "도서관 블라인드 확인 바랍니다.", "블라인드 확인이 기록되었습니다.", "블라인드 확인 기록이 없습니다.", LimitSeconds, 2, 3, 4, 5),
            DutyDef.Gaze("W12", "T-3", FearAxis.Illuminance, "화장실 거울 조명 확인 바랍니다.", "거울 조명 확인이 기록되었습니다.", "거울 조명 확인 기록이 없습니다.", LimitSeconds, 2, 3, 4, 5),
            DutyDef.Gaze("W13", "L-1", FearAxis.Layout, "도서관 열람석 확인 바랍니다.", "열람석 확인이 기록되었습니다.", "열람석 확인 기록이 없습니다.", LimitSeconds, 2, 3, 4, 5),
            DutyDef.Gaze("W14", "T-2", FearAxis.Auditory, "화장실 칸 문 확인 바랍니다.", "칸 문 확인이 기록되었습니다.", "칸 문 확인 기록이 없습니다.", LimitSeconds, 3, 4, 5),

            // 65차(민: 「반납 상자는 book16(2) 관련 지시로 — 도서관 안쪽 책상에 있는 biology 책을 습득하세요. 클릭해서 습득하면 완료」). 문구는 민 그대로(「~바랍니다」 예외).
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
            if (def == null || def.Seconds <= 0f || issuedMinute < 0f) return string.Empty;
            return "\n" + DeadlineText(issuedMinute, def.Seconds);
        }

        /// <summary>
        /// 그날의 지시 문자(머리글 포함). 머리글은 늘 멀쩡하고 <b>내용이</b> 뒤틀린다 — 2일차부터 W3에 「지나친 확신」,
        /// 4일차 W2는 「다른 근무자」 변주(제안서 대표 장면 ①②).
        /// </summary>
        public static string OrderText(DutyDef def, int day)
        {
            if (def == null) return string.Empty;
            if (def.Kind == DutyKind.LightsOutCheck && day >= 2) return Header + def.Order + "\n1-3 교실은 소등되어 있습니다.";
            if (def.Kind == DutyKind.Patrol && day == 4) return Header + "복도 순찰은 다른 근무자가 진행 중입니다. " + def.Order;
            return Header + def.Order;
        }

        /// <summary>67차: 태블릿에 실리는 지시 문자 = 지시 문장 + 마감 줄(「02:08까지」, 제한 없는 W2·W4·W5·W15는 없음).</summary>
        public static string MessageText(DutyDef def, int day, float issuedMinute)
        {
            return OrderText(def, day) + LimitLine(def, issuedMinute);
        }

        /// <summary>완료 답장(「~이 기록되었습니다. 00:42」). 「확인되었습니다.」는 역설 답장 전용이라 쓰지 않는다.</summary>
        public static string DoneText(DutyDef def, int day, int minute)
        {
            if (def == null) return string.Empty;
            string text = def.Kind == DutyKind.Patrol && day == 4 ? "순찰이 기록되었습니다. 근무자 2명." : def.Done;
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
