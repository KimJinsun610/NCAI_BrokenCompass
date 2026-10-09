namespace NightDuty
{
    // 71차 — CCTV 사람 자리(민: 「사람이 CCTV에 등장하는 이벤트가 결정되면, 등장할 장소도 함께 결정되도록」).
    public static partial class NightRun
    {
        private static CctvSpot _cctvPersonSpot;
        private static CctvSpot _cctvAnomalySpot;
        private static string _lastCctvSpot;
        private static System.Random s_cctvRoll = new System.Random();

        /// <summary>
        /// 그 밤 K1 조우(<see cref="ProgramCatalog.CctvPerson"/>)의 사람이 나타날 자리 — 편성에 그 조우가 있으면 밤 시작에 정한다(재시작해도 같다). 없으면 null.
        /// 긴장 디렉터는 플레이어가 <b>이 자리의 채널</b>을 보고 있을 때 조우를 건다(슬롯 끝 10분 전부터는 어느 채널이든 — 그때는 연출이 보고 있는 채널의 자리로 바꾼다).
        /// </summary>
        public static CctvSpot CctvPersonSpot
        {
            get { return _cctvPersonSpot; }
        }

        /// <summary>그 밤 K-1 이상(「CCTV 모든 채널은 비어 있습니다」의 이상)의 사람이 걸어 다닐 자리. K-1이 이상이 아니면 null.</summary>
        public static CctvSpot CctvAnomalySpot
        {
            get { return _cctvAnomalySpot; }
        }

        /// <summary>시험·재현용 — CCTV 자리 고르기 씨앗.</summary>
        public static int CctvSeed
        {
            set { s_cctvRoll = new System.Random(value); }
        }

        /// <summary>밤 시작(재시작 제외) — 그날 CCTV 사람 자리를 정한다. 빈 방 채널(K2)과 어제 자리는 피한다.</summary>
        private static void PlanCctvSpots()
        {
            _cctvPersonSpot = null;
            _cctvAnomalySpot = null;
            int vacant = _emptyRoomChannel;

            if (_program != null && _program.HasEncounter(ProgramCatalog.CctvPerson))
            {
                _cctvPersonSpot = CctvSpots.Pick(s_cctvRoll, -1, vacant, _lastCctvSpot);
                if (_cctvPersonSpot != null) _lastCctvSpot = _cctvPersonSpot.Id;
            }

            InspectionPlan plan = Board.Plan;
            if (plan != null)
            {
                for (int i = 0; i < plan.Assignments.Count; i++)
                {
                    if (plan.Assignments[i].Id != "K-1" || !plan.Assignments[i].IsAnomaly) continue;
                    _cctvAnomalySpot = CctvSpots.Pick(s_cctvRoll, -1, vacant, _cctvPersonSpot != null ? _cctvPersonSpot.Id : _lastCctvSpot);
                    if (_cctvPersonSpot == null && _cctvAnomalySpot != null) _lastCctvSpot = _cctvAnomalySpot.Id;
                    break;
                }
            }

            if (_tension != null) _tension.CctvPersonChannel = _cctvPersonSpot != null ? _cctvPersonSpot.ChannelId : string.Empty;
            if (_cctvPersonSpot != null) UnityEngine.Debug.Log("[NightRun] CCTV 사람 자리 — " + _cctvPersonSpot);
            if (_cctvAnomalySpot != null) UnityEngine.Debug.Log("[NightRun] K-1 이상 사람 자리 — " + _cctvAnomalySpot);
        }

        /// <summary>디버그: K1 조우 자리를 바꾼다(개발자 모드). 없는 ID면 false.</summary>
        public static bool DebugSetCctvPersonSpot(string spotId)
        {
            CctvSpot spot = CctvSpots.Find(spotId);
            if (spot == null) return false;
            _cctvPersonSpot = spot;
            if (_tension != null) _tension.CctvPersonChannel = spot.ChannelId;
            return true;
        }

        private static void ResetCctv(bool all)
        {
            _cctvPersonSpot = null;
            _cctvAnomalySpot = null;
            if (all) _lastCctvSpot = null;
        }
    }
}
