using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 「두 곳이 같은 말을 하는지」만 보는 검사들(2026-09-21 신설).
    /// <para>
    /// 다른 테스트는 「값이 맞는지」를 본다. 여기 있는 것은 <b>값이 두 군데 적혀 있을 때 둘이 어긋났는지</b>만 본다.
    /// 한쪽만 고치고 다른 쪽을 두면 아무도 모르게 어긋난다. 사람의 주의력 대신 이 파일이 막는다.
    /// </para>
    /// <para>
    /// 2026-10-03: 옛 24장 카드(H·C·S·T 1~6) 에셋을 폐기하면서 「에셋 ↔ 카드 빌더」 드리프트 검사와
    /// 「하루 6장 축 쿼터」 불변식 검사 3건을 함께 지웠다. 검사할 대상이 더는 없다 — 되살리지 마십시오.
    /// 같은 날 옛 역설 연출기(ParadoxDirector)를 폐기하며 「역설 최소 신뢰 = 신뢰 구간 1 하한」 검사도 지웠다(새 역설은 10단계).
    /// </para>
    /// </summary>
    public sealed class DesignInvariantTests
    {
        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
        }

        /// <summary>일차 하한 곡선이 뒤집히거나 Band4까지 공짜로 올라가는 것을 막는다(2026-09-30: 연출 구간 하한).</summary>
        [Test]
        public void 일차하한은_줄지않고_Band4에_닿지_않는다()
        {
            Assert.AreEqual(Band.Band0, DayFloor.Of(1), "1일차는 하한 없이 시작한다");
            for (int day = 2; day <= DayFloor.LastDay; day++)
            {
                Assert.GreaterOrEqual(
                    (int)DayFloor.Of(day),
                    (int)DayFloor.Of(day - 1),
                    day + "일차 하한 " + DayFloor.Of(day) + "이 " + (day - 1) + "일차 " + DayFloor.Of(day - 1) + "보다 낮습니다.");
            }

            Band last = DayFloor.Of(DayFloor.LastDay);
            Assert.Greater((int)last, (int)DayFloor.Of(1), "마지막 날 하한이 첫날과 같습니다. 날이 갈수록 나빠지지 않습니다.");
            Assert.Less(
                (int)last,
                (int)Band.Band4,
                "마지막 일차 하한 " + last + "이 Band4입니다. Band4는 「당신이 어겨서 여기까지 왔다」는 구간이라 "
                    + "하한으로 공짜로 주면 경고로서의 뜻이 사라집니다.");
        }
    }

    /// <summary>
    /// C. 문서 드리프트 — <c>CLAUDE.md</c>의 숫자가 코드와 같은지.
    /// <para>
    /// <c>CLAUDE.md</c>는 다음 세션이 정본으로 믿는 파일이다. 여기가 낡으면 없어진 규칙을 그대로 구현한다.
    /// 그래서 <b>특정 문자열이 본문에 있는지·없는지</b>만 본다 — 문장을 해석하지 않는다.
    /// </para>
    /// <para>
    /// 개정 이력(<c>&gt; </c>로 시작하는 인용 블록)과 「되살리지 마십시오」 줄에는 <b>일부러 옛 값을 적어 둔다.</b>
    /// 그 줄들은 본문에서 뺀다.
    /// </para>
    /// </summary>
    public sealed class ClaudeMdDriftTests
    {
        /// <summary>구간 표기에 쓰는 EN DASH(U+2013). 하이픈(-)과 눈으로 구별되지 않아 이스케이프로 적는다.</summary>
        private const string Dash = "–";

        private string _path;
        private string[] _lines;

        [SetUp]
        public void SetUp()
        {
            _path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "CLAUDE.md"));
            _lines = System.IO.File.Exists(_path) ? System.IO.File.ReadAllLines(_path) : null;
        }

        [TearDown]
        public void TearDown()
        {
            _lines = null;
            _path = null;
            EventBus.ClearAll();
        }

        /// <summary>파일이 없으면 실패가 아니라 건너뛴다. 빌드 환경에 따라 없을 수 있다.</summary>
        private void RequireFile()
        {
            if (_lines == null)
            {
                Assert.Inconclusive("CLAUDE.md가 없습니다(" + _path + "). 빌드 환경에 따라 없을 수 있어 실패로 세지 않습니다.");
            }
        }

        /// <summary>개정 이력과 「되살리지 마십시오」 줄은 옛 값을 일부러 적어 두는 자리라 본문에서 뺀다.</summary>
        private static bool IsExempt(string line)
        {
            if (line.TrimStart().StartsWith(">", StringComparison.Ordinal))
            {
                return true;
            }

            return line.Contains("되살리지");
        }

        /// <summary>코드가 말하는 현재 경계를 한 줄로 만든다. 실패 메시지의 「코드는 △△」 쪽이다.</summary>
        private static string CodeBounds()
        {
            List<string> parts = new List<string>();
            for (int b = 0; b < Bands.Count; b++)
            {
                Band band = (Band)b;
                parts.Add(band + " = " + BandText(band));
            }

            return string.Join(" · ", parts);
        }

        private static string BandText(Band band)
        {
            return Bands.LowerBound(band) + Dash + Bands.UpperBound(band);
        }

        /// <summary>그 문자열이 본문(면제 줄 제외)에 처음 나오는 줄 번호(1부터). 없으면 0.</summary>
        private int FindInBody(string needle)
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                if (IsExempt(_lines[i]))
                {
                    continue;
                }

                if (_lines[i].Contains(needle))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary>구간 경계를 코드에서 옮긴 뒤 문서에 옛 숫자가 남는 것을 막는다.</summary>
        [Test]
        public void 옛_구간경계가_본문에_남아있지_않다()
        {
            RequireFile();

            string[] banned =
            {
                "0~23",
                "24~47",
                "48~71",
                "72~89",
                "0" + Dash + "23",
                "24" + Dash + "47",
                "48" + Dash + "71",
                "72" + Dash + "89",
                "0/0/12/24/48/72"
            };

            string code = CodeBounds();
            List<string> hits = new List<string>();

            for (int i = 0; i < _lines.Length; i++)
            {
                if (IsExempt(_lines[i]))
                {
                    continue;
                }

                for (int b = 0; b < banned.Length; b++)
                {
                    if (_lines[i].Contains(banned[b]))
                    {
                        hits.Add("CLAUDE.md " + (i + 1) + "줄: " + banned[b] + " — 코드는 " + code);
                    }
                }
            }

            if (hits.Count > 0)
            {
                Assert.Fail(
                    "폐기된 구간 경계가 본문에 남아 있습니다 " + hits.Count + "건:\n- " + string.Join("\n- ", hits)
                    + "\n(개정 이력과 「되살리지 마십시오」 줄은 세지 않았습니다.)");
            }
        }

        /// <summary>코드의 경계를 고쳐 놓고 문서에 새 숫자를 안 적는 것을 막는다.</summary>
        [Test]
        public void 현재_구간경계가_본문에_그대로_적혀있다()
        {
            RequireFile();

            string code = CodeBounds();
            List<string> missing = new List<string>();

            for (int b = 0; b < Bands.Count; b++)
            {
                Band band = (Band)b;
                string want = BandText(band);
                if (FindInBody(want) > 0)
                {
                    continue;
                }

                missing.Add("CLAUDE.md 본문에 「" + want + "」(" + band + ")가 없습니다 — 코드는 " + code);
            }

            if (missing.Count > 0)
            {
                Assert.Fail(
                    "현재 구간 경계가 문서에 적혀 있지 않습니다 " + missing.Count + "건:\n- " + string.Join("\n- ", missing)
                    + "\n경계 숫자는 Bands.cs의 LowerBounds 배열이 정본입니다. 문서를 그 값으로 고치십시오.");
            }
        }

        /// <summary>신뢰 전용 경계를 코드에서 바꾸고 문서를 안 고치는 것을 막는다(2026-10-01).</summary>
        [Test]
        public void 신뢰_전용경계가_본문에_적혀있다()
        {
            RequireFile();

            string want = Bands.TrustLowerBound(Band.Band1) + "/" + Bands.TrustLowerBound(Band.Band2) + "/"
                + Bands.TrustLowerBound(Band.Band3) + "/" + Bands.TrustLowerBound(Band.Band4);
            Assert.Greater(
                FindInBody(want),
                0,
                "CLAUDE.md 본문에 신뢰 전용 경계 「" + want + "」가 없습니다(Bands.cs의 TrustLowerBounds). 문서를 그 값으로 고치십시오.");
        }

        /// <summary>일차 하한 곡선을 코드에서 바꾸고 문서를 안 고치는 것을 막는다.</summary>
        [Test]
        public void 일차하한_곡선이_본문에_적혀있다()
        {
            RequireFile();

            List<string> values = new List<string>();
            for (int day = 1; day <= DayFloor.LastDay; day++)
            {
                values.Add(DayFloor.Of(day).ToString());
            }

            string curve = string.Join("/", values);
            if (FindInBody(curve) > 0)
            {
                return;
            }

            // 곡선을 한 줄로 안 적고 표로 적은 경우도 받아 준다.
            // 최소 조건: 「하한」이라는 말과 함께 마지막 두 일차의 값이 같은 줄에 있어야 한다.
            string last = DayFloor.Of(DayFloor.LastDay).ToString();
            string secondLast = DayFloor.Of(DayFloor.LastDay - 1).ToString();

            for (int i = 0; i < _lines.Length; i++)
            {
                if (IsExempt(_lines[i]))
                {
                    continue;
                }

                string line = _lines[i];
                if (line.Contains("하한") && line.Contains(last) && line.Contains(secondLast))
                {
                    return;
                }
            }

            Assert.Fail(
                "CLAUDE.md 본문에 일차 하한 곡선이 없습니다 — 코드는 " + curve
                + " (DayFloor.cs의 Floors 배열). 한 줄 표기(" + curve + ")나, 「하한」과 함께 "
                + secondLast + "·" + last + "이 들어간 표 줄 가운데 하나는 있어야 합니다.");
        }
    }
}
