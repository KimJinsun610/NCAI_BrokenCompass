using System;

namespace NightDuty
{
    /// <summary>
    /// 태블릿 들기 제한(61차, 2026-10-07 플레이테스트: 「플레이어가 태블릿을 너무 올리고 다닌다」).
    /// 화면 쪽(<c>TabletZoom</c> — 태블릿은 늘 손에 들려 있고 「올림」 = 확대)이 매 프레임 <see cref="Tick"/>을 부르고, true면 확대를 푼다.
    /// 주요 연출 중에는 화면 쪽이 손의 태블릿까지 시야 밖으로 내린다.
    /// <para>69차(민: 「태블릿을 들었을 때 5초 뒤에 내려오는 건 폐기 — 연출이 나오면 내려오는 건 유지」): 들고 있는 시간으로는 내리지 않는다.</para>
    /// <para>73차(민: 「토글로 바꾸자」): 내린 뒤 쉬는 시간도 없앴다 — <b>Tab은 순수 토글</b>이고, 막 내린 직후에도 바로 다시 든다.</para>
    /// <list type="bullet">
    /// <item>주요 연출(조우의 전조·대면·마무리, 몹을 세워 둔 동안 — <c>TensionDirector.Busy</c>) 동안은 들 수 없고, 들고 있었으면 곧바로 내린다.</item>
    /// <item>그 밖에는 **무엇도 저절로 내리지 않는다** — 들고 있는 시간도, 방금 내렸다는 사실도 다시 드는 것을 막지 않는다.</item>
    /// </list>
    /// </summary>
    public sealed class TabletLimit
    {
        private float _openFor;

        /// <summary>지금 들 수 없는지(주요 연출 중).</summary>
        public bool Locked { get; private set; }

        /// <summary>이번에 들고 있은 시간(초).</summary>
        public float OpenFor
        {
            get { return _openFor; }
        }

        /// <summary>
        /// 시간을 흘린다. <paramref name="isOpen"/> = 태블릿이 들려 있는지, <paramref name="directionBusy"/> = 주요 연출 중인지.
        /// true면 지금 태블릿을 내려야 한다.
        /// </summary>
        public bool Tick(float dt, bool isOpen, bool directionBusy)
        {
            if (dt < 0f) dt = 0f;
            Locked = directionBusy;

            if (!isOpen)
            {
                _openFor = 0f;
                return false;
            }

            if (directionBusy) return true;   // 연출 중 — 들고 있었으면 내리고, 들려 하면 곧바로 내린다
            _openFor += Math.Max(0f, dt);
            return false;   // 69차·73차: 시간으로도 쉬는 시간으로도 내리지 않는다
        }

        /// <summary>처음 상태로(밤 시작·재시작).</summary>
        public void Reset()
        {
            _openFor = 0f;
            Locked = false;
        }
    }
}
