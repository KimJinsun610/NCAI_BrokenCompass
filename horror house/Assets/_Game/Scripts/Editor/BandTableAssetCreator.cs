using System;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 조도 축 밴드 표(<see cref="BandTableSO"/>) 에셋을 메뉴 한 번으로 만들고 관리하는 에디터 도구입니다.
    /// </summary>
    /// <remarks>
    /// 이 도구가 존재하는 이유:
    /// 밴드 표는 Project 창 우클릭으로 만든 뒤 5행짜리 표와 공간 5개의 등 개수를 손으로 채워야 합니다.
    /// 팀원마다 다른 값을 넣으면 같은 밴드에서 서로 다른 화면을 보게 되고, 그 상태에서 모은 밸런싱
    /// 피드백은 아무 의미가 없습니다. 그래서 "정해진 경로"에 "기획 확정값"이 채워진 에셋을 만드는
    /// 경로를 하나로 고정했습니다. 값을 되돌릴 때도 같은 확정값으로만 되돌아갑니다.
    /// </remarks>
    public static class BandTableAssetCreator
    {
        /// <summary>밴드 표 에셋이 놓여야 하는 고정 경로입니다. 팀 전체가 이 경로 하나만 사용합니다.</summary>
        public static string DefaultAssetPath
        {
            get { return "Assets/_Game/ScriptableObjects/BandTable.asset"; }
        }

        /// <summary>에셋을 담을 폴더 경로입니다.</summary>
        private const string TargetFolder = "Assets/_Game/ScriptableObjects";

        /// <summary><see cref="AssetDatabase.FindAssets(string)"/>에 넘길 타입 필터입니다.</summary>
        private const string TypeFilter = "t:BandTableSO";

        /// <summary>다이얼로그 제목에 공통으로 쓰는 도구 이름입니다.</summary>
        private const string DialogTitle = "조도 표";

        /// <summary>
        /// 프로젝트 안에서 이미 만들어져 있는 밴드 표 에셋을 찾습니다.
        /// 고정 경로의 에셋을 우선 반환하고, 없으면 프로젝트 전체에서 처음 발견된 것을 반환합니다.
        /// </summary>
        /// <returns>찾은 에셋. 하나도 없으면 <c>null</c>.</returns>
        public static BandTableSO FindExisting()
        {
            BandTableSO atDefault = AssetDatabase.LoadAssetAtPath<BandTableSO>(DefaultAssetPath);
            if (atDefault != null)
            {
                return atDefault;
            }

            string[] guids = FindAllGuids();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                BandTableSO found = AssetDatabase.LoadAssetAtPath<BandTableSO>(path);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// 프로젝트 안의 모든 밴드 표 에셋 GUID를 반환합니다. 실패해도 예외 대신 빈 배열을 돌려줍니다.
        /// </summary>
        private static string[] FindAllGuids()
        {
            try
            {
                string[] guids = AssetDatabase.FindAssets(TypeFilter);
                return guids != null ? guids : new string[0];
            }
            catch (Exception e)
            {
                Debug.LogWarning("[조도 표] 에셋 검색에 실패했습니다: " + e.Message);
                return new string[0];
            }
        }

        // ------------------------------------------------------------------
        // 메뉴 1 — 생성
        // ------------------------------------------------------------------

        /// <summary>
        /// 고정 경로에 기획 확정값이 채워진 밴드 표 에셋을 만듭니다.
        /// 이미 있으면 덮어쓰지 않고 확인을 받습니다.
        /// </summary>
        [MenuItem("NightDuty/조도 표 에셋 생성", false, 100)]
        private static void CreateBandTableAsset()
        {
            BandTableSO existing = AssetDatabase.LoadAssetAtPath<BandTableSO>(DefaultAssetPath);
            if (existing != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    DialogTitle + " — 이미 존재함",
                    "다음 경로에 밴드 표 에셋이 이미 있습니다.\n\n" + DefaultAssetPath +
                    "\n\n덮어쓰면 지금 들어 있는 값이 모두 기획 확정값으로 바뀝니다. 덮어쓸까요?",
                    "덮어쓰기",
                    "취소");

                if (!overwrite)
                {
                    Selection.activeObject = existing;
                    EditorGUIUtility.PingObject(existing);
                    Debug.Log("[조도 표] 생성을 취소했습니다. 기존 에셋: " + DefaultAssetPath);
                    return;
                }

                // 덮어쓰기를 택한 경우, 새 에셋을 만드는 대신 기존 에셋의 값만 확정값으로 되돌립니다.
                // (에셋을 지웠다 다시 만들면 GUID가 바뀌어 기존 참조가 전부 끊어집니다.)
                ResetOne(existing, false);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!EnsureFolders())
            {
                return;
            }

            BandTableSO asset = ScriptableObject.CreateInstance<BandTableSO>();
            if (asset == null)
            {
                Debug.LogWarning("[조도 표] BandTableSO 인스턴스를 만들지 못했습니다. 스크립트 컴파일 상태를 확인하십시오.");
                return;
            }

            try
            {
                asset.ResetToDesignDefaults();
                AssetDatabase.CreateAsset(asset, DefaultAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[조도 표] 에셋 생성에 실패했습니다: " + e.Message);
                return;
            }

            BandTableSO created = AssetDatabase.LoadAssetAtPath<BandTableSO>(DefaultAssetPath);
            if (created == null)
            {
                Debug.LogWarning("[조도 표] 에셋을 만들었지만 다시 불러오지 못했습니다: " + DefaultAssetPath);
                return;
            }

            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
            Debug.Log("[조도 표] 기획 확정값으로 에셋을 만들었습니다: " + DefaultAssetPath, created);
        }

        /// <summary>
        /// 대상 폴더 계층을 한 단계씩 확인하며 만듭니다.
        /// <see cref="AssetDatabase.CreateFolder(string, string)"/>는 한 번에 한 단계만 만들 수 있습니다.
        /// </summary>
        /// <returns>폴더가 준비되면 <c>true</c>.</returns>
        private static bool EnsureFolders()
        {
            try
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Game"))
                {
                    AssetDatabase.CreateFolder("Assets", "_Game");
                }

                if (!AssetDatabase.IsValidFolder("Assets/_Game"))
                {
                    Debug.LogWarning("[조도 표] 폴더를 만들지 못했습니다: Assets/_Game");
                    return false;
                }

                if (!AssetDatabase.IsValidFolder(TargetFolder))
                {
                    AssetDatabase.CreateFolder("Assets/_Game", "ScriptableObjects");
                }

                if (!AssetDatabase.IsValidFolder(TargetFolder))
                {
                    Debug.LogWarning("[조도 표] 폴더를 만들지 못했습니다: " + TargetFolder);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[조도 표] 폴더 생성 중 오류가 났습니다: " + e.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------
        // 메뉴 2 — 기획값으로 되돌리기
        // ------------------------------------------------------------------

        /// <summary>
        /// 프로젝트 안의 모든 밴드 표 에셋을 하나씩 확인받아 기획 확정값으로 되돌립니다.
        /// 되돌리기 전에 <see cref="Undo.RecordObject"/>를 호출하므로 Ctrl+Z로 복구할 수 있습니다.
        /// </summary>
        [MenuItem("NightDuty/조도 표 기획값으로 되돌리기", false, 101)]
        private static void ResetBandTablesToDesignDefaults()
        {
            string[] guids = FindAllGuids();
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle + " — 에셋 없음",
                    "프로젝트에서 밴드 표 에셋을 찾지 못했습니다.\n\n먼저 메뉴 [NightDuty > 조도 표 에셋 생성]으로 조도 표 에셋을 생성하십시오.",
                    "확인");
                return;
            }

            int resetCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                BandTableSO table = AssetDatabase.LoadAssetAtPath<BandTableSO>(path);
                if (table == null)
                {
                    continue;
                }

                bool confirmed = EditorUtility.DisplayDialog(
                    DialogTitle + " — 기획값으로 되돌리기",
                    "다음 에셋의 값을 기획 확정값으로 되돌립니다.\n\n" + path +
                    "\n\n지금 들어 있는 값은 사라집니다. (Ctrl+Z로 되돌릴 수 있습니다.)",
                    "되돌리기",
                    "건너뛰기");

                if (!confirmed)
                {
                    continue;
                }

                if (ResetOne(table, true))
                {
                    resetCount++;
                }
            }

            if (resetCount > 0)
            {
                Debug.Log("[조도 표] 에셋 " + resetCount + "개를 기획 확정값으로 되돌렸습니다.");
            }
            else
            {
                Debug.Log("[조도 표] 되돌린 에셋이 없습니다.");
            }
        }

        /// <summary>
        /// 에셋 하나를 기획 확정값으로 되돌리고 저장합니다.
        /// </summary>
        /// <param name="table">되돌릴 에셋.</param>
        /// <param name="quiet">true면 개별 로그를 남기지 않습니다(호출 측에서 합계를 남길 때).</param>
        /// <returns>성공하면 <c>true</c>.</returns>
        private static bool ResetOne(BandTableSO table, bool quiet)
        {
            if (table == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(table);

            try
            {
                Undo.RecordObject(table, "조도 표 기획값으로 되돌리기");
                table.ResetToDesignDefaults();
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[조도 표] 되돌리기에 실패했습니다 (" + path + "): " + e.Message);
                return false;
            }

            if (!quiet)
            {
                Debug.Log("[조도 표] 기획 확정값으로 되돌렸습니다: " + path, table);
            }

            return true;
        }

        // ------------------------------------------------------------------
        // 메뉴 3 — 선택
        // ------------------------------------------------------------------

        /// <summary>
        /// 기존 밴드 표 에셋을 Project 창에서 선택하고 표시합니다.
        /// </summary>
        [MenuItem("NightDuty/조도 표 선택", false, 102)]
        private static void SelectBandTableAsset()
        {
            BandTableSO table = FindExisting();
            if (table == null)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle + " — 에셋 없음",
                    "프로젝트에서 밴드 표 에셋을 찾지 못했습니다.\n\n먼저 메뉴 [NightDuty > 조도 표 에셋 생성]으로 조도 표 에셋을 생성하십시오.",
                    "확인");
                return;
            }

            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);
            Debug.Log("[조도 표] 에셋을 선택했습니다: " + AssetDatabase.GetAssetPath(table), table);
        }
    }
}
