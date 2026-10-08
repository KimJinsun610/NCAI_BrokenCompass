using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 플레이어 몸 규칙(61차, 2026-10-07 플레이테스트). 화면 쪽 <c>PlayerBody</c>가 쓴다.
    /// <list type="bullet">
    /// <item>키 1.7m → <see cref="Height"/>. 처음엔 2.2m(「플레이어 키 약간 높이기, 2.2로」)였으나 민: 「키가 너무 커, 조금 낮춰 줘, 문은 지나갈 수 있게」 —
    /// 김진선님 사망 컷신 타임라인이 맞춰 둔 눈높이 1.7m에 맞춰 1.95m(눈 1.70m)로 내렸다. 발 위치는 그대로, 눈은 정수리 아래 <see cref="HeadRoom"/>.</item>
    /// <item>점프 금지 — 위로 솟는 속도를 <see cref="MaxRiseSpeed"/>로 묶는다(키 입력 점프는 원래 없고, 가구에 끼었다 튀어 오르는 것까지 막는다).</item>
    /// <item>벽 비비기 뚫림 — 한 프레임 이동을 캡슐로 미리 쓸어 보고 벽을 따라 미끄러지게 한다(<see cref="SlideAlong"/>).</item>
    /// </list>
    /// </summary>
    public static class PlayerBodyRules
    {
        /// <summary>몸 높이(m). 옛 값 1.7 → 61차 2.2 → 1.95(눈 1.70m, 김진선님 컷신 기준 눈높이).</summary>
        public const float Height = 1.95f;

        /// <summary>정수리에서 눈까지(m). 옛 몸(1.7m·눈 1.45m)과 같은 거리.</summary>
        public const float HeadRoom = 0.25f;

        /// <summary>눈높이(바닥에서, m).</summary>
        public const float EyeHeight = Height - HeadRoom;

        /// <summary>이 높이(m) 아래의 턱(문지방 등)은 쓸기에서 무시한다 — 물리가 넘겨 준다.</summary>
        public const float StepHeight = 0.3f;

        /// <summary>벽과 남겨 두는 틈(m).</summary>
        public const float Skin = 0.03f;

        /// <summary>위로 솟는 속도의 상한(m/s). 문지방은 넘되 튀어 오르지는 못한다.</summary>
        public const float MaxRiseSpeed = 0.6f;

        /// <summary>한 프레임에 이보다 멀리(수평, m) 옮겨지면 순간이동(재시작·연출)으로 보고 쓸지 않는다.</summary>
        public const float TeleportDistance = 0.75f;

        /// <summary>
        /// 벽에 막히고 남은 이동을 벽면을 따라 미끄러지게 한다. 벽 법선의 수직 성분은 무시하고,
        /// 벽을 파고드는 성분만 깎는다(벽에서 멀어지는 이동은 그대로).
        /// </summary>
        public static Vector3 SlideAlong(Vector3 rest, Vector3 normal)
        {
            rest.y = 0f;
            normal.y = 0f;
            if (normal.sqrMagnitude < 1e-6f) return Vector3.zero;
            normal.Normalize();
            float into = Vector3.Dot(rest, normal);
            if (into >= 0f) return rest;
            return rest - normal * into;
        }

        /// <summary>위로 솟는 속도를 묶는다.</summary>
        public static float ClampRise(float verticalSpeed)
        {
            return Mathf.Min(verticalSpeed, MaxRiseSpeed);
        }

        /// <summary>순간이동으로 볼 만큼 옮겨졌는지(수평 거리).</summary>
        public static bool IsTeleport(Vector3 delta)
        {
            delta.y = 0f;
            return delta.sqrMagnitude > TeleportDistance * TeleportDistance;
        }

        /// <summary>옛 몸에서 늘어날 높이(m). 이미 늘어 있으면 0.</summary>
        public static float GrowFrom(float currentHeight)
        {
            return currentHeight < Height - 0.01f ? Height - currentHeight : 0f;
        }
    }
}
