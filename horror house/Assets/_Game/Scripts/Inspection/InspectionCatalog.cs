using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 점검 이상의 네 가지 틀(2026-09-30 최종 기획서 「공간별 설계」). 컴포넌트 하나(대상·틀·축·구간별 강도)로 모두 만든다.
    /// </summary>
    public enum AnomalyTemplate
    {
        /// <summary>[옮김] 물체가 제자리에 없다. 최소 15° 회전 또는 실루엣이 바뀔 만큼 움직인다.</summary>
        Move = 0,

        /// <summary>[켬] 없던 상태가 켜진다(물이 넘침·화면 속 사람 등).</summary>
        Switch = 1,

        /// <summary>[빛] 꺼져 있어야 할 빛이 있다.</summary>
        Light = 2,

        /// <summary>[소리] 대상 위치의 3D 음. 다가가면 커진다.</summary>
        Sound = 3
    }

    /// <summary>점검 항목 한 개의 정의. 항목 하나에 축 하나다.</summary>
    public sealed class InspectionItem
    {
        /// <summary>항목 ID(「H-1」 등). 씬의 점검 대상 ID는 <see cref="TargetId"/>.</summary>
        public readonly string Id;

        /// <summary>점검표에 쓰는 이름(「소화기」).</summary>
        public readonly string Name;

        /// <summary>공간.</summary>
        public readonly SpaceId Space;

        /// <summary>이 항목의 축. 놓침·오보·가까이·정확 보고가 모두 이 축에 걸린다.</summary>
        public readonly FearAxis Axis;

        /// <summary>이상의 틀.</summary>
        public readonly AnomalyTemplate Template;

        /// <summary>점검표 문구 — 76차부터 민 문구(「~의 상태 보고.」·「~인지 확인.」). 옛 52~67차는 정상 상태를 단정하는 문장이었다.</summary>
        public readonly string TabletLine;

        /// <summary>항목을 만든다.</summary>
        public InspectionItem(string id, string name, SpaceId space, FearAxis axis, AnomalyTemplate template, string tabletLine)
        {
            Id = id;
            Name = name;
            Space = space;
            Axis = axis;
            Template = template;
            TabletLine = tabletLine;
        }

        /// <summary>씬의 점검 대상 ID(<c>inspect.H-1</c>). 센서·연출이 이 ID로 대상을 찾는다.</summary>
        public string TargetId
        {
            get { return InspectionCatalog.TargetPrefix + Id; }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Id + " " + Name + "(" + Space + ", " + Axis + ", " + Template + ")";
        }
    }

    /// <summary>
    /// 점검 항목 29개(복도 6 · 교실 5 · 과학실 6 · 화장실 5 · 도서관 4 · 경비실 3 — 66차 민: 「점검은 안 겹칠수록 좋아, 필요하면 하루 점검을 줄여도 돼」로 13개 더함). <b>점검 항목의 구현값 정본</b>이다 —
    /// 기획 정본은 최종 기획서 「공간별 설계」이고, 거기서 문구·축·틀이 바뀌면 여기를 고친다.
    /// 「가까이」 연출 내용과 이상의 모습은 연출 단계(7단계)의 몫이라 여기 적지 않는다.
    /// 2026-10-05(49차 민): H-4 복도 사물함(관물대)을 뺐다 — 복도 라커가 142개라 어느 것을 보라는지 찾기 어렵다. ID H-4는 다시 쓰지 않는다.
    /// 104차(사용자: 「T-2는 그냥 폐기해줘」): T-2 화장실 칸 문은 <see cref="Retired"/>로 폐기 — 정의·씬 대상·소리는 남기고 배정·지시에서만 뺀다(<see cref="InSpace"/>가 거른다).
    /// </summary>
    public static class InspectionCatalog
    {
        /// <summary>씬 점검 대상 ID의 접두사.</summary>
        public const string TargetPrefix = "inspect.";

        /// <summary>점검표 머리의 점검 수칙 한 줄(모든 항목 공통).</summary>
        public const string HeaderRule = "점검 대상에 가까이 가거나 손대지 마십시오.";

        /// <summary>1일차 튜토리얼 이상 — 과학실 현미경 불(빛).</summary>
        public const string TutorialLight = "S-2";

        /// <summary>1일차 튜토리얼 이상 — 교실 화분(옮김). 교실은 1일차 마지막 점검 공간이라 호출 2(02:16)에 열린다.</summary>
        public const string TutorialMove = "C-1";

        /// <summary>회차 첫 점검. 반드시 정상이다.</summary>
        public const string FirstInspection = "K-1";

        /// <summary>역보고(T4)가 걸리는 항목.</summary>
        public const string ReverseReportItem = "T-1";

        /// <summary>
        /// 104차(사용자: 「T-2는 그냥 폐기해줘」): 폐기한 항목 ID. <see cref="All"/>·<see cref="Find"/>에는 남아 있지만(옛 기록·씬 대상·소리 표가 그대로 읽힌다)
        /// <see cref="InSpace"/>가 걸러 배정기(<see cref="AnomalyAssigner"/>)·조우 보충(<c>NightRun.PatchPlanForProgram</c>)에 나오지 않는다 — 지시·태블릿·결과에도 나오지 않는다.
        /// 되살리려면 여기서 ID를 빼면 된다. 옛 81차 T-4·80차 L-4처럼 줄을 지우지 않은 것은 되살릴 수 있게 하려는 것이다.
        /// </summary>
        public static readonly string[] Retired = { "T-2" };

        /// <summary>폐기한 항목인지(<see cref="Retired"/>).</summary>
        public static bool IsRetired(string id)
        {
            return id != null && Array.IndexOf(Retired, id) >= 0;
        }

        private static readonly InspectionItem[] Items =
        {
            new InspectionItem("H-1", "소화기", SpaceId.Corridor, FearAxis.Illuminance, AnomalyTemplate.Light, "화장실 쪽 복도 소화기가 빛나지 않는지 확인해주십시오."),   // 104차(사용자 결정): 옛 「화장실 쪽 복도 소화기의 상태를 보고해주십시오.」
            new InspectionItem("H-2", "식수대", SpaceId.Corridor, FearAxis.Illuminance, AnomalyTemplate.Switch,   /* 103차 축 균형: 배치 → 조도(붉은 물 — 보이는 색) */ "과학실 쪽 복도 식수대의 상태를 보고해주십시오."),
            new InspectionItem("H-3", "알림종", SpaceId.Corridor, FearAxis.Auditory, AnomalyTemplate.Sound, "경비실 문 옆 알림종이 조용한지 확인해주십시오."),   // 104차(사용자 결정): 옛 「경비실 쪽 복도 위 알림종이 꺼져 있는지 확인해주십시오.」
            new InspectionItem("H-4", "복도 쓰레기통", SpaceId.Corridor, FearAxis.Auditory, AnomalyTemplate.Switch,   /* 103차 축 균형: 배치 → 청각(걷어차이는 소리) */ "도서관 문 옆 복도 쓰레기통이 제자리에 있는지 확인해주십시오."),   // 104차(사용자 결정): 옛 이름 「쓰레기통」(L-5와 같았음) · 옛 「복도 쓰레기통의 상태를 보고해주십시오.」 · 65차 민: 이상이면 다가갈 때 누가 걷어찬 듯 날아간다(L-3 반납 상자 대체 — 반납 상자는 [근무 지시] W15 「biology 책 습득」으로)
            new InspectionItem("H-5", "스피커", SpaceId.Corridor, FearAxis.Auditory, AnomalyTemplate.Sound, "도서관 옆 복도 스피커가 조용한지 확인해주십시오."),   // 104차(사용자 결정): 옛 「도서관 옆 복도의 스피커가 꺼져 있는지 확인해주십시오.」 · 66차(민: 「점검은 안 겹칠수록 좋아」) — 화장실 앞 복도 벽 높이의 스피커, 이상이면 지직거림
            new InspectionItem("H-6", "벤치", SpaceId.Corridor, FearAxis.Layout, AnomalyTemplate.Move, "화장실 쪽 복도 벤치가 벽에 붙어 있는지 확인해주십시오."),   // 66차 — 이상이면 한쪽 끝이 벽에서 떨어져 복도로 돌아 나와 있다

            new InspectionItem("C-1", "교실 화분", SpaceId.Classroom, FearAxis.Layout, AnomalyTemplate.Move, "교실 창가 화분이 제자리에 있는지 확인해주십시오."),   // 104차(사용자 결정): 옛 이름 「화분」(K-3과 같았음) · 옛 「교실 화분이 제자리에 있는지 확인해주십시오.」
            new InspectionItem("C-2", "책상 램프", SpaceId.Classroom, FearAxis.Illuminance, AnomalyTemplate.Light, "교실 책상 램프가 꺼져 있는지 확인해주십시오."),
            new InspectionItem("C-3", "사다리", SpaceId.Classroom, FearAxis.Layout, AnomalyTemplate.Move, "교실 안쪽에 작은 사다리가 있는지 확인해주십시오."),   // 52차 민 문구(이상 = 사다리 없음)
            new InspectionItem("C-4", "시계", SpaceId.Classroom, FearAxis.Layout, AnomalyTemplate.Move, "교실 시계가 제자리에 있는지 확인해주십시오."),   // 66차 — 이상이면 벽 아래 바닥에 떨어져 있다
            new InspectionItem("C-5", "교사 의자", SpaceId.Classroom, FearAxis.Auditory, AnomalyTemplate.Move, "교실 교사 의자가 교탁 쪽을 보는지 확인해주십시오."),   // 104차(사용자 결정): 옛 「교사 의자가 교탁 안에 들어가 있는지 확인해주십시오.」 · 66차 — 이상이면 교실 쪽으로 돌아앉음 · 103차(축 균형): 의자 끄는 소리 → 청각

            new InspectionItem("S-1", "인체 모형", SpaceId.ScienceRoom, FearAxis.Layout, AnomalyTemplate.Move, "과학실 인체 모형이 정면을 향하는지 확인해주십시오."),   // 64차 민: 실제 이상은 90° 돌아감(회전)이라 그에 맞춘 문구
            new InspectionItem("S-2", "현미경", SpaceId.ScienceRoom, FearAxis.Illuminance, AnomalyTemplate.Light, "과학실 현미경이 꺼져 있는지 확인해주십시오."),
            new InspectionItem("S-3", "개수대", SpaceId.ScienceRoom, FearAxis.Auditory, AnomalyTemplate.Sound, "과학실 개수대 수도꼭지가 조용한지 확인해주십시오."),   // 104차(사용자 결정): 옛 「과학실 개수대가 잠겨 있는지 확인해주십시오.」
            new InspectionItem("S-4", "지구본", SpaceId.ScienceRoom, FearAxis.Auditory, AnomalyTemplate.Switch,   /* 103차 축 균형: 배치 → 청각(혼자 도는 삐걱임) */ "과학실 지구본이 멈춰 있는지 확인해주십시오."),   // 104차(사용자 결정): 옛 「과학실 지구본의 상태를 보고해주십시오.」 · 66차 — 이상이면 혼자 천천히 돈다
            new InspectionItem("S-5", "플라스크", SpaceId.ScienceRoom, FearAxis.Illuminance, AnomalyTemplate.Light, "과학실 선반의 플라스크의 상태를 보고해주십시오."),   // 66차 — 이상이면 유리가 붉게 빛난다(깜빡임)
            new InspectionItem("S-6", "세계 지도", SpaceId.ScienceRoom, FearAxis.Layout, AnomalyTemplate.Move, "과학실 세계 지도가 반듯한지 확인해주십시오."),   // 104차(사용자 결정): 옛 이름 「지도」 · 66차 — 이상이면 기울거나 세로로·거꾸로

            new InspectionItem("T-1", "변기", SpaceId.Toilet, FearAxis.Auditory, AnomalyTemplate.Switch,   /* 103차 축 균형: 배치 → 청각(물소리) */ "화장실 3번 칸 변기의 상태를 보고해주십시오."),   // 104차(사용자 결정): 옛 「화장실 변기의 상태를 보고해주십시오.」 — 칸을 문장에 넣어 목격 지시의 「(3번 칸)」 꼬리를 뗐다
            new InspectionItem("T-2", "칸 문", SpaceId.Toilet, FearAxis.Auditory, AnomalyTemplate.Sound, "화장실 빈 칸의 상태를 보고해주십시오."),   // 104차(사용자: 「T-2는 그냥 폐기해줘」): 폐기(Retired) — 정의는 되살릴 수 있게 남긴다
            new InspectionItem("T-3", "거울", SpaceId.Toilet, FearAxis.Illuminance, AnomalyTemplate.Light, "화장실 거울 쪽 조명의 상태를 보고해주십시오."),
            // 81차(민: 「세면대 물 퀄리티 부족 — T-4는 항목 폐기」): T-4 화장실 세면대 폐기. ID T-4는 다시 쓰지 않는다.
            new InspectionItem("T-5", "수건", SpaceId.Toilet, FearAxis.Layout, AnomalyTemplate.Move, "화장실 세면대 옆 칸막이에 수건이 걸려 있는지 확인해주십시오."),   // 104차(사용자 결정): 옛 「화장실 세면대 옆 수건이 칸에 걸려 있는지 확인해주십시오.」 · 66차 — 이상이면 바닥에 떨어져 있다

            new InspectionItem("L-1", "도서관 의자", SpaceId.Library, FearAxis.Layout, AnomalyTemplate.Move, "도서관 입구에서 가장 가까운 의자가 책상 안에 들어가 있는지 확인해주십시오."),   // 104차(사용자 결정): 옛 이름 「열람석」 · 옛 「도서관 의자가 책상 안에 들어가 있는지 확인해주십시오.」
            new InspectionItem("L-2", "블라인드", SpaceId.Library, FearAxis.Illuminance, AnomalyTemplate.Light, "도서관 입구 맞은편 창 블라인드 둘이 내려져 있는지 확인해주십시오."),   // 65차(민: 「어떤 블라인드를 지칭하는지 헷갈려」 — 도서관엔 창 블라인드 5개 + 바닥에 떨어진 조각들. 이상은 입구 맞은편 서쪽 벽 둘 중 하나가 올라감)
            // 80차(민: 「이 보고하지마 칠판과 관련된 건 폐기해줘」): L-4 도서관 칠판(분필 글씨) 폐기 — ID L-3·L-4는 다시 쓰지 않는다.
            new InspectionItem("L-5", "도서관 쓰레기통", SpaceId.Library, FearAxis.Layout, AnomalyTemplate.Move, "도서관 쓰레기통이 바닥에 누워 있는지 확인해주십시오."),   // 104차(사용자 결정): 옛 이름 「쓰레기통」(H-4와 같았음) · 옛 「도서관 쓰레기통이 세워져 있는지 확인해주십시오.」(정상·이상이 문장과 반대였다) · 66차 — 씬에서 원래 누워 있다. 이상이면 누가 세워 놓았다

            new InspectionItem("K-1", "CCTV 전 채널", SpaceId.SecurityRoom, FearAxis.Illuminance, AnomalyTemplate.Switch,   /* 103차 축 균형: 배치 → 조도(화면) */ "CCTV 모든 채널이 정상인지 확인해주십시오."),
            new InspectionItem("K-2", "접이식 의자", SpaceId.SecurityRoom, FearAxis.Layout, AnomalyTemplate.Move, "경비실 접이식 의자 둘이 같은 쪽을 보는지 확인해주십시오."),   // 66차 — 이상이면 하나가 돌아앉음 · 103차(민): 경비실 명시
            new InspectionItem("K-3", "경비실 화분", SpaceId.SecurityRoom, FearAxis.Layout, AnomalyTemplate.Move, "경비실 화분이 똑바로 서 있는지 확인해주십시오.")   // 104차(사용자 결정): 옛 이름 「화분」(C-1과 같았음) · 옛 「경비실 화분이 제자리인지 확인해주십시오.」 · 66차 — 이상이면 쓰러져 있다
        };

        private static readonly Dictionary<string, InspectionItem> ById = BuildIndex();

        /// <summary>모든 항목(공간 순).</summary>
        public static IReadOnlyList<InspectionItem> All
        {
            get { return Items; }
        }

        /// <summary>
        /// 65차(민: 「사다리 없는 자리에서 없다고 보고할 수 있도록」): 보고 키의 이름 — 「있는지 확인」 항목(C-3 사다리)은 있음/없음, 나머지는 정상/이상.
        /// 판정은 그대로다(없음 = 이상).
        /// </summary>
        public static string ReportWord(string itemId, bool anomaly)
        {
            if (itemId == "C-3") return anomaly ? "없음" : "있음";
            return anomaly ? "이상" : "정상";
        }

        /// <summary>ID로 찾는다. 없으면 null.</summary>
        public static InspectionItem Find(string id)
        {
            InspectionItem item;
            return id != null && ById.TryGetValue(id, out item) ? item : null;
        }

        /// <summary>씬 대상 ID(<c>inspect.H-1</c>)나 항목 ID로 찾는다. 없으면 null.</summary>
        public static InspectionItem FindByTarget(string targetOrId)
        {
            if (string.IsNullOrEmpty(targetOrId))
            {
                return null;
            }

            return targetOrId.StartsWith(TargetPrefix, StringComparison.Ordinal)
                ? Find(targetOrId.Substring(TargetPrefix.Length))
                : Find(targetOrId);
        }

        /// <summary>그 공간의 항목들(카탈로그 순). 104차: 폐기한 항목(<see cref="Retired"/>)은 뺀다 — 배정·조우 보충이 모두 여기서 고른다.</summary>
        public static List<InspectionItem> InSpace(SpaceId space)
        {
            SpaceId canonical = SpaceIds.Canonical(space);
            List<InspectionItem> list = new List<InspectionItem>();
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].Space == canonical && !IsRetired(Items[i].Id))
                {
                    list.Add(Items[i]);
                }
            }

            return list;
        }

        private static Dictionary<string, InspectionItem> BuildIndex()
        {
            Dictionary<string, InspectionItem> index = new Dictionary<string, InspectionItem>(StringComparer.Ordinal);
            for (int i = 0; i < Items.Length; i++)
            {
                index.Add(Items[i].Id, Items[i]);
            }

            return index;
        }
    }
}
