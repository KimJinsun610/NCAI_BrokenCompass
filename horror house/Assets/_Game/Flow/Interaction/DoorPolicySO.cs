using System;
using UnityEngine;

/// <summary>
/// 어느 문이 열려도 되는가. <see cref="PlayerInteractor"/>가 조준한 대상을 여기에 물어본다.
///
/// <para><b>왜 표로 두는가.</b> 씬의 문 56개는 대부분 벤더 레벨 에셋이 딸려 온 것이고, 기획이 쓰는 문은
/// 그중 일부입니다. 전부 열리면 플레이어가 기획에 없는 방으로 새고, 전부 잠그면 순찰이 막힙니다.
/// 어느 쪽인지는 <b>기획 판단</b>이라 코드에 박지 않고 에셋으로 뺐습니다 — 민이 인스펙터에서 고칩니다.</para>
///
/// <para><b>정본은 결국 씬입니다.</b> 문마다 <c>keySystem</c>이 있으니, 씬에서 그 값을 제대로 채우면
/// 이 표는 필요 없어집니다. 지금은 씬 작업 전에 걸어서 시험할 수 있게 하는 <b>덮개</b>입니다.</para>
/// </summary>
[CreateAssetMenu(fileName = "DoorPolicy", menuName = "NightDuty/문 개폐 정책")]
public sealed class DoorPolicySO : ScriptableObject
{
    /// <summary>대상이 무엇으로 분류됐는가.</summary>
    public enum Kind
    {
        /// <summary>서랍·사물함·책장. 기획과 무관하게 여닫아도 된다.</summary>
        Storage,

        /// <summary>기획이 쓰는 문. 열린다.</summary>
        Openable,

        /// <summary>기획에 없는 문. 잠겨 있다.</summary>
        Sealed
    }

    [Header("수납가구 — 여기 해당하면 기획과 무관하게 여닫는다")]
    [Tooltip("오브젝트 이름이 이 중 하나로 시작하면 수납가구로 본다. ToiletCabin은 점검칸이라 일부러 뺐다.")]
    [SerializeField]
    private string[] storagePrefixes =
    {
        "Locker", "Bookcase", "Drawer", "TeacherTable", "Cabinet", "Shelf", "Rostrum", "Tribune", "Wardrobe"
    };

    [Tooltip("수납가구를 여닫을 수 있게 할지. 끄면 서랍도 안 열린다.")]
    [SerializeField] private bool storageIsOpenable = true;

    [Header("열리는 문")]
    [Tooltip("씬 하이어라키 경로. 이름만으로는 DoorBack이 셋이라 구분이 안 되므로 경로로 적는다.")]
    [SerializeField] private string[] openablePaths = new string[0];

    [Tooltip("판정 ID(JudgeTarget). 경로가 바뀌어도 ID는 남으므로 이쪽이 더 질깁니다.")]
    [SerializeField] private string[] openableIds = new string[0];

    [Tooltip("JudgeTarget이 붙은 문은 표에 없어도 열리게 할지. 판정에 쓰이는 문은 당연히 열려야 한다.")]
    [SerializeField] private bool judgeTargetsAreOpenable = true;

    [Header("표에 없는 문")]
    [Tooltip("켜면 표에 없는 문도 전부 열린다. 씬을 둘러볼 때만 쓰십시오(하네스 F5).")]
    [SerializeField] private bool openEverything;

    /// <summary>표를 무시하고 전부 연다. 하네스가 F5로 켜고 끈다. 저장되지 않는 런타임 값이다.</summary>
    public static bool OpenEverythingOverride { get; set; }

    /// <summary>표에 적힌 「열리는 문」 경로. 상태판이 보여 준다.</summary>
    public string[] OpenablePaths
    {
        get { return openablePaths; }
    }

    /// <summary>이 대상이 무엇인가.</summary>
    public Kind Classify(string objectName, string hierarchyPath, string judgeId)
    {
        if (IsStorage(objectName))
        {
            return storageIsOpenable ? Kind.Storage : Kind.Sealed;
        }

        if (openEverything || OpenEverythingOverride)
        {
            return Kind.Openable;
        }

        if (judgeTargetsAreOpenable && !string.IsNullOrEmpty(judgeId))
        {
            return Kind.Openable;
        }

        if (Contains(openableIds, judgeId) || Contains(openablePaths, hierarchyPath))
        {
            return Kind.Openable;
        }

        return Kind.Sealed;
    }

    private bool IsStorage(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || storagePrefixes == null)
        {
            return false;
        }

        for (int i = 0; i < storagePrefixes.Length; i++)
        {
            string prefix = storagePrefixes[i];
            if (!string.IsNullOrEmpty(prefix) && objectName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string[] set, string value)
    {
        if (set == null || string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < set.Length; i++)
        {
            if (string.Equals(set[i], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // ─────────────────────────────── 불러오기 ───────────────────────────────

    private static DoorPolicySO s_loaded;
    private static bool s_looked;

    /// <summary>
    /// <c>Resources/DoorPolicy</c>를 읽는다. 없으면 <b>기본값 하나를 메모리에 만들어</b> 돌려준다 —
    /// 표가 없다고 문이 통째로 잠겨 시험이 막히면 안 되므로, 그때는 판정 대상과 수납가구만 열립니다.
    /// </summary>
    public static DoorPolicySO Load()
    {
        if (s_looked)
        {
            return s_loaded;
        }

        s_looked = true;
        s_loaded = Resources.Load<DoorPolicySO>("DoorPolicy");

        if (s_loaded == null)
        {
            Debug.LogWarning("[문 정책] Resources/DoorPolicy.asset이 없습니다. " +
                             "판정 대상 문과 수납가구만 열립니다(메뉴 「NightDuty/문 개폐 정책 에셋 생성」).");
            s_loaded = CreateInstance<DoorPolicySO>();
            s_loaded.hideFlags = HideFlags.HideAndDontSave;
        }

        return s_loaded;
    }
}
