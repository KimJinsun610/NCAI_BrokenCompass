using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 마우스로 드래그해 서명을 적는 칸. RawImage에 실행 중에만 만드는 텍스처로 그린다.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class SignaturePad : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Tooltip("칸 크기(UI 단위) 대비 텍스처 해상도 배율. 크면 선이 매끄럽다.")]
    [SerializeField, Range(1f, 4f)] private float resolutionScale = 2f;
    [Tooltip("펜 굵기 (텍스처 픽셀 반지름)")]
    [SerializeField, Range(1f, 16f)] private float brushRadius = 4f;
    [SerializeField] private Color inkColor = new Color(0.08f, 0.08f, 0.12f, 1f);

    /// <summary>처음으로 잉크가 묻었을 때, 또는 지웠을 때 발생</summary>
    public event Action InkChanged;

    public bool HasInk { get; private set; }

    private RawImage rawImage;
    private Texture2D texture;
    private Color32[] pixels;
    private Vector2 lastPoint;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rawImage.raycastTarget = true;

        Rect rect = rawImage.rectTransform.rect;
        int width = Mathf.Max(8, Mathf.RoundToInt(rect.width * resolutionScale));
        int height = Mathf.Max(8, Mathf.RoundToInt(rect.height * resolutionScale));

        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        pixels = new Color32[width * height];
        rawImage.texture = texture;
        rawImage.color = Color.white;
        Upload();
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    public void Clear()
    {
        Array.Clear(pixels, 0, pixels.Length);
        Upload();

        if (!HasInk) return;
        HasInk = false;
        InkChanged?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!TryGetPixel(eventData, out Vector2 point)) return;

        lastPoint = point;
        Stamp(point);
        Commit();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!TryGetPixel(eventData, out Vector2 point)) return;

        float distance = Vector2.Distance(lastPoint, point);
        float step = Mathf.Max(1f, brushRadius * 0.5f);
        int count = Mathf.CeilToInt(distance / step);
        for (int i = 1; i <= count; i++)
        {
            Stamp(Vector2.Lerp(lastPoint, point, (float)i / count));
        }

        lastPoint = point;
        Commit();
    }

    private bool TryGetPixel(PointerEventData eventData, out Vector2 pixel)
    {
        RectTransform rt = rawImage.rectTransform;
        pixel = default;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            return false;
        }

        Rect rect = rt.rect;
        // 칸 밖으로 드래그해도 가장자리에 붙여서 선이 끊기지 않게 한다
        float u = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
        float v = Mathf.Clamp01((local.y - rect.yMin) / rect.height);
        pixel = new Vector2(u * (texture.width - 1), v * (texture.height - 1));
        return true;
    }

    private void Stamp(Vector2 center)
    {
        int radius = Mathf.CeilToInt(brushRadius);
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        float radiusSqr = brushRadius * brushRadius;
        Color32 ink = inkColor;

        for (int y = cy - radius; y <= cy + radius; y++)
        {
            if (y < 0 || y >= texture.height) continue;
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || x >= texture.width) continue;
                float dx = x - center.x;
                float dy = y - center.y;
                if (dx * dx + dy * dy > radiusSqr) continue;
                pixels[y * texture.width + x] = ink;
            }
        }
    }

    private void Commit()
    {
        Upload();
        if (HasInk) return;
        HasInk = true;
        InkChanged?.Invoke();
    }

    private void Upload()
    {
        texture.SetPixels32(pixels);
        texture.Apply(false);
    }
}
