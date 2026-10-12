using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolveGames
{
    public class StartAudio : MonoBehaviour
    {
        [SerializeField] GameObject Audio;
        public void PlayAudio(AudioClip clip)
        {
            var a = Instantiate(Audio);
            Destroy(a, clip.length + 0.4f);
            if (a.TryGetComponent<AudioSource>(out var au))
            {
                au.clip = clip;
                au.Play();
            }
        }
    }
}