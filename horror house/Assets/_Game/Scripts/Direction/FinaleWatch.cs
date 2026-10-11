using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 5일차 피날레의 판정(최종 기획서 「5일차 피날레」, 11단계 · 2026-10-10 기획 가이드로 결말 개편). 근무 시계가 05:00에 닿은 뒤 <see cref="NightRun.BeginFinale"/>로 열린다.
    /// <list type="bullet">
    /// <item><b>K4</b> 「근무 종료 후에는 경비실을 나가지 마십시오.」 — 경비실을 나가면(<see cref="SignalKind.SpaceExited"/> 경비실, 또는 다른 공간에 들어감) 위반.
    /// 축은 오르지 않는다. 연출이 가장 높은 감각축의 붙잡힘 장면을 틀고 <see cref="NightRun.RestartFinale"/>로 처음부터 한다.</item>
    /// <item><b>「봤다」</b>(2026-10-10 기획 가이드) — 응시 창(<see cref="OpenGazeWindow"/> ~ <see cref="CloseGazeWindow"/>, 연출의 「들여보내줘」 4.5초) 동안
    /// 창밖 남자가 <b>화면에 보인 시간</b>(<see cref="FeedSight"/> — 화면 가장자리 12% 제외·가림 없음)이 <b>합쳐서</b> <see cref="GazeSeconds"/>초면 「봤다」.
    /// 끊겨도 이어 센다. 조준점 응시 신호·손전등 비춤은 세지 않는다(김진선님 B안). 결말을 가른다.</item>
    /// <item><b>CCTV</b> — 넘겨 본 채널 ID를 모은다(기록만 — 피날레 진행에는 더 쓰지 않는다).</item>
    /// </list>
    /// 판정만 한다 — 문·창 두드림·문자·화면은 연출(<c>FinaleDirector</c>)이 맡는다.
    /// </summary>
    public sealed class FinaleWatch
    {
        /// <summary>피날레가 있는 일차.</summary>
        public const int Day = 5;

        /// <summary>창밖 정장 남자의 판정 ID(피날레 배역표 기본값과 같다).</summary>
        public const string WindowTarget = "rule.K4.window";

        /// <summary>「봤다」 응시 시간(초, 응시 창 안에서 합친 시간).</summary>
        public const float GazeSeconds = 1.5f;

        private readonly HashSet<string> _channels = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>응시 창이 열려 있는지(이 동안의 응시만 센다).</summary>
        public bool GazeWindowOpen { get; private set; }

        /// <summary>이번 시도의 응시 창에서 창밖 남자를 본 시간 합(초).</summary>
        public float GazeTotal { get; private set; }

        /// <summary>피날레가 진행 중인지.</summary>
        public bool Active { get; private set; }

        /// <summary>이번 시도에서 창밖 남자를 「봤는지」.</summary>
        public bool Seen { get; private set; }

        /// <summary>이번 시도에서 K4를 어겼는지(다시 시작하면 지운다).</summary>
        public bool Violated { get; private set; }

        /// <summary>몇 번째 시도인지(첫 시도 1, K4 위반 뒤 다시 하면 늘어남).</summary>
        public int Attempt { get; private set; }

        /// <summary>이번 시도에서 넘겨 본 CCTV 채널 수.</summary>
        public int ChannelsSeen
        {
            get { return _channels.Count; }
        }

        /// <summary>마지막 위반 사유(디버그).</summary>
        public string ViolationReason { get; private set; }

        /// <summary>K4를 어겼다(경비실을 나감).</summary>
        public event Action Violation;

        /// <summary>「봤다」가 됐다.</summary>
        public event Action SeenNow;

        /// <summary>새 채널을 봤다. 인자: 채널 ID.</summary>
        public event Action<string> ChannelSeen;

        /// <summary>처음 연다.</summary>
        internal void Begin()
        {
            Reset();
            Active = true;
            Attempt = 1;
        }

        /// <summary>K4 위반 뒤 처음부터(봤다·채널·응시도 지운다).</summary>
        internal void Restart()
        {
            Seen = false;
            Violated = false;
            ViolationReason = string.Empty;
            _channels.Clear();
            GazeWindowOpen = false;
            GazeTotal = 0f;
            Attempt++;
        }

        /// <summary>응시 창을 연다 — 이 뒤의 창밖 남자 응시를 합쳐 센다(이미 센 시간은 그대로). 피날레가 아니면 무시.</summary>
        public void OpenGazeWindow()
        {
            if (Active) GazeWindowOpen = true;
        }

        /// <summary>응시 창을 닫는다. 「봤다」는 그대로 남는다.</summary>
        public void CloseGazeWindow()
        {
            GazeWindowOpen = false;
        }

        /// <summary>
        /// 창밖 남자가 이 프레임 화면에 보였는지(연출이 잰다 — 화면 안쪽·가림 없음, <c>SightProbe.VisibleNow</c>)와 그 시간.
        /// 응시 창이 열린 동안 보인 시간을 합쳐 <see cref="GazeSeconds"/>가 되면 「봤다」. 피날레가 아니거나 어긴 시도면 무시.
        /// </summary>
        public void FeedSight(bool visible, float seconds)
        {
            if (!Active || Violated || !GazeWindowOpen || Seen || !visible || seconds <= 0f) return;
            GazeTotal += seconds;   // 끊겨도 합쳐서 센다
            if (GazeTotal >= GazeSeconds - 0.0001f) MarkSeen();
        }

        /// <summary>결말로 닫는다(봤다는 남겨 둔다).</summary>
        internal void End()
        {
            Active = false;
            GazeWindowOpen = false;
        }

        /// <summary>모두 지운다(밤 시작·회차 시작).</summary>
        internal void Reset()
        {
            Active = false;
            Seen = false;
            Violated = false;
            ViolationReason = string.Empty;
            Attempt = 0;
            _channels.Clear();
            GazeWindowOpen = false;
            GazeTotal = 0f;
        }

        /// <summary>판정 신호 하나. 피날레가 아니거나 이미 어긴 시도면 무시한다.</summary>
        public void Process(in JudgeSignal s)
        {
            if (!Active || Violated) return;

            switch (s.Kind)
            {
                case SignalKind.SpaceExited:
                    if (SpaceIds.Canonical(s.Space) == SpaceId.SecurityRoom) Violate("경비실을 나감");
                    break;

                case SignalKind.SpaceEntered:
                    if (s.Space != SpaceId.None && SpaceIds.Canonical(s.Space) != SpaceId.SecurityRoom) Violate(s.Space + " 진입");
                    break;

                // 2026-10-10(김진선님 B안): 조준점 응시(GazeSample)로는 세지 않는다 — 화면에 보인 시간(FeedSight)으로 센다.

                case SignalKind.CctvChannel:
                case SignalKind.CctvViewSample:
                    if (!string.IsNullOrEmpty(s.TargetId) && _channels.Add(s.TargetId))
                    {
                        Action<string> seen = ChannelSeen;
                        if (seen != null) seen(s.TargetId);
                    }

                    break;
            }
        }

        private void Violate(string reason)
        {
            Violated = true;
            ViolationReason = reason;
            Action handler = Violation;
            if (handler != null) handler();
        }

        private void MarkSeen()
        {
            if (Seen) return;
            Seen = true;
            Action handler = SeenNow;
            if (handler != null) handler();
        }
    }

    /// <summary>피날레 결말.</summary>
    public enum FinaleEnding
    {
        /// <summary>아직 없음.</summary>
        None = 0,

        /// <summary>창을 보지 않음 — 전화로 퇴근 → 성공 엔딩 씬(SuccessEnding) → 크레딧 → 메인.</summary>
        ShiftOver = 1,

        /// <summary>창을 봄 — 「들어왔다」 → 문자 몰아치기 → 정지 → 「근무 종료, 철거가 예정대로 진행됩니다.」 → 메인.</summary>
        ShiftChange = 2,
    }
}
