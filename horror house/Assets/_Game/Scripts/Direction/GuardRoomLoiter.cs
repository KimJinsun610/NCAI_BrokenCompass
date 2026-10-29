using System;

namespace NightDuty
{
    /// <summary>104차: 경비실 버티기 단계.</summary>
    public enum LoiterStage
    {
        /// <summary>아무 일 없음.</summary>
        None = 0,

        /// <summary>약 30초 — 형광등이 깜빡이고 무전 잡음(문구 없음).</summary>
        Flicker = 1,

        /// <summary>약 60초 — CCTV 화면이 검은 남자의 얼굴로 채워진다(잡음이 튀어 시선을 끈다).</summary>
        CctvFace = 2,

        /// <summary>약 90초 — 경비실 정전, 손전등만. 2일차부터는 더 남아 있으면 붙잡힌다.</summary>
        Blackout = 3
    }

    /// <summary>
    /// 104차(사용자: 「플레이어가 경비실에 camping을 하는 걸 방지 — 약 30초 형광등 깜빡임·무전 잡음, 약 60초 CCTV가 검은 남자 얼굴로, 약 90초 정전 · 그래도 남으면 검은 남자에게 붙잡힘」):
    /// 경비실 체류 시간과 단계만 판단하는 순수 로직(연출은 <c>GuardRoomCamp</c>가 결과만 받아 튼다). 시간은 게임 시간(일시정지 중엔 멈춘다).
    /// <list type="bullet">
    /// <item><b>세는 때</b>: <see cref="Tick"/>의 <c>counting</c>이 참(출근 뒤 · 밤 진행 · 피날레·샌드박스·연출 아님 · 밀린 지시나 남은 조우가 있음)이고 경비실 안이며
    /// 면제(<c>exempt</c> — 전화·서명 시간·경비실 점검·경비실 조우·CCTV 지시가 있을 때의 CCTV)가 아닐 때만 시간이 오른다.</item>
    /// <item><b>나감</b>: 경비실 밖(문에서 떨어진 곳)에 <see cref="OutsideSettleSeconds"/>초 넘게 있어야 나간 것 — 문턱을 밟고 오가는 것은 나간 것이 아니다.
    /// 나가도 시간은 0이 되지 않고 멈춘다(돌아오면 이어서). 0이 되는 것은 다른 공간에 들어갔을 때(<see cref="NoteOtherSpace"/>)나 지시를 하나 끝냈을 때(<see cref="NoteDutyDone"/>)뿐.</item>
    /// <item><b>단계 기억</b>: 정전까지 간 뒤 <see cref="MemorySeconds"/>초 안에 0이 되었다가 돌아오면 60초 단계부터.</item>
    /// <item><b>붙잡힘</b>: 정전 뒤 <see cref="CaptureGraceSeconds"/>초 더 남아 있으면 — 2일차부터(<see cref="CaptureFromDay"/>). 한 번만 알린다(<see cref="ConsumeCapture"/>).</item>
    /// </list>
    /// </summary>
    public sealed class GuardRoomLoiter
    {
        public const float FlickerSeconds = 30f;
        public const float FaceSeconds = 60f;
        public const float BlackoutSeconds = 90f;

        /// <summary>정전 뒤 붙잡히기까지(초).</summary>
        public const float CaptureGraceSeconds = 15f;

        /// <summary>붙잡힘은 이 날부터(1일차는 정전까지만).</summary>
        public const int CaptureFromDay = 2;

        /// <summary>문에서 떨어진 밖에 이만큼(초) 있어야 나간 것.</summary>
        public const float OutsideSettleSeconds = 3f;

        /// <summary>정전 단계 기억(초) — 이 안에 0이 되면 다시 60초 단계부터.</summary>
        public const float MemorySeconds = 120f;

        /// <summary>
        /// 면제(경비실에서 할 일) 한도(초) — 한 번 머무는 동안. 경비실 점검·지시를 일부러 남겨 두고 버티는 것을 막는다(다 쓰면 그냥 센다).
        /// 0이 될 때(다른 공간·지시 끝) 다시 찬다.
        /// </summary>
        public const float ExemptBudgetSeconds = 60f;

        private float _seconds;
        private float _outside;
        private bool _left;
        private float _since;
        private float _peakAt = -1f;
        private bool _captureDue;
        private bool _captured;
        private bool _inside;
        private bool _counting;
        private float _exemptUsed;

        /// <summary>쌓인 체류 시간(초).</summary>
        public float Seconds
        {
            get { return _seconds; }
        }

