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
        static void ReloadOnPlay() { DT.LoadAll(); lock (_layoutCounts) _layoutCounts.Clear(); lock (_fx) _fx.Clear(); lock (_fxSeries) _fxSeries.Clear(); lock (_judge) _judge.Clear(); lock (_enc) _enc.Clear(); }   // 플레이마다 새로 읽음(에디터에서 CSV를 다시 내보낸 뒤 바로 반영)

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

        // ── 표시 코드(2단계) ─────────────────────────────────────────────
        static System.Collections.Generic.Dictionary<uint, string> _labels;
        static System.Collections.Generic.Dictionary<string, uint> _codes;
        static object _labelSource;

        /// <summary>
        /// 행의 표시 코드(xlsx 「표시 코드」 칸 = 옛 코드, 예: 110001 → "H1"). 없으면 이름(Name), 그것도 없으면 문구(Text), 그것도 없으면 ID 숫자.
        /// 로그·테스트·디버그 표시 전용 — 키로 쓰지 마십시오(키는 ID).
        /// </summary>
        public static string Label(uint id)
        {
            BuildLabels();
            return _labels.TryGetValue(id, out var s) ? s : id.ToString();
        }

        /// <summary>표시 코드로 행 ID를 찾는다(전 표에서 유일해야 함). 없으면 0. 테스트·이전 도구용.</summary>
        public static uint IdOfCode(string code)
        {
            BuildLabels();
            return code != null && _codes.TryGetValue(code, out var id) ? id : 0u;
        }

        /// <summary>공간 enum → Space_Table ID(표시 코드 = enum 이름). 표에 없는 공간(None·교실 1-1/1-3 같은 세부 공간)은 0.</summary>
        public static uint SpaceRowId(SpaceId space) => RowIdByCode(DT.Space.All, space.ToString());

        /// <summary>밤 구간 enum → Night_Segment_Table ID(표시 코드 = enum 이름). Ended는 0.</summary>
        public static uint SegmentRowId(NightPhase phase) => RowIdByCode(DT.NightSegment.All, phase.ToString());

        static uint RowIdByCode<T>(System.Collections.Generic.IReadOnlyList<T> rows, string code) where T : class, IDTRow
        {
            Ensure();
            var f = typeof(T).GetField("Code");
            for (int i = 0; i < rows.Count; i++)
                if (f != null && (string)f.GetValue(rows[i]) == code) return rows[i].Id;
            return 0u;
        }

        static void BuildLabels()
        {
            Ensure();
            if (_labels != null && ReferenceEquals(_labelSource, DT.Setting)) return;
            var labels = new System.Collections.Generic.Dictionary<uint, string>();
            var codes = new System.Collections.Generic.Dictionary<string, uint>();
            foreach (var p in typeof(DT).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (!p.PropertyType.IsGenericType) continue;
                var rowType = p.PropertyType.GetGenericArguments()[0];
                var all = p.PropertyType.GetProperty("All")?.GetValue(p.GetValue(null)) as System.Collections.IEnumerable;
                if (all == null) continue;
                var fc = rowType.GetField("Code"); var fn = rowType.GetField("Name"); var ft = rowType.GetField("Text");
                foreach (IDTRow r in all)
                {
                    string code = fc?.GetValue(r) as string;
                    string s = !string.IsNullOrEmpty(code) ? code : fn?.GetValue(r) as string;
                    if (string.IsNullOrEmpty(s)) s = ft?.GetValue(r) as string;
                    if (!string.IsNullOrEmpty(s)) labels[r.Id] = s;
                    if (!string.IsNullOrEmpty(code))
                    {
                        if (codes.TryGetValue(code, out var prev) && prev != r.Id)
                            throw new System.InvalidOperationException("DT 표시 코드 중복: " + code + " (" + prev + ", " + r.Id + ")");
                        codes[code] = r.Id;
                    }
                }
            }
            _labels = labels; _codes = codes; _labelSource = DT.Setting;
        }

        // ── Day·Space 행(3단계) ──────────────────────────────────────────
        static System.Collections.Generic.List<DayRow> _days;
        static object _daysSource;

        static System.Collections.Generic.List<DayRow> Days()
        {
            Ensure();
            if (_days != null && ReferenceEquals(_daysSource, DT.Day)) return _days;
            var rows = new System.Collections.Generic.List<DayRow>(DT.Day.All);
            rows.Sort((a, b) => a.Day_No.CompareTo(b.Day_No));
            _days = rows; _daysSource = DT.Day;
            return rows;
        }

        /// <summary>Day_Table 그 일차 행. 첫날 전은 첫날, 마지막 날 뒤는 마지막 날 행(옛 코드의 클램프와 같음).</summary>
        public static DayRow Day(int day)
        {
            var rows = Days();
            if (rows.Count == 0) throw new System.InvalidOperationException("DT Day_Table 비어 있음");
            for (int i = 0; i < rows.Count; i++) if (rows[i].Day_No >= day) return rows[i];
            return rows[rows.Count - 1];
        }

        /// <summary>Day_Table 그 일차 행 — 표에 없는 날은 null(클램프 없음).</summary>
        public static DayRow DayExact(int day)
        {
            var rows = Days();
            for (int i = 0; i < rows.Count; i++) if (rows[i].Day_No == day) return rows[i];
            return null;
        }

        /// <summary>표에 정의된 마지막 일차.</summary>
        public static int LastDay { get { var rows = Days(); return rows.Count == 0 ? 0 : (int)rows[rows.Count - 1].Day_No; } }

        /// <summary>Band_Table ID(330000~) → 구간 번호.</summary>
        public static int BandOfId(uint bandId)
        {
            Ensure();
            if (DT.Band.Get(bandId) == null) throw new System.InvalidOperationException("DT Band " + bandId + " 없음");
            return (int)bandId - 330000;
        }

        /// <summary>Space_Table ID → 공간 enum(표시 코드 = enum 이름). 0·없는 ID는 None.</summary>
        public static SpaceId SpaceOf(uint spaceRowId)
        {
            Ensure();
            var row = spaceRowId == 0 ? null : DT.Space.Get(spaceRowId);
            return row != null && System.Enum.TryParse(row.Code, out SpaceId s) ? s : SpaceId.None;
        }

        /// <summary>
        /// 공간 enum의 Space_Table 행. 세부 공간 교실 1-3(Classroom_1_3 = 씬 Classroom02)은 표의 「교실」 행을 쓴다(표 설명: 유일한 교실). 없으면 null.
        /// </summary>
        public static SpaceRow SpaceRowOf(SpaceId space)
        {
            Ensure();
            uint id = SpaceRowId(space == SpaceId.Classroom_1_3 ? SpaceId.Classroom : space);
            return id == 0 ? null : DT.Space.Get(id);
        }

        /// <summary>Common_enum 공포 축 ID → 축.</summary>
        public static FearAxis AxisOf(uint axisId)
        {
            switch (axisId)
            {
                case 510101: return FearAxis.Auditory;
                case 510102: return FearAxis.Illuminance;
                case 510103: return FearAxis.Layout;
                case 510104: return FearAxis.Trust;
                default: throw new System.InvalidOperationException("DT 공포 축 " + axisId + " 모름");
            }
        }

        /// <summary>키 문자열 → 행 ID. 6자리 숫자면 그대로, 아니면 표시 코드로 찾는다. 없으면 0.</summary>
        public static uint IdOf(string codeOrId)
        {
            return uint.TryParse(codeOrId, out var n) && n >= 100000 && n <= 999999 ? n : IdOfCode(codeOrId);
        }

        /// <summary>Message_Table 행. 없거나 꺼져 있으면(Is_Active) 예외.</summary>
        public static MessageRow MessageRow(uint id)
        {
            Ensure();
            var row = DT.Message.Get(id);
            if (row == null) throw new System.InvalidOperationException("DT Message " + id + " 없음");
            return row;
        }

        /// <summary>Message_Table 문구(Text). 문구의 정본은 xlsx — 코드에 같은 문장을 다시 적지 마십시오.</summary>
        public static string Msg(uint id) => MsgText(MessageRow(id));

        /// <summary>2026-11-22: Message_Table 행의 글을 지금 언어로(<see cref="Loc"/> — 한국어면 그대로).</summary>
        public static string MsgText(MessageRow row) => row == null ? string.Empty : Loc.Dt(row.ID, "Text", row.Text);

        /// <summary>여러 문구 칸(「a | b | c」)을 나눈다.</summary>
        public static string[] MsgParts(uint id) => Msg(id).Split(new[] { " | " }, System.StringSplitOptions.None);

        /// <summary>엔딩 enum → Ending_Table ID(표시 코드 = enum 이름). None은 0.</summary>
        public static uint EndingRowId(FinaleEnding ending) => RowIdByCode(DT.Ending.All, ending.ToString());

        /// <summary>그 엔딩의 조건 값(조건 종류 ID — 580101 창밖 응시 합 등). 칸이 없으면 예외.</summary>
        public static float EndingCond(FinaleEnding ending, uint condType)
        {
            Ensure();
            var row = DT.Ending.Get(EndingRowId(ending));
            if (row != null)
            {
                if (row.Cond_1_Type_ID == condType) return row.Cond_1_Value;
                if (row.Cond_2_Type_ID == condType) return row.Cond_2_Value;
                if (row.Cond_3_Type_ID == condType) return row.Cond_3_Value;
            }

            throw new System.InvalidOperationException("DT Ending " + ending + " 조건 " + condType + " 없음");
        }

        static readonly System.Collections.Generic.Dictionary<uint, int[]> _layoutCounts = new System.Collections.Generic.Dictionary<uint, int[]>();

        /// <summary>Layout_Prop_Table 띠 0~4별 소품 수(길이 5, 캐시 — 고치지 마십시오). 없으면 예외.</summary>
        public static int[] LayoutCounts(uint id)
        {
            Ensure();
            lock (_layoutCounts)
            {
                if (_layoutCounts.TryGetValue(id, out var cached)) return cached;
                var row = DT.LayoutProp.Get(id);
                if (row == null) throw new System.InvalidOperationException("DT Layout_Prop " + id + " 없음");
                var counts = new[] { (int)row.Band_0_Count, (int)row.Band_1_Count, (int)row.Band_2_Count, (int)row.Band_3_Count, (int)row.Band_4_Count };
                _layoutCounts[id] = counts;
                return counts;
            }
        }

        static readonly System.Collections.Generic.Dictionary<(uint, string), float> _fx = new System.Collections.Generic.Dictionary<(uint, string), float>();
        static readonly System.Collections.Generic.Dictionary<(uint, string), float[]> _fxSeries = new System.Collections.Generic.Dictionary<(uint, string), float[]>();

        /// <summary>Fx_Table 행. 없으면 예외.</summary>
        public static FxRow FxRow(uint id)
        {
            Ensure();
            var r = DT.Fx.Get(id);
            if (r == null) throw new System.InvalidOperationException("DT Fx " + id + " 없음");
            return r;
        }

        /// <summary>
        /// Fx 값 — 칸 <b>이름</b>(Param_n_Name)으로 찾는다. 기획자가 칸 순서를 바꿔도 값이 엉뚱한 곳에 들어가지 않는다.
        /// 이름이 없거나 값이 비면 예외(빈칸이 0으로 조용히 읽히지 않게). 캐시 — 플레이 시작 때 비운다.
        /// </summary>
        public static float Fx(uint id, string name)
        {
            lock (_fx)
            {
                if (_fx.TryGetValue((id, name), out var v)) return v;
                var r = FxRow(id);
                for (int i = 1; i <= 12; i++)
                {
                    if (FxParamName(r, i) != name) continue;
                    if (r.IsBlank("Param_" + i + "_Value")) throw new System.InvalidOperationException("DT Fx " + id + " 「" + name + "」 값이 비어 있음");
                    v = FxParamValue(r, i);
                    _fx[(id, name)] = v;
                    return v;
                }

                throw new System.InvalidOperationException("DT Fx " + id + " 「" + name + "」 칸 없음");
            }
        }

        /// <summary>Fx 값(정수로 반올림).</summary>
        public static int FxInt(uint id, string name) => Mathf.RoundToInt(Fx(id, name));

        /// <summary>Fx 켬/끔(0이 아니면 켬).</summary>
        public static bool FxOn(uint id, string name) => Fx(id, name) != 0f;

        /// <summary>Fx 색 — 「접두 R」「접두 G」「접두 B」 세 칸.</summary>
        public static Color FxColor(uint id, string prefix) => new Color(Fx(id, prefix + " R"), Fx(id, prefix + " G"), Fx(id, prefix + " B"));

        /// <summary>연속 행(firstId, firstId+1, …)의 같은 이름 칸 — 구간·층·일차별 줄. 캐시(고치지 마십시오).</summary>
        public static float[] FxSeries(uint firstId, int count, string name)
        {
            lock (_fxSeries)
            {
                if (_fxSeries.TryGetValue((firstId, name), out var cached) && cached.Length == count) return cached;
                var values = new float[count];
                for (int i = 0; i < count; i++) values[i] = Fx(firstId + (uint)i, name);
                _fxSeries[(firstId, name)] = values;
                return values;
            }
        }

        /// <summary>Fx 행에 그 이름 칸이 있는가(값이 비어도 참).</summary>
        public static bool FxHas(uint id, string name)
        {
            var r = FxRow(id);
            for (int i = 1; i <= 12; i++)
                if (FxParamName(r, i) == name) return true;
            return false;
        }

        static string FxParamName(FxRow r, int i)
        {
            switch (i)
            {
                case 1: return r.Param_1_Name;
                case 2: return r.Param_2_Name;
                case 3: return r.Param_3_Name;
                case 4: return r.Param_4_Name;
                case 5: return r.Param_5_Name;
                case 6: return r.Param_6_Name;
                case 7: return r.Param_7_Name;
                case 8: return r.Param_8_Name;
                case 9: return r.Param_9_Name;
                case 10: return r.Param_10_Name;
                case 11: return r.Param_11_Name;
                case 12: return r.Param_12_Name;
                default: return null;
            }
        }

        static float FxParamValue(FxRow r, int i)
        {
            switch (i)
            {
                case 1: return r.Param_1_Value;
                case 2: return r.Param_2_Value;
                case 3: return r.Param_3_Value;
                case 4: return r.Param_4_Value;
                case 5: return r.Param_5_Value;
                case 6: return r.Param_6_Value;
                case 7: return r.Param_7_Value;
                case 8: return r.Param_8_Value;
                case 9: return r.Param_9_Value;
                case 10: return r.Param_10_Value;
                case 11: return r.Param_11_Value;
                case 12: return r.Param_12_Value;
                default: return 0f;
            }
        }

        static readonly System.Collections.Generic.Dictionary<(uint, string), float> _judge = new System.Collections.Generic.Dictionary<(uint, string), float>();

        /// <summary>
        /// Rule_Table 판정 수치 — 칸 이름(Judge_Time·Judge_Time2·Judge_Dist·Judge_Dist2·Judge_Angle·Judge_Angle2·Judge_Angle3·Judge_Count)으로.
        /// 행이 없거나 값이 비면 예외(빈칸이 0으로 조용히 읽히지 않게). 캐시 — 플레이 시작 때 비운다.
        /// </summary>
        public static float Judge(uint id, string column)
        {
            lock (_judge)
            {
                if (_judge.TryGetValue((id, column), out var v)) return v;
                Ensure();
                var r = DT.Rule.Get(id);
                if (r == null) throw new System.InvalidOperationException("DT Rule " + id + " 없음");
                if (r.IsBlank(column)) throw new System.InvalidOperationException("DT Rule " + id + " 「" + column + "」 값이 비어 있음");
                switch (column)
                {
                    case "Judge_Time": v = r.Judge_Time; break;
                    case "Judge_Time2": v = r.Judge_Time2; break;
                    case "Judge_Dist": v = r.Judge_Dist; break;
                    case "Judge_Dist2": v = r.Judge_Dist2; break;
                    case "Judge_Angle": v = r.Judge_Angle; break;
                    case "Judge_Angle2": v = r.Judge_Angle2; break;
                    case "Judge_Angle3": v = r.Judge_Angle3; break;
                    case "Judge_Count": v = r.Judge_Count; break;
                    default: throw new System.InvalidOperationException("Rule 판정 칸 「" + column + "」 없음");
                }

                _judge[(id, column)] = v;
                return v;
            }
        }

        static readonly System.Collections.Generic.Dictionary<(uint, string), float> _enc = new System.Collections.Generic.Dictionary<(uint, string), float>();

        /// <summary>
        /// Encounter_Table 방아쇠·자리 수치 — 칸 이름(Trigger_Value·Window_Sec·Ahead_M·Behind_M·After_Sec·Presence_Sec·Foreshadow_Fixed_Sec)으로.
        /// 행이 없거나 값이 비면 예외. 캐시 — 플레이 시작 때 비운다.
        /// </summary>
        public static float EncounterValue(uint id, string column)
        {
            lock (_enc)
            {
                if (_enc.TryGetValue((id, column), out var v)) return v;
                Ensure();
                var r = DT.Encounter.Get(id);
                if (r == null) throw new System.InvalidOperationException("DT Encounter " + id + " 없음");
                if (r.IsBlank(column)) throw new System.InvalidOperationException("DT Encounter " + id + " 「" + column + "」 값이 비어 있음");
                switch (column)
                {
                    case "Trigger_Value": v = r.Trigger_Value; break;
                    case "Window_Sec": v = r.Window_Sec; break;
                    case "Ahead_M": v = r.Ahead_M; break;
                    case "Behind_M": v = r.Behind_M; break;
                    case "After_Sec": v = r.After_Sec; break;
                    case "Presence_Sec": v = r.Presence_Sec; break;
                    case "Foreshadow_Fixed_Sec": v = r.Foreshadow_Fixed_Sec; break;
                    default: throw new System.InvalidOperationException("Encounter 칸 「" + column + "」 없음");
                }

                _enc[(id, column)] = v;
                return v;
            }
        }

        /// <summary>판정 수치(정수로 반올림).</summary>
        public static int JudgeInt(uint id, string column) => Mathf.RoundToInt(Judge(id, column));

        /// <summary>Task_Table 행(표시 코드 또는 ID 문자열로). 없으면 예외.</summary>
        public static TaskRow TaskRow(string codeOrId)
        {
            Ensure();
            uint id = IdOf(codeOrId);
            var row = id == 0 ? null : DT.Task.Get(id);
            if (row == null) throw new System.InvalidOperationException("DT Task " + codeOrId + " 없음");
            return row;
        }
    }
}
