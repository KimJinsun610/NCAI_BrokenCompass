namespace NightDuty
{
    /// <summary>
    /// 판정 입력 신호의 종류. 확정 기획서 공통 명세 2절·5절의 「기존 이벤트 연결」을 그대로 옮긴 어휘다.
    /// <para>
    /// 신호는 클라이언트(플레이어·문·구역·응시 추적기)가 만들어 <see cref="RuleBook.Dispatch"/>로 보낸다.
    /// Core는 위치·물리·카메라를 모른다. 거리 계산과 레이캐스트는 클라이언트가 하고, 결과만 신호로 넘긴다.
    /// </para>
    /// <para>
    /// <b>숫자를 바꾸지 말 것.</b> RuleSO 에셋에 직렬화된다. 새 종류는 빈 번호에 덧붙인다.
    /// </para>
    /// </summary>
    public enum SignalKind
    {
        /// <summary>미지정.</summary>
        None = 0,

        /// <summary>판정 시간이 흘렀다. Value = 경과 초. <b>Tab이 열려 있는 동안에는 보내지 않는다.</b></summary>
        Tick = 1,

        /// <summary>문 E 명령이 <b>수락</b>됐다(사거리 밖이라 무시된 입력은 보내지 않는다). TargetId = 문 ID, Flag = 닫기 명령이면 true, Source = 조작 출처.</summary>
        DoorCommandAccepted = 10,

        /// <summary>문 닫힘 애니메이션이 <b>완료</b>됐다. TargetId = 문 ID, Source = 조작 출처. (T3만 이 신호를 요구한다.)</summary>
        DoorCloseCompleted = 11,

        /// <summary>문이 연출로 자동 개방되는 동작을 플레이어가 실제로 관찰했다. TargetId = 문 ID.</summary>
        DoorAutoOpenObserved = 12,

        /// <summary>플레이어 발밑 기준점이 공간 안으로 넘어갔다. Space = 들어간 공간.</summary>
        SpaceEntered = 20,

        /// <summary>플레이어 발밑 기준점이 공간 밖으로 넘어갔다. Space = 나온 공간.</summary>
        SpaceExited = 21,

        /// <summary>지정 구역(통행·점검·칸 내부·금지 영역 등)에 들어갔다. TargetId = 구역 ID.</summary>
        ZoneEntered = 22,

        /// <summary>지정 구역에서 나왔다. TargetId = 구역 ID.</summary>
        ZoneExited = 23,

        /// <summary>통행 완료 지점을 지나 해당 통행 구역을 이탈했다. TargetId = 통행 구역 ID.</summary>
        PassageCompleted = 24,

        /// <summary>일반 점검이 완료됐다(점검 구역 1초 체류 후 공간 이탈). Space = 점검 ID.</summary>
        InspectionCompleted = 25,

        /// <summary>청각 단서가 청취 구역 안의 플레이어에게 정상 재생·전달됐다. TargetId = 단서 이벤트 ID.</summary>
        ClueDelivered = 30,

        /// <summary>시각 단서를 안전 관찰 지점에서 식별했다(중앙 0.2초). TargetId = 단서 대상 ID.</summary>
        ClueIdentified = 31,

        /// <summary>지정 오디오·연출 시퀀스가 끝났다. TargetId = 시퀀스 ID.</summary>
        SequenceEnded = 32,

        /// <summary>
        /// 응시 샘플. TargetId = 카메라 중앙의 첫 가시 충돌체 대상 ID(없으면 빈 문자열), Value = 이 샘플이 대표하는 시간(초).
        /// 클라이언트는 프레임레이트와 무관한 고정 간격(0.1초)으로 보낸다.
        /// </summary>
        GazeSample = 40,

        /// <summary>근접 샘플. TargetId = 바닥 기준점 ID, Value = 플레이어 발밑 기준점과의 <b>수평</b> 거리(m).</summary>
        ProximitySample = 41,

        /// <summary>손전등 상태가 바뀌었다. Flag = 켜짐.</summary>
        FlashlightChanged = 42,

        /// <summary>인체모형을 가림 없이 1초 관찰했다. TargetId = 장면 ID(H-A 등).</summary>
        ModelObserved = 50,

        /// <summary>태블릿(Tab) 상태가 바뀌었다. Flag = 열림.</summary>
        TabChanged = 60,

        /// <summary>근무 종료 요청이 수락됐다. 보통은 신호 대신 <see cref="RuleBook.EndNight"/>를 직접 호출한다.</summary>
        NightEndAccepted = 70
    }

    /// <summary>조작 출처. 연출이 움직인 문을 플레이어 조작으로 세지 않기 위해 구분한다.</summary>
    public enum ActionSource
    {
        /// <summary>플레이어 입력.</summary>
        Player = 0,

        /// <summary>연출·시스템.</summary>
        Direction = 1
    }

    /// <summary>
    /// 판정 입력 신호 한 건. 값 타입이며 할당 없이 전달된다.
    /// 필드의 의미는 <see cref="SignalKind"/>마다 다르다(각 열거자 주석 참조).
    /// </summary>
    public readonly struct JudgeSignal
    {
        /// <summary>신호 종류.</summary>
        public readonly SignalKind Kind;

        /// <summary>관련 공간. 공간과 무관하면 <see cref="SpaceId.None"/>.</summary>
        public readonly SpaceId Space;

        /// <summary>대상 ID(문·구역·단서·기준점·장면). 없으면 빈 문자열.</summary>
        public readonly string TargetId;

        /// <summary>조작 출처.</summary>
        public readonly ActionSource Source;

        /// <summary>불리언 값(닫기 명령, 손전등 켜짐, Tab 열림).</summary>
        public readonly bool Flag;

        /// <summary>실수 값(경과 초, 거리 m, 응시 샘플 시간).</summary>
        public readonly float Value;

        /// <summary>모든 필드를 지정해 신호를 만든다.</summary>
        public JudgeSignal(SignalKind kind, SpaceId space, string targetId, ActionSource source, bool flag, float value)
        {
            Kind = kind;
            Space = space;
            TargetId = targetId ?? string.Empty;
            Source = source;
            Flag = flag;
            Value = value;
        }

        /// <summary>판정 시간 경과.</summary>
        public static JudgeSignal Tick(float seconds)
        {
            return new JudgeSignal(SignalKind.Tick, SpaceId.None, string.Empty, ActionSource.Direction, false, seconds);
        }

        /// <summary>문 E 명령 수락.</summary>
        public static JudgeSignal DoorCommand(string doorId, bool isClose, ActionSource source)
        {
            return new JudgeSignal(SignalKind.DoorCommandAccepted, SpaceId.None, doorId, source, isClose, 0f);
        }

        /// <summary>대상 ID만 필요한 신호(구역·단서·시퀀스·모형 등).</summary>
        public static JudgeSignal Target(SignalKind kind, string targetId)
        {
            return new JudgeSignal(kind, SpaceId.None, targetId, ActionSource.Player, false, 0f);
        }

        /// <summary>공간 진입·이탈·점검 완료.</summary>
        public static JudgeSignal OfSpace(SignalKind kind, SpaceId space)
        {
            return new JudgeSignal(kind, space, string.Empty, ActionSource.Player, false, 0f);
        }

        /// <summary>응시 샘플.</summary>
        public static JudgeSignal Gaze(string firstVisibleTargetId, float sampleSeconds)
        {
            return new JudgeSignal(SignalKind.GazeSample, SpaceId.None, firstVisibleTargetId, ActionSource.Player, false, sampleSeconds);
        }

        /// <summary>근접 샘플.</summary>
        public static JudgeSignal Proximity(string anchorId, float horizontalMeters)
        {
            return new JudgeSignal(SignalKind.ProximitySample, SpaceId.None, anchorId, ActionSource.Player, false, horizontalMeters);
        }

        /// <summary>손전등 상태 변경.</summary>
        public static JudgeSignal Flashlight(bool isOn)
        {
            return new JudgeSignal(SignalKind.FlashlightChanged, SpaceId.None, string.Empty, ActionSource.Player, isOn, 0f);
        }

        /// <summary>Tab 상태 변경.</summary>
        public static JudgeSignal Tab(bool isOpen)
        {
            return new JudgeSignal(SignalKind.TabChanged, SpaceId.None, string.Empty, ActionSource.Player, isOpen, 0f);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Kind + "(" + Space + ", '" + TargetId + "', " + Source + ", " + Flag + ", " + Value + ")";
        }
    }
}