        /// <summary>지금 연출할 단계 — 세는 중이고 경비실 안(또는 문턱)일 때만. 나갔거나 세지 않으면 <see cref="LoiterStage.None"/>.</summary>
        public LoiterStage Stage
        {
            get
            {
                if (!_counting || _left) return LoiterStage.None;
                return StageAt(_seconds);
            }
        }

        /// <summary>남은 면제 한도(초) — 0이면 할 일이 있어도 센다.</summary>
        public float ExemptLeft
        {
            get { return Math.Max(0f, ExemptBudgetSeconds - _exemptUsed); }
        }

        /// <summary>정전 뒤 붙잡힘까지 남은 초(정전 전이면 음수 아님 — 0 이상, 정전 단계가 아니면 <see cref="CaptureGraceSeconds"/>).</summary>
        public float CaptureLeft
        {
            get { return Math.Max(0f, BlackoutSeconds + CaptureGraceSeconds - _seconds); }
        }

        /// <summary>체류 시간에 해당하는 단계.</summary>
        public static LoiterStage StageAt(float seconds)
        {
            if (seconds >= BlackoutSeconds) return LoiterStage.Blackout;
            if (seconds >= FaceSeconds) return LoiterStage.CctvFace;
            if (seconds >= FlickerSeconds) return LoiterStage.Flicker;
            return LoiterStage.None;
        }

        /// <summary>
        /// 한 프레임. <paramref name="dt"/> 게임 초 · <paramref name="inside"/> 경비실 안(문턱 포함) · <paramref name="farOutside"/> 문에서 떨어진 밖 ·
        /// <paramref name="counting"/> 셀 수 있는 때 · <paramref name="exempt"/> 경비실에서 할 일을 하는 중 · <paramref name="day"/> 일차.
        /// </summary>
        public void Tick(float dt, bool inside, bool farOutside, bool counting, bool exempt, int day)
        {
            if (dt < 0f) dt = 0f;
            _since += dt;
            _inside = inside;
            _counting = counting;
            if (inside)
            {
                _outside = 0f;
                _left = false;
            }
            else if (farOutside)
            {
                _outside += dt;
                if (_outside >= OutsideSettleSeconds) _left = true;
            }
            else
            {
                _outside = 0f;   // 104차 QA: 문턱 띠로 돌아오면 「떨어진 밖」은 처음부터 — 이어서 3초여야 나간 것
            }

            if (!counting || !inside) return;
            if (exempt && _exemptUsed < ExemptBudgetSeconds)
            {
                _exemptUsed += dt;
                return;
            }

            float before = _seconds;
            _seconds += dt;
            if (before < BlackoutSeconds && _seconds >= BlackoutSeconds) _peakAt = _since;
            if (_seconds >= BlackoutSeconds) _peakAt = _since;   // 정전 중이면 기억을 늘린다
            if (!_captured && day >= CaptureFromDay && _seconds >= BlackoutSeconds + CaptureGraceSeconds)
            {
                _captured = true;
                _captureDue = true;
            }
        }

        /// <summary>경비실·복도가 아닌 다른 공간에 들어갔다 — 0으로(정전 기억 안이면 60초 단계부터).</summary>
        public void NoteOtherSpace()
        {
            Restart();
        }

        /// <summary>지시를 하나 끝냈다 — 0으로(정전 기억 안이면 60초 단계부터).</summary>
        public void NoteDutyDone()
        {
            Restart();
        }

        private void Restart()
        {
            bool remember = _peakAt >= 0f && _since - _peakAt <= MemorySeconds;
            _seconds = remember ? FaceSeconds : 0f;
            if (!remember) _peakAt = -1f;
            _captured = false;
            _captureDue = false;
            _exemptUsed = 0f;
        }

        /// <summary>붙잡을 때가 되었으면 true를 한 번 돌려준다.</summary>
        public bool ConsumeCapture()
        {
            if (!_captureDue) return false;
            _captureDue = false;
            return true;
        }

        /// <summary>붙잡지 못했다(다른 연출 중 등) — 다음 판단으로 미룬다.</summary>
        public void DeferCapture()
        {
            _captured = false;
        }

        /// <summary>밤 시작·재시작 — 모두 처음으로.</summary>
        public void Reset()
        {
            _seconds = 0f;
            _outside = 0f;
            _left = false;
            _since = 0f;
            _peakAt = -1f;
            _captureDue = false;
            _captured = false;
            _inside = false;
            _counting = false;
            _exemptUsed = 0f;
        }
    }
}
