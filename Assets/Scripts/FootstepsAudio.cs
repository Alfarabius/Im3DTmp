using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FootstepsAudio : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stepClip;
    [SerializeField, Range(0.5f, 1.5f)] private float minPitch = 0.9f;
    [SerializeField, Range(0.5f, 1.5f)] private float maxPitch = 1.1f;
    [SerializeField, Range(0.3f, 1.5f)] private float stepVolume = 0.3f;
    
    private float _defaultPitch;
    private float _defaultVolume;
    private Coroutine _resetPitchCoroutine;

    private void Awake()
    {
        if (audioSource == null) 
            audioSource = GetComponent<AudioSource>();
            
        _defaultPitch = audioSource.pitch;
        _defaultVolume = audioSource.volume;
    }

    public void PlayStepSound()
    {
        if (stepClip == null) return;
        
        // Останавливаем только конкретную корутину, если она уже работает
        if (_resetPitchCoroutine != null)
        {
            StopCoroutine(_resetPitchCoroutine);
        }
        
        _resetPitchCoroutine = StartCoroutine(PlayAndResetPitch());
    }

    private IEnumerator PlayAndResetPitch()
    {
        audioSource.volume = stepVolume;
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(stepClip);
        
        yield return new WaitForSeconds(stepClip.length);
        
        audioSource.pitch = _defaultPitch;
        audioSource.volume = _defaultVolume;
        _resetPitchCoroutine = null; // Очищаем ссылку после завершения
    }
}