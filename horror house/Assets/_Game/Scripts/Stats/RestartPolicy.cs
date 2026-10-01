using System;

namespace NightDuty
{
    /// <summary>
    /// 붙잡힌 뒤 그 밤을 다시 하는 규칙(2026-09-30 최종 기획서 「붙잡힘과 재시작」·「무한 루프 방지 장치」).
    /// <para>
    /// <b>k</b>는 그 밤 몇 번째 재시작인지다(첫 시도에서 붙잡혀 다시 하면 k = 1). 체크포인트에서 다시 하는 것도 센다.
    /// 시뮬(서툰 플레이어 4,000회 × 시드 5개)에서 이 규칙들로 한 밤 재시작 4회 이하 99.8%, 무한 루프 0이다.
    /// </para>
    /// <list type="table">
    /// <item><term>0</term><description>모든 감각 축 −10×k, 하한 min(스냅샷 값, 40). 빼면 한 밤 4회 이하 41.9%, 결근 46.8% — 핵심.</description></item>
    /// <item><term>1</term><description>k≥2: 보너스 슬롯 C 끄기, 헛예고 절반.</description></item>
    /// <item><term>2</term><description>k≥3: 붙잡힌 축의 공간 수칙 1장을 다른 축 수칙으로 교체.</description></item>
    /// <item><term>3</term><description>처벌·정산 델타로는 붙잡히지 않음(<see cref="Deltas.SoftCap"/>).</description></item>
    /// <item><term>4</term><description>출근·이완 구간 판정 정지(<see cref="NightClock.IsJudging"/>).</description></item>
    /// <item><term>5</term><description>체크포인트(02:16 중간 서명).</description></item>
    /// <item><term>6</term><description>6번째 시도도 붙잡히면 결근. k≥4부터는 스스로 조퇴(결과는 결근과 같음).</description></item>
    /// </list>
    /// </summary>
    public static class RestartPolicy
    {
        /// <summary>재시작 보정 하한. 이보다 낮은 값에서 시작한 축은 보정하지 않는다 — 일부러 죽어서 축을 깎는 악용을 막는다.</summary>
        public const int ReliefFloor = 40;

        /// <summary>이 수만큼 재시작한 뒤(6번째 시도) 또 붙잡히면 결근이다.</summary>
        public const int AbsenceAfterRestarts = 5;

        /// <summary>결근한 다음 밤 감각 축의 상한.</summary>
        public const int AbsenceNextNightCap = 60;

        /// <summary>이 k부터 보너스 슬롯 C를 끄고 헛예고를 절반으로 줄인다(장치 1).</summary>
        public const int CalmFrom = 2;

        /// <summary>이 k부터 붙잡힌 축의 공간 수칙 1장을 다른 축 수칙으로 바꾼다(장치 2).</summary>
        public const int SwapRuleFrom = 3;

        /// <summary>이 k부터 근무일지에서 「조퇴」를 고를 수 있다.</summary>
        public const int VoluntaryLeaveFrom = 4;

        /// <summary>
        /// k번째 재시작의 감각 축 시작값 = max(min(스냅샷, 40), 스냅샷 − 10×k).
        /// 신뢰에는 쓰지 않는다(신뢰는 스냅샷 그대로).
        /// </summary>
        /// <param name="snapshotValue">스냅샷의 생존 수치.</param>
        /// <param name="k">그 밤 몇 번째 재시작인지(1부터).</param>
        public static int RestoredValue(int snapshotValue, int k)
        {
            if (k <= 0)
            {
                return snapshotValue;
            }

            int floor = Math.Min(snapshotValue, ReliefFloor);
            return Math.Max(floor, snapshotValue - Deltas.RestartRelief * k);
        }

        /// <summary>이미 이만큼 재시작한 밤에서 또 붙잡히면 결근인지.</summary>
        public static bool IsAbsence(int restartsSoFar)
        {
            return restartsSoFar >= AbsenceAfterRestarts;
        }

        /// <summary>결근한 다음 밤의 감각 축 값 = min(현재, 60).</summary>
        public static int AbsenceValue(int current)
        {
            return Math.Min(current, AbsenceNextNightCap);
        }

        /// <summary>보너스 슬롯 C를 켜도 되는지(장치 1).</summary>
        public static bool SlotCAllowed(int restarts)
        {
            return restarts < CalmFrom;
        }

        /// <summary>헛예고를 절반으로 줄이는지(장치 1).</summary>
        public static bool HalveFakeForewarnings(int restarts)
        {
            return restarts >= CalmFrom;
        }

        /// <summary>붙잡힌 축의 공간 수칙을 바꾸는지(장치 2).</summary>
        public static bool SwapCapturedRule(int restarts)
        {
            return restarts >= SwapRuleFrom;
        }

        /// <summary>스스로 조퇴를 고를 수 있는지.</summary>
        public static bool CanLeaveVoluntarily(int restarts)
        {
            return restarts >= VoluntaryLeaveFrom;
        }
    }

    /// <summary>재시작 요청의 결과 종류.</summary>
    public enum RestartKind
    {
        /// <summary>재시작할 밤이 없었다(포획되지 않았거나 밤이 시작되지 않음).</summary>
        None = 0,

        /// <summary>밤 시작 스냅샷에서 다시 한다.</summary>
        FromNightStart = 1,

        /// <summary>02:16 체크포인트에서 다시 한다.</summary>
        FromCheckpoint = 2,

        /// <summary>결근(6번째 시도에서 또 붙잡힘) 또는 조퇴 — 판정 없이 그 밤을 넘긴다.</summary>
        Absent = 3
    }

    /// <summary>재시작 요청의 결과. 구동기는 이것을 보고 씬·시계를 되돌린다.</summary>
    public readonly struct RestartResult
    {
        /// <summary>결과 종류.</summary>
        public readonly RestartKind Kind;

        /// <summary>그 밤 몇 번째 재시작인지(결근이면 마지막 k).</summary>
        public readonly int K;

        /// <summary>게임 시계를 돌려놓을 시각(근무 시작부터의 분). 결근이면 -1.</summary>
        public readonly int StartMinute;

        /// <summary>붙잡힌 축. 조퇴면 의미 없음.</summary>
        public readonly FearAxis CapturedAxis;

        /// <summary>결과를 만든다.</summary>
        public RestartResult(RestartKind kind, int k, int startMinute, FearAxis capturedAxis)
        {
            Kind = kind;
            K = k;
            StartMinute = startMinute;
            CapturedAxis = capturedAxis;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Kind + " k=" + K + " @" + StartMinute + " (" + CapturedAxis + ")";
        }
    }
}
