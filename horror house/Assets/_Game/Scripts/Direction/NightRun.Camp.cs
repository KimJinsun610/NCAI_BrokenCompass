namespace NightDuty
{
    /// <summary>
    /// 104차(사용자: 「플레이어가 경비실에 camping을 하는 걸 방지」): 경비실 버티기 판단을 밤 진행에 붙인다 — 판단은 <see cref="GuardRoomLoiter"/>, 연출은 Flow의 <c>GuardRoomCamp</c>.
    /// <list type="bullet">
    /// <item>세는 때(<see cref="CampCounting"/>): 출근 뒤 · 밤 진행 · 붙잡히지 않음 · 샌드박스·자동 연출 끔·피날레 아님 · 경비실 조우(CCTV 사람) 진행 중 아님 · 밀린 점검·지시나 남은 조우가 있음.</item>
    /// <item>면제(<see cref="CampExempt"/> — 한도 <see cref="GuardRoomLoiter.ExemptBudgetSeconds"/>초): 근무일지 서명 시간 · 경비실 지시(전화·열쇠함·CCTV 채널 등) · 경비실 점검 항목 · 경비실 수칙 연출(전화벨·공실 채널) · CCTV로 확인하라는 역설. K3 판정도 같은 때(와 경비실 조우 중) 쉰다(<see cref="GuardRoomDwellPaused"/>).</item>
    /// <item>0으로: 경비실·복도가 아닌 공간에 들어감 · 지시·점검 보고를 하나 끝냄. 밤 시작에 모두 처음으로.</item>
    /// </list>
    /// </summary>
    public static partial class NightRun
    {
        private static readonly GuardRoomLoiter _loiter = new GuardRoomLoiter();
        private static bool s_campInside;
        private static bool s_campFar;

        /// <summary>경비실 버티기 장치를 쓸지(개발·시험).</summary>
        public static bool CampEnabled { get; set; } = true;

        /// <summary>지금 연출할 경비실 버티기 단계.</summary>
        public static LoiterStage CampStage
        {
            get
            {
                if (!CampEnabled || !_nightOpen) return LoiterStage.None;
                LoiterStage s = _loiter.Stage;
                // 104차 QA: 경비실에서 CCTV 일(K-1 점검·CCTV 역설·CCTV 사람 조우 등)을 하는 동안 얼굴로 채널을 가리지 않는다 — 깜빡임까지만.
                if (s == LoiterStage.CctvFace && GuardRoomDwellPaused(SpaceId.SecurityRoom)) return LoiterStage.Flicker;
                return s;
            }
        }

        /// <summary>쌓인 경비실 체류 시간(초, 검수용).</summary>
        public static float CampSeconds
        {
            get { return _loiter.Seconds; }
        }

        /// <summary>붙잡힘 출처 — 붙잡힘 연출이 검은 남자 얼굴로 조도 처형 얼굴 장면부터 튼다.</summary>
        public const string CampCaptureSource = "E.GuardRoom.camp";

        /// <summary>붙잡힘 카드 문구.</summary>
        public const string CampCaptureLine = "경비실에 오래 머물지 마십시오.";

        /// <summary>연출(Flow)이 매 프레임 알린다 — 경비실 안(문턱 포함)인지 · 문에서 떨어진 밖인지.</summary>
        public static void SetCampPresence(bool inside, bool farOutside)
        {
            s_campInside = inside;
            s_campFar = farOutside;
        }

        /// <summary>붙잡을 때가 되었으면 한 번 true.</summary>
        public static bool ConsumeCampCapture()
        {
            return CampEnabled && _loiter.ConsumeCapture();
        }

        /// <summary>붙잡지 못했다(다른 연출 중) — 다음 판단으로 미룬다.</summary>
        public static void DeferCampCapture()
        {
            _loiter.DeferCapture();
        }

        /// <summary>검수·개발: 체류 시간을 이만큼 더한다(세는 조건과 상관없이).</summary>
        public static void DebugAddCampSeconds(float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.5f) _loiter.Tick(0.5f, true, false, true, false, System.Math.Max(Day, 1));
        }

        private static void ResetCamp()
        {
            _loiter.Reset();
            s_campInside = false;
            s_campFar = false;
        }

        private static void CampTick(float dt)
        {
            _loiter.Tick(dt, s_campInside, s_campFar, CampCounting, CampExempt, Day);
        }

        /// <summary>세는 때인지.</summary>
        public static bool CampCounting
        {
            get
            {
                if (!CampEnabled || !_nightOpen || IsCaptured || !s_shiftStarted || _sandbox || !DirectorAutoRun || _finale.Active) return false;
                if (GuardRoomEncounterLive) return false;
                return CampHasWork;
            }
        }

        /// <summary>경비실 조우(CCTV 사람 등)가 예고~정리 중인지.</summary>
        private static bool GuardRoomEncounterLive
        {
            get
            {
                if (_tension == null) return false;
                foreach (EncounterRun r in _tension.Runs)
                {
                    if (r == null || r.Def == null || SpaceIds.Canonical(r.Def.Space) != SpaceId.SecurityRoom) continue;
                    if (r.State == EncounterRunState.Foreshadow || r.State == EncounterRunState.Active || r.State == EncounterRunState.Presenting || r.State == EncounterRunState.Releasing) return true;
                }

                return false;
            }
        }

        /// <summary>104차 QA: CCTV로 확인하라는 역설이 와서 아직 안전하게 읽지 않았다(경비실 모니터를 봐야 한다).</summary>
        private static bool CctvParadoxPending
        {
            get { return _paradox.Sent && !_paradox.SafeRead && !_paradoxEnded && _paradox.Pattern == SafeReadPattern.Cctv; }
        }

        /// <summary>
        /// 104차 QA: K3 머문 시간 판정을 쉬는지 — 경비실에서 할 일이 있는 동안(<see cref="CampExempt"/> · 경비실 조우).
        /// 버티기 장치와 달리 한도가 없다(오래 쓰면 장치 쪽 면제 한도가 먼저 끝난다).
        /// </summary>
        private static bool GuardRoomDwellPaused(SpaceId space)
        {
            return SpaceIds.Canonical(space) == SpaceId.SecurityRoom && (CampExempt || GuardRoomEncounterLive);
        }

        /// <summary>K3 머문 시간 판정이 지금 경비실에서 쉬는지(검수용).</summary>
        public static bool GuardRoomDwellPausedNow
        {
            get { return GuardRoomDwellPaused(SpaceId.SecurityRoom); }
        }

        private static bool CampHasWork
        {
            get
            {
                if (Board != null && Board.IssuedPendingCountExcept(_unavoidable != null ? _unavoidable.Banned : SpaceId.None) > 0) return true;
                if (_duties != null && _duties.Active != null) return true;
                return _tension != null && _tension.EncountersPending;
            }
        }

        private static bool CampExempt
        {
            get
            {
                if (CanSignCheckpointNow) return true;
                if (_duties != null && _duties.Active != null && SpaceIds.Canonical(_duties.Active.Space) == SpaceId.SecurityRoom) return true;
                if (Board != null && Board.IssuedPendingCount > Board.IssuedPendingCountExcept(SpaceId.SecurityRoom)) return true;
                if (CctvParadoxPending) return true;
                if (_tension != null)
                {
                    foreach (RuleTriggerRun r in _tension.RuleRuns)
                    {
                        if (r != null && r.Running && r.Script != null && SpaceIds.Canonical(r.Script.Space) == SpaceId.SecurityRoom) return true;
                    }
                }

                return false;
            }
        }

        private static void CampObserve(in JudgeSignal signal)
        {
            if (signal.Kind != SignalKind.SpaceEntered) return;
            SpaceId c = SpaceIds.Canonical(signal.Space);
            if (c != SpaceId.None && c != SpaceId.SecurityRoom && c != SpaceId.Corridor) _loiter.NoteOtherSpace();
        }

        private static void CampNoteDone()
        {
            _loiter.NoteDutyDone();
        }
    }
}
