using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public class GameCutscenes : MonoBehaviour
{
    [SerializeField] private Sprite[] introFrames;
    [SerializeField] private Sprite[] victoryFrames;
    [SerializeField, Min(1f)] private float charactersPerSecond = 30f;
    [FormerlySerializedAs("secondsPerFrame")]
    [SerializeField, Min(0.1f)] private float minimumReadTime = 4f;
    [SerializeField, Min(1f)] private float readingCharactersPerSecond = 18f;
    [SerializeField, Min(0.1f)] private float holdToSkip = 1f;

    public static bool IsPlaying { get; private set; }

    private GameObject _overlay;
    private Image _picture;
    private TMP_Text _caption;
    private string _captionKey;
    private int _captionCharacterCount;
    private float _typingElapsed;
    private float _readingElapsed;
    private RectTransform _skipFill;
    private TMP_Text _skipHint;
    private Image _skipTrack;
    private float _previousTimeScale;
    private bool _previousAudioPause;
    private bool _playing;

    private void OnEnable()
    {
        GameInput.ControlsChanged += RefreshSkipHint;
        LocalizationService.LanguageChanged += RefreshText;
    }

    private void RefreshText()
    {
        RefreshSkipHint();
        RefreshCaption();
    }

    private void RefreshSkipHint()
    {
        if (_skipHint != null)
            _skipHint.text = LocalizationService.Get(GameInput.UsingGamepad ? "cutscene.skip_gamepad" : "cutscene.skip");
    }

    private void RefreshCaption()
    {
        if (_caption == null || string.IsNullOrEmpty(_captionKey)) return;
        _caption.text = LocalizationService.Get(_captionKey);
        _caption.ForceMeshUpdate();
        _captionCharacterCount = _caption.textInfo.characterCount;
        _readingElapsed = 0f;
        UpdateCaptionVisibility();
    }

    private void UpdateCaptionVisibility()
    {
        float speed = Mathf.Max(1f, charactersPerSecond);
        _caption.maxVisibleCharacters = _typingElapsed >= _captionCharacterCount / speed
            ? _captionCharacterCount : Mathf.FloorToInt(_typingElapsed * speed);
    }

    private float ReadingTime => Mathf.Max(Mathf.Max(0.1f, minimumReadTime),
        _captionCharacterCount / Mathf.Max(1f, readingCharactersPerSecond));

    private void Start() => Play(introFrames, "cutscene.intro.", null);

    public bool PlayVictory(Action onFinished) => Play(victoryFrames, "cutscene.victory.", onFinished);

    private bool Play(Sprite[] frames, string captionPrefix, Action onFinished)
    {
        if (_playing || frames == null || !Array.Exists(frames, frame => frame != null)) return false;
        LocalizationService.EnsureInitialized();
        if (_overlay == null) CreateOverlay();
        RefreshSkipHint();
        _skipHint.gameObject.SetActive(false);
        _skipTrack.gameObject.SetActive(false);
        _previousTimeScale = Time.timeScale;
        _previousAudioPause = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        _playing = true;
        IsPlaying = true;
        _picture.sprite = Array.Find(frames, frame => frame != null);
        _overlay.SetActive(true);
        StartCoroutine(Show(frames, captionPrefix, onFinished));
        return true;
    }

    private IEnumerator Show(Sprite[] frames, string captionPrefix, Action onFinished)
    {
        float held = 0f;
        bool skipped = false;
        for (int i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            if (frame == null) continue;
            _picture.sprite = frame;
            _captionKey = captionPrefix + (i + 1);
            _typingElapsed = 0f;
            RefreshCaption();
            bool advanceFrame = false;
            while (!advanceFrame && _readingElapsed < ReadingTime)
            {
                bool pressed = Application.isFocused && GameInput.AnyButtonPressed();
                _skipHint.gameObject.SetActive(pressed);
                _skipTrack.gameObject.SetActive(pressed);
                held = pressed ? held + Time.unscaledDeltaTime : 0f;
                _skipFill.anchorMax = new Vector2(Mathf.Clamp01(held / holdToSkip), 1f);
                if (held >= holdToSkip)
                {
                    skipped = true;
                    break;
                }
                if (Application.isFocused)
                {
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        if (_caption.maxVisibleCharacters >= _captionCharacterCount)
                        {
                            advanceFrame = true;
                        }
                        else
                        {
                            _typingElapsed = _captionCharacterCount / Mathf.Max(1f, charactersPerSecond);
                            _readingElapsed = 0f;
                            UpdateCaptionVisibility();
                        }
                    }
                    else if (_caption.maxVisibleCharacters < _captionCharacterCount)
                    {
                        _typingElapsed += Time.unscaledDeltaTime;
                        UpdateCaptionVisibility();
                    }
                    else
                    {
                        // Reading time starts on the frame after the final character appears.
                        _readingElapsed += Time.unscaledDeltaTime;
                    }
                }
                // Consume the click before entering the next slide's input loop.
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
        _captionKey = null;
        Time.timeScale = _previousTimeScale;
        AudioListener.pause = _previousAudioPause;
        if (_overlay != null) _overlay.SetActive(false);
    }

    private void OnDisable()
    {
        GameInput.ControlsChanged -= RefreshSkipHint;
        LocalizationService.LanguageChanged -= RefreshText;
        StopAllCoroutines();
        RestoreGame();
    }

    private void CreateOverlay()
    {
        _overlay = new GameObject("Cutscene", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _overlay.SetActive(false);
        _overlay.transform.SetParent(transform, false);
        var canvas = _overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = _overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var background = CreateImage("Background", _overlay.transform, Color.black);
        Stretch(background.rectTransform);
        _picture = CreateImage("Frame", _overlay.transform, Color.white);
        Stretch(_picture.rectTransform);
        _picture.rectTransform.anchorMin = new Vector2(0.08f, 0.24f);
        _picture.rectTransform.anchorMax = new Vector2(0.92f, 0.94f);
        _picture.preserveAspect = true;

        var captionObject = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        captionObject.transform.SetParent(_overlay.transform, false);
        _caption = captionObject.GetComponent<TextMeshProUGUI>();
        _caption.font = LocalizationService.InterfaceFont;
        _caption.fontSize = 32f;
        _caption.enableAutoSizing = true;
        _caption.fontSizeMin = 24f;
        _caption.fontSizeMax = 32f;
        _caption.textWrappingMode = TextWrappingModes.Normal;
        _caption.alignment = TextAlignmentOptions.Top;
        _caption.raycastTarget = false;
        Stretch(_caption.rectTransform);
        _caption.rectTransform.anchorMin = new Vector2(0.1f, 0.1f);
        _caption.rectTransform.anchorMax = new Vector2(0.9f, 0.2f);

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
