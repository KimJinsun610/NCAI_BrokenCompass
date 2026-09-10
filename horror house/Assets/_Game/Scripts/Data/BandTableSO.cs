using System;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 한 구간(Band)의 조명 표현 값 한 줄. 색온도·세기·색 보정을 담는다.
    /// </summary>
    /// <remarks>
    /// 이 값들은 구간 경계에서 그대로 쓰이고, 구간 내부에서는
    /// <see cref="BandTableSO"/>가 다음 구간 값과 보간하여 사용한다.
    /// </remarks>
    [Serializable]
    public struct BandRow
    {
        /// <summary>색온도(켈빈). 6500 백색 → 1200 진한 빨강 방향으로 내려간다.</summary>
        [Range(1000f, 10000f)]
        public float Kelvin;

        /// <summary>이 구간의 조명 세기 배수. 1.0이 기본 밝기.</summary>
        [Range(0f, 2f)]
        public float Intensity;

        /// <summary>색 보정. 기본은 흰색이며, 최상위 구간에서만 붉은 기를 준다.</summary>
        public Color Tint;
    }

    /// <summary>
    /// 한 공간이 각 구간에서 켜 두는 형광등 개수. 배열 길이는 5(Band0~Band4).
    /// </summary>
    /// <remarks>
    /// 조도 표 자체는 전 공간이 공유하고, 공간마다 다른 것은 이 등 개수뿐이다.
    /// 덕분에 명목상 20개(4공간 × 5구간)의 상태가 실작업 5개로 압축된다.
    /// </remarks>
    [Serializable]
    public struct SpaceLightCount
    {
        /// <summary>대상 공간.</summary>
        public SpaceId Space;

        /// <summary>구간별 점등 개수. 인덱스는 <see cref="Band"/>의 정수값(0~4).</summary>
        public int[] LitCountPerBand;
    }

    /// <summary>
    /// 조도(Illuminance) 축의 구간별 조명 표현을 담는 공유 데이터 에셋.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MonoBehaviour의 인스펙터 값은 Play 모드에서 바꿔도 종료하면 되돌아간다.
    /// ScriptableObject 에셋은 Play 중에 고친 값이 그대로 남는다.
    /// 이 게임은 "등 4개 3200K가 진짜 불안한가"를 표만 보고는 정할 수 없고
    /// 실제로 서 보며 눈으로만 정할 수 있으므로, 플레이하면서 값을 만지고
    /// 그 값이 유지되는 것이 밸런싱의 전제다. 그래서 이 표는 에셋이다.
    /// </para>
    /// <para>
    /// 구간 폭은 균일하지 않다: Band0=0~24, Band1=25~49, Band2=50~74,
    /// Band3=75~89(15칸), Band4=90~100(11칸).
    /// <see cref="Bands.RedThreshold"/>(=75)는 "조명이 붉게 보인다면"으로 시작하는
    /// 근무수칙이 참조하는 임계이며, 손전등과는 무관하게 조도 축 값으로만 판정한다.
    /// 50~74(3200K)는 붉게 보이지만 붉음으로 치지 않는 회색지대로 의도된 것이다.
    /// </para>
    /// <para>
    /// 등 개수의 기획 확정값:
    /// Band0 복도 8 / 교실 8 / 화장실 4 / 과학실 4,
    /// Band1 6 / 6 / 3 / 3, Band2 4 / 4 / 2 / 2, Band3 2 / 2 / 1 / 1,
    /// Band4 0 / 0 / 0 / <b>1</b>.
    /// </para>
    /// <para>
    /// <b>과학실 Band4가 0이 아니라 1인 것은 의도된 예외다.</b>
    /// 근무수칙 7번이 "손전등을 끄고 점검하십시오"이고 1번이
    /// "인체 모형이 제자리에 있는지 확인하십시오"(응시 1초)인데,
    /// 완전 암흑이면 판정은 통과하지만 플레이어는 아무것도 볼 수 없다.
    /// 그래서 모형 실루엣만 겨우 드러나는 붉은 잔광 한 등을 남긴다.
    /// 이 1은 버그가 아니므로 0으로 "고치지" 말 것.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "NightDuty/Band Table", fileName = "BandTable")]
    public sealed class BandTableSO : ScriptableObject
    {
        /// <summary>이 표가 기대하는 행 수(Band0~Band4).</summary>
        public const int ExpectedRows = 5;

        [SerializeField]
        private BandRow[] _rows = new BandRow[ExpectedRows];

        [SerializeField]
        private SpaceLightCount[] _spaces = new SpaceLightCount[0];

        /// <summary>구간별 조명 값 행. 길이 5, 인덱스는 Band0~Band4.</summary>
        public BandRow[] Rows
        {
            get { return _rows; }
        }

        /// <summary>
        /// 에셋이 로드될 때 표가 비어 있거나 행 수가 맞지 않으면 기획 확정값으로 자가 치유한다.
        /// 새로 만든 에셋이 빈 채로 보여 "표가 없다"는 오해를 만들지 않기 위함이다.
        /// </summary>
        private void OnEnable()
        {
            if (_rows == null || _rows.Length != ExpectedRows)
            {
                ResetToDesignDefaults();
            }
        }

        /// <summary>
        /// 지정 구간의 색온도를 반환한다. 구간 내부에서는 다음 구간 값으로 보간하며,
        /// 마지막 구간(Band4)은 다음이 없으므로 자기 값을 유지한다.
        /// </summary>
        /// <param name="band">대상 구간.</param>
        /// <param name="progress01">구간 안에서의 진행도. 0..1로 클램프된다.</param>
        public float KelvinFor(Band band, float progress01)
        {
            int index = ClampIndex((int)band);
            if (!HasRows())
            {
                return 6500f;
            }

            float t = Mathf.Clamp01(progress01);
            float from = _rows[index].Kelvin;
            if (index >= _rows.Length - 1)
            {
                return from;
            }

            return Mathf.Lerp(from, _rows[index + 1].Kelvin, t);
        }

        /// <summary>
        /// 지정 구간의 조명 세기 배수를 반환한다. 보간 규칙은 <see cref="KelvinFor"/>와 같다.
        /// </summary>
        /// <param name="band">대상 구간.</param>
        /// <param name="progress01">구간 안에서의 진행도. 0..1로 클램프된다.</param>
        public float IntensityFor(Band band, float progress01)
        {
            int index = ClampIndex((int)band);
            if (!HasRows())
            {
                return 1f;
            }

            float t = Mathf.Clamp01(progress01);
            float from = _rows[index].Intensity;
            if (index >= _rows.Length - 1)
            {
                return from;
            }

            return Mathf.Lerp(from, _rows[index + 1].Intensity, t);
        }

        /// <summary>
        /// 지정 구간의 색 보정을 반환한다. 보간 규칙은 <see cref="KelvinFor"/>와 같다.
        /// </summary>
        /// <param name="band">대상 구간.</param>
        /// <param name="progress01">구간 안에서의 진행도. 0..1로 클램프된다.</param>
        public Color TintFor(Band band, float progress01)
        {
            int index = ClampIndex((int)band);
            if (!HasRows())
            {
                return Color.white;
            }

            float t = Mathf.Clamp01(progress01);
            Color from = _rows[index].Tint;
            if (index >= _rows.Length - 1)
            {
                return from;
            }

            return Color.Lerp(from, _rows[index + 1].Tint, t);
        }

        /// <summary>
        /// 해당 공간이 이 구간에서 켜 두는 형광등 개수를 반환한다.
        /// </summary>
        /// <param name="space">대상 공간.</param>
        /// <param name="band">대상 구간.</param>
        /// <remarks>
        /// 공간을 찾지 못하면 0이 아니라 <b>첫 번째 항목의 값</b>을 fallback으로 쓴다.
        /// 아직 표에 등록되지 않은 공간에서 0을 반환하면 그 공간의 등이 전부 꺼져
        /// 조도 축이 최대인 것처럼 보이고, 데이터 누락이 연출 버그로 오인되기 때문이다.
        /// 항목이 하나도 없을 때만 0을 반환한다.
        /// </remarks>
        public int LitCountFor(SpaceId space, Band band)
        {
            if (_spaces == null || _spaces.Length == 0)
            {
                return 0;
            }

            int bandIndex = ClampIndex((int)band);

            for (int i = 0; i < _spaces.Length; i++)
            {
                if (_spaces[i].Space == space)
                {
                    return ReadCount(_spaces[i], bandIndex);
                }
            }

            // 등록되지 않은 공간: 첫 항목을 대역으로 쓴다(위 주석 참조).
            return ReadCount(_spaces[0], bandIndex);
        }

        /// <summary>
        /// 기획이 확정한 값으로 구간 표와 공간별 등 개수를 되돌린다.
        /// 밸런싱 중 값이 엉켰을 때의 복구 지점이다.
        /// </summary>
        [ContextMenu("기획 확정값으로 되돌리기")]
        public void ResetToDesignDefaults()
        {
            _rows = new BandRow[ExpectedRows];

            _rows[0] = MakeRow(6500f, 1.00f, Color.white);   // Band0 : 6500K 백색
            _rows[1] = MakeRow(4500f, 0.90f, Color.white);   // Band1 : 4500K 옅은 노랑
            _rows[2] = MakeRow(3200f, 0.75f, Color.white);   // Band2 : 3200K 주황(붉게 보이나 붉음은 아님)
            _rows[3] = MakeRow(2000f, 0.55f, Color.white);   // Band3 : 2000K 적갈, RedThreshold 진입
            _rows[4] = MakeRow(1200f, 0.15f, new Color(1f, 0.45f, 0.35f)); // Band4 : 진한 빨강

            _spaces = new SpaceLightCount[5];
            _spaces[0] = MakeSpace(SpaceId.Corridor, 8, 6, 4, 2, 0);
            _spaces[1] = MakeSpace(SpaceId.Toilet, 4, 3, 2, 1, 0);
            _spaces[2] = MakeSpace(SpaceId.Classroom_1_1, 8, 6, 4, 2, 0);
            _spaces[3] = MakeSpace(SpaceId.Classroom_1_3, 8, 6, 4, 2, 0);
            // 과학실 Band4만 1: 손전등을 끈 채 인체 모형을 응시해야 하는 수칙 때문에
            // 완전 암흑을 피하고 실루엣용 붉은 잔광 한 등을 남긴다. 의도된 예외다.
            _spaces[4] = MakeSpace(SpaceId.ScienceRoom, 4, 3, 2, 1, 1);
        }

        /// <summary>행 배열이 사용 가능한 상태인지 검사한다.</summary>
        private bool HasRows()
        {
            return _rows != null && _rows.Length > 0;
        }

        /// <summary>구간 인덱스를 현재 행 배열의 유효 범위로 클램프한다.</summary>
        private int ClampIndex(int index)
        {
            int last = (_rows == null || _rows.Length == 0) ? 0 : _rows.Length - 1;
            if (index < 0)
            {
                return 0;
            }
            if (index > last)
            {
                return last;
            }
            return index;
        }

        /// <summary>한 공간 항목에서 구간 인덱스의 등 개수를 안전하게 읽는다.</summary>
        private static int ReadCount(SpaceLightCount entry, int bandIndex)
        {
            int[] counts = entry.LitCountPerBand;
            if (counts == null || counts.Length == 0)
            {
                return 0;
            }

            int i = bandIndex;
            if (i < 0)
            {
                i = 0;
            }
            if (i > counts.Length - 1)
            {
                i = counts.Length - 1;
            }

            int value = counts[i];
            return value < 0 ? 0 : value;
        }

        /// <summary>기본값 채우기용 행 생성 도우미.</summary>
        private static BandRow MakeRow(float kelvin, float intensity, Color tint)
        {
            BandRow row = new BandRow();
            row.Kelvin = kelvin;
            row.Intensity = intensity;
            row.Tint = tint;
            return row;
        }

        /// <summary>기본값 채우기용 공간 항목 생성 도우미.</summary>
        private static SpaceLightCount MakeSpace(SpaceId space, int b0, int b1, int b2, int b3, int b4)
        {
            SpaceLightCount entry = new SpaceLightCount();
            entry.Space = space;
            entry.LitCountPerBand = new int[ExpectedRows] { b0, b1, b2, b3, b4 };
            return entry;
        }
    }
}
