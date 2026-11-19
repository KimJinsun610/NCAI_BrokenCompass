using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InstructionDT;
using NUnit.Framework;
using UnityEngine;

namespace NightDuty.Tests
{
    /// <summary>
    /// 표 대조 틀(DT 적용 2단계). 카탈로그 항목(코드가 정본이던 것)과 DT 행을 표시 코드로 짝지어 필드마다 비교한다.
    /// 3단계에서 표를 옮길 때마다 <see cref="Diff{TItem,TRow}"/>로 「대조 → 교체 → 대조」를 돌린다.
    /// </summary>
    public static class DTCompare
    {
        /// <summary>한 필드의 비교 규칙: 이름, 카탈로그 쪽 값, DT 쪽 값.</summary>
        public sealed class Field<TItem, TRow>
        {
            public string Name;
            public Func<TItem, object> Code;
            public Func<TRow, object> Dt;
        }

        public static Field<TItem, TRow> F<TItem, TRow>(string name, Func<TItem, object> code, Func<TRow, object> dt)
            => new Field<TItem, TRow> { Name = name, Code = code, Dt = dt };

        /// <summary>
        /// 다른 점 목록(빈 목록 = 일치). 짝이 없는 항목(카탈로그만·DT만)도 한 줄씩 보고한다.
        /// <paramref name="codeOf"/>: 카탈로그 항목의 옛 코드(또는 이미 바뀐 ID 문자열).
        /// </summary>
        public static List<string> Diff<TItem, TRow>(IEnumerable<TItem> items, Func<TItem, string> codeOf, IReadOnlyList<TRow> rows,
            params Field<TItem, TRow>[] fields) where TRow : class, IDTRow
        {
            var diffs = new List<string>();
            var byId = rows.ToDictionary(r => r.Id);
            var matched = new HashSet<uint>();
            foreach (var item in items)
            {
                string code = codeOf(item);
                uint id = uint.TryParse(code, out var n) && n >= 100000 ? n : DesignData.IdOfCode(code);
                if (id == 0 || !byId.TryGetValue(id, out var row)) { diffs.Add("DT에 없음: " + code); continue; }
                matched.Add(id);
                foreach (var f in fields)
                {
                    object a = Norm(f.Code(item)), b = Norm(f.Dt(row));
                    if (!Equals(a, b)) diffs.Add(DesignData.Label(id) + "(" + id + ")." + f.Name + ": 코드=" + a + " DT=" + b);
                }
            }
            foreach (var r in rows) if (!matched.Contains(r.Id)) diffs.Add("코드에 없음: " + DesignData.Label(r.Id) + "(" + r.Id + ")");
            return diffs;
        }

        static object Norm(object v)
        {
            switch (v)
            {
                case null: return "";
                case string s: return s.Trim();
                case float f: return Math.Round(f, 4);
                case double d: return Math.Round(d, 4);
                case int i: return (double)i;
                case uint u: return (double)u;
                case bool b: return b;
                default: return v.ToString();
            }
        }
    }

    /// <summary>표시 코드 칸·enum 연결 확인(DT 적용 2단계).</summary>
    public class DesignDataCodeTests
    {
        static string Read(string name) => File.ReadAllText(Path.Combine(Application.dataPath, "Resources", DT.ResourceFolder, name + ".csv"));

        [SetUp]
        public void Load() => DT.LoadAll(Read);

        [Test]
        public void 표시_코드로_라벨을_보여준다()
        {
            Assert.That(DesignData.Label(110001), Is.EqualTo("H1"));
            Assert.That(DesignData.IdOfCode("W5"), Is.Not.EqualTo(0u));
            Assert.That(DesignData.IdOfCode("없는 코드"), Is.EqualTo(0u));
            // 표시 코드가 없는 행은 이름
            Assert.That(DesignData.Label(330002), Is.EqualTo(DT.Band.Get(330002).Name));
        }

        [Test]
        public void 공간_enum은_Space_Table과_이어진다()
        {
            var expectMissing = new[] { SpaceId.None, SpaceId.Classroom_1_1, SpaceId.Classroom_1_3 };   // 세부 공간 — 표에 없음(교실은 Classroom 하나)
            foreach (SpaceId s in Enum.GetValues(typeof(SpaceId)))
            {
                uint id = DesignData.SpaceRowId(s);
                if (expectMissing.Contains(s)) Assert.That(id, Is.EqualTo(0u), s.ToString());
                else Assert.That(DT.Space.Get(id), Is.Not.Null, s + " → Space_Table 행 없음");
            }
            Assert.That(DT.Space.Count, Is.EqualTo(Enum.GetValues(typeof(SpaceId)).Length - expectMissing.Length), "표에만 있는 공간");
        }

        [Test]
        public void 밤_구간_enum은_Night_Segment_Table과_이어지고_시각이_맞다()
        {
            foreach (NightPhase p in Enum.GetValues(typeof(NightPhase)))
            {
                uint id = DesignData.SegmentRowId(p);
                if (p == NightPhase.Ended) { Assert.That(id, Is.EqualTo(0u)); continue; }
                Assert.That(id, Is.Not.EqualTo(0u), p.ToString());
                int start = DesignData.SegmentMinute(id, end: false);
                Assert.That(NightClock.PhaseAt(start), Is.EqualTo(p), p + " 시작 " + start + "분");
            }
        }

        [Test]
        public void 대조_틀은_다른_점과_짝없는_행을_보고한다()
        {
            var items = new[] { ("Corridor", "복도"), ("Toilet", "틀린 이름"), ("Nowhere", "") };
            var diffs = DTCompare.Diff(items, i => i.Item1, DT.Space.All,
                DTCompare.F<(string, string), SpaceRow>("Name", i => i.Item2, r => r.Name));
            Assert.That(diffs.Any(d => d.StartsWith("Toilet(") && d.Contains(".Name")), Is.True, string.Join("\n", diffs));
            Assert.That(diffs.Contains("DT에 없음: Nowhere"), Is.True);
            Assert.That(diffs.Count(d => d.StartsWith("코드에 없음")), Is.EqualTo(DT.Space.Count - 2));
            Assert.That(diffs.Any(d => d.StartsWith("Corridor(")), Is.False);
        }
    }
}
