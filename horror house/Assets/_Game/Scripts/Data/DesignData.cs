using InstructionDT;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 기획 데이터 테이블(DT, DesignData/INSTRUCTION_DT_v6.xlsx → Assets/Resources/DT/*.csv) 진입점.
    /// 게임 시작 때 한 번 읽고, 그 전에 누가 값을 찾으면(EditMode 테스트 등) 그때 읽습니다.
    /// 값의 정본은 xlsx입니다. 코드에 같은 숫자를 다시 적지 마십시오.
    /// </summary>
    public static class DesignData
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReloadOnPlay() => DT.LoadAll();   // 플레이마다 새로 읽음(에디터에서 CSV를 다시 내보낸 뒤 바로 반영)

        static readonly object Gate = new object();

        public static void Ensure()
        {
            if (DT.Loaded) return;
            lock (Gate)
            {
                if (DT.Loaded) return;
#if UNITY_EDITOR
                // 에디터: 파일에서 바로 읽음. 씬 역직렬화·생성자 안에서 처음 불려도 Resources.Load 제약에 걸리지 않게.
                var dir = System.IO.Path.Combine(Application.dataPath, "Resources", DT.ResourceFolder);
                DT.LoadAll(name => System.IO.File.ReadAllText(System.IO.Path.Combine(dir, name + ".csv")));
#else
                DT.LoadAll();
#endif
            }
        }

        /// <summary>Setting_Table 값. 행이 없거나 빈칸이면 예외 — 기획 누락을 조용히 넘기지 않습니다.</summary>
        public static float Setting(uint id)
        {
            Ensure();
            var row = DT.Setting.Get(id);
            if (row == null || row.IsBlank("Value"))
                throw new System.InvalidOperationException("DT Setting " + id + " 없음 또는 빈칸 — INSTRUCTION_DT_v6.xlsx 확인");
            return row.Value;
        }

        /// <summary>Night_Segment_Table 구간의 시작(또는 끝) 게임 분.</summary>
        public static int SegmentMinute(uint id, bool end)
        {
            Ensure();
            var row = DT.NightSegment.Get(id);
            if (row == null) throw new System.InvalidOperationException("DT Night_Segment " + id + " 없음");
            return (int)(end ? row.End_Min : row.Start_Min);
        }

        static readonly System.Collections.Generic.Dictionary<string, int[]> DayCache = new System.Collections.Generic.Dictionary<string, int[]>();
        static object _daySource;

        /// <summary>Day_Table 한 칸을 일차 순(1일차 = [0])으로. DT를 다시 읽기 전까지 같은 배열을 돌려줍니다(호출마다 할당 없음).</summary>
        public static int[] DayInts(string column, System.Func<DayRow, uint> pick)
        {
            Ensure();
            if (!ReferenceEquals(_daySource, DT.Day)) { DayCache.Clear(); _daySource = DT.Day; }
            if (DayCache.TryGetValue(column, out var cached)) return cached;
            var rows = new System.Collections.Generic.List<DayRow>(DT.Day.All);
            rows.Sort((a, b) => a.Day_No.CompareTo(b.Day_No));
            var arr = new int[rows.Count];
            for (int i = 0; i < rows.Count; i++) arr[i] = (int)pick(rows[i]);
            DayCache[column] = arr;
            return arr;
        }

        public static int SettingInt(uint id) => Mathf.RoundToInt(Setting(id));
        public static bool SettingBool(uint id) => Setting(id) != 0f;

        /// <summary>값이 Band_Table ID(330000~330004)인 설정을 구간 번호(0~4)로.</summary>
        public static int BandLevel(uint id)
        {
            int band = SettingInt(id);
            Ensure();
            if (DT.Band.Get((uint)band) == null) throw new System.InvalidOperationException("DT Setting " + id + " 값 " + band + "은 Band_Table ID가 아님");
            return band - 330000;
        }
    }
}
