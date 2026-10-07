using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 모든 씬의 UI 버튼 클릭음. 마우스 왼쪽을 누른 순간 그 아래에 <b>누를 수 있는</b> 버튼이 있으면 SceneFlowConfig의 클릭음을 낸다.
///
/// <para>· 버튼마다 붙이지 않는다 — 나중에 생기는 버튼(일시정지 메뉴·사망 화면 등)도 그대로 소리가 난다.
/// · 비활성 버튼(interactable 꺼짐, 예: 서명 전 「출근하기」)에는 소리가 없다.
/// · 일시정지 중(AudioListener.pause)에도 메뉴 버튼 소리가 들리게 ignoreListenerPause.
/// · SceneBgmPlayer가 같은 오브젝트에 붙인다(씬이 바뀌어도 살아 있음).</para>
/// </summary>
[DisallowMultipleComponent]
public class UiClickSound : MonoBehaviour
{
    private AudioSource source;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
    }

    private void Update()
    {
        if (!LeftPressedThisFrame(out Vector2 position)) return;

        SceneFlowConfig config = SceneFlow.Config;
        if (config == null || config.ButtonClick == null) return;

        EventSystem es = EventSystem.current;
        if (es == null) return;

        // 씬마다 EventSystem이 바뀌므로 누를 때마다 새로 만든다(클릭 순간에만 — 매 프레임 아님)
        var pointer = new PointerEventData(es) { position = position };
        hits.Clear();
        es.RaycastAll(pointer, hits);
        if (hits.Count == 0) return;

        // 맨 앞에 맞은 UI가 버튼(또는 버튼의 자식)이고 누를 수 있을 때만
        Button button = hits[0].gameObject.GetComponentInParent<Button>();
        if (button == null || !button.IsActive() || !button.IsInteractable()) return;

        source.PlayOneShot(config.ButtonClick, config.ButtonClickVolume);
    }

    private static bool LeftPressedThisFrame(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            return mouse.leftButton.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        position = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
#else
        position = Vector2.zero;
        return false;
#endif
    }
}
