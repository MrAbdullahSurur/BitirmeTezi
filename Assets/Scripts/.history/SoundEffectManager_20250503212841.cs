using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;
using Unity.Netcode;

public class SoundEffectManager : MonoBehaviour
{
    // Singleton instance
    public static SoundEffectManager Instance { get; private set; }
    
    [System.Serializable]
    public class SoundEffect
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitch = 1f;
        [Range(0f, 0.5f)] public float randomPitchVariation = 0.1f;
        public bool loop = false;
        [Range(0f, 1f)] public float spatialBlend = 0f; // 0 = 2D, 1 = 3D
        [Range(0f, 500f)] public float maxDistance = 50f;
        
        [HideInInspector] public AudioSource source;
    }
    
    [Header("Sound Effects")]
    [SerializeField] private List<SoundEffect> soundEffects = new List<SoundEffect>();
    
    [Header("Audio Settings")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private int audioSourcePoolSize = 80;
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [SerializeField] private bool muteAllSounds = false;
    
    [Header("Sound Limiting")]
    [SerializeField] private int maxInstancesOfSameSound = 6;  // Maximum instances of the same sound
    [SerializeField] private float minTimeBetweenSameSounds = 0.05f;  // Minimum time between identical sounds
    
    // Pool of audio sources for playing multiple sounds simultaneously
    private List<AudioSource> audioSourcePool = new List<AudioSource>();
    
    // Dictionary for quick lookup of sound effects by name
    private Dictionary<string, SoundEffect> soundEffectDictionary = new Dictionary<string, SoundEffect>();
    
    // Dictionary to track active background/environment sounds
    private Dictionary<string, AudioSource> activeBackgroundSounds = new Dictionary<string, AudioSource>();
    
    // Dictionary to track active sound instances per sound type
    private Dictionary<string, int> activeSoundInstances = new Dictionary<string, int>();
    // Dictionary to track last time a sound was played
    private Dictionary<string, float> lastPlayTimes = new Dictionary<string, float>();
    
    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize the audio source pool
        InitializeAudioSourcePool();
        
        // Build the dictionary for quick lookup
        foreach (SoundEffect sound in soundEffects)
        {
            if (!soundEffectDictionary.ContainsKey(sound.name))
            {
                soundEffectDictionary.Add(sound.name, sound);
            }
            else
            {
                Debug.LogWarning($"Duplicate sound effect name: {sound.name}. Only the first one will be used.");
            }
        }
        
        // Add this to your Awake method after building the dictionary
        Debug.Log($"SoundEffectManager initialized with {soundEffectDictionary.Count} sounds: {string.Join(", ", soundEffectDictionary.Keys)}");
    }
    
    private void Start()
    {
        // Ensure background music starts playing only on the host/server
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // Check if the sound effect exists before trying to play it
            if (HasSoundEffect("BackgroundMusic"))
            {
                PlayBackgroundSound("BackgroundMusic");
                Debug.Log("Host/Server attempting to play BackgroundMusic.");
            }
            else
            {
                Debug.LogWarning("SoundEffect 'BackgroundMusic' not found in the SoundEffectManager list!");
            }
        }
        else
        {
            Debug.Log("Client instance - skipping automatic background music start.");
        }
    }
    
    private void InitializeAudioSourcePool()
    {
        // Create a child object to hold all audio sources
        Transform audioSourceContainer = new GameObject("Audio Source Pool").transform;
        audioSourceContainer.SetParent(transform);
        
        // Create the pool of audio sources
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            GameObject audioSourceObj = new GameObject($"Audio Source {i}");
            audioSourceObj.transform.SetParent(audioSourceContainer);
            
            AudioSource source = audioSourceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            
            if (sfxMixerGroup != null)
            {
                source.outputAudioMixerGroup = sfxMixerGroup;
            }
            
            audioSourcePool.Add(source);
        }
    }
    
    // Get an available audio source from the pool
    private AudioSource GetAvailableAudioSource()
    {
        // First, try to find an audio source that's not playing
        foreach (AudioSource source in audioSourcePool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        
        // If all sources are in use, find the one that started playing the longest time ago
        AudioSource oldestSource = audioSourcePool[0];
        float oldestPlayTime = float.MaxValue;
        
        foreach (AudioSource source in audioSourcePool)
        {
            // Get the time position in the clip
            float playTime = source.time;
            
            if (playTime < oldestPlayTime)
            {
                oldestPlayTime = playTime;
                oldestSource = source;
            }
        }
        
        // Stop the oldest source and return it
        oldestSource.Stop();
        return oldestSource;
    }
    
    // Play a sound effect by name
    public AudioSource PlaySound(string soundName)
    {
        return PlaySound(soundName, Vector3.zero, false);
    }
    
    // Play a sound effect at a specific position
    public AudioSource PlaySound(string soundName, Vector3 position = default, bool is3D = false)
    {
        // Check if sound exists
        if (!soundEffectDictionary.TryGetValue(soundName, out SoundEffect sound))
        {
            Debug.LogWarning($"Sound effect '{soundName}' not found!");
            return null;
        }
        
        // Don't play if muted - add debug to verify this check
        if (muteAllSounds)
        {
            Debug.Log($"Skipping sound '{soundName}' because sounds are muted");
            return null;
        }
        
        // Get an available audio source
        AudioSource audioSource = GetAvailableAudioSource();
        
        // Configure the audio source
        audioSource.clip = sound.clip;
        audioSource.volume = sound.volume * masterVolume;
        
        // Apply random pitch variation if specified
        float randomPitch = Random.Range(-sound.randomPitchVariation, sound.randomPitchVariation);
        audioSource.pitch = sound.pitch + randomPitch;
        
        audioSource.loop = sound.loop;
        
        // Set spatial settings
        if (is3D)
        {
            audioSource.spatialBlend = sound.spatialBlend;
            audioSource.maxDistance = sound.maxDistance;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.transform.position = position;
        }
        else
        {
            audioSource.spatialBlend = 0f; // 2D sound
        }
        
        // Play the sound
        audioSource.Play();
        
        return audioSource;
    }
    
    // Stop a specific sound
    public void StopSound(string soundName)
    {
        foreach (AudioSource source in audioSourcePool)
        {
            if (source.isPlaying && source.clip != null && source.clip.name == soundName)
            {
                source.Stop();
            }
        }
    }
    
    // Stop all sounds
    public void StopAllSounds()
    {
        Debug.Log($"Stopping all sounds, found {audioSourcePool.Count} sources");
        
        int stoppedCount = 0;
        // Stop all audio sources in the pool
        foreach (AudioSource source in audioSourcePool)
        {
            if (source != null && source.isPlaying)
            {
                source.Stop();
                stoppedCount++;
            }
        }
        
        Debug.Log($"Stopped {stoppedCount} playing sounds");
        
        // Handle background sounds
        foreach (var kvp in activeBackgroundSounds)
        {
            if (kvp.Value != null && kvp.Value.isPlaying)
            {
                Debug.Log($"Stopping background sound: {kvp.Key}");
                kvp.Value.Stop();
            }
        }
    }
    
    // Play a sound as a background/environment sound
    public AudioSource PlayBackgroundSound(string soundName, float fadeInTime = 0f)
    {
        // If this background sound is already playing AND the source is valid, just return its source
        if (activeBackgroundSounds.TryGetValue(soundName, out AudioSource existingSource) && existingSource != null && existingSource.isPlaying)
        {
            Debug.Log($"PlayBackgroundSound: '{soundName}' is already playing. Returning existing source.");
            return existingSource;
        }
        
        // Check if sound exists
        if (!soundEffectDictionary.TryGetValue(soundName, out SoundEffect sound))
        {
            Debug.LogWarning($"Sound effect '{soundName}' not found!");
            return null;
        }
        
        // Don't play if muted
        if (muteAllSounds)
            return null;
        
        // Get an available audio source
        AudioSource audioSource = GetAvailableAudioSource();
        
        // Configure the audio source
        audioSource.clip = sound.clip;
        audioSource.volume = fadeInTime > 0 ? 0 : sound.volume * masterVolume; // Start at 0 volume if fading in
        audioSource.pitch = sound.pitch;
        audioSource.loop = true; // Force loop for background sounds
        audioSource.spatialBlend = 0f; // Keep background sounds as 2D
        
        // Log details before playing
        Debug.Log($"PlayBackgroundSound: Playing '{soundName}'. Muted: {muteAllSounds}, Source Volume: {audioSource.volume}, Source Pitch: {audioSource.pitch}, Clip: {(audioSource.clip != null ? audioSource.clip.name : "NULL")}");

        // Play the sound
        audioSource.Play();
        
        // Add to active background sounds dictionary
        if (activeBackgroundSounds.ContainsKey(soundName))
        {
            activeBackgroundSounds[soundName] = audioSource;
        }
        else
        {
            activeBackgroundSounds.Add(soundName, audioSource);
        }
        
        // Handle fade in if needed
        if (fadeInTime > 0)
        {
            StartCoroutine(FadeInSound(audioSource, sound.volume * masterVolume, fadeInTime));
        }
        
        return audioSource;
    }
    
    // Fade in a sound gradually
    private IEnumerator FadeInSound(AudioSource source, float targetVolume, float duration)
    {
        float startVolume = source.volume;
        float timer = 0f;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, timer / duration);
            yield return null;
        }
        
        source.volume = targetVolume;
    }
    
    // Stop a specific background sound with optional fade out
    public void StopBackgroundSound(string soundName, float fadeOutTime = 0f)
    {
        if (!activeBackgroundSounds.TryGetValue(soundName, out AudioSource source) || !source.isPlaying)
        {
            return;
        }
        
        if (fadeOutTime > 0)
        {
            StartCoroutine(FadeOutAndStop(source, fadeOutTime, soundName));
        }
        else
        {
            source.Stop();
            activeBackgroundSounds.Remove(soundName);
        }
    }
    
    // Fade out and stop a sound
    private IEnumerator FadeOutAndStop(AudioSource source, float duration, string soundName)
    {
        float startVolume = source.volume;
        float timer = 0f;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
            yield return null;
        }
        
        source.Stop();
        if (activeBackgroundSounds.ContainsKey(soundName))
        {
            activeBackgroundSounds.Remove(soundName);
        }
    }
    
    // Set the volume of a specific background sound
    public void SetBackgroundSoundVolume(string soundName, float volume, float fadeTime = 0f)
    {
        if (!activeBackgroundSounds.TryGetValue(soundName, out AudioSource source) || !source.isPlaying)
        {
            return;
        }
        
        float targetVolume = volume * masterVolume;
        
        if (fadeTime > 0)
        {
            StartCoroutine(FadeVolume(source, targetVolume, fadeTime));
        }
        else
        {
            source.volume = targetVolume;
        }
    }
    
    // Fade volume to a target value
    private IEnumerator FadeVolume(AudioSource source, float targetVolume, float duration)
    {
        float startVolume = source.volume;
        float timer = 0f;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, timer / duration);
            yield return null;
        }
        
        source.volume = targetVolume;
    }
    
    // Stop all background sounds
    public void StopAllBackgroundSounds(float fadeOutTime = 0f)
    {
        foreach (var soundPair in activeBackgroundSounds.ToList())
        {
            StopBackgroundSound(soundPair.Key, fadeOutTime);
        }
    }
    
    // Set the master volume
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        
        // Update volume of all currently playing sounds
        foreach (AudioSource source in audioSourcePool)
        {
            if (source.isPlaying)
            {
                // Find the original sound effect to get its base volume
                foreach (SoundEffect sound in soundEffects)
                {
                    if (source.clip == sound.clip)
                    {
                        source.volume = sound.volume * masterVolume;
                        break;
                    }
                }
            }
        }
    }
    
    // Mute/unmute all sounds
    public void SetMute(bool mute)
    {
        Debug.Log($"SetMute called with: {mute}, previous state: {muteAllSounds}");
        
        // Only take action if the state is changing
        if (mute == muteAllSounds)
        {
            Debug.Log("Mute state unchanged, taking no action");
            return;
        }
        
        muteAllSounds = mute;
        
        // If muting, stop all playing sounds
        if (muteAllSounds)
        {
            Debug.Log("Muting all sounds - stopping all currently playing sounds");
            StopAllSounds(); // This stops one-shots and background sounds
        }
        else
        {
            Debug.Log("Unmuting sounds - allowing new sounds to play");
            
            // NOTE: We don't automatically restart sounds here.
            // GameManager.TryStartAmbientSound handles the main ambient track.
            // Other systems should request their sounds again if needed.
            
            // This loop is okay for potentially resuming other specific background sounds
            // if they were managed differently, but the main ambient is handled by GameManager.
            /*
            foreach (var kvp in activeBackgroundSounds)
            {
                if (kvp.Value != null && !kvp.Value.isPlaying) // Check if not already playing
                {
                    // Check if this sound *should* be playing (e.g., based on game state)
                    // For now, we only explicitly restart the main ambient via GameManager
                    // Debug.Log($"Restarting background sound: {kvp.Key}");
                    // kvp.Value.Play();
                }
            }
            */
        }
    }
    
    // Add a new sound effect at runtime
    public void AddSoundEffect(string name, AudioClip clip, float volume = 1f, float pitch = 1f, bool loop = false)
    {
        if (soundEffectDictionary.ContainsKey(name))
        {
            Debug.LogWarning($"Sound effect '{name}' already exists. Use UpdateSoundEffect instead.");
            return;
        }
        
        SoundEffect newSound = new SoundEffect
        {
            name = name,
            clip = clip,
            volume = volume,
            pitch = pitch,
            loop = loop
        };
        
        soundEffects.Add(newSound);
        soundEffectDictionary.Add(name, newSound);
    }
    
    // Update an existing sound effect
    public void UpdateSoundEffect(string name, AudioClip clip = null, float? volume = null, float? pitch = null, bool? loop = null)
    {
        if (!soundEffectDictionary.TryGetValue(name, out SoundEffect sound))
        {
            Debug.LogWarning($"Sound effect '{name}' not found. Cannot update.");
            return;
        }
        
        if (clip != null) sound.clip = clip;
        if (volume.HasValue) sound.volume = volume.Value;
        if (pitch.HasValue) sound.pitch = pitch.Value;
        if (loop.HasValue) sound.loop = loop.Value;
    }
    
    // Add this method to help debug sound issues
    public bool HasSoundEffect(string name)
    {
        return soundEffectDictionary.ContainsKey(name);
    }
    
    // Play a sound with specific pitch
    public AudioSource PlaySoundWithPitch(string soundName, float pitch)
    {
        // Check if sound exists
        if (!soundEffectDictionary.TryGetValue(soundName, out SoundEffect sound))
        {
            Debug.LogWarning($"Sound effect '{soundName}' not found!");
            return null;
        }
        
        // Don't play if muted
        if (muteAllSounds)
            return null;
        
        // Initialize tracking dictionaries if needed
        if (!activeSoundInstances.ContainsKey(soundName))
            activeSoundInstances[soundName] = 0;
        if (!lastPlayTimes.ContainsKey(soundName))
            lastPlayTimes[soundName] = -100f; // Far in the past
        
        // Check if we're exceeding the maximum instances of this sound
        if (activeSoundInstances[soundName] >= maxInstancesOfSameSound)
        {
            // Too many instances, don't play another one
            return null;
        }
        
        // Check if we're trying to play the sound too quickly after the last instance
        if (Time.time - lastPlayTimes[soundName] < minTimeBetweenSameSounds)
        {
            // Too soon, don't play another one
            return null;
        }
        
        // Update tracking
        activeSoundInstances[soundName]++;
        lastPlayTimes[soundName] = Time.time;
        
        // Get an available audio source
        AudioSource audioSource = GetAvailableAudioSource();
        
        // Configure the audio source
        audioSource.clip = sound.clip;
        audioSource.volume = sound.volume * masterVolume;
        audioSource.pitch = pitch;
        audioSource.loop = sound.loop;
        audioSource.spatialBlend = 0f; // 2D sound for UI
        
        // Play the sound
        audioSource.Play();
        
        // Start a coroutine to track when this sound instance ends
        StartCoroutine(TrackSoundCompletion(soundName, audioSource));
        
        return audioSource;
    }
    
    // Add this coroutine to track when a sound completes
    private IEnumerator TrackSoundCompletion(string soundName, AudioSource source)
    {
        // Wait until the sound is no longer playing
        while (source != null && source.isPlaying)
        {
            yield return null;
        }
        
        // Decrement the active instance count
        if (activeSoundInstances.ContainsKey(soundName))
        {
            activeSoundInstances[soundName] = Mathf.Max(0, activeSoundInstances[soundName] - 1);
        }
    }
    
    public AudioSource PlaySoundWithPitchAndVolume(string soundName, float pitch, float volumeMultiplier)
    {
        // Similar to PlaySoundWithPitch but with an added volume multiplier
        if (!soundEffectDictionary.TryGetValue(soundName, out SoundEffect sound))
        {
            Debug.LogWarning($"Sound effect '{soundName}' not found!");
            return null;
        }
        
        // Don't play if muted
        if (muteAllSounds)
            return null;
        
        // Get an available audio source
        AudioSource audioSource = GetAvailableAudioSource();
        
        // Configure the audio source with the volume multiplier
        audioSource.clip = sound.clip;
        audioSource.volume = sound.volume * masterVolume * volumeMultiplier;
        audioSource.pitch = pitch;
        audioSource.loop = sound.loop;
        audioSource.spatialBlend = 0f;
        
        // Play the sound
        audioSource.Play();
        
        return audioSource;
    }
} 