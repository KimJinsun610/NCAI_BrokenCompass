using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 판정 공통 정의(2026-09-30 최종 기획서 「공간별 설계」 머리의 공통 정의). 표에 따로 적지 않으면 이 값을 쓴다.
    /// <b>센서가 이 숫자를 각자 들고 있지 않게 여기 한 곳에 둔다.</b>
    /// <list type="bullet">
    /// <item>응시(본다): 대상의 판정 콜라이더 중심이 응시 기준점(보통 화면 중심, 태블릿을 든 동안은 태블릿 위 가운데 = 화면 높이 78%)에서
    /// 10° 안에 있고 레이로 가려지지 않음. 0.2초 이내의 끊김은 연속으로 본다.</item>
    /// <item>비춤: 대상이 손전등 원뿔 15° 안, 8m 안에 있고 가려지지 않음.</item>
    /// <item>정지: 방아쇠 순간의 위치에서 수평 변위 0.2m 이하. 회전은 허용.</item>
    /// <item>보고: 점검 대상 2m 안에서 1초 응시 → 보고 가능(2m 안에 있는 동안 유지) → [정상]/[이상] 0.5초 길게 눌러 확정.</item>
    /// <item>점검 수칙 「가까이」: 0.8m(보고 거리 2m와 따로 둔다).</item>
    /// </list>
    /// </summary>
    public static class SensingRules
    {
        /// <summary>응시 원뿔 반각(°).</summary>
        public const float GazeConeDegrees = 10f;

        /// <summary>응시가 끊겨도 연속으로 보는 틈(초).</summary>
        public const float GazeGapSeconds = 0.2f;

        /// <summary>손전등 비춤 원뿔 반각(°).</summary>
        public const float BeamConeDegrees = 15f;

        /// <summary>손전등 비춤 거리(m).</summary>
        public const float BeamRange = 8f;

        /// <summary>정지로 보는 수평 변위 한도(m).</summary>
        public const float StillRadius = 0.2f;

        /// <summary>보고가 켜지는 거리(m).</summary>
        public const float ReportRange = 2f;

        /// <summary>보고가 켜지기까지의 응시 시간(초).</summary>
        public const float ReportGazeSeconds = 1f;

        /// <summary>[정상]/[이상] 확정에 필요한 길게 누르기(초).</summary>
        public const float ReportHoldSeconds = 0.5f;

        /// <summary>점검 수칙 「가까이」 거리(m).</summary>
        public const float NearRange = 0.8f;

        /// <summary>「가까이」를 들여다보기로 치는 응시 시간(초). 지나가다 스친 것은 세지 않는다.</summary>
        public const float NearGazeSeconds = 0.3f;

        /// <summary>태블릿을 든 동안의 응시 기준점 높이(뷰포트 0~1).</summary>
        public const float TabletGazeViewportY = 0.78f;

        /// <summary>평소 응시 기준점 높이(뷰포트, 화면 중심).</summary>
        public const float NormalGazeViewportY = 0.5f;

        /// <summary>응시 기준점 높이(뷰포트). 태블릿을 들었으면 78%.</summary>
        public static float GazeViewportY(bool tabletRaised)
        {
            return tabletRaised ? TabletGazeViewportY : NormalGazeViewportY;
        }

        /// <summary>점이 원뿔(꼭짓점 <paramref name="origin"/>, 축 <paramref name="forward"/>, 반각 <paramref name="halfAngle"/>) 안에 있는지.</summary>
        public static bool InCone(Vector3 origin, Vector3 forward, Vector3 point, float halfAngle)
        {
            Vector3 to = point - origin;
            if (to.sqrMagnitude < 1e-6f) return true;
            return Vector3.Angle(forward, to) <= halfAngle;
        }

        /// <summary>손전등 비춤 판정(가림은 부르는 쪽이 레이로 본다).</summary>
        public static bool InBeam(Vector3 lightPos, Vector3 lightForward, Vector3 point)
        {
            return (point - lightPos).sqrMagnitude <= BeamRange * BeamRange && InCone(lightPos, lightForward, point, BeamConeDegrees);
        }

        /// <summary>수평 거리(m).</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>방아쇠 순간 위치에서 정지로 보는 범위 안인지(회전은 허용).</summary>
        public static bool IsStill(Vector3 anchor, Vector3 now)
        {
            return HorizontalDistance(anchor, now) <= StillRadius;
        }
    }

    /// <summary>
    /// 점검 항목 하나의 보고 준비 상태(순수 계산 — 센서가 0.1초마다 먹인다).
    /// 2m 안에서 1초 응시하면 켜지고(0.2초 이내 끊김은 연속), 켜진 뒤에는 2m 안에 있는 동안 유지된다. 2m를 벗어나면 처음부터.
    /// 0.8m 안에서 0.3초 들여다보면 「가까이」를 한 번 알린다.
    /// </summary>
    public sealed class ReportReadiness
    {
        private float _gaze;
        private float _gap;
        private float _nearGaze;

        /// <summary>보고할 수 있는 상태인지.</summary>
        public bool Ready { get; private set; }

        /// <summary>지금까지 모은 응시 시간(초, 0~1).</summary>
        public float GazeProgress
        {
            get { return Mathf.Clamp01(_gaze / SensingRules.ReportGazeSeconds); }
        }

        /// <summary>비운다.</summary>
        public void Reset()
        {
            Ready = false;
            _gaze = 0f;
            _gap = 0f;
            _nearGaze = 0f;
        }

        /// <summary>
        /// 한 샘플을 먹인다.
        /// </summary>
        /// <param name="distance">대상과의 거리(m).</param>
        /// <param name="gazing">이번 샘플에 대상을 응시했는지.</param>
        /// <param name="dt">샘플 시간(초).</param>
        /// <param name="nearTriggered">이번 샘플에서 「가까이」(0.8m 안 0.3초 들여다보기)가 처음 성립했으면 true.</param>
        /// <returns>준비 상태가 바뀌었으면 true.</returns>
        public bool Feed(float distance, bool gazing, float dt, out bool nearTriggered)
        {
            nearTriggered = false;
            bool before = Ready;

            if (distance > SensingRules.ReportRange)
            {
                Reset();
                return before != Ready;
            }

            if (gazing)
            {
                _gaze += dt + _gap;   // 0.2초 이내의 끊김은 이어 붙인다.
                _gap = 0f;
            }
            else if (_gaze > 0f)
            {
                _gap += dt;
                if (_gap > SensingRules.GazeGapSeconds)
                {
                    _gaze = 0f;
                    _gap = 0f;
                }
            }

            if (!Ready && _gaze >= SensingRules.ReportGazeSeconds - 1e-4f)
            {
                Ready = true;
            }

            if (distance <= SensingRules.NearRange && gazing)
            {
                float was = _nearGaze;
                _nearGaze += dt;
                nearTriggered = was < SensingRules.NearGazeSeconds - 1e-4f && _nearGaze >= SensingRules.NearGazeSeconds - 1e-4f;
            }
            else if (distance > SensingRules.NearRange)
            {
                _nearGaze = 0f;
            }

            return before != Ready;
        }
    }
}
