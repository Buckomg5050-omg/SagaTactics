using UnityEngine;

public class GlobalSFXPlayer : MonoBehaviour
{
    public static GlobalSFXPlayer Instance { get; private set; }
    private AudioSource audioSource;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make it persist across scene loads if needed
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("GlobalSFXPlayer is missing an AudioSource component!", this);
            enabled = false; // Disable script if no AudioSource
        }
    }

    /// <summary>
    /// Plays a one-shot AudioClip at a specific world position.
    /// Assumes the AudioSource on this GameObject is set to 3D spatial blend.
    /// </summary>
    /// <param name="clipToPlay">The AudioClip to play.</param>
    /// <param name="position">The world position where the sound should emanate from.</param>
    /// <param name="volume">The volume to play the clip at (0.0 to 1.0).</param>
    public void PlaySFXAtPosition(AudioClip clipToPlay, Vector3 position, float volume = 1.0f)
    {
        if (clipToPlay == null || audioSource == null)
        {
            if(clipToPlay == null) Debug.LogWarning("GlobalSFXPlayer: Attempted to play a null AudioClip.", this);
            if(audioSource == null) Debug.LogWarning("GlobalSFXPlayer: AudioSource is null.", this);
            return;
        }

        // Set the position of this AudioSource temporarily to play the 3D sound from the right spot.
        // This is a simple way; a more advanced system might use multiple pooled AudioSources.
        transform.position = position; 
        audioSource.PlayOneShot(clipToPlay, volume);
        Debug.Log($"GlobalSFXPlayer: Playing '{clipToPlay.name}' at position {position} with volume {volume}.", this);
    }

    /// <summary>
    /// Plays a one-shot AudioClip as a 2D sound (ignoring position).
    /// </summary>
    /// <param name="clipToPlay">The AudioClip to play.</param>
    /// <param name="volume">The volume to play the clip at (0.0 to 1.0).</param>
    public void PlaySFX_2D(AudioClip clipToPlay, float volume = 1.0f)
    {
        if (clipToPlay == null || audioSource == null) return;
        
        // To ensure it's 2D for this call if the source is 3D, we could temporarily change spatial blend,
        // but PlayOneShot doesn't directly use the source's position if the source is already 2D.
        // A simpler approach for a dedicated 2D sound might be a separate AudioSource configured as 2D.
        // For now, let's assume if you call this, you intend it as a non-spatialized sound.
        // If audioSource.spatialBlend is 0, its position doesn't matter.
        audioSource.PlayOneShot(clipToPlay, volume);
        Debug.Log($"GlobalSFXPlayer: Playing 2D SFX '{clipToPlay.name}' with volume {volume}.", this);
    }
}