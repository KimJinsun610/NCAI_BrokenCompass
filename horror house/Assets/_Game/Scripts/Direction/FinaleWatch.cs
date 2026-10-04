using System;
using System.Collections.Generic;

namespace NightDuty
{
    /// <summary>
    /// 5일차 피날레의 판정(최종 기획서 「5일차 피날레」, 11단계). 근무 시계가 04:00에 닿은 뒤 <see cref="NightRun.BeginFinale"/>로 열린다.
    /// <list type="bullet">
    /// <item><b>K4</b> 「근무 종료 후에는 경비실을 나가지 마십시오.」 — 경비실을 나가면(<see cref="SignalKind.SpaceExited"/> 경비실, 또는 다른 공간에 들어감) 위반.
    /// 축은 오르지 않는다. 연출이 가장 높은 감각축의 붙잡힘 장면을 틀고 <see cref="NightRun.RestartFinale"/>로 처음부터 한다.</item>
    /// <item><b>「봤다」</b> — 창밖 남자(<see cref="WindowTarget"/>)를 비추거나 2초 이어서 응시(L5와 같은 기준, 틈 허용 <see cref="SensingRules.GazeGapSeconds"/>). 결말을 가른다.</item>
    /// <item><b>CCTV</b> — 넘겨 본 채널 ID를 모은다. 다섯 채널을 다 보면 연출이 「모든 몹이 제자리에서 사라져 있다」로 넘어간다.</item>
    /// </list>
    /// 판정만 한다 — 문·창 두드림·문자·화면은 연출(<c>FinaleDirector</c>)이 맡는다.
    /// </summary>
    public sealed class FinaleWatch
    {
        /// <summary>피날레가 있는 일차.</summary>
        public const int Day = 5;

        /// <summary>창밖 정장 남자의 판정 ID(피날레 배역표 기본값과 같다).</summary>
        public const string WindowTarget = "rule.K4.window";

        /// <summary>「봤다」 응시 시간(초) — L5와 같다.</summary>
        public const float GazeSeconds = 2f;

        private readonly HashSet<string> _channels = new HashSet<string>(StringComparer.Ordinal);
        private SampleStreak _streak;

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

        /// <summary>K4 위반 뒤 처음부터(봤다·채널도 지운다).</summary>
        internal void Restart()
        {
            Seen = false;
            Violated = false;
            ViolationReason = string.Empty;
            _channels.Clear();
            _streak.Clear();
            Attempt++;
        }

        /// <summary>결말로 닫는다(봤다는 남겨 둔다).</summary>
        internal void End()
        {
            Active = false;
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
            _streak.Clear();
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

                case SignalKind.BeamSample:
                    if (s.TargetId == WindowTarget) MarkSeen();
                    break;

                case SignalKind.GazeSample:
                    if (_streak.Feed(s.TargetId == WindowTarget, s.Value, SensingRules.GazeGapSeconds) >= GazeSeconds) MarkSeen();
                    break;

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

        /// <summary>창을 보지 않음 — 「근무 종료. 수고하셨습니다.」</summary>
        ShiftOver = 1,

        /// <summary>창을 봄 — 「근무 교대. 수고하셨습니다.」</summary>
        ShiftChange = 2,
    }
}
