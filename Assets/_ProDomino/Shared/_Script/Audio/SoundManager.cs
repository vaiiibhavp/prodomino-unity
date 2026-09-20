using System.Collections.Generic;
using UnityEngine;

namespace ProDomino.Shared
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance;

        [Header("Audio Sources")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        [Header("Volumes")]
        [Range(0f, 1f)] public float musicVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        [Header("Audio library")]
        public List<AudioEntry> audioLibrary = new List<AudioEntry>();
        private Dictionary<IDAudioClip, AudioClip> clipMap;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // If there are no sources assigned, we create them.
                if (musicSource == null)
                {
                    musicSource = gameObject.AddComponent<AudioSource>();
                    musicSource.loop = true;
                }

                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                    sfxSource.loop = false;
                }

                clipMap = new Dictionary<IDAudioClip, AudioClip>();
                foreach (var entry in audioLibrary)
                {
                    if (clipMap.ContainsKey(entry.id))
                    {
                        Debug.LogWarning($"⚠️ Duplicate ID in AudioLibrary: '{entry.id}'. Only the first assigned clip will be used.");
                        continue;
                    }

                    clipMap.Add(entry.id, entry.clip);
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /*void Start()
        {
            //SoundManager.Instance.PlayMusic(IDAudioClip.musicTheme_1);
        }*/

        private void Update()
        {
            // Synchronize volume in real time (only useful in Play mode)
            if (musicSource != null)
                musicSource.volume = musicVolume;

            if (sfxSource != null)
                sfxSource.volume = sfxVolume;
        }

        //Background music
        public void PlayMusic(IDAudioClip id, bool loop = true)
        {
            var clip = GetClip(id);

            if (clip == null) return;

            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }

        //Stop music
        public void StopMusic() => musicSource.Stop();

        //Play effects
        public void PlaySFX(IDAudioClip id)
        {
            Debug.Log("Audio: " + id);
            
            var clip = GetClip(id);

            if (clip == null) return;

            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        private AudioClip GetClip(IDAudioClip id)
        {
            if (clipMap == null || !clipMap.TryGetValue(id, out var clip))
            {
                Debug.LogWarning($"Clip with ID '{id}' not found in the library.");
                return null;
            }
            return clip;
        }

        //Control volumes dynamically
        public void SetMusicVolume(float value)
        {
            musicVolume = value;
            musicSource.volume = value;
        }

        public void SetSFXVolume(float value)
        {
            sfxVolume = value;
            sfxSource.volume = value;
        }
    }

    [System.Serializable]
    public class AudioEntry
    {
        public IDAudioClip id;
        public AudioClip clip;
    }

    public enum IDAudioClip
    {
        none,
        passTurn,
        placePiece,
        roundOver,
        selectPiece,
        shuffle,
        sorting,
        //buttonPress,
        //buttonHover,
        musicTheme_1,
        //musicTheme_2
    }
}
