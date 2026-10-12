using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 불 꺼진 교실(44차, 몹 연출 장면 「교실 조우」 — 등이 꺼진 교실을 손전등으로 본다). 근무 동안 교실 두 곳을
/// <list type="bullet">
/// <item><b>구운 빛을 <see cref="BakedScale"/>배로</b>: 그 방 렌더러가 쓰는 라이트맵을 어둡게 복사해 덧붙이고 방 안 렌더러만 복사본을 보게 한다.
/// 방 안 라이트 프로브도 같은 배수(움직이는 대역·소품도 같이 어둡다).</item>
/// <item><b>천장 형광등을 끈다</b>: 실시간 라이트와 발광(MaterialPropertyBlock). 단 C4 「붉은 불빛」으로 그 방 묶음이 물들어 있는 동안은
/// 등이 붉게 켜진다(<see cref="LightGroup.IsLightTinted"/>) — 어두운 교실에 갑자기 붉은 등이 들어온다.</item>
/// </list>
/// 씬·라이트맵 에셋은 고치지 않는다(런타임 복사본, 끝나면 되돌림). 판정과 무관하다. 근무 씬이면 스스로 선다.
/// 블릿 셰이더 <c>Resources/NightDutyLightmapScale.shader</c>가 없으면 등만 끈다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(160)]
public sealed class RoomDarkness : MonoBehaviour
{
    /// <summary>어둡게 둘 방.</summary>
    public static readonly SpaceId[] DarkRooms = { SpaceId.Classroom_1_1, SpaceId.Classroom_1_3 };

    /// <summary>구운 빛 배수(0.4 — 모양은 보이고 글씨·색은 손전등으로).</summary>
    public const float BakedScale = 0.4f;

    /// <summary>
    /// 86차(민: 「소등 조우 때 소등은 되는데 라이팅은 계속 유지돼」): 소등 조우(<see cref="LightGroup.TurnOff"/> — 과학실·화장실)가 끄는 방.
    /// 실시간 등·발광만 꺼지고 구운 빛(라이트맵·프로브)은 그대로라 방이 밝은 채였다 — 소등 동안 그 방 구운 빛을 <see cref="BlackoutScale"/>배로.
    /// </summary>
    public static readonly SpaceId[] BlackoutRooms = { SpaceId.ScienceRoom, SpaceId.Toilet };

    /// <summary>소등 동안 구운 빛 배수 — 손전등 없이는 윤곽만.</summary>
    public const float BlackoutScale = 0.05f;   // 0.12는 등 바로 아래 벽의 밝은 얼룩이 남았다(밝기 설정을 올리면 더)

    private const string ShaderName = "Hidden/NightDuty/LightmapScale";
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color RedGlow = new Color(1f, 0.12f, 0.08f) * 1.6f;

    private sealed class Blackout
    {
        public SpaceId Space;
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public readonly List<int> Lit = new List<int>();
        public readonly List<int> Dark = new List<int>();
        public readonly List<int> Probes = new List<int>();
        public UnityEngine.Rendering.SphericalHarmonicsL2[] ProbeBase;
        public bool On;
    }

    private readonly List<Blackout> _blackouts = new List<Blackout>();

    private sealed class Room
    {
        public SpaceId Space;
        public readonly List<Light> Lights = new List<Light>();
        public readonly List<Renderer> Lamps = new List<Renderer>();
        public bool Red;
    }

    private static RoomDarkness s_active;

    private readonly List<Room> _rooms = new List<Room>();
    private readonly List<KeyValuePair<Renderer, int>> _remapped = new List<KeyValuePair<Renderer, int>>();
    private readonly List<Texture2D> _copies = new List<Texture2D>();
    private LightmapData[] _originalMaps;
    private UnityEngine.Rendering.SphericalHarmonicsL2[] _originalProbes;
    private MaterialPropertyBlock _block;
    private bool _built;

    /// <summary>지금 살아 있는 것. 없으면 null.</summary>
    public static RoomDarkness Active
    {
        get { return s_active; }
    }

    /// <summary>복사본으로 옮긴 렌더러 수. 시험용.</summary>
    public int RemappedCount
    {
        get { return _remapped.Count; }
    }

