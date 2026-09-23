using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Put this file anywhere inside your own 06.UI folder.
// One controller per screen. Assign the FINAL, already-designed UI positions/rotations in the
// editor -- this script reads that as the resting pose, offsets each element back to its Figma
// "start" pose at Start(), then tweens it into the resting pose with real DOTween tweens.
//
// Figma file gZLmJxT0GgyLUraGumwDf0, page "UI Animation" (node 356:468). Per-effect node ids
// (re-pulled from Figma and checked value by value against this file):
//   StartLogo : 356:491 ui_logo_main
//   Loading   : 356:532 ui_t_paper, 356:539 ui_t_vinyl
//   PauseMenu : 356:576 PAUSE, 356:583 RESUME, 356:590 QUIT
//   DayTitle  : 356:601 DAY1
// (The previous version's inline comments, e.g. "229:84", pointed at node ids that don't exist
// in this file at all -- stale references from a different export. Fixed below.)
[DisallowMultipleComponent]
[AddComponentMenu("UI/NC UI Figma Motion")]
public sealed class NCUIFigmaMotion : MonoBehaviour
{
    public enum ScreenMotion { StartLogo, Loading, PauseMenu, DayTitle }

    [Header("1. Choose the screen effect")]
    public ScreenMotion effect = ScreenMotion.Loading;

    [Header("2. Drag objects from Hierarchy (not Project)")]
    [Tooltip("StartLogo: logo / Loading: paper / PauseMenu: PAUSE / DayTitle: DAY1")]
    public RectTransform target1;
    [Tooltip("Loading: vinyl / PauseMenu: RESUME / Other effects: leave empty")]
    public RectTransform target2;
    [Tooltip("PauseMenu: QUIT / Other effects: leave empty")]
    public RectTransform target3;

    [Header("3. Playback")]
    public bool playOnEnable = true;
    [Tooltip("Off = play once when the screen opens. On = repeat the Figma 2-second timeline.")]
    public bool loop = false;
    [Tooltip("Show the final state without animation.")]
    public bool reduceMotion = false;
    [Tooltip("Use 1 with a 1920x1080 reference Canvas. Adjust only if your UI uses a different coordinate scale.")]
    [Min(0f)] public float distanceMultiplier = 1f;

    // Every one of these screens is authored in Figma as a fixed 2-second timeline.
    private const float TimelineSeconds = 2f;

    // The two cubic-bezier() easings Figma used across every track in this file.
    //   ease     -> every opacity track and every rotation track.
    //   moveEase -> every translate track except StartLogo's, which is linear.
    // DOTween has no built-in equivalent for arbitrary bezier control points, so these solve
    // the curve the same way a browser does: find the bezier parameter whose X equals the
    // elapsed fraction, then read Y at that parameter.
    private static readonly EaseFunction ease = CubicBezierEase(0.5f, 0f, 0.5f, 1f);
    private static readonly EaseFunction moveEase = CubicBezierEase(0.86f, 0.02f, 0.54f, 0.91f);

    private readonly List<Track> tracks = new List<Track>();
    private Sequence sequence;
    private bool initialized;

    private sealed class Track
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 restPos;
        public Quaternion restRot;
        public float restAlpha;

        public Vector2 offset;              // Unity-space start offset from the resting position
        public float moveStart, moveEnd;    // seconds, within the 2s timeline
        public bool linearMove;

        public float rotationOffsetDeg;     // Unity-space start rotation offset (Z, degrees)
        public float rotateStart, rotateEnd;

