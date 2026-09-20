using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 손·태블릿처럼 카메라 바로 앞에 붙는 물건(뷰모델)을 씬 조명·후처리에서 떼어 낸다.
///
/// 씬에는 아무것도 추가하지 않고, 실행할 때 이 스크립트가 알아서 한다.
///   1) 뷰모델을 <b>전용 오브젝트 레이어</b>로 옮기고, 본 카메라는 그 레이어를 그리지 않게 한다.
///   2) <b>오버레이 카메라</b>를 만들어 뷰모델만 그린다. 이 카메라는 후처리를 끄기 때문에
///      실내 볼륨의 노출 보정(+5.5)이나 블룸이 태블릿 화면을 하얗게 만들지 않는다.
///      깊이를 따로 지우므로 손이 벽을 뚫고 나가는 것도 막아 준다.
///   3) <b>렌더링 레이어</b>로 씬 조명(손전등 포함)이 뷰모델을 비추지 않게 하고,
///      대신 카메라에 붙은 전용 조명 하나로만 밝기를 정한다.
///
/// 되돌리려면 이 컴포넌트를 꺼 두면 된다(그러면 예전처럼 씬 조명을 그대로 받는다).
/// </summary>
[DisallowMultipleComponent]
public class ViewmodelRig : MonoBehaviour
{
    [Header("레이어")]
    [Tooltip("뷰모델을 올려 둘 오브젝트 레이어 이름. Project Settings > Tags and Layers 에 있어야 한다.")]
    public string viewmodelLayerName = "Viewmodel";
    [Tooltip("뷰모델 전용 렌더링 레이어 번호(0~31). 이 번호를 가진 조명만 뷰모델을 비춘다.")]
    [Range(1, 31)] public int viewmodelRenderingLayer = 1;

    [Header("전용 조명")]
    public bool createLight = true;
    public Color lightColor = new Color(1f, 0.97f, 0.92f);
    [Tooltip("뷰모델 밝기. 씬이 어두워도 이 값으로 고정된다.")]
    public float lightIntensity = 1.1f;
    [Tooltip("조명이 향할 방향(카메라 기준). 기본값은 오른쪽 위 앞.")]
    public Vector3 lightAngles = new Vector3(35f, -25f, 0f);

    [Header("오버레이 카메라")]
    [Tooltip("손이 벽에 박히지 않도록 아주 가깝게 잡는다.")]
    public float nearClip = 0.01f;
    public float farClip = 3f;

    private Camera _baseCamera;
    private Camera _overlayCamera;
    private Light _light;

    private void Start()
    {
        _baseCamera = Camera.main;
        if (_baseCamera == null)
        {
            Debug.LogWarning("[ViewmodelRig] MainCamera를 찾지 못해 그냥 둡니다.");
            return;
        }

        int layer = LayerMask.NameToLayer(viewmodelLayerName);
        if (layer < 0)
        {
            Debug.LogWarning("[ViewmodelRig] '" + viewmodelLayerName + "' 레이어가 없습니다. Project Settings > Tags and Layers 에서 만들어 주세요.");
            return;
        }

        ApplyLayer(layer);
        ExcludeFromSceneLights();
        SetupOverlayCamera(layer);
        SetupLight();
    }

    /// <summary>뷰모델 전체를 전용 레이어로 옮기고, 조명은 전용 렌더링 레이어만 받게 한다.</summary>
    private void ApplyLayer(int layer)
    {
        uint mask = 1u << viewmodelRenderingLayer;

        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].gameObject.layer = layer;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].renderingLayerMask = mask;
            // 카메라 바로 앞 물건이라 그림자를 만들 이유가 없다.
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// <summary>
    /// 씬에 있는 모든 조명에서 뷰모델 렌더링 레이어를 빼서, 손전등 같은 빛이 닿지 않게 한다.
    ///
    /// URP는 <b>Light.renderingLayerMask를 보지 않는다.</b> 같은 오브젝트에 붙는
    /// UniversalAdditionalLightData의 renderingLayers를 봐야 한다. 여기를 안 고치면
    /// 아무리 마스크를 바꿔도 조명이 그대로 닿는다.
    /// </summary>
    private void ExcludeFromSceneLights()
    {
        uint mask = 1u << viewmodelRenderingLayer;

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == _light) continue;

            UniversalAdditionalLightData data = lights[i].GetUniversalAdditionalLightData();
            if (data == null) continue;

            data.renderingLayers = data.renderingLayers.value & ~mask;
            if (data.customShadowLayers)
            {
                data.shadowRenderingLayers = data.shadowRenderingLayers.value & ~mask;
            }
        }
    }

    /// <summary>뷰모델만 그리는 오버레이 카메라를 만들어 본 카메라 위에 얹는다.</summary>
    private void SetupOverlayCamera(int layer)
    {
        UniversalAdditionalCameraData baseData = _baseCamera.GetUniversalAdditionalCameraData();
        if (baseData.renderType != CameraRenderType.Base)
        {
            Debug.LogWarning("[ViewmodelRig] 메인 카메라가 Base 타입이 아니라 오버레이를 얹지 않았습니다.");
            return;
        }

        // 본 카메라는 뷰모델을 그리지 않는다. 오버레이 카메라가 그린다.
        _baseCamera.cullingMask &= ~(1 << layer);

        GameObject go = new GameObject("ViewmodelCamera");
        go.transform.SetParent(_baseCamera.transform, false);

        _overlayCamera = go.AddComponent<Camera>();
        _overlayCamera.clearFlags = CameraClearFlags.Depth;
        _overlayCamera.cullingMask = 1 << layer;
        _overlayCamera.nearClipPlane = nearClip;
        _overlayCamera.farClipPlane = farClip;
        _overlayCamera.fieldOfView = _baseCamera.fieldOfView;
        _overlayCamera.useOcclusionCulling = false;

        UniversalAdditionalCameraData data = _overlayCamera.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Overlay;
        data.renderPostProcessing = false;   // 노출 보정·블룸·그레인이 태블릿 화면에 묻지 않게
        data.renderShadows = false;

        baseData.cameraStack.Add(_overlayCamera);
    }

    /// <summary>뷰모델만 비추는 조명. 씬이 어두워도 손이 보이는 밝기를 여기서 정한다.</summary>
    private void SetupLight()
    {
        if (!createLight) return;

        GameObject go = new GameObject("ViewmodelLight");
        go.transform.SetParent(_baseCamera.transform, false);
        go.transform.localRotation = Quaternion.Euler(lightAngles);

        _light = go.AddComponent<Light>();
        _light.type = LightType.Directional;
        _light.color = lightColor;
        _light.intensity = lightIntensity;
        _light.shadows = LightShadows.None;

        // 이 조명만 뷰모델을 비춘다. URP는 여기(UniversalAdditionalLightData)를 본다.
        UniversalAdditionalLightData data = _light.GetUniversalAdditionalLightData();
        data.renderingLayers = 1u << viewmodelRenderingLayer;
    }

    private void OnDestroy()
    {
        // 씬을 옮길 때 만들어 둔 것들을 정리한다.
        if (_overlayCamera != null)
        {
            if (_baseCamera != null)
            {
                UniversalAdditionalCameraData baseData = _baseCamera.GetUniversalAdditionalCameraData();
                baseData.cameraStack.Remove(_overlayCamera);
            }
            Destroy(_overlayCamera.gameObject);
        }

        if (_light != null) Destroy(_light.gameObject);
    }
}
