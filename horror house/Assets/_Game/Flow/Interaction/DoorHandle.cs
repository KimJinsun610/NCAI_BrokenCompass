using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 문 하나를 다루는 손잡이. 문이 어떤 구현이든 <b>플레이어 상호작용이 알아야 할 것만</b> 추려 보여 준다 —
/// 열려 있나, 잠겼나, 스스로 닫히나, 그리고 열어라·닫아라.
///
/// <para><b>벤더 <c>DoorScript</c>를 타입으로 참조하지 않는다.</b> <c>Assets/NOT_Lonely/</c>는 asmdef가 없어
/// <c>Assembly-CSharp</c>에 들어가고, 폴더 자체가 gitignore 대상이다(CLAUDE.md §5.1-2).
/// 이름으로 참조하면 패키지를 받지 않은 팀원의 빌드가 깨진다. <see cref="DoorRelay"/>가
/// <c>UnityEngine.Animation</c>만 보는 것과 같은 이유이고, 같은 해법이다 — 여기서는 리플렉션으로 벗겨 쓴다.</para>
///
/// <para><b>나중에 <c>_Game</c>에 자체 문 컨트롤러를 만들면 이 파일 하나만 갈아 끼우면 된다.</b>
/// <see cref="PlayerInteractor"/>는 아래 속성들만 알지, 벤더를 모른다.</para>
///
/// <para><b>벤더의 열기 규칙을 그대로 옮겼다</b>(<c>DoorScript.Update</c> 226~247행).
/// 잠금 장치가 꺼져 있으면 <c>OpenDoor</c>, 켜져 있고 열쇠가 있으면 <c>OpenLockDoor</c>,
/// 열쇠가 없으면 <c>PlayClosedFXs</c>(덜컹거리고 안 열림)다. 셋을 섞으면 잠금이 무의미해진다.</para>
/// </summary>
public struct DoorHandle
{
    private const int StyleAutomatic = 1;   // DoorScript.OpenStyle.AUTOMATIC

    private static Type s_type;
    private static bool s_looked;

    private static FieldInfo s_fOpened;
    private static FieldInfo s_fControls;
    private static FieldInfo s_fKeySystem;
    private static FieldInfo s_fOpenMethod;
    private static FieldInfo s_fOpenButton;
    private static FieldInfo s_fAutoClose;
    private static FieldInfo s_fKsEnabled;
    private static FieldInfo s_fKsUnlock;
    private static MethodInfo s_mOpen;
    private static MethodInfo s_mOpenLocked;
    private static MethodInfo s_mClose;
    private static MethodInfo s_mRattle;

    private readonly Component _door;

    private DoorHandle(Component door)
    {
        _door = door;
    }

    /// <summary>이 손잡이가 실제 문을 가리키는가.</summary>
    public bool IsValid
    {
        get { return _door != null && s_mOpen != null; }
    }

    /// <summary>문 오브젝트. 로그와 <see cref="DoorRelay"/> 조회에 쓴다.</summary>
    public Component Owner
    {
        get { return _door; }
    }

    /// <summary>지금 열려 있는가.</summary>
    public bool IsOpen
    {
        get { return ReadBool(s_fOpened, _door); }
    }

    /// <summary>잠금 장치가 걸려 있고 열쇠가 없는가. 이 문은 지금 열 수 없다.</summary>
    public bool IsLocked
    {
        get
        {
            object ks = KeySystem();
            return ks != null && ReadBool(s_fKsEnabled, ks) && !ReadBool(s_fKsUnlock, ks);
        }
    }

    /// <summary>잠금 장치가 걸려 있고 열쇠를 가졌는가. 여는 동작이 <c>OpenDoor</c>가 아니라 <c>OpenLockDoor</c>다.</summary>
    public bool HasKey
    {
        get
        {
            object ks = KeySystem();
            return ks != null && ReadBool(s_fKsEnabled, ks) && ReadBool(s_fKsUnlock, ks);
        }
    }

    /// <summary>플레이어가 멀어지면 스스로 닫히는 문인가. 이런 문에는 「닫기」를 권하지 않는다.</summary>
    public bool ClosesByItself
    {
        get
        {
            object c = Controls();
            return c != null && ReadBool(s_fAutoClose, c);
        }
    }