        public bool fade;
        public float fadeStart, fadeEnd;
    }

    private void Start()
    {
        // Start runs after Awake/OnEnable on the screen's other components.
        Canvas.ForceUpdateCanvases();
        BuildTracks();
        initialized = true;
        if (playOnEnable) Play();
    }

    private void OnEnable()
    {
        if (initialized && playOnEnable) Play();
    }

    private void OnDisable()
    {
        StopAndRestore();
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }

    // Public so a UnityEvent can call this later if needed.
    public void Play()
    {
        if (!initialized || !isActiveAndEnabled) return;
        StopAndRestore();
        if (reduceMotion || tracks.Count == 0) return;

        sequence = BuildSequence();
        if (sequence == null) return;
        sequence.SetUpdate(true).SetLoops(loop ? -1 : 1, LoopType.Restart).Play();
    }

    public void StopAndRestore()
    {
        sequence?.Kill();
        sequence = null;
        foreach (Track t in tracks)
        {
            if (!t.rect) continue;
            t.rect.anchoredPosition = t.restPos;
            t.rect.localRotation = t.restRot;
            if (t.group) t.group.alpha = t.restAlpha;
        }
    }

    private Track Add(RectTransform rect, bool fade)
    {
        if (!rect)
        {
            Debug.LogWarning("NCUIFigmaMotion: A required Target is empty on " + name, this);
            return null;
        }
        foreach (Track existing in tracks)
        {
            if (existing.rect != rect) continue;
            Debug.LogWarning("NCUIFigmaMotion: Assign a different object to each Target on " + name, this);
            return null;
        }
        CanvasGroup group = null;
        if (fade)
        {
            group = rect.GetComponent<CanvasGroup>();
            if (!group) group = rect.gameObject.AddComponent<CanvasGroup>();
        }
        Track t = new Track
        {
            rect = rect, restPos = rect.anchoredPosition,
            restRot = rect.localRotation, group = group,
            restAlpha = group ? group.alpha : 1f, fade = fade
        };
        tracks.Add(t);
        return t;
    }

    private void BuildTracks()
    {
        tracks.Clear();
        Track t;
        switch (effect)
        {
            case ScreenMotion.StartLogo: // 356:491 ui_logo_main
                t = Add(target1, true);
                if (t == null) break;
                t.offset = new Vector2(-16f, 0f);
                t.moveStart = .018f; t.moveEnd = 1.00842f;
                t.fadeEnd = 1.00842f; t.linearMove = true;
                break;
            case ScreenMotion.Loading:
                t = Add(target1, false); // 356:532 ui_t_paper
                if (t != null)
                {
                    t.offset = new Vector2(-405.699f, .002f);
                    t.moveStart = .034f; t.moveEnd = .49866f;
                    t.rotationOffsetDeg = .494f * Mathf.Rad2Deg;
                    t.rotateEnd = .54996f;
                }
                t = Add(target2, false); // 356:539 ui_t_vinyl
                if (t != null)
                {
                    t.offset = new Vector2(-152.761f, -40.406f);
                    t.moveStart = .434f; t.moveEnd = .752f;
                    t.rotationOffsetDeg = .606f * Mathf.Rad2Deg;
                    t.rotateStart = .19012f; t.rotateEnd = .80316f;
                }
                break;
            case ScreenMotion.PauseMenu:
                AddPause(target1, -297.446f, 0f, .597f);         // 356:576 PAUSE
                AddPause(target2, -286.446f, .19476f, .80534f);  // 356:583 RESUME
                AddPause(target3, -222.946f, .37978f, 1.00492f); // 356:590 QUIT
                break;
            case ScreenMotion.DayTitle: // 356:601 DAY1
                t = Add(target1, true);
                if (t == null) break;
                t.offset = new Vector2(-46f, 0f);
                t.moveEnd = 1.04856f;
                t.fadeStart = .35702f; t.fadeEnd = 1.8f;
                break;
        }
    }

    private void AddPause(RectTransform rect, float x, float start, float end)
    {
        Track t = Add(rect, true);
        if (t == null) return;
        t.offset = new Vector2(x, 0f);
        t.moveStart = t.fadeStart = start;
        t.moveEnd = t.fadeEnd = end;
    }

    private Sequence BuildSequence()
    {
        Sequence seq = DOTween.Sequence();
        bool any = false;

        foreach (Track t in tracks)
        {
            if (!t.rect) continue;

            if (t.moveEnd > t.moveStart)
            {
                any = true;
                t.rect.anchoredPosition = t.restPos + t.offset * distanceMultiplier;
                Tweener move = t.rect.DOAnchorPos(t.restPos, t.moveEnd - t.moveStart);
                if (t.linearMove) move.SetEase(Ease.Linear);
                else move.SetEase(moveEase);
                seq.Insert(t.moveStart, move);
            }

            if (t.rotateEnd > t.rotateStart)
            {
                any = true;
                Vector3 restEuler = t.restRot.eulerAngles;
                t.rect.localRotation = t.restRot * Quaternion.Euler(0f, 0f, t.rotationOffsetDeg);
                Tweener rotate = t.rect.DOLocalRotate(restEuler, t.rotateEnd - t.rotateStart, RotateMode.Fast);
                rotate.SetEase(ease);
                seq.Insert(t.rotateStart, rotate);
            }

            if (t.fade && t.group && t.fadeEnd > t.fadeStart)
            {
                any = true;
                t.group.alpha = 0f;
                Tweener fadeTween = t.group.DOFade(t.restAlpha, t.fadeEnd - t.fadeStart);
                fadeTween.SetEase(ease);
                seq.Insert(t.fadeStart, fadeTween);
            }
        }

        if (!any)
        {
            seq.Kill();
            return null;
        }

        // Pad the sequence out to the full authored 2s timeline so a looping effect keeps
        // Figma's hold-then-restart rhythm instead of snapping back the instant the last track ends.
        seq.InsertCallback(TimelineSeconds, NoOp);
        return seq;
    }

    private static void NoOp() { }

    // Reproduces a CSS cubic-bezier(x1, y1, x2, y2) timing function exactly, the same way
    // Figma/browsers evaluate it: solve for the bezier parameter whose X equals the elapsed
    // fraction, then read Y at that parameter. (Evaluating Y directly at the elapsed fraction
    // gives the wrong curve.)
    private static EaseFunction CubicBezierEase(float x1, float y1, float x2, float y2)
    {
        return (time, duration, overshootOrAmplitude, period) =>
        {
            float x = duration > 0f ? Mathf.Clamp01(time / duration) : 1f;
            if (x <= 0f || x >= 1f) return x;
            float low = 0f, high = 1f;
            for (int i = 0; i < 20; i++)
            {
                float mid = (low + high) * .5f;
                if (CubicComponent(mid, x1, x2) < x) low = mid; else high = mid;
            }
            return CubicComponent((low + high) * .5f, y1, y2);
        };
    }

    private static float CubicComponent(float t, float p1, float p2)
    {
        float u = 1f - t;
        return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
    }
}
