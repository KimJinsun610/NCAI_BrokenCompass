using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 기획서(2026-09-12)의 공간별 이상현상 표로 <see cref="SpaceAnomalyTableSO"/> 에셋을 만든다.
    /// <para>
    /// 이미 에셋이 있으면 건드리지 않는다. 초기값으로 되돌리려면 에셋을 지우고 다시 실행한다.
    /// 확인 대화상자를 띄우지 않으므로 Unity CLI/MCP로도 실행할 수 있다.
    /// </para>
    /// <para>
    /// 큐 ID 목록은 표 문구를 연출 단위로 나눈 <b>해석</b>이다. 「유지」·「추가」는 앞 구간의 큐를 이어받는 것으로 읽었다.
    /// </para>
    /// </summary>
    public static class SpaceAnomalyTableBuilder
    {
        /// <summary>에셋 경로.</summary>
        public const string AssetPath = "Assets/_Game/ScriptableObjects/SpaceAnomalyTable.asset";

        /// <summary>메뉴: 이상현상 표 에셋 생성.</summary>
        [MenuItem("NightDuty/이상현상 표 에셋 생성", false, 121)]
        public static void BuildMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<SpaceAnomalyTableSO>(AssetPath) != null)
            {
                Debug.Log("[이상현상 표] 이미 있습니다: " + AssetPath);
                return;
            }

            SpaceAnomalyTableSO table = ScriptableObject.CreateInstance<SpaceAnomalyTableSO>();
            table.SetCells(Build().ToArray());
            AssetDatabase.CreateAsset(table, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[이상현상 표] 만들었습니다: " + AssetPath + " (" + table.Cells.Count + "칸)");
        }

        private static List<SpaceAnomalyTableSO.Cell> Build()
        {
            List<SpaceAnomalyTableSO.Cell> list = new List<SpaceAnomalyTableSO.Cell>();

            // ── 복도 (등 8개) ──
            SpaceId s = SpaceId.Corridor;
            Add(list, s, FearAxis.Auditory, Band.Band0, "실제 문·발소리와 기본 환경음", "amb.base");
            Add(list, s, FearAxis.Auditory, Band.Band1, "앞쪽 지정 문 닫힘 1회", "door.close.front");
            Add(list, s, FearAxis.Auditory, Band.Band2, "앞쪽 닫힘 뒤 지나온 문 닫힘 1회", "door.close.front", "door.close.back");
            Add(list, s, FearAxis.Auditory, Band.Band3, "뒤쪽 닫힘 앞에 손잡이 놓는 소리 추가", "door.close.front", "handle.back", "door.close.back");
            Add(list, s, FearAxis.Auditory, Band.Band4, "뒤쪽 소리 단서 유지, 지정 문 무음 개방 1회", "door.close.front", "handle.back", "door.close.back", "door.open.silent");
            AddLight(list, s, 8, 6, 4, 2);
            Add(list, s, FearAxis.Layout, Band.Band0, "지정 통행에서 문 자동 개방 1회", "door.auto_open");
            Add(list, s, FearAxis.Layout, Band.Band1, "중앙 상자 1개, 벽 쪽 안전 통로 유지", "box.center");
            Add(list, s, FearAxis.Layout, Band.Band2, "상자 상태 유지 (모형은 조우 규약으로 별도)", "box.center");
            Add(list, s, FearAxis.Layout, Band.Band3, "기존 천장 조각 낙하 1회, 낙하 영역은 우회 가능", "box.center", "debris.fall");
            Add(list, s, FearAxis.Layout, Band.Band4, "기존 나무·풀 묶음 추가, 통로는 남김", "box.center", "debris.fall", "tree.grass");

            // ── 교실 1-1·1-3 (등 8개) ──
            s = SpaceId.Classroom_1_1;
            Add(list, s, FearAxis.Auditory, Band.Band0, "1-1 문밖에서 분필 3획, 하루 1회", "chalk.3");
            Add(list, s, FearAxis.Auditory, Band.Band1, "분필 단서 유지, 실내 지우개 털기 소리 1회", "chalk.3", "eraser");
            Add(list, s, FearAxis.Auditory, Band.Band2, "점검 후 퇴실 때 뒷줄 책상 타격음 1회", "desk.hit");
            Add(list, s, FearAxis.Auditory, Band.Band3, "별도 방문의 입구에서 칠판 긁기 + 교탁 의자 마찰음", "board.scratch", "chair.scrape");
            Add(list, s, FearAxis.Auditory, Band.Band4, "칠판 소리 종료 뒤 교탁 마찰음 한 구절 추가", "board.scratch", "chair.scrape", "chair.scrape.phrase");
            AddLight(list, s, 8, 6, 4, 2);
            Add(list, s, FearAxis.Layout, Band.Band0, "정상 책상 방향", "desks.normal");
            Add(list, s, FearAxis.Layout, Band.Band1, "뒤쪽 지정 책상·의자 한 묶음이 출입문을 향함", "desks.normal", "desks.turned.one");
            Add(list, s, FearAxis.Layout, Band.Band2, "회전 좌석 유지", "desks.normal", "desks.turned.one");
            Add(list, s, FearAxis.Layout, Band.Band3, "해당 열의 기존 의자 방향 변경", "desks.normal", "desks.turned.one", "chairs.turned.row");
            Add(list, s, FearAxis.Layout, Band.Band4, "다른 열까지 방향 변화, 책상 수·안전 통로 유지", "desks.normal", "desks.turned.one", "chairs.turned.row", "chairs.turned.others");

            // ── 과학실 (등 4개) ──
            s = SpaceId.ScienceRoom;
            Add(list, s, FearAxis.Auditory, Band.Band0, "유효 점검 후 퇴실할 때 유리 파손음 1회", "glass.break");
            Add(list, s, FearAxis.Auditory, Band.Band1, "지정 유리 기구 접촉음 추가", "glass.break", "glass.touch");
            Add(list, s, FearAxis.Auditory, Band.Band2, "같은 실험대에서 짧은 긁힘 추가", "glass.break", "glass.touch", "glass.scrape");
            Add(list, s, FearAxis.Auditory, Band.Band3, "접촉음 위치를 실험대 아래쪽으로 조정", "glass.break", "glass.touch.under", "glass.scrape.under");
            Add(list, s, FearAxis.Auditory, Band.Band4, "퇴실 파손음 뒤 무거운 물체 놓는 소리 1회", "glass.break", "heavy.drop", "glass.touch.under", "glass.scrape.under");
            AddLight(list, s, 4, 3, 2, 1);
            Add(list, s, FearAxis.Layout, Band.Band0, "기존 실험대·기구 정상 배치", "lab.normal");
            Add(list, s, FearAxis.Layout, Band.Band1, "의자 하나가 책상에서 빠져나와 전시형 인체모형을 향함", "lab.normal", "chair.toward.model");
            Add(list, s, FearAxis.Layout, Band.Band2, "여러 의자가 전시형 인체모형을 향함", "lab.normal", "chair.toward.model", "chairs.toward.model");
            Add(list, s, FearAxis.Layout, Band.Band3, "의자들이 전시형 인체모형을 향한 반원 배치 — 배치축 하이라이트", "lab.normal", "chair.toward.model", "chairs.toward.model", "chairs.semicircle");
            Add(list, s, FearAxis.Layout, Band.Band4, "반원 배치 유지. 반원 바깥 의자 하나만 출입구를 향함 (모형 위치는 S-A·S-B 장면으로 별도)", "lab.normal", "chair.toward.model", "chairs.toward.model", "chairs.semicircle", "chair.toward.exit");

            // ── 화장실 (등 4개) ──
            s = SpaceId.Toilet;
            Add(list, s, FearAxis.Auditory, Band.Band0, "기본 환경음", "amb.base");
            Add(list, s, FearAxis.Auditory, Band.Band1, "안쪽 칸 물 내림 1회", "flush.inner");
            Add(list, s, FearAxis.Auditory, Band.Band2, "별도 방문에서 세면대 쪽 '근무자님' 호칭", "call.worker");
            Add(list, s, FearAxis.Auditory, Band.Band3, "물 내림 끝에 손잡이 소리 추가", "flush.inner", "handle.after_flush");
            Add(list, s, FearAxis.Auditory, Band.Band4, "물 내림 뒤 닫힌 칸 위치에서 들숨 1회 (닫힌 칸 없으면 생략)", "flush.inner", "handle.after_flush", "breath.closed_stall");
            AddLight(list, s, 4, 3, 2, 1);
            Add(list, s, FearAxis.Layout, Band.Band0, "입구 쪽 개폐형 칸 자동 개방 1회", "stalls.base", "stall.entry.auto_open");
            Add(list, s, FearAxis.Layout, Band.Band1, "안쪽 칸이 이미 열린 상태 추가", "stalls.base", "stall.inner.open");
            Add(list, s, FearAxis.Layout, Band.Band2, "일반 칸 상태 유지", "stalls.base", "stall.inner.open");
            Add(list, s, FearAxis.Layout, Band.Band3, "개폐형 점검칸 두 곳 모두 열린 상태 (T1 제외)", "stalls.base", "stall.inner.open", "stall.entry.open");
            Add(list, s, FearAxis.Layout, Band.Band4, "동일", "stalls.base", "stall.inner.open", "stall.entry.open");

            return list;
        }

        private static void AddLight(List<SpaceAnomalyTableSO.Cell> list, SpaceId s, int b0, int b1, int b2, int b3)
        {
            Add(list, s, FearAxis.Illuminance, Band.Band0, "등 " + b0 + "개, 6500K 백색");
            Add(list, s, FearAxis.Illuminance, Band.Band1, "등 " + b1 + "개, 4500K 옅은 노랑");
            Add(list, s, FearAxis.Illuminance, Band.Band2, "등 " + b2 + "개, 3200K 주황");
            Add(list, s, FearAxis.Illuminance, Band.Band3, "등 " + b3 + "개, 2000K 적갈");
            Add(list, s, FearAxis.Illuminance, Band.Band4, "등 0개, 붉은 잔광");
        }

        private static void Add(List<SpaceAnomalyTableSO.Cell> list, SpaceId space, FearAxis axis, Band band, string label, params string[] cues)
        {
            list.Add(new SpaceAnomalyTableSO.Cell
            {
                Space = space,
                Axis = axis,
                Band = band,
                Label = label,
                Cues = cues ?? new string[0]
            });
        }
    }
}