    // ─────────────────────────────── 설치 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<RoomDarkness>(scene)) return;
        if (Object.FindAnyObjectByType<SpaceZones>() == null) return;
        FlowAutoInstall.CreateHost<RoomDarkness>(scene, "RoomDarkness (auto)");
    }

    private void OnEnable()
    {
        s_active = this;
    }

    private void OnDisable()
    {
        if (s_active == this) s_active = null;
    }

    private void OnDestroy()
    {
        Restore();
    }

    // ─────────────────────────────── 준비 ───────────────────────────────

    private void Start()
    {
        _block = new MaterialPropertyBlock();
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        if (zones == null) return;

        List<Bounds> boxes = new List<Bounds>();
        for (int i = 0; i < DarkRooms.Length; i++)
        {
            Bounds box;
            if (!zones.TryGetSpaceBox(DarkRooms[i], out box)) continue;
            boxes.Add(box);
            _rooms.Add(CollectLamps(DarkRooms[i], box));
        }

        DimBaked(boxes);
        BuildBlackouts(zones);
        _built = true;
    }

    // ─────────────────────────────── 86차 소등 ───────────────────────────────

    /// <summary>소등 조우가 그 방을 껐다/켰다(<see cref="LightGroup"/>가 부른다). 그 방 구운 빛을 어둡게/되돌린다.</summary>
    public static void SetBlackout(SpaceId space, bool dark)
    {
        if (s_active != null) s_active.ApplyBlackout(SpaceIds.Canonical(space), dark);
    }

    /// <summary>그 방이 지금 소등으로 어두운지(시험·검수).</summary>
    public static bool IsBlackedOut(SpaceId space)
    {
        if (s_active == null) return false;
        SpaceId s = SpaceIds.Canonical(space);
        for (int i = 0; i < s_active._blackouts.Count; i++)
        {
            if (s_active._blackouts[i].Space == s) return s_active._blackouts[i].On;
        }

        return false;
    }

    /// <summary>소등 방마다 그 방 렌더러가 쓰는 라이트맵의 아주 어두운 복사본을 미리 덧붙여 둔다(소등 때 렌더러만 옮긴다).</summary>
    private void BuildBlackouts(SpaceZones zones)
    {
        Shader shader = Shader.Find(ShaderName);
        LightmapData[] maps = LightmapSettings.lightmaps;
        LightProbes probes = LightmapSettings.lightProbes;
        Vector3[] probePos = probes != null && probes.count > 0 ? probes.positions : new Vector3[0];
        List<LightmapData> all = maps != null ? new List<LightmapData>(maps) : new List<LightmapData>();
        Dictionary<int, int> copyOf = new Dictionary<int, int>();
        Material blit = shader != null ? new Material(shader) : null;
        if (blit != null) blit.SetFloat("_Scale", BlackoutScale);

        for (int b = 0; b < BlackoutRooms.Length; b++)
        {
            Bounds box;
            if (!zones.TryGetSpaceBox(BlackoutRooms[b], out box)) continue;
            List<Bounds> wide = new List<Bounds> { new Bounds(box.center, box.size + new Vector3(0.6f, 3f, 0.6f)) };
            Blackout room = new Blackout { Space = SpaceIds.Canonical(BlackoutRooms[b]) };
            if (blit != null && maps != null)
            {
                foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    int li = r.lightmapIndex;
                    if (li < 0 || li >= maps.Length || !Inside(wide, r.bounds.center) || maps[li].lightmapColor == null) continue;
                    int dark;
                    if (!copyOf.TryGetValue(li, out dark))
                    {
                        Texture2D copy = Darkened(maps[li].lightmapColor, blit);
                        dark = -1;
                        if (copy != null)
                        {
                            _copies.Add(copy);
                            dark = all.Count;
                            all.Add(new LightmapData { lightmapColor = copy, lightmapDir = maps[li].lightmapDir, shadowMask = maps[li].shadowMask });
                        }

                        copyOf[li] = dark;
                    }

                    if (dark < 0) continue;
                    room.Renderers.Add(r);
                    room.Lit.Add(li);
                    room.Dark.Add(dark);
                }
            }

            for (int i = 0; i < probePos.Length; i++)
            {
                if (Inside(wide, probePos[i])) room.Probes.Add(i);
            }

            _blackouts.Add(room);
        }

        if (blit != null) Destroy(blit);
        if (maps != null && all.Count > maps.Length)
        {
            if (_originalMaps == null) _originalMaps = maps;
            LightmapSettings.lightmaps = all.ToArray();
        }
    }

    private void ApplyBlackout(SpaceId space, bool dark)
    {
        for (int b = 0; b < _blackouts.Count; b++)
        {
            Blackout room = _blackouts[b];
            if (room.Space != space || room.On == dark) continue;
            room.On = dark;
            for (int i = 0; i < room.Renderers.Count; i++)
            {
                if (room.Renderers[i] != null) room.Renderers[i].lightmapIndex = dark ? room.Dark[i] : room.Lit[i];
            }

            LightProbes probes = LightmapSettings.lightProbes;
            if (probes != null && room.Probes.Count > 0)
            {
                UnityEngine.Rendering.SphericalHarmonicsL2[] sh = probes.bakedProbes;
                if (dark)
                {
                    room.ProbeBase = new UnityEngine.Rendering.SphericalHarmonicsL2[room.Probes.Count];
                    for (int i = 0; i < room.Probes.Count; i++)
                    {
                        int k = room.Probes[i];
                        if (k >= sh.Length) continue;
                        room.ProbeBase[i] = sh[k];
                        sh[k] *= BlackoutScale;
                    }
                }
                else if (room.ProbeBase != null)
                {
                    for (int i = 0; i < room.Probes.Count && i < room.ProbeBase.Length; i++)
                    {
                        if (room.Probes[i] < sh.Length) sh[room.Probes[i]] = room.ProbeBase[i];
                    }

                    room.ProbeBase = null;
                }

                probes.bakedProbes = sh;
            }

            if (DirectionStage.Verbose) Debug.Log("[RoomDarkness] 소등 " + (dark ? "— 구운 빛 " + BlackoutScale + "배 " : "풀림 ") + space + " (렌더러 " + room.Renderers.Count + " · 프로브 " + room.Probes.Count + ")");
        }
    }

    private static Room CollectLamps(SpaceId space, Bounds box)
    {
        Room room = new Room { Space = space };
        // 천장 등은 상자 위에 있으므로 높이는 넉넉히(LightGroup과 같은 기준).
        Bounds wide = new Bounds(box.center + Vector3.up * 1f, new Vector3(box.size.x + 0.5f, box.size.y + 4f, box.size.z + 0.5f));
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) continue;
            if (l.transform.parent == null || !l.transform.parent.name.StartsWith("LampFluo")) continue;
            if (l.GetComponentInParent<FlashlightRelay>() != null || l.GetComponentInParent<CctvSystem>() != null) continue;
            if (wide.Contains(l.transform.position)) room.Lights.Add(l);
        }

        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r.name.StartsWith("LampFluo") && wide.Contains(r.bounds.center)) room.Lamps.Add(r);
        }

        return room;
    }

    /// <summary>방 안 렌더러가 쓰는 라이트맵만 어둡게 복사해 덧붙이고, 그 렌더러를 복사본으로 옮긴다. 방 안 프로브도 같은 배수.</summary>
    private void DimBaked(List<Bounds> boxes)
    {
        if (boxes.Count == 0) return;
        List<Bounds> wide = new List<Bounds>();
        for (int i = 0; i < boxes.Count; i++) wide.Add(new Bounds(boxes[i].center, boxes[i].size + new Vector3(0.6f, 3f, 0.6f)));

        Shader shader = Shader.Find(ShaderName);
        LightmapData[] maps = LightmapSettings.lightmaps;
        if (shader != null && maps != null && maps.Length > 0)
        {
            List<MeshRenderer> inside = new List<MeshRenderer>();
            bool[] used = new bool[maps.Length];
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int li = r.lightmapIndex;
                if (li < 0 || li >= maps.Length || !Inside(wide, r.bounds.center)) continue;
                inside.Add(r);
                used[li] = true;
            }

            if (inside.Count > 0)
            {
                Material blit = new Material(shader);
                blit.SetFloat("_Scale", BakedScale);
                int[] copyOf = new int[maps.Length];
                List<LightmapData> all = new List<LightmapData>(maps);
                for (int i = 0; i < maps.Length; i++)
                {
                    copyOf[i] = -1;
                    if (!used[i] || maps[i].lightmapColor == null) continue;
                    Texture2D copy = Darkened(maps[i].lightmapColor, blit);
                    if (copy == null) continue;
                    _copies.Add(copy);
                    copyOf[i] = all.Count;
                    all.Add(new LightmapData { lightmapColor = copy, lightmapDir = maps[i].lightmapDir, shadowMask = maps[i].shadowMask });
                }

                Destroy(blit);
                if (_copies.Count > 0)
                {
                    _originalMaps = maps;
                    LightmapSettings.lightmaps = all.ToArray();
                    for (int i = 0; i < inside.Count; i++)
                    {
                        int li = inside[i].lightmapIndex;
                        if (copyOf[li] < 0) continue;
                        _remapped.Add(new KeyValuePair<Renderer, int>(inside[i], li));
                        inside[i].lightmapIndex = copyOf[li];
                    }
                }
            }
        }

        LightProbes probes = LightmapSettings.lightProbes;
        if (probes != null && probes.count > 0)
        {
            UnityEngine.Rendering.SphericalHarmonicsL2[] sh = probes.bakedProbes;
            Vector3[] pos = probes.positions;
            _originalProbes = (UnityEngine.Rendering.SphericalHarmonicsL2[])sh.Clone();
            for (int i = 0; i < pos.Length && i < sh.Length; i++)
            {
                if (Inside(wide, pos[i])) sh[i] *= BakedScale;
            }

            probes.bakedProbes = sh;
        }
    }

    private static bool Inside(List<Bounds> boxes, Vector3 p)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            if (boxes[i].Contains(p)) return true;
        }

        return false;
    }

    private static Texture2D Darkened(Texture2D source, Material blit)
    {
        // HDR(BC6H)을 그대로 담는 부동소수 형식. 복사본은 GPU에만 있다(CPU 읽기 없음).
        // (DX11에서 B10G11R11은 Texture2D 샘플이 안 된다 — 둘 다 되는 형식만.)
        GraphicsFormat format = GraphicsFormat.B10G11R11_UFloatPack32;
        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render) || !SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample)) format = GraphicsFormat.R16G16B16A16_SFloat;
        if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render) || !SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample)) return null;
        RenderTexture rt = new RenderTexture(source.width, source.height, 0, format);
        rt.Create();
        RenderTexture before = RenderTexture.active;
        Graphics.Blit(source, rt, blit);
        RenderTexture.active = before;   // 103차 QA 4회: Blit이 rt를 활성으로 둔 채 Release해 「Releasing render texture that is set to be RenderTexture.active!」 경고가 매번 났다
        Texture2D copy = new Texture2D(source.width, source.height, format, TextureCreationFlags.None);
        copy.name = source.name + " (어둡게)";
        copy.wrapMode = source.wrapMode;
        copy.filterMode = source.filterMode;
        Graphics.CopyTexture(rt, copy);
        rt.Release();
        Destroy(rt);
        return copy;
    }

    private void Restore()
    {
        for (int b = 0; b < _blackouts.Count; b++)
        {
            if (_blackouts[b].On) ApplyBlackout(_blackouts[b].Space, false);
        }

        _blackouts.Clear();
        for (int i = 0; i < _remapped.Count; i++)
        {
            if (_remapped[i].Key != null) _remapped[i].Key.lightmapIndex = _remapped[i].Value;
        }

        _remapped.Clear();
        if (_originalMaps != null)
        {
            LightmapSettings.lightmaps = _originalMaps;
            _originalMaps = null;
        }

        if (_originalProbes != null && LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count == _originalProbes.Length)
        {
            LightmapSettings.lightProbes.bakedProbes = _originalProbes;
        }

        _originalProbes = null;
        for (int i = 0; i < _copies.Count; i++)
        {
            if (_copies[i] != null) Destroy(_copies[i]);
        }

        _copies.Clear();
        for (int r = 0; r < _rooms.Count; r++)
        {
            Room room = _rooms[r];
            for (int i = 0; i < room.Lamps.Count; i++)
            {
                if (room.Lamps[i] != null) room.Lamps[i].SetPropertyBlock(null);
            }
        }
    }

    // ─────────────────────────────── 매 프레임 ───────────────────────────────

    private void LateUpdate()
    {
        if (!_built) return;
        for (int r = 0; r < _rooms.Count; r++)
        {
            Room room = _rooms[r];
            bool red = false;
            for (int i = 0; i < room.Lights.Count; i++)
            {
                Light l = room.Lights[i];
                if (l == null) continue;
                bool on = LightGroup.IsLightTinted(l);
                red |= on;
                if (l.enabled != on) l.enabled = on;
            }

            // 발광은 LightGroup.TurnOn이 블록을 비울 수 있어 매 프레임 맞춘다.
            Color glow = red ? RedGlow : Color.black;
            for (int i = 0; i < room.Lamps.Count; i++)
            {
                Renderer lamp = room.Lamps[i];
                if (lamp == null) continue;
                lamp.GetPropertyBlock(_block);
                if (_block.GetColor(EmissionId) == glow && !_block.isEmpty && room.Red == red) continue;
                _block.SetColor(EmissionId, glow);
                lamp.SetPropertyBlock(_block);
            }

            room.Red = red;
        }
    }
}
