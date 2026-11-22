using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 2026-11-22 로컬라이징 1단계 — 언어 값(<see cref="Loc"/>)·번역표(<see cref="LocTable"/>)·빈칸 해독의 언어 무관 논리.
    /// 한국어(기본)에서는 화면 글이 옛 글과 글자 하나 다르지 않아야 한다.
    /// </summary>
    public class LocalizationTests
    {
        private const string Header = "Key,상태,KO_원문,EN,현재_KO,출처,메모\n";

        [TearDown]
        public void Restore()
        {
            Loc.Set(TextLang.Korean);
            Loc.SetTableForTest(null);
        }

        // ── 한국어: 옛 글 그대로 ─────────────────────────────

        [Test]
        public void 한국어면_수칙_지시_점검_문자_글이_정본과_같다()
        {
            Assert.IsTrue(Loc.IsKorean);
            foreach (RuleDef r in ProgramCatalog.AllRules) Assert.AreEqual(r.Text, r.ShownText, r.Id);
            foreach (UnavoidableDef u in UnavoidableCatalog.All) Assert.AreEqual(u.Message, u.ShownMessage, u.Id);
            Assert.AreEqual(ProgramCatalog.FinaleBlankFilled, ProgramCatalog.FinaleBlankFilledText);
            Assert.AreEqual(DutyCatalog.TrustLine, DutyCatalog.TrustLineText);
            Assert.AreEqual("01:30까지", string.Format(DutyCatalog.UntilFormat, "01:30"));
        }

        [Test]
        public void 한국어_빈칸_해독은_DT_해독문과_옛_조사_바꿈이_같다()
        {
            Assert.AreEqual("교실의 소녀는 무시하십시오.", BlankDecoding.Decode("110006", "교실의 _? 는 무시하십시오."));
            Assert.AreEqual("도서관 담당자를 계속 비추십시오.", BlankDecoding.Decode("110024", "도서관 ?_? 을 계속 비추십시오."));
            Assert.AreEqual("화면 속 사망자가 지나갈 때까지 CCTV에서 눈을 떼지 마십시오.", BlankDecoding.Decode("110029", "화면 속 !_ 이 지나갈 때까지 CCTV에서 눈을 떼지 마십시오."));
            // 미세 변조(끝에 「?」)는 DT 해독문에 없는 글 — 옛 조사 바꿈으로
            Assert.AreEqual("교실의 소녀는 무시하십시오?.", BlankDecoding.Decode("110006", ParadoxCatalog.Subtle("교실의 _? 는 무시하십시오.")));
        }

        // ── 영어: 찾기 · 폴백 · 전환 ─────────────────────────

        [Test]
        public void 영어면_번역을_쓰고_없거나_원문이_다르면_한국어()
        {
            Loc.SetTableForTest(Header
                + "1.Text,완료,가,A,,,\n"
                + "2.Text,번역 없음,나,,,,\n"
                + "3.Text,완료,옛 원문,C,,,\n"
                + "ui.x,완료,\"{0}까지\",By {0},,,\n");
            Loc.Set(TextLang.English);
            Assert.AreEqual("A", Loc.Dt(1u, "Text", "가"));
            Assert.AreEqual("나", Loc.Dt(2u, "Text", "나"), "영어 빈칸 → 한국어");
            Assert.AreEqual("새 원문", Loc.Dt(3u, "Text", "새 원문"), "원문이 바뀌면 낡은 번역을 쓰지 않는다");
            Assert.AreEqual("By {0}", Loc.T("ui.x", "{0}까지"));
            Assert.AreEqual("없는 키", Loc.T("ui.none", "없는 키"));
        }

        [Test]
        public void 언어를_바꾸면_Changed가_한_번_오고_같은_값이면_오지_않는다()
        {
            int n = 0;
            System.Action h = () => n++;
            Loc.Changed += h;
            try
            {
                Loc.Set(TextLang.English);
                Loc.Set(TextLang.English);
                Loc.Set(TextLang.Korean);
                Assert.AreEqual(2, n);
            }
            finally
            {
                Loc.Changed -= h;
            }
        }

        [Test]
        public void 영어_빈칸_해독도_같은_논리_원문_해독문_그리고_가림표_바꿈()
        {
            Loc.SetTableForTest(Header
                + "110006.Text,완료,교실의 _? 는 무시하십시오.,Ignore the _? in the classroom.,,,\n"
                + "110006.Blank_Decoded,완료,교실의 소녀는 무시하십시오.,Ignore the girl in the classroom.,,,\n"
                + "110006.Blank_Answer,완료,소녀,girl,,,\n");
            Loc.Set(TextLang.English);
            RuleDef c2 = ProgramCatalog.Rule("110006");
            Assert.AreEqual("Ignore the _? in the classroom.", c2.ShownText);
            Assert.AreEqual("Ignore the girl in the classroom.", BlankDecoding.Decode("110006", c2.ShownText));
            Assert.AreEqual("Ignore the girl in the classroom?.", BlankDecoding.Decode("110006", ParadoxCatalog.Subtle(c2.ShownText)), "미세 변조 — 가림표 → 해독 낱말");
            Assert.AreEqual("girl", BlankDecoding.Word("110006"));
        }

        [Test]
        public void 문자는_다시_만드는_함수로_지금_언어_글을_준다()
        {
            Loc.SetTableForTest(Header + "duty.trust,완료,근무 평가에 반영되었습니다.,Reviewed.,,,\n");
            ParadoxMessage m = new ParadoxMessage("x", "y", SpaceId.None, DutyCatalog.TrustLineText, 0, () => DutyCatalog.TrustLineText);
            Assert.AreEqual("근무 평가에 반영되었습니다.", m.Text);
            Loc.Set(TextLang.English);
            Assert.AreEqual("근무 평가에 반영되었습니다.", m.Text, "보낸 순간의 글은 그대로");
            Assert.AreEqual("Reviewed.", m.CurrentText);
        }

        // ── 번역표 갱신 ─────────────────────────────────────

        [Test]
        public void 번역표_갱신은_새_줄을_더하고_원문_바뀜과_사라짐을_표시한다()
        {
            string old = Header
                + "1.Text,완료,가,A,,,메모1\n"
                + "2.Text,완료,나,B,,,\n"
                + "9.Text,완료,사라질 글,Z,,,\n";
            List<LocTable.Source> now = new List<LocTable.Source>
            {
                new LocTable.Source { Key = "1.Text", Korean = "가", Origin = "t" },
                new LocTable.Source { Key = "2.Text", Korean = "나나", Origin = "t" },
                new LocTable.Source { Key = "3.Text", Korean = "다", Origin = "t" },
            };
            LocTable.Result r = LocTable.Merge(now, old);
            Assert.AreEqual(1, r.Done);
            Assert.AreEqual(1, r.Changed);
            Assert.AreEqual(1, r.Todo);
            Assert.AreEqual(1, r.Gone);
            Assert.AreEqual(1, r.Added);
            StringAssert.Contains("2.Text,원문 바뀜,나,B,나나", r.Csv, "옛 원문을 남기고 현재_KO에 새 원문");
            StringAssert.Contains("메모1", r.Csv, "메모를 잃지 않는다");
            StringAssert.Contains("9.Text,사라짐,사라질 글,Z", r.Csv, "사라진 키의 번역을 잃지 않는다");
        }

        [Test]
        public void 코드_글_찾기는_주석을_건너뛰고_이스케이프를_푼다()
        {
            string code = "/// <c>Loc.T(\"예시\", \"주석\")</c>\n"
                + "// Loc.T(\"a\", \"b\")\n"
                + "string s = Loc.T(\"ui.a\", \"줄\\n바꿈\");";
            List<LocTable.Source> list = LocTable.FromCode("f.cs", code);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("ui.a", list[0].Key);
            Assert.AreEqual("줄\n바꿈", list[0].Korean);
        }

        // ── 실제 번역표의 약속 ───────────────────────────────

        private static List<string[]> RealTable()
        {
            string path = Path.Combine(Application.dataPath, "Resources", Loc.ResourcePath + ".csv");
            Assert.IsTrue(File.Exists(path), "번역표 사본 없음 — Tools/DT/번역 목록 갱신 (EN)");
            return InstructionDT.DTCsv.Parse(File.ReadAllText(path));
        }

        [Test]
        public void 번역표의_영어는_자리표시자와_구분자를_한국어와_같게_둔다()
        {
            List<string[]> rows = RealTable();
            string[] h = rows[0];
            int k = System.Array.IndexOf(h, "Key"), ko = System.Array.IndexOf(h, "KO_원문"), en = System.Array.IndexOf(h, "EN");
            Regex ph = new Regex("\\{\\d+\\}");
            for (int i = 1; i < rows.Count; i++)
            {
                string[] r = rows[i];
                if (r.Length <= en || string.IsNullOrEmpty(r[en])) continue;
                Assert.AreEqual(ph.Matches(r[ko]).Count, ph.Matches(r[en]).Count, r[k] + " — {0} 자리표시자 수");
                Assert.AreEqual(Regex.Matches(r[ko], " \\| ").Count, Regex.Matches(r[en], " \\| ").Count, r[k] + " — 「 | 」 구분 수");
                Assert.AreEqual(Regex.Matches(r[ko], " → ").Count, Regex.Matches(r[en], " → ").Count, r[k] + " — 「 → 」 구분 수(ShiftEndPhone이 나눈다)");
                Assert.AreEqual(Regex.Matches(r[ko], "\\[[A-Z]\\]").Count, Regex.Matches(r[en], "\\[[A-Z]\\]").Count, r[k] + " — 키 표시([E]) 수");
                Assert.AreEqual(Regex.Matches(r[ko], "<[^>]+>").Count, Regex.Matches(r[en], "<[^>]+>").Count, r[k] + " — 서식 태그 수");
            }
        }

        [Test]
        public void 빈칸_수칙의_영어_원문에는_같은_가림표가_있고_해독문에는_없다()
        {
            List<string[]> rows = RealTable();
            string[] h = rows[0];
            int k = System.Array.IndexOf(h, "Key"), en = System.Array.IndexOf(h, "EN");
            Dictionary<string, string> map = new Dictionary<string, string>();
            for (int i = 1; i < rows.Count; i++) if (rows[i].Length > en) map[rows[i][k]] = rows[i][en];

            DesignData.Ensure();
            foreach (InstructionDT.RuleRow r in InstructionDT.DT.Rule.All)
            {
                if (string.IsNullOrEmpty(r.Blank_Mark)) continue;
                string text, decoded;
                if (!map.TryGetValue(r.ID + ".Text", out text) || string.IsNullOrEmpty(text)) continue;
                StringAssert.Contains(r.Blank_Mark, text, r.ID + " 영어 원문에 가림표");
                Assert.IsTrue(map.TryGetValue(r.ID + ".Blank_Decoded", out decoded) && !string.IsNullOrEmpty(decoded), r.ID + " 영어 해독문 필요");
                StringAssert.DoesNotContain(r.Blank_Mark, decoded, r.ID + " 해독문에는 가림표가 없다");

                // 변조본도 같은 약속 — 한국어 변조본에 가림표가 있으면 영어 변조본에도, 영어 해독 변조본에는 없음
                string variant, decodedVariant;
                if (string.IsNullOrEmpty(r.Variant_Text) || r.Variant_Text.IndexOf(r.Blank_Mark, System.StringComparison.Ordinal) < 0) continue;
                if (!map.TryGetValue(r.ID + ".Variant_Text", out variant) || string.IsNullOrEmpty(variant)) continue;
                StringAssert.Contains(r.Blank_Mark, variant, r.ID + " 영어 변조본에 가림표");
                Assert.IsTrue(map.TryGetValue(r.ID + ".Blank_Decoded_Variant", out decodedVariant) && !string.IsNullOrEmpty(decodedVariant), r.ID + " 영어 해독 변조본 필요");
                StringAssert.DoesNotContain(r.Blank_Mark, decodedVariant, r.ID + " 해독 변조본에는 가림표가 없다");
            }
        }

        [Test]
        public void 원문_키_글과_화면_글자_바꿈은_양쪽으로_된다()
        {
            string ko = "파견 근무 계약서";
            Loc.SetTableForTest(Header + Loc.KeyOf(ko) + ",완료," + ko + ",Contract,,,\n");
            Assert.AreEqual(ko, Loc.K(ko), "한국어면 그대로");
            Assert.IsNull(Loc.Swap(ko), "한국어 화면의 한국어는 바꿀 것 없음");
            Assert.AreEqual(ko, Loc.Swap("Contract"), "한국어로 돌아오면 영어 글을 한국어로");
            Loc.Set(TextLang.English);
            Assert.AreEqual("Contract", Loc.K(ko));
            Assert.AreEqual("Contract", Loc.Swap(ko));
            Assert.IsNull(Loc.Swap("표에 없는 글"));
            Assert.AreEqual(Loc.KeyOf("a\r\nb"), Loc.KeyOf("a\nb"), "줄바꿈 표기 차이는 같은 키");
        }

        [Test]
        public void K2_역설_문자는_채널_번호를_채우기_전_글로_번역을_찾는다()
        {
            Loc.SetTableForTest(Header + "110030.Paradox_Text,완료,공실 CAM{0}에 움직임이 있습니다. 즉시 확인하십시오.,Motion on VACANT CAM{0}.,,,\n");
            ParadoxEntry k2 = ParadoxCatalog.Find("110030");
            Assert.IsNotNull(k2);
            Loc.Set(TextLang.English);
            Assert.AreEqual("Motion on VACANT CAM{0}.", k2.ShownMessage);
            Assert.AreEqual(string.Format(k2.ShownMessage, "03"), "Motion on VACANT CAM03.");
        }
    }
}
