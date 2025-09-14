using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("BGM Audio Source")]
    public AudioSource bgmAudioSource;
    [Space]
    [Tooltip("SFX Audio Source")]
    public AudioSource sfxAudioSource;
    [Tooltip("SFX Audio Source")]
    public AudioSource sfxAudioSourceReserve;
    [Tooltip("SFX")]
    public AudioClip[] sfx;
    [Space]
    [Tooltip("动物的 SFX Audio Source")]
    public AudioSource animalSFXAudioSource;
    [Tooltip("动物的 SFX")]
    public AudioClip[] animalSFX;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TurnManager] 已存在另一个实例，当前实例将被摧毁。");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public void PlaySFX(int index, float v = 1f, float p = 1f, bool b = true)
    {
        if(b)
        {
            sfxAudioSource.clip = sfx[index];
            sfxAudioSource.volume = v;
            sfxAudioSource.pitch = p;
            sfxAudioSource.Play();
        }
        else
        {
            sfxAudioSourceReserve.clip = sfx[index];
            sfxAudioSourceReserve.volume = v;
            sfxAudioSourceReserve.pitch = p;
            sfxAudioSourceReserve.Play();
        }
    }

    public void PlayAnimalSFX(int index, float v = 1f, float p = 1f)
    {
        animalSFXAudioSource.clip = animalSFX[index];
        animalSFXAudioSource.volume = v;
        animalSFXAudioSource.pitch = p;
        animalSFXAudioSource.Play();
    }

}
