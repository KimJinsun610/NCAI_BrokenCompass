using System;

namespace NightDuty
{
    // 71차 — 개발자 모드 「흐름 정지」(민: 「개발자 모드를 켜면 원래 게임의 흐름이 멈추고, 수칙과 연출을 내가 직접 호출하고 상호작용을 확인할 수 있게」).
    public static partial class NightRun
    {
        private static bool _sandbox;
        private static bool _sandboxNoCapture = true;

        /// <summary>
        /// 개발자 모드의 흐름 정지. 켜져 있는 동안
        /// <list type="bullet">
        /// <item>긴장 디렉터가 새 조우·수칙 단서·가짜 놀람을 스스로 걸지 않는다(진행 중인 단계와 디버그 강제 실행은 끝까지 흐른다).</item>
        /// <item>점검 지시·근무 지시를 스스로 내지 않는다(버튼으로 낸 것은 판정된다 — 시간 초과는 멈춘다).</item>
        /// <item>역설 문자·회피 불가 역설을 스스로 보내지 않는다(버튼으로 보낸 뒤의 안전한 읽기·재입실 금지는 그대로).</item>
        /// <item>판정 시간창을 무시한다 — 출근·이완·03:30 뒤에도 늘 판정한다(직접 부른 수칙이 어느 시각에서나 판정되게).</item>
        /// <item><see cref="SandboxNoCapture"/>면 감각 축이 99에서 멈춰 붙잡히지 않는다(디버그 붙잡힘 버튼은 그대로).</item>
        /// </list>
        /// 게임 시계는 개발자 모드(Flow)가 멈추고, 시계가 멈춰 있어도 구동기는 판정·연출 시간을 흘린다(<c>NightRunDriver</c>).
        /// 다른 스위치(<see cref="DirectorAutoRun"/>·<see cref="JudgingWindowEnabled"/>)는 바꾸지 않는다 — 끄면 그대로 돌아간다.
        /// </summary>
        public static bool Sandbox
        {
            get { return _sandbox; }
            set
            {
                if (_sandbox == value) return;
                _sandbox = value;
                ApplySandboxCap();
                UnityEngine.Debug.Log("[NightRun] 개발자 모드 흐름 " + (value ? "정지" : "재개"));
            }
        }

        /// <summary>흐름 정지 중 붙잡히지 않게(감각 축 99에서 멈춤). 기본 켬.</summary>
        public static bool SandboxNoCapture
        {
            get { return _sandboxNoCapture; }
            set
            {
                _sandboxNoCapture = value;
                ApplySandboxCap();
            }
        }

        /// <summary>흐름 정지 중 손전등 배터리가 닳지 않게(Flow <c>FlashlightPower</c>가 읽는다). 기본 켬.</summary>
        public static bool SandboxInfiniteBattery { get; set; } = true;

        /// <summary>흐름 정지 중이고 배터리를 닳지 않게 했는지.</summary>
        public static bool BatteryFrozen
        {
            get { return _sandbox && SandboxInfiniteBattery; }
        }

        /// <summary>
        /// 디버그 엿보기: 판정 신호가 들어올 때마다(밤 진행 중, <see cref="Send"/> 첫머리). 개발자 모드의 「상호작용 확인」이 읽는다.
        /// 게임 동작에는 쓰지 않는다.
        /// </summary>
        public static event Action<JudgeSignal> SignalObserved;

        private static void ApplySandboxCap()
        {
            if (_axes != null) _axes.HardCap = _sandbox && _sandboxNoCapture ? (int?)(Bands.Max - 1) : null;
        }

        private static void ObserveSignalForDebug(in JudgeSignal signal)
        {
            Action<JudgeSignal> h = SignalObserved;
            if (h == null) return;
            try
            {
                h(signal);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        private static void ResetSandbox()
        {
            _sandbox = false;
            _sandboxNoCapture = true;
            SandboxInfiniteBattery = true;
            SignalObserved = null;
            ApplySandboxCap();
        }
    }
}
