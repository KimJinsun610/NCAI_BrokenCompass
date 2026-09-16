namespace NightDuty
{
    /// <summary>조건의 대상 ID 칸에 쓰는 특수 값.</summary>
    public static class TargetMatchIds
    {
        /// <summary>아무 대상이나 허용.</summary>
        public const string Any = TargetMatch.Any;

        /// <summary>이 사건을 시작시킨 신호의 대상과 같아야 함.</summary>
        public const string Trigger = TargetMatch.Trigger;
    }
}
