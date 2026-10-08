using System;

namespace NightDuty
{
    /// <summary>
    /// 태블릿 들기 제한(61차, 2026-10-07 플레이테스트: 「플레이어가 태블릿을 너무 올리고 다닌다」 → 민 결정:
    /// 「주요 연출이 나오면 강제로 내리고 끝날 때까지 못 올리고, 한 번 올리면 5초 뒤에 내려가고, 2초 후에 다시 올릴 수 있도록」).
    /// 화면 쪽(<c>TabletZoom</c> — 태블릿은 늘 손에 들려 있고 「올림」 = 확대)이 매 프레임 <see cref="Tick"/>을 부르고, true면 확대를 푼다.
    /// 주요 연출 중에는 화면 쪽이 손의 태블릿까지 시야 밖으로 내린다.
    /// <list type="bullet">
    /// <item>들고 있는 시간이 <see cref="OpenSeconds"/>초가 되면 내린다.</item>
    /// <item>내린 뒤(스스로 내렸든 강제로 내렸든) <see cref="ReopenCooldown"/>초 동안은 들 수 없다 — 들려 하면 곧바로 내린다.</item>
    /// <item>주요 연출(조우의 전조·대면·마무리, 몹을 세워 둔 동안 — <c>TensionDirector.Busy</c>) 동안은 들 수 없고, 들고 있었으면 곧바로 내린다.</item>
    /// </list>
    /// </summary>
    public sealed class TabletLimit
    {
        /// <summary>한 번 들면 이만큼(초) 뒤 내려간다.</summary>
        public const float OpenSeconds = 5f;

        /// <summary>내린 뒤 다시 들 수 있기까지(초).</summary>
        public const float ReopenCooldown = 2f;

        private float _openFor;
        private float _cooldown;
        private bool _wasOpen;

        /// <summary>지금 들 수 없는지(연출 중이거나 쉬는 중).</summary>
        public bool Locked { get; private set; }

        /// <summary>다시 들 수 있기까지 남은 시간(초).</summary>
        public float Cooldown
        {
            get { return _cooldown; }
        }

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
            if (_cooldown > 0f) _cooldown = Math.Max(0f, _cooldown - dt);
            Locked = directionBusy || _cooldown > 0f;

            if (!isOpen)
            {
                if (_wasOpen)
                {
                    _wasOpen = false;
                    _openFor = 0f;
                    _cooldown = ReopenCooldown;
                    Locked = true;
                }

                return false;
            }

            if (!_wasOpen && Locked) return true;   // 잠긴 동안 들려고 함 — 곧바로 내린다(쉬는 시간은 늘리지 않는다)
            _wasOpen = true;
            if (directionBusy) return true;
            _openFor += dt;
            return _openFor >= OpenSeconds;
        }

        /// <summary>처음 상태로(밤 시작·재시작).</summary>
        public void Reset()
        {
            _openFor = 0f;
            _cooldown = 0f;
            _wasOpen = false;
            Locked = false;
        }
    }
}
