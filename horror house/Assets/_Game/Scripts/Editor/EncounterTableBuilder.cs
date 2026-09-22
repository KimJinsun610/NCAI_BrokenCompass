using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 조우 8장면 + 재방문 문자(N1)로 <see cref="EncounterTableSO"/> 에셋을 만든다.
    /// <para>
    /// <b>문구는 여기서 지어내지 않는다.</b> 유도 문자 G 7통과 재방문 문자 N1의 원문은
    /// <b>기획서 10-3절이 정본</b>이며, 그 원문이 아직 이 저장소에 없다. 그래서 본문 칸에는
    /// <c>"(G-H1 문구 미정 — 기획서 10-3절)"</c> 꼴의 <b>자리표시자</b>만 넣는다.
    /// 자리표시자를 넣는 이유는 <see cref="EncounterTableSO.Validate"/>가 「문자 ID가 있으면 본문도 있어야 한다」를
    /// 검사하기 때문이고, 빈 칸으로 두면 검사 결과가 통째로 빨개져서 <b>진짜 데이터 오류가 묻힌다</b>.
    /// 기획서 원문이 들어오면 인스펙터에서 이 자리표시자만 갈아 끼우면 된다.
    /// </para>
    /// <para>
    /// <b>이미 에셋이 있으면 건드리지 않는다</b>(<see cref="CorridorCardBuilder"/>·<see cref="SpaceAnomalyTableBuilder"/>와 같은 규칙).
    /// 기획팀이 인스펙터에서 고친 문구를 메뉴 한 번으로 날리지 않기 위해서다.
    /// 초기값으로 되돌리려면 에셋을 지우고 메뉴를 다시 실행한다.
    /// 확인 대화상자를 띄우지 않으므로 Unity CLI/MCP로도 실행할 수 있다.
    /// </para>
    /// <para>
    /// <b>이 표는 좌표를 갖지 않는다.</b> 접근 3지점과 퇴실 게이트 지점은 전부 씬의 <see cref="JudgeTarget"/> ID 문자열이고,
    /// 실제 위치는 씬 쪽이 그 ID를 단 빈 오브젝트로 잡는다(<see cref="EncounterTableSO"/> 주석 참조).
    /// 그래서 이 빌더의 마지막 일은 <b>씬에 만들어야 할 대상 ID 전체를 콘솔에 찍는 것</b>이다 — 그 목록이 곧 다음 씬 작업의 입력이다.
    /// </para>
    /// <para>
    /// 대상 ID는 연결 약속 문서의 규칙(&lt;장면 ID&gt;.&lt;구분&gt;)을 따라 장면 ID에서 기계적으로 만든다:
    /// 접근 지점은 <c>scene.ha.1</c>·<c>scene.ha.2</c>·<c>scene.ha.3</c>, 퇴실 게이트 지점은 <c>scene.hb.gate</c>.
    /// 손으로 24+3개를 적지 않는 이유는 오타 하나가 「모형을 놓을 자리가 없는 밤」으로 조용히 이어지기 때문이다.
    /// </para>
    /// </summary>
    public static class EncounterTableBuilder
    {
        /// <summary>
        /// 에셋 경로. <see cref="EncounterDirector"/>가 <c>Resources.Load</c>로
        /// <see cref="EncounterTableSO.ResourcePath"/>(= <c>"EncounterTable"</c>)를 찾으므로
        /// <b>반드시 Resources 폴더 바로 아래·이 파일 이름</b>이어야 한다. 편성표(NightDeckTable)와 같은 폴더다.
        /// </summary>
        public const string AssetPath = ResourceFolder + "/EncounterTable.asset";

        /// <summary>Resources 폴더. 없으면 만든다.</summary>
        public const string ResourceFolder = "Assets/_Game/Resources";

        /// <summary>
        /// 재방문 문자(N1)가 부르는 공간. <b>목적지가 그날 정해지므로 표에 고정 공간을 적지 않는다</b>는 뜻으로 <see cref="SpaceId.None"/>을 쓴다.
        /// <para>
        /// <b>주의 — 알고 넣는 불일치다.</b> 현재 <see cref="EncounterTableSO.Validate"/>는 재방문 문자 공간이
        /// <see cref="SpaceId.None"/>이면 오류로 잡는다("재방문 문자가 부를 공간이 없습니다"). 즉 생성 직후 검사에
        /// 이 한 줄이 반드시 뜬다. 데이터 쪽 지시가 <see cref="SpaceId.None"/>이고 이 빌더는 판정 코어를 고치지 않는 도구라,
        /// <b>지시대로 넣고 검사 결과로 드러나게</b> 두었다. 둘 중 하나를 정해야 한다 —
        /// (a) 표에 고정 공간(예: <see cref="SpaceId.Corridor"/>)을 적거나, (b) <see cref="EncounterTableSO"/> 쪽 검사를 완화하거나.
        /// 어느 쪽이든 <b>이 빌더가 아니라 그 결정이 난 뒤에</b> 고친다.
        /// </para>
        /// </summary>
        private const SpaceId NoticeSpace = SpaceId.None;

        /// <summary>콘솔 로그 머리말. 다른 빌더처럼 한 눈에 걸러 읽을 수 있게 붙인다.</summary>
        private const string LogTag = "[조우 표] ";

        /// <summary>퇴실 게이트 지점 ID의 꼬리. 장면 ID + 이것이 곧 게이트 대상 ID다.</summary>
        private const string GateSuffix = ".gate";

        /// <summary>
        /// 8장면의 표 기준 정의.
        /// <para>
        /// 발견형·퇴실 게이트는 <b>여기 적힌 값을 쓰지 않고</b> <see cref="EncounterDirector"/>에서 읽어 채운다.
        /// 이 칸들은 <see cref="CrossCheck"/>가 코드와 <b>대조</b>하는 용도다 — 표와 코드가 어긋난 채로 에셋이 생기면
        /// 그 오류는 에셋을 다시 만들 때까지 남는데, 대조를 여기서 하면 만들기 전에 콘솔에서 잡힌다.
        /// </para>
        /// </summary>
        private static readonly SceneDef[] Defs =
        {
            // 복도 A. 평범한 장면 — 발견형도 아니고 게이트도 없다.
            new SceneDef { SceneId = EncounterDirector.SceneHA, Space = SpaceId.Corridor, MessageId = "G-H1", IsDiscovery = false, UsesExitGate = false },

            // 복도 B. 발견형이자 퇴실 게이트 장면(둘 다인 유일한 장면).
            new SceneDef { SceneId = EncounterDirector.SceneHB, Space = SpaceId.Corridor, MessageId = "G-H2", IsDiscovery = true, UsesExitGate = true },

            // 교실 1-1 A.
            new SceneDef { SceneId = EncounterDirector.SceneCA, Space = SpaceId.Classroom_1_1, MessageId = "G-C1", IsDiscovery = false, UsesExitGate = false },

            // 교실 1-1 B. 발견형.
            new SceneDef { SceneId = EncounterDirector.SceneCB, Space = SpaceId.Classroom_1_1, MessageId = "G-C2", IsDiscovery = true, UsesExitGate = false },

            // 과학실 A. 1일차 고정·최초 조우라 유도할 필요가 없다 —
            // 유도 문자를 갖지 않는 유일한 장면이고(EncounterDirector.FirstDaySceneId), 그래서 MessageId가 빈 문자열이다.
            new SceneDef { SceneId = EncounterDirector.SceneSA, Space = SpaceId.ScienceRoom, MessageId = "", IsDiscovery = false, UsesExitGate = false },

            // 과학실 B. 퇴실 게이트 장면. 카드 S5의 트리거 ID가 이 장면 ID(scene.sb)다 — 2026-09-21 실측 기준선.
            new SceneDef { SceneId = EncounterDirector.SceneSB, Space = SpaceId.ScienceRoom, MessageId = "G-S1", IsDiscovery = false, UsesExitGate = true },

            // 화장실 A. 퇴실 게이트 장면. 카드 T3의 트리거 ID가 이 장면 ID(scene.ta)다 — 2026-09-21 실측 기준선.
            new SceneDef { SceneId = EncounterDirector.SceneTA, Space = SpaceId.Toilet, MessageId = "G-T1", IsDiscovery = false, UsesExitGate = true },

            // 화장실 B. 발견형.
            new SceneDef { SceneId = EncounterDirector.SceneTB, Space = SpaceId.Toilet, MessageId = "G-T2", IsDiscovery = true, UsesExitGate = false }
        };

        /// <summary>메뉴: 조우 표 에셋 생성. 이미 있으면 만들지 않고 로그만 남긴다.</summary>
        [MenuItem("NightDuty/조우 표 에셋 생성", false, 126)]
        public static void BuildMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<EncounterTableSO>(AssetPath) != null)
            {
                // 덮어쓰지 않는다. 기획팀이 넣은 문구·조정한 지점 ID를 날리지 않기 위해서다.
                Debug.Log(LogTag + "이미 있습니다: " + AssetPath + " (덮어쓰지 않았습니다. 초기값으로 되돌리려면 에셋을 지우고 다시 실행하십시오.)");
                return;
            }

            EnsureFolder(ResourceFolder);

            EncounterTableSO table = ScriptableObject.CreateInstance<EncounterTableSO>();
            table.SetScenes(BuildScenes());
            table.SetNotice(EncounterDirector.NoticeMessageId, Placeholder(EncounterDirector.NoticeMessageId), NoticeSpace);

            AssetDatabase.CreateAsset(table, AssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log(LogTag + "만들었습니다: " + AssetPath + " (" + table.Scenes.Count + "장면 + 재방문 문자 " + table.NoticeMessageId + ")");

            // 만든 직후에 검사한다. 에셋이 잘못 생겼다는 사실을 밤이 시작될 때가 아니라 지금 알아야 한다.
            ReportValidation(table);

            // 그 다음 일이 씬 작업이므로, 씬에 만들어야 할 대상 ID를 여기서 바로 뽑아 준다.
            ReportReferences(table);
        }

        /// <summary>8장면을 만든다. 발견형·게이트 여부는 <see cref="EncounterDirector"/>가 정본이다.</summary>
        private static EncounterTableSO.Scene[] BuildScenes()
        {
            EncounterTableSO.Scene[] scenes = new EncounterTableSO.Scene[Defs.Length];

            for (int i = 0; i < Defs.Length; i++)
            {
                SceneDef d = Defs[i];

                // 표와 코드가 어긋나면 여기서 먼저 소리를 낸다. 만들기는 계속한다 —
                // 코드 값으로 채우므로 에셋 자체는 검사를 통과하고, 고칠 곳(이 파일의 표)만 알려 주면 되기 때문이다.
                CrossCheck(d);

                EncounterTableSO.Scene s = new EncounterTableSO.Scene();
                s.SceneId = d.SceneId;
                s.Space = d.Space;

                // 발견형 표시는 EncounterDirector.DiscoverySceneIds가 정본이다.
                // 표를 그대로 믿지 않는 이유: 이 값이 어긋나면 Validate가 잡아 주기는 해도,
                // 애초에 코드에서 읽으면 어긋날 일이 없다.
                s.IsDiscovery = EncounterDirector.IsDiscovery(d.SceneId);

                s.MessageId = d.MessageId ?? string.Empty;

                // 문자가 없는 장면(S-A)은 본문도 비워야 한다. 자리표시자를 넣으면
                // 「1일차 고정 장면은 유도 문자를 가질 수 없다」는 규칙과 부딪힌다.
                s.MessageText = string.IsNullOrEmpty(s.MessageId) ? string.Empty : Placeholder(s.MessageId);

                s.ApproachTargetIds = ApproachIdsOf(d.SceneId);

                // 퇴실 게이트도 EncounterDirector.ExitGateSceneIds가 정본이다.
                // 전부에 붙이면 「안 보고 나가면 문 앞에 선다」가 조작법으로 읽힌다는 것이 2026-09-21 결정의 이유라,
                // 데이터가 조용히 넷째를 늘리지 못하게 코드에서 읽는다.
                s.ExitGateTargetId = EncounterDirector.UsesExitGate(d.SceneId) ? d.SceneId + GateSuffix : string.Empty;

                scenes[i] = s;
            }

            return scenes;
        }

        /// <summary>
        /// 장면의 접근 3지점 ID. <c>scene.ha</c> → <c>scene.ha.1</c>·<c>scene.ha.2</c>·<c>scene.ha.3</c>.
        /// <para>
        /// 순서가 곧 접근 순서다(1=원래 장면 지점, 2=동선 3m 이내, 3=동선 위·문 옆).
        /// 개수는 <see cref="EncounterTableSO.ApproachCount"/>에서 읽는다 — 단계 수가 바뀌면 이 빌더가 따라가야지,
        /// 3이라고 적어 두면 표만 조용히 옛날 개수로 남는다.
        /// </para>
        /// </summary>
        private static string[] ApproachIdsOf(string sceneId)
        {
            string[] ids = new string[EncounterTableSO.ApproachCount];

            for (int k = 0; k < ids.Length; k++)
            {
                ids[k] = sceneId + "." + (k + 1);
            }

            return ids;
        }

        /// <summary>
        /// 문자 본문 자리표시자. 원문은 기획서 10-3절이 정본이며 여기서 지어내지 않는다.
        /// 사람이 인스펙터에서 보자마자 「아직 안 들어왔다」를 알아보게 괄호로 감싼다.
        /// </summary>
        private static string Placeholder(string messageId)
        {
            return "(" + messageId + " 문구 미정 — 기획서 10-3절)";
        }

        /// <summary>이 파일의 표가 <see cref="EncounterDirector"/>와 어긋나면 콘솔에 오류를 남긴다.</summary>
        private static void CrossCheck(SceneDef d)
        {
            if (!EncounterDirector.IsSceneId(d.SceneId))
            {
                Debug.LogError(LogTag + d.SceneId + "은(는) 조우 장면 ID가 아닙니다(EncounterDirector.AllSceneIds와 대조).");
                return;
            }

            bool codeDiscovery = EncounterDirector.IsDiscovery(d.SceneId);
            if (d.IsDiscovery != codeDiscovery)
            {
                Debug.LogError(LogTag + d.SceneId + "의 발견형 표시가 코드와 다릅니다. 표=" + d.IsDiscovery +
                               " / EncounterDirector=" + codeDiscovery + " (발견형은 " + EncounterDirector.DiscoverySceneList() + " 셋입니다).");
            }

            bool codeGate = EncounterDirector.UsesExitGate(d.SceneId);
            if (d.UsesExitGate != codeGate)
            {
                Debug.LogError(LogTag + d.SceneId + "의 퇴실 게이트 표시가 코드와 다릅니다. 표=" + d.UsesExitGate +
                               " / EncounterDirector=" + codeGate + " (게이트는 " + EncounterDirector.ExitGateSceneList() + " 셋뿐입니다).");
            }

            // 유도 문자가 없어야 하는 장면은 1일차 고정 장면(S-A) 하나뿐이다.
            bool wantsMessage = d.SceneId != EncounterDirector.FirstDaySceneId;
            bool hasMessage = !string.IsNullOrEmpty(d.MessageId);
            if (wantsMessage != hasMessage)
            {
                Debug.LogError(LogTag + d.SceneId + "의 유도 문자 유무가 설계와 다릅니다. 문자가 없는 장면은 " +
                               EncounterDirector.FirstDaySceneId + "뿐입니다.");
            }
        }

        /// <summary>
        /// 만든 표를 <see cref="EncounterTableSO.Validate"/>로 검사하고, 문제가 있으면 <b>한 줄씩 전부</b> 오류로 찍는다.
        /// 한 덩어리로 합치지 않는 이유는 콘솔에서 줄을 눌러 골라 볼 수 있게 하기 위해서다.
        /// </summary>
        private static void ReportValidation(EncounterTableSO table)
        {
            List<string> errors = new List<string>();
            table.Validate(errors);

            if (errors.Count == 0)
            {
                Debug.Log(LogTag + "검사 통과 — 데이터 규칙 위반 없음.");
                return;
            }

            Debug.LogError(LogTag + "검사 문제 " + errors.Count + "건 — 아래에 한 줄씩 찍습니다.");
            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError(LogTag + errors[i], table);
            }
        }

        /// <summary>
        /// 표가 씬에 요구하는 대상 ID 전부를 콘솔에 찍는다.
        /// <b>이 목록이 곧 씬에 만들어야 할 <see cref="JudgeTarget"/> 목록</b>이고, 다음 씬 작업의 입력이다.
        /// 그대로 복사해 쓸 수 있게 한 줄에 하나씩 낸다.
        /// </summary>
        private static void ReportReferences(EncounterTableSO table)
        {
            List<string> ids = new List<string>();
            table.CollectReferences(ids);

            if (ids.Count == 0)
            {
                Debug.LogWarning(LogTag + "씬에 요구하는 대상 ID가 하나도 없습니다 — 표가 비었는지 확인하십시오.");
                return;
            }

            string body = string.Join("\n", ids.ToArray());
            Debug.Log(LogTag + "씬에 만들어야 할 JudgeTarget " + ids.Count + "개 (접근 지점 + 퇴실 게이트 지점):\n" + body, table);
        }

        /// <summary>
        /// 폴더가 없으면 위에서부터 만든다. <see cref="CorridorCardBuilder"/>와 같은 구현이다 —
        /// <see cref="AssetDatabase.CreateAsset"/>는 폴더가 없으면 조용히 실패한다.
        /// </summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>표 한 줄. 에셋에 직접 들어가는 형이 아니라 <b>이 빌더의 입력</b>이다.</summary>
        private sealed class SceneDef
        {
            /// <summary>장면 ID. EncounterDirector의 상수를 그대로 쓴다 — 문자열을 손으로 적으면 오타가 표에 그대로 남는다.</summary>
            public string SceneId;

            /// <summary>장면이 벌어지는 공간.</summary>
            public SpaceId Space;

            /// <summary>유도 문자 ID(G-H1 등). 빈 문자열이면 문자가 없다 — S-A뿐이다.</summary>
            public string MessageId;

            /// <summary>발견형인가(표 기준). 실제 값은 EncounterDirector에서 읽고, 이 칸은 대조에만 쓴다.</summary>
            public bool IsDiscovery;

            /// <summary>퇴실 게이트 장면인가(표 기준). 실제 값은 EncounterDirector에서 읽고, 이 칸은 대조에만 쓴다.</summary>
            public bool UsesExitGate;
        }
    }
}
