using UnityEngine;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    private AudioSource audioSource;

    public AudioClip bgmClip;
    [Range(0f, 1f)]
    public float bgmVolume = 0.5f;

    void Awake()
    {
        //Singleton (if it plays again, than destroy)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);


        audioSource = GetComponent<AudioSource>();

        audioSource.clip = bgmClip;
        audioSource.volume = bgmVolume;
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        audioSource.Play(); //Startplayin
    }

    public void ChangeBGM(AudioClip newClip)
    {
        if (audioSource.clip == newClip && audioSource.isPlaying)
            return;

        audioSource.clip = newClip;
        audioSource.Play();
    }

    public void SetVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        audioSource.volume = bgmVolume;
    }
}

