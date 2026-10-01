using System;

namespace NightDuty
{
    /// <summary>
    /// 놀람 예산(최종 기획서 「놀람 예산」). 한 밤에 강도별로 몇 번까지, 강도 3↑ 사이 90초·강도 4↑ 뒤 120초,
    /// 조우가 끝난 뒤 60~90초는 위협을 걸지 않는다. 시간은 디렉터의 실제 초다.
    /// <para>「가까이」 놀람·가짜 놀람·신뢰 연출은 세지 않는다.</para>
    /// </summary>
    public sealed class SurpriseBudget
    {
        /// <summary>강도 3↑ 시작 사이 최소 간격(초).</summary>
        public const float GapAfter3 = 90f;

        /// <summary>강도 4↑ 시작 뒤 최소 간격(초).</summary>
        public const float GapAfter4 = 120f;

        /// <summary>조우가 끝난 뒤 위협을 걸지 않는 시간(초) 최소.</summary>
        public const float QuietMin = 60f;

        /// <summary>최대.</summary>
        public const float QuietMax = 90f;

        private readonly int _day;
        private int _used3;
        private int _used4;
        private int _used5;
        private float _last3 = float.NegativeInfinity;
        private float _last4 = float.NegativeInfinity;
        private float _quietUntil = float.NegativeInfinity;

        /// <summary>그 일차의 예산.</summary>
        public SurpriseBudget(int day)
        {
            _day = Math.Max(1, day);
        }

        /// <summary>강도 3 이상 상한.</summary>
        public int Cap3
        {
            get { return _day <= 2 ? 3 : 4; }
        }

        /// <summary>강도 4 이상 상한.</summary>
        public int Cap4
        {
            get { return _day <= 2 ? 1 : 2; }
        }

        /// <summary>강도 5 상한(3~4일차는 교차 조건을 채운 경우만 1).</summary>
        public int Cap5(bool isCross)
        {
            if (_day <= 2) return 0;
            if (_day <= 4) return isCross ? 1 : 0;
            return 1;
        }

        /// <summary>쓴 횟수(강도 3↑, 4↑, 5).</summary>
        public string Usage
        {
            get { return "3↑ " + _used3 + "/" + Cap3 + " · 4↑ " + _used4 + "/" + Cap4 + " · 5 " + _used5; }
        }

        /// <summary>조우 뒤 휴지가 끝나는 시각(초).</summary>
        public float QuietUntil
        {
            get { return _quietUntil; }
        }

        /// <summary>지금(<paramref name="now"/>) 그 강도의 조우를 시작할 수 있는지. 아니면 <paramref name="why"/>에 사유.</summary>
        public bool CanStart(int intensity, bool isCross, float now, out string why)
        {
            why = string.Empty;
            if (now < _quietUntil)
            {
                why = "조우 뒤 휴지 " + (_quietUntil - now).ToString("0") + "초";
                return false;
            }

            if (intensity >= 5 && _used5 >= Cap5(isCross))
            {
                why = "강도 5 상한";
                return false;
            }

            if (intensity >= 4)
            {
                if (_used4 >= Cap4)
                {
                    why = "강도 4↑ 상한";
                    return false;
                }

                if (now - _last4 < GapAfter4)
                {
                    why = "강도 4↑ 간격";
                    return false;
                }
            }

            if (intensity >= 3)
            {
                if (_used3 >= Cap3)
                {
                    why = "강도 3↑ 상한";
                    return false;
                }

                if (now - _last3 < GapAfter3 || now - _last4 < GapAfter4)
                {
                    why = "강도 3↑ 간격";
                    return false;
                }
            }

            return true;
        }

        /// <summary>조우가 대면을 시작했다.</summary>
        public void Commit(int intensity, float now)
        {
            if (intensity >= 3)
            {
                _used3++;
                _last3 = now;
            }

            if (intensity >= 4)
            {
                _used4++;
                _last4 = now;
            }

            if (intensity >= 5) _used5++;
        }

        /// <summary>조우가 끝났다 — 휴지를 건다(<paramref name="quietSeconds"/>는 60~90에서 고른 값).</summary>
        public void EndEncounter(float now, float quietSeconds)
        {
            _quietUntil = Math.Max(_quietUntil, now + quietSeconds);
        }

        /// <summary>재시작 — 사용량은 그대로 두고(같은 밤의 연장) 간격·휴지만 푼다.</summary>
        public void ResetTimers()
        {
            _last3 = float.NegativeInfinity;
            _last4 = float.NegativeInfinity;
            _quietUntil = float.NegativeInfinity;
        }
    }

    /// <summary>디렉터의 강도 단계(최종 기획서 「긴장 디렉터」 — 생존 수치로 판단).</summary>
    public enum DirectorMood
    {
        /// <summary>가장 높은 감각 생존 수치 0–49 — 대응 창 −20%, 가짜 놀람 +1.</summary>
        Easy = 0,

        /// <summary>50–74 — 기본값.</summary>
        Normal = 1,

        /// <summary>75–99 또는 재시작 k≥2 — 전조 길게, 대응 창 +30%, 헛예고 절반.</summary>
        Danger = 2
    }

    /// <summary>강도 단계의 수치.</summary>
    public static class DirectorMoods
    {
        /// <summary>최고 감각 생존 수치와 재시작 횟수로 단계를 정한다.</summary>
        public static DirectorMood Of(int highestSensory, int restarts)
        {
            if (highestSensory >= 75 || restarts >= RestartPolicy.CalmFrom) return DirectorMood.Danger;
            return highestSensory >= 50 ? DirectorMood.Normal : DirectorMood.Easy;
        }

        /// <summary>대응 창 배율.</summary>
        public static float WindowScale(DirectorMood m)
        {
            return m == DirectorMood.Easy ? 0.8f : m == DirectorMood.Danger ? 1.3f : 1f;
        }

        /// <summary>전조 길이 배율.</summary>
        public static float ForeshadowScale(DirectorMood m)
        {
            return m == DirectorMood.Danger ? 1.5f : 1f;
        }

        /// <summary>헛예고 확률 배율.</summary>
        public static float FalseScale(DirectorMood m)
        {
            return m == DirectorMood.Danger ? 0.5f : 1f;
        }

        /// <summary>가짜 놀람 추가 횟수.</summary>
        public static int ExtraFakes(DirectorMood m)
        {
            return m == DirectorMood.Easy ? 1 : 0;
        }
    }
}