    /// <summary>다가가면 스스로 열리는 문인가(<c>AUTOMATIC</c>). 상호작용 대상이 아니다.</summary>
    public bool OpensByItself
    {
        get
        {
            object c = Controls();
            if (c == null || s_fOpenMethod == null)
            {
                return false;
            }

            try
            {
                return Convert.ToInt32(s_fOpenMethod.GetValue(c)) == StyleAutomatic;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    // ─────────────────────────────── 찾기 ───────────────────────────────

    /// <summary>이 콜라이더가 속한 문. 문이 아니면 <see cref="IsValid"/>가 false인 손잡이를 돌려준다.</summary>
    public static DoorHandle Of(Component any)
    {
        Type t = DoorType();
        if (t == null || any == null)
        {
            return new DoorHandle(null);
        }

        return new DoorHandle(any.GetComponentInParent(t) as Component);
    }

    /// <summary>이 씬에 벤더 문 구현이 있는가. 없으면 상호작용기는 조용히 쉰다.</summary>
    public static bool ImplementationExists()
    {
        return DoorType() != null;
    }

    /// <summary>씬에 있는 문 전부. 시작할 때 벤더 입력을 거두는 데 쓴다.</summary>
    public static DoorHandle[] All()
    {
        Type t = DoorType();
        if (t == null)
        {
            return new DoorHandle[0];
        }

        UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None);
        DoorHandle[] handles = new DoorHandle[found.Length];
        for (int i = 0; i < found.Length; i++)
        {
            handles[i] = new DoorHandle(found[i] as Component);
        }

        return handles;
    }

    // ─────────────────────────────── 동작 ───────────────────────────────

    /// <summary>
    /// 연다. 잠금 상태에 맞는 벤더 경로를 고른다. <b><see cref="IsLocked"/>일 때는 아무것도 하지 않는다</b> —
    /// 잠긴 문을 흔드는 것은 <see cref="Rattle"/>이다.
    /// </summary>
    public void Open()
    {
        if (!IsValid || IsOpen || IsLocked)
        {
            return;
        }

        Call(HasKey ? s_mOpenLocked : s_mOpen);
    }

    /// <summary>닫는다.</summary>
    public void Close()
    {
        if (!IsValid || !IsOpen)
        {
            return;
        }

        Call(s_mClose);
    }

    /// <summary>잠긴 문을 흔든다. 벤더의 「안 열림」 연출이다. 문은 움직이지 않으므로 판정 신호도 나가지 않는다.</summary>
    public void Rattle()
    {
        if (!IsValid)
        {
            return;
        }

        Call(s_mRattle);
    }

    /// <summary>
    /// 잠금을 푼 것으로 친다. <b>시험용</b>이다 — 열쇠 연출이 생기기 전까지 잠긴 문 12개가 길을 막는다.
    /// 런타임 값만 바꾸므로 씬 파일은 그대로다.
    /// </summary>
    public void ForceUnlock()
    {
        object ks = KeySystem();
        if (ks == null || s_fKsUnlock == null)
        {
            return;
        }

        try
        {
            s_fKsUnlock.SetValue(ks, true);
            s_fKeySystem.SetValue(_door, ks);   // 값 타입이어도 되돌려 넣는다.
        }
        catch (Exception e)
        {
            Debug.LogException(e, _door);
        }
    }

    /// <summary>
    /// 벤더가 <b>자기 키로 스스로 여는 것</b>을 막는다(<c>openButton</c>을 <c>None</c>으로).
    /// 상호작용을 <see cref="PlayerInteractor"/>가 온전히 소유해야 <see cref="DoorRelay.BeginPlayerMove"/>로
    /// 출처를 못 박을 수 있고, 같은 프레임에 두 번 열리는 일도 없다.
    /// <para><c>AUTOMATIC</c> 문과 <c>autoClose</c>는 건드리지 않는다 — 그쪽은 연출이지 조작이 아니다.</para>
    /// </summary>
    public void SilenceVendorInput()
    {
        object c = Controls();
        if (c == null || s_fOpenButton == null || OpensByItself)
        {
            return;
        }

        try
        {
            if ((KeyCode)s_fOpenButton.GetValue(c) != KeyCode.None)
            {
                s_fOpenButton.SetValue(c, KeyCode.None);
                s_fControls.SetValue(_door, c);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e, _door);
        }
    }

    // ─────────────────────────────── 리플렉션 ───────────────────────────────

    private static Type DoorType()
    {
        if (s_looked)
        {
            return s_type;
        }

        s_looked = true;

        Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < all.Length && s_type == null; i++)
        {
            try
            {
                s_type = all[i].GetType("DoorScript", false);
            }
            catch (Exception)
            {
                s_type = null;
            }
        }

        if (s_type == null)
        {
            return null;
        }

        BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        s_fOpened = s_type.GetField("Opened", any);
        s_fControls = s_type.GetField("controls", any);
        s_fKeySystem = s_type.GetField("keySystem", any);
        s_mOpen = s_type.GetMethod("OpenDoor", any);
        s_mOpenLocked = s_type.GetMethod("OpenLockDoor", any);
        s_mClose = s_type.GetMethod("CloseDoor", any);
        s_mRattle = s_type.GetMethod("PlayClosedFXs", any);

        if (s_fControls != null)
        {
            Type c = s_fControls.FieldType;
            s_fOpenMethod = c.GetField("openMethod", any);
            s_fOpenButton = c.GetField("openButton", any);
            s_fAutoClose = c.GetField("autoClose", any);
        }

        if (s_fKeySystem != null)
        {
            Type k = s_fKeySystem.FieldType;
            s_fKsEnabled = k.GetField("enabled", any);
            s_fKsUnlock = k.GetField("isUnlock", any);
        }

        if (s_mOpen == null || s_fOpened == null)
        {
            Debug.LogWarning("[DoorHandle] DoorScript의 모양이 바뀐 듯합니다(OpenDoor·Opened를 못 찾았습니다). " +
                             "문 상호작용을 쉽니다.");
        }

        return s_type;
    }

    private object Controls()
    {
        return Read(s_fControls, _door);
    }

    private object KeySystem()
    {
        return Read(s_fKeySystem, _door);
    }

    private static object Read(FieldInfo field, object target)
    {
        if (field == null || target == null)
        {
            return null;
        }

        try
        {
            return field.GetValue(target);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool ReadBool(FieldInfo field, object target)
    {
        object v = Read(field, target);
        return v is bool && (bool)v;
    }

    private void Call(MethodInfo method)
    {
        if (method == null)
        {
            return;
        }

        try
        {
            method.Invoke(_door, null);
        }
        catch (Exception e)
        {
            Debug.LogException(e, _door);
        }
    }
}
