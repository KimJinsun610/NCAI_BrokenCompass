using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 공간별 이상현상 표 — 공간 × 축(청각·조도·배치) × 구간(Band0~4).
    /// <para>
    /// 기획서 각 공간의 「이상현상 청각 · 조도 · 배치」 표를 연출이 읽을 수 있는 형태로 옮긴 <b>초기값</b>이다.
    /// 정본은 기획서이며, 문구(<see cref="Cell.Label"/>)는 확인용 요약이다.
    /// 큐 ID(<see cref="Cell.Cues"/>)는 연출이 재생·표시할 단위이고, 실제 음원·소품은 클라이언트가 이 ID에 연결한다.
    /// </para>
    /// <para>
    /// 조도의 실제 수치(등 개수·색온도)는 <see cref="BandTableSO"/>가 소유한다. 이 표의 조도 칸은 설명만 담는다.
    /// 교실 1-1과 1-3은 같은 표를 쓴다(<see cref="Group"/>).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "NightDuty/Space Anomaly Table", fileName = "SpaceAnomalyTable")]
    public sealed class SpaceAnomalyTableSO : ScriptableObject
    {
        /// <summary>표 한 칸.</summary>
        [Serializable]
        public sealed class Cell
        {
            [Tooltip("공간 (교실은 Classroom_1_1로 적는다)")]
            public SpaceId Space;

            [Tooltip("청각·조도·배치 중 하나")]
            public FearAxis Axis;

            public Band Band;

            [TextArea(1, 3), Tooltip("확인용 요약 문구. 정본은 기획서")]
            public string Label = string.Empty;

            [Tooltip("재생·표시 단위 ID. 청각은 재생 순서대로, 배치는 이 구간에서 보여야 하는 상태 목록")]
            public string[] Cues = new string[0];
        }

        private static readonly string[] NoCues = new string[0];

        [SerializeField]
        private Cell[] _cells = new Cell[0];

        /// <summary>전체 칸.</summary>
        public IReadOnlyList<Cell> Cells
        {
            get { return _cells ?? new Cell[0]; }
        }

        /// <summary>표에서 쓰는 공간 그룹. 교실 1-3은 1-1 표를 쓴다.</summary>
        public static SpaceId Group(SpaceId space)
        {
            return space == SpaceId.Classroom_1_3 ? SpaceId.Classroom_1_1 : space;
        }

        /// <summary>칸을 찾는다. 없으면 null.</summary>
        public Cell Find(SpaceId space, FearAxis axis, Band band)
        {
            if (_cells == null)
            {
                return null;
            }

            SpaceId group = Group(space);
            for (int i = 0; i < _cells.Length; i++)
            {
                Cell c = _cells[i];
                if (c != null && c.Space == group && c.Axis == axis && c.Band == band)
                {
                    return c;
                }
            }

            return null;
        }

        /// <summary>요약 문구. 없으면 빈 문자열.</summary>
        public string LabelFor(SpaceId space, FearAxis axis, Band band)
        {
            Cell c = Find(space, axis, band);
            return c != null ? c.Label : string.Empty;
        }

        /// <summary>큐 ID 목록. 없으면 빈 배열.</summary>
        public IReadOnlyList<string> CuesFor(SpaceId space, FearAxis axis, Band band)
        {
            Cell c = Find(space, axis, band);
            return c != null && c.Cues != null ? c.Cues : NoCues;
        }

        /// <summary>에디터 도구가 표를 채울 때 쓴다.</summary>
        internal void SetCells(Cell[] cells)
        {
            _cells = cells ?? new Cell[0];
        }
    }
}
