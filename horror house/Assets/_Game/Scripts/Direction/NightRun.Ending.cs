using UnityEngine;

namespace NightDuty
{
    public static partial class NightRun
    {
        private static readonly FinaleWatch _finale = new FinaleWatch();

        /// <summary>5일차 피날레 판정(K4·봤다·CCTV 채널). 피날레 밖에서는 <see cref="FinaleWatch.Active"/>가 false.</summary>
        public static FinaleWatch Finale
        {
            get { return _finale; }
        }

        /// <summary>마지막 피날레 결말(결과창·엔딩이 읽는다). 회차를 새로 시작하면 지운다.</summary>
        public static FinaleEnding LastFinaleEnding { get; private set; }

        /// <summary>G3 빈칸을 채웠는지(태블릿 표시).</summary>
        public static bool FinaleBlankFilled { get; private set; }

        /// <summary>
        /// 5일차 04:00 — 근무를 정산하지 않고 피날레를 연다(밤은 열린 채, 판정 시계는 이미 멈춰 있다). 결말에서 <see cref="EndFinale"/>가 정산한다.
        /// 5일차가 아니거나, 밤이 닫혔거나, 붙잡혔거나, 이미 열렸으면 false.
        /// </summary>
        public static bool BeginFinale()
        {
            if (!_nightOpen || IsCaptured || Day < FinaleWatch.Day || _finale.Active) return false;
            _finale.Begin();
            Debug.Log("[NightRun] 피날레 시작(5일차 05:00) — K4: 경비실을 나가지 않기");
            return true;
        }

        /// <summary>K4 위반 붙잡힘 연출 뒤 피날레를 처음부터 한다(축·재시작 횟수는 그대로).</summary>
        public static void RestartFinale()
        {
            if (!_finale.Active) return;
            _finale.Restart();
            Debug.Log("[NightRun] 피날레 처음부터(시도 " + _finale.Attempt + ")");
        }

        /// <summary>결말 — K4를 기록하고 근무를 정산한다(<see cref="RequestEndNight"/>). 피날레가 아니면 false.</summary>
        public static bool EndFinale()
        {
            if (!_finale.Active) return false;
            bool seen = _finale.Seen;
            _finale.End();
            LastFinaleEnding = seen ? FinaleEnding.ShiftChange : FinaleEnding.ShiftOver;
            if (_finalBook != null) _finalBook.NoteExternal(ProgramCatalog.FinaleRule, false, seen ? "결말: 창을 봄(근무 교대)" : "결말: 창을 보지 않음(근무 종료)");
            Debug.Log("[NightRun] 피날레 결말 — " + LastFinaleEnding);
            return RequestEndNight();
        }

        /// <summary>태블릿 G3의 빈칸을 「당신」으로 채운다(피날레 「들어왔다」). 오늘 표시 카드에 G3이 없으면 false.</summary>
        public static bool FillFinaleBlank()
        {
            for (int i = 0; i < _displayDeck.Count; i++)
            {
                RuleSO card = _displayDeck[i];
                if (card == null || card.CardId != ProgramCatalog.FinaleBlankRule) continue;
                card.Configure(new RuleSO.Config { CardId = card.CardId, Space = card.Space, PlayerText = ProgramCatalog.FinaleBlankFilled, HowTo = card.HowTo });
                FinaleBlankFilled = true;
                return true;
            }

            return false;
        }

        private static void FinaleObserve(in JudgeSignal signal)
        {
            if (_finale.Active) _finale.Process(signal);
        }

        private static void ResetFinale(bool newRun)
        {
            _finale.Reset();
            FinaleBlankFilled = false;
            if (newRun) LastFinaleEnding = FinaleEnding.None;
        }
    }
}
