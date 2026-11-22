using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using InstructionDT;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 기획 데이터 테이블(DT) 연결 확인. 기획이 xlsx에서 행을 지우거나 칸을 비우면 여기서 먼저 깨집니다.
    /// </summary>
    public class DesignDataTests
    {
        static string CsvDir => Path.Combine(Application.dataPath, "Resources", DT.ResourceFolder);
        static string Read(string name) => File.ReadAllText(Path.Combine(CsvDir, name + ".csv"));

        [TearDown]
        public void Reload() => DT.LoadAll(Read);   // 테스트가 바꾼 표를 원본으로 되돌림

        [Test]
        public void DT_표를_오류_없이_읽는다()
        {
            DT.LoadAll(Read);
            Assert.That(DT.Loaded, Is.True);
            Assert.That(DT.Setting.Count, Is.GreaterThan(0));
            Assert.That(DT.Band.Count, Is.EqualTo(5));
            Assert.That(DT.NightSegment.Count, Is.EqualTo(7));
        }

        [Test]
        public void 코드가_가리키는_설정ID는_모두_DT에_값이_있다()
        {
            DT.LoadAll(Read);
            var rx = new Regex(@"DesignData\.Setting(?:Int|Bool)?\((\d+)\)");
            var root = Path.Combine(Application.dataPath, "_Game");
            var ids = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => rx.Matches(File.ReadAllText(f)).Cast<Match>())
                .Select(m => uint.Parse(m.Groups[1].Value)).Distinct().ToList();
            Assert.That(ids.Count, Is.GreaterThan(100), "설정 참조를 찾지 못함 — 정규식 확인");
            var missing = ids.Where(id => { var r = DT.Setting.Get(id); return r == null || r.IsBlank("Value"); }).ToList();
            Assert.That(missing, Is.Empty, "DT에 없거나 빈칸인 설정 ID: " + string.Join(", ", missing));
        }

        [Test]
        public void 구간_경계는_DT를_따른다()
        {
            Assert.That(Bands.Of(24), Is.EqualTo(Band.Band0));
            Assert.That(Bands.Of(25), Is.EqualTo(Band.Band1));
            // 기획이 「미약한 위화감」 하한을 25 → 30으로 바꾸면 코드도 따라간다
            DT.LoadAll(n => n == "Band_Table" ? Read(n).Replace("330001,미약한 위화감,25,", "330001,미약한 위화감,30,") : Read(n));
            Assert.That(Bands.Of(27), Is.EqualTo(Band.Band0));
            Assert.That(Bands.Of(30), Is.EqualTo(Band.Band1));
        }

        [Test]
        public void 밤_시간표는_DT를_따른다()
        {
            Assert.That(NightClock.ShiftEnd, Is.EqualTo((int)DT.NightSegment.Get(340007).End_Min));
            Assert.That(NightClock.Call2, Is.EqualTo((int)DT.NightSegment.Get(340005).Start_Min));
        }
    }
}
