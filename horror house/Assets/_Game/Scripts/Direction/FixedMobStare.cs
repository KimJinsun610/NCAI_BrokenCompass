using System;

namespace NightDuty
{
    /// <summary>
    /// 64차(플레이테스트 2026-10-07: 「고정된 몹(인체모형, 인체나무) 계속 보면 바로 수치 계속 떨구고, 엔딩 보도록」) — 고정 몹 응시.
    /// 과학실 인체 모형(<see cref="FinalCues.ModelTarget"/>)이나 사람 나무(<see cref="FinalCues.H1Object"/>)를 계속 바라보면
    /// 유예(<see cref="GraceFor"/>) 뒤부터 배치 축이 오르고, 오래 볼수록 빨리 올라 끝내 붙잡힌다(100).
    /// <list type="bullet">
    /// <item>유예는 역설 「눈으로만」 읽기(<c>ParadoxRun.EyesOnlySeconds</c> 1초)보다 길다 — 「확인하십시오」를 따른 짧은 확인은 벌하지 않는다.
    /// 1일차 2.5초 · 2일차 2초 · 3일차부터 1.5초(공포 그라데이션).</item>
    /// <item>속도 = 초당 <see cref="StartRate"/> + 유예 뒤 경과 초 × <see cref="RateGrowth"/>, 최대 <see cref="MaxRate"/>.
    /// 30에서 시작하면 약 12초, 60에서 시작하면 약 8초 만에 붙잡힌다.</item>
    /// <item>잠깐(<see cref="LookAwayTolerance"/>초 미만) 시선이 빗나가면 이어서 센다. 그보다 오래 떼면 처음부터.</item>
    /// </list>
    /// 판정(축 반영)은 <c>NightRun</c>이 판정 시간창 안에서만 한다. 이 클래스는 연속 응시 시간(<see cref="Seconds"/>)도 내준다 — 모형의 목 꺾임(Flow)이 읽는다.
    /// </summary>
    public sealed class FixedMobStare
    {
        /// <summary>오르는 축.</summary>
        public const FearAxis Axis = FearAxis.Layout;

        /// <summary>오르기 시작한 첫 초당 속도.</summary>
        public const float StartRate = 3f;

        /// <summary>유예 뒤 1초마다 늘어나는 초당 속도.</summary>
        public const float RateGrowth = 0.5f;

        /// <summary>최대 초당 속도.</summary>
        public const float MaxRate = 10f;

        /// <summary>이보다 짧게 빗나간 시선은 끊김으로 보지 않는다(초).</summary>
        public const float LookAwayTolerance = 0.3f;

        /// <summary>출처 ID 머리(<c>stare.</c> + 대상 ID).</summary>
        public const string SourcePrefix = "stare.";

        private float _seconds;
        private float _away;
        private float _carry;
        private string _target = string.Empty;

        /// <summary>지금 대상을 이어서 바라본 시간(초). 대상이 없으면 0.</summary>
        public float Seconds
        {
            get { return _seconds; }
        }

        /// <summary>지금(또는 방금 전까지) 바라보던 고정 몹 ID. 없으면 빈 문자열.</summary>
        public string TargetId
        {
            get { return _target; }
        }

        /// <summary>고정 몹인지(과학실 인체 모형·사람 나무).</summary>
        public static bool IsFixedMob(string id)
        {
            return id == FinalCues.ModelTarget || id == FinalCues.H1Object;
        }

        /// <summary>그날의 유예(초) — 이만큼 바라본 뒤부터 오른다.</summary>
        public static float GraceFor(int day)
        {
            if (day <= 1) return 2.5f;
            if (day == 2) return 2f;
            return 1.5f;
        }

        /// <summary>재시작 카드에 쓸 이름. 응시 출처가 아니면 null.</summary>
        public static string SourceName(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId) || !sourceId.StartsWith(SourcePrefix, StringComparison.Ordinal)) return null;
            string id = sourceId.Substring(SourcePrefix.Length);
            if (id == FinalCues.ModelTarget) return "[응시] 과학실 인체 모형을 오래 바라봄";
            if (id == FinalCues.H1Object) return "[응시] 복도의 사람 나무를 오래 바라봄";
            return "[응시] " + id;
        }

        /// <summary>
        /// 응시 샘플 하나(<paramref name="gazeId"/> = 화면 가운데 대상, <paramref name="dt"/> = 샘플 시간). 이번 샘플로 올릴 정수 점수를 돌려준다(소수는 다음으로 넘긴다).
        /// </summary>
        public int Observe(string gazeId, float dt, int day)
        {
            if (dt <= 0f) return 0;
            if (!IsFixedMob(gazeId))
            {
                if (_target.Length == 0) return 0;
                _away += dt;
                if (_away + 1e-4f >= LookAwayTolerance) Reset();
                return 0;
            }

            if (gazeId != _target)
            {
                _target = gazeId;
                _seconds = 0f;
                _carry = 0f;
            }

            _away = 0f;
            float before = _seconds;
            _seconds += dt;
            float grace = GraceFor(day);
            float over = _seconds - grace;
            if (over <= 0f) return 0;

            // 유예를 넘는 몫만 — 속도는 구간 가운데에서 잰다.
            float span = Math.Min(dt, _seconds - Math.Max(before, grace));
            float mid = over - span * 0.5f;
            float rate = Math.Min(MaxRate, StartRate + RateGrowth * mid);
            _carry += rate * span;
            int whole = (int)(_carry + 1e-3f);   // 0.1초 샘플의 부동소수 오차(6.9999…)를 정수로 넘긴다
            _carry -= whole;
            return whole;
        }

        /// <summary>처음부터(새 밤·재시작·시선을 뗐을 때).</summary>
        public void Reset()
        {
            _seconds = 0f;
            _away = 0f;
            _carry = 0f;
            _target = string.Empty;
        }
    }
}
