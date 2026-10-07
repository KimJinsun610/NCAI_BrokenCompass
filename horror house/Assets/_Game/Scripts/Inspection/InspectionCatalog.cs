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

        /// <summary>점검표 문구 — 정상 상태를 말로 단정한다(기준 사진은 쓰지 않는다).</summary>
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
    /// 점검 항목 16개(복도 3 · 교실 3 · 과학실 3 · 화장실 3 · 도서관 3 · 경비실 1). <b>점검 항목의 구현값 정본</b>이다 —
    /// 기획 정본은 최종 기획서 「공간별 설계」이고, 거기서 문구·축·틀이 바뀌면 여기를 고친다.
    /// 「가까이」 연출 내용과 이상의 모습은 연출 단계(7단계)의 몫이라 여기 적지 않는다.
    /// 2026-10-05(49차 민): H-4 복도 사물함(관물대)을 뺐다 — 복도 라커가 142개라 어느 것을 보라는지 찾기 어렵다. ID H-4는 다시 쓰지 않는다.
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

        private static readonly InspectionItem[] Items =
        {
            new InspectionItem("H-1", "소화기", SpaceId.Corridor, FearAxis.Illuminance, AnomalyTemplate.Light, "소화기 압력계에는 불이 들어오지 않습니다."),
            new InspectionItem("H-2", "식수대", SpaceId.Corridor, FearAxis.Layout, AnomalyTemplate.Switch, "식수대 바닥은 말라 있습니다."),
            new InspectionItem("H-3", "알림종", SpaceId.Corridor, FearAxis.Auditory, AnomalyTemplate.Sound, "알림종은 수업 종이 칠 때만 울립니다."),

            new InspectionItem("C-1", "화분", SpaceId.Classroom, FearAxis.Layout, AnomalyTemplate.Move, "화분은 창가에 있습니다."),
            new InspectionItem("C-2", "책상 램프", SpaceId.Classroom, FearAxis.Illuminance, AnomalyTemplate.Light, "책상 램프는 꺼져 있습니다."),
            new InspectionItem("C-3", "사다리", SpaceId.Classroom, FearAxis.Layout, AnomalyTemplate.Move, "교실 안쪽에 작은 사다리가 있는지 확인하십시오."),   // 52차 민 문구(이상 = 사다리 없음)

            new InspectionItem("S-1", "인체 모형", SpaceId.ScienceRoom, FearAxis.Layout, AnomalyTemplate.Move, "모형은 테이프 안에 그대로 서 있습니다."),
            new InspectionItem("S-2", "현미경", SpaceId.ScienceRoom, FearAxis.Illuminance, AnomalyTemplate.Light, "현미경은 꺼져 있습니다."),
            new InspectionItem("S-3", "개수대", SpaceId.ScienceRoom, FearAxis.Auditory, AnomalyTemplate.Sound, "수도는 잠겨 있습니다."),

            new InspectionItem("T-1", "변기", SpaceId.Toilet, FearAxis.Layout, AnomalyTemplate.Switch, "변기 물은 맑습니다."),
            new InspectionItem("T-2", "칸 문", SpaceId.Toilet, FearAxis.Auditory, AnomalyTemplate.Sound, "빈 칸의 문은 열려 있습니다."),
            new InspectionItem("T-3", "거울", SpaceId.Toilet, FearAxis.Illuminance, AnomalyTemplate.Light, "거울 위 조명은 깜빡이지 않습니다."),

            new InspectionItem("L-1", "열람석", SpaceId.Library, FearAxis.Layout, AnomalyTemplate.Move, "열람석 의자는 모두 책상 안에 들어가 있습니다."),
            new InspectionItem("L-2", "블라인드", SpaceId.Library, FearAxis.Illuminance, AnomalyTemplate.Light, "블라인드는 모두 내려져 있습니다."),
            new InspectionItem("L-3", "반납 상자", SpaceId.Library, FearAxis.Auditory, AnomalyTemplate.Sound, "반납 상자는 조용합니다."),

            new InspectionItem("K-1", "CCTV 전 채널", SpaceId.SecurityRoom, FearAxis.Layout, AnomalyTemplate.Switch, "CCTV 화면에는 사람이 없습니다.")
        };

        private static readonly Dictionary<string, InspectionItem> ById = BuildIndex();

        /// <summary>모든 항목(공간 순).</summary>
        public static IReadOnlyList<InspectionItem> All
        {
            get { return Items; }
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

        /// <summary>그 공간의 항목들(카탈로그 순).</summary>
        public static List<InspectionItem> InSpace(SpaceId space)
        {
            SpaceId canonical = SpaceIds.Canonical(space);
            List<InspectionItem> list = new List<InspectionItem>();
            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i].Space == canonical)
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
