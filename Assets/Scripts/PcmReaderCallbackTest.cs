using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PcmReaderCallbackTest : MonoBehaviour
{
    public int sampleRate = 44100;
    public int outputChannelCount = 2;
    public int lengthInSamples = 2048;
    
    private AudioSource audioSource;
    private int totalSampleIndex;

    private bool shouldPlay;
    private bool oldShouldPlay;

    private bool filledSound;
    private bool oldFilledSound;
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Comment out this line to use OnAudioFilterRead instead of OnAudioRead
        audioSource.clip = AudioClip.Create("PcmReaderCallbackTest", lengthInSamples, outputChannelCount, sampleRate, true, OnAudioRead);
        
        audioSource.Play();
    }

    private void Update()
    {
        shouldPlay = Input.GetMouseButton(0);
        if (shouldPlay != oldShouldPlay)
        {
            Debug.Log("shouldPlay: " + shouldPlay);
            oldShouldPlay = shouldPlay;
        }
    }
    
    private void OnAudioRead(float[] data)
    {
        FillBuffer(data, outputChannelCount);
    }

    // Uncomment this method to use OnAudioFilterRead instead of OnAudioRead
    // private void OnAudioFilterRead(float[] data, int channelCount)
    // {
    //     FillBuffer(data, channelCount);
    // }
    
    private void FillBuffer(float[] data, int channelCount)
    {
        if (!shouldPlay)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 0;
            }

            filledSound = false;
            if (filledSound != oldFilledSound)
            {
                Debug.Log("filledSound: " + filledSound);
                oldFilledSound = filledSound;
            }
            return;
        }

        int frequency = 440;
        for (int sampleIndex = 0; sampleIndex < data.Length; sampleIndex += channelCount)
        {
            for (int channelIndex = 0; channelIndex < outputChannelCount; channelIndex++)
            {
                data[sampleIndex + channelIndex] = Mathf.Sin(2 * Mathf.PI * frequency * totalSampleIndex / sampleRate);
            }
            data[sampleIndex] = Mathf.Sin(2 * Mathf.PI * frequency * totalSampleIndex / sampleRate);
            totalSampleIndex++;
        }
        
        filledSound = true;
        if (filledSound != oldFilledSound)
        {
            Debug.Log("filledSound: " + filledSound);
            oldFilledSound = filledSound;
        }
    }
}