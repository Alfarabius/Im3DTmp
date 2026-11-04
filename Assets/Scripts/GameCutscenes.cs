using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public class GameCutscenes : MonoBehaviour
{
    [SerializeField] private Sprite[] introFrames;
    [SerializeField] private Sprite[] victoryFrames;
    [SerializeField, Min(0.1f)] private float secondsPerFrame = 4f;
    [SerializeField, Min(0.1f)] private float holdToSkip = 1f;

    public static bool IsPlaying { get; private set; }

    private GameObject _overlay;
    private Image _picture;
    private RectTransform _skipFill;
    private TMP_Text _skipHint;
    private Image _skipTrack;
    private float _previousTimeScale;
    private bool _previousAudioPause;
    private bool _playing;

    private void Start() => Play(introFrames, null);

    public bool PlayVictory(Action onFinished) => Play(victoryFrames, onFinished);

    private bool Play(Sprite[] frames, Action onFinished)
    {
        if (_playing || frames == null || !Array.Exists(frames, frame => frame != null)) return false;
        if (_overlay == null) CreateOverlay();
        _skipHint.text = LocalizationService.Get("cutscene.skip");
        _skipHint.gameObject.SetActive(false);
        _skipTrack.gameObject.SetActive(false);
        _previousTimeScale = Time.timeScale;
        _previousAudioPause = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        _playing = true;
        IsPlaying = true;
        _overlay.SetActive(true);
        StartCoroutine(Show(frames, onFinished));
        return true;
    }

    private IEnumerator Show(Sprite[] frames, Action onFinished)
    {
        float held = 0f;
        bool skipped = false;
        foreach (var frame in frames)
        {
            if (frame == null) continue;
            _picture.sprite = frame;
            float age = 0f;
            while (age < secondsPerFrame)
            {
                bool pressed = Application.isFocused && Mouse.current != null && Mouse.current.leftButton.isPressed;
                _skipHint.gameObject.SetActive(pressed);
                _skipTrack.gameObject.SetActive(pressed);
                held = pressed ? held + Time.unscaledDeltaTime : 0f;
                _skipFill.anchorMax = new Vector2(Mathf.Clamp01(held / holdToSkip), 1f);
                if (held >= holdToSkip)
                {
                    skipped = true;
                    break;
                }
                if (Application.isFocused) age += Time.unscaledDeltaTime;
                yield return null;
            }
            if (skipped) break;
        }
        RestoreGame();
        onFinished?.Invoke();
    }

    private void RestoreGame()
    {
        if (!_playing) return;
        _playing = false;
        IsPlaying = false;
        Time.timeScale = _previousTimeScale;
        AudioListener.pause = _previousAudioPause;
        if (_overlay != null) _overlay.SetActive(false);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestoreGame();
    }

    private void CreateOverlay()
    {
        _overlay = new GameObject("Cutscene", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _overlay.transform.SetParent(transform, false);
        var canvas = _overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = _overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var background = CreateImage("Background", _overlay.transform, Color.black);
        Stretch(background.rectTransform);
        _picture = CreateImage("Frame", _overlay.transform, Color.white);
        Stretch(_picture.rectTransform);
        _picture.preserveAspect = true;

        var hintObject = new GameObject("Skip hint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintObject.transform.SetParent(_overlay.transform, false);
        var hint = hintObject.GetComponent<TextMeshProUGUI>();
        _skipHint = hint;
        hint.font = LocalizationService.InterfaceFont;
        hint.text = LocalizationService.Get("cutscene.skip");
        hint.fontSize = 26f;
        hint.alignment = TextAlignmentOptions.MidlineRight;
        hint.raycastTarget = false;
        var rect = hint.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        rect.sizeDelta = new Vector2(800f, 48f);

        var track = CreateImage("Skip progress", _overlay.transform, new Color(0.2f, 0.2f, 0.2f));
        _skipTrack = track;
        rect = track.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 30f);
        rect.sizeDelta = new Vector2(240f, 5f);
        _skipFill = CreateImage("Fill", track.transform, Color.white).rectTransform;
        Stretch(_skipFill);
        _skipFill.anchorMax = new Vector2(0f, 1f);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
