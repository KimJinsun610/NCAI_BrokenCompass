using UnityEngine;

namespace NightDuty
{
    public static partial class NightRun
    {
        private static ProgramDirector _programDirector = new ProgramDirector();
        private static NightProgram _program = NightProgram.Empty(0);

        /// <summary>
        /// 밤 편성(조우 슬롯 + 새 수칙 덱, 최종 기획서 「수칙과 덱」)을 만들지. 게임 구동기가 켠다.
        /// 켜면 밤 시작(재시작 제외)에 점검 편성 뒤 <see cref="ProgramDirector"/>가 조우를 먼저 뽑고 대응 수칙을 고정한다.
        /// <para>켜면 새 수칙 판정(<see cref="FinalRules"/>)이 판정한다. 끄면(기본값·테스트) 편성도 판정도 없다 — 옛 판정 책은 2026-10-03에 폐기했다.</para>
        /// </summary>
        public static bool ProgramEnabled { get; set; }

        /// <summary>오늘 편성. 꺼져 있거나 밤 전이면 빈 편성.</summary>
        public static NightProgram Program
        {
            get { return _program; }
        }

        /// <summary>회차 편성기(예약·역보고 배정 수·본 조우 기록).</summary>
        public static ProgramDirector Programs
        {
            get { return _programDirector; }
        }

        /// <summary>
        /// 다음 밤 조우를 예약한다 — S3 위반이면 <see cref="ProgramCatalog.ModelRush"/>, 노란 얼굴에서 빛을 떼면 <see cref="ProgramCatalog.SuitMan"/>.
        /// 수칙 판정이 부른다.
        /// </summary>
        public static void ReserveEncounter(string encounterId)
        {
            _programDirector.Reserve(encounterId);
        }

        /// <summary>밤 시작(재시작 제외)에 점검 편성 바로 뒤 불린다.</summary>
        private static void OnNightPlanned(InspectionPlan inspections)
        {
            if (!ProgramEnabled)
            {
                _program = NightProgram.Empty(Day);
                DisposeFinalRules();
                return;
            }

            ProgramRequest request = new ProgramRequest
            {
                Day = Day,
                Shown = _bands.Shown,
                Survival = _axes,
                Inspections = inspections
            };

            _program = _programDirector.Build(request);
            Debug.Log("[NightRun] " + _program.Report);
            BeginFinalRules();
        }

        /// <summary>회차 시작·플레이 시작 때 확장 상태를 비운다. <paramref name="clearSwitches"/>면 켜기 스위치도 끈다.</summary>
        private static void ResetExtensions(bool clearSwitches)
        {
            ResetTabletState();
            _programDirector = new ProgramDirector();
            _program = NightProgram.Empty(0);
            DisposeFinalRules();
            if (clearSwitches)
            {
                DirectorAutoRun = true;
                ProgramEnabled = false;
            }
        }
    }
}
