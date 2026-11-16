using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PauseCutsceneMusic : MonoBehaviour
{
    private AudioSource _source;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;
        _source.ignoreListenerPause = true;
    }

    private void LateUpdate()
    {
        bool audible = PauseManager.IsPaused || GameCutscenes.IsPlaying;
        if (audible && !_source.isPlaying && _source.clip != null) _source.Play();
        else if (!audible && _source.isPlaying) _source.Stop();
    }

    private void OnDisable()
    {
        if (_source != null) _source.Stop();
    }
}
