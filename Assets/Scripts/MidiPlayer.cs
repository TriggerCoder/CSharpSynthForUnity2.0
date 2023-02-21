using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AudioSynthesis.Bank;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Synthesis;
using CircularBuffer;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityMidi
{
    [RequireComponent(typeof(AudioSource))]
    public class MidiPlayer : MonoBehaviour
    {
        private const int BufferSize = 1024;

        public bool playOnAwake = true;
        public bool loop;
        public bool playCreatedMidi;
        public bool useAudioClip;
        public string midiFilePath;
        public string bankFile = "soundfonts/Scc1t2.sf2";
        public int midiNote = 60;
        public int midiVelocity = 80;

        public bool restart;
        public bool setPosition;
        public float setPositionTimeInSeconds;
        
        private AudioSource audioSource;
        private PatchBank bank;
        private MidiFile loadedMidiFile;
        private Synthesizer synthesizer;
        private MidiFileSequencer sequencer;
        private int midiSynthesizerChannelCount = 1;

        private bool isPlayingNote;

        private int audioFilterReadSampleRate; 
        private CircularBuffer<float> availableSingleChannelOutputSamples;
        
        public void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            
            audioFilterReadSampleRate = AudioSettings.outputSampleRate;
            // Synthesize samples in mono.
            synthesizer = new Synthesizer(audioFilterReadSampleRate, midiSynthesizerChannelCount, BufferSize, 1);
            sequencer = new MidiFileSequencer(synthesizer);

            availableSingleChannelOutputSamples = new CircularBuffer<float>(audioFilterReadSampleRate);

            LoadBank();
            if (playCreatedMidi)
            {
                LoadMidiFile(CreateMidiFile());
            }
            else
            {
                string midiFilePathWithFileExtension = AddFileExtensionIfNone(midiFilePath, ".mid");
                LoadMidiFile(new MidiFile(new StreamingAssetResource(midiFilePathWithFileExtension)));
            }

            restart = false;
            if (playOnAwake)
            {
                Play();
            }

            StartCoroutine(LogTime());
        }

        IEnumerator LogTime()
        {
            while (true)
            {
                yield return new WaitForSeconds(1);
                if (useAudioClip)
                {
                    Debug.Log($"AudioSource.time: {audioSource.time}");
                }
            }
        }

        private MidiFile CreateMidiFile()
        {
            MidiFile midiFile = new();

            List<MidiEvent> midiEvents = new();
            AddNoteOnOffEvents(midiEvents, 60, 0, 1000);
            AddNoteOnOffEvents(midiEvents, 62, 2000, 5000);
            AddNoteOnOffEvents(midiEvents, 60, 1000, 1000);
            midiFile.Tracks[0].MidiEvents = midiEvents.ToArray();
            
            return midiFile;
        }
        
        void AddNoteOnOffEvents(List<MidiEvent> midiEvents, byte pitch, int noteOnDeltaTimeInMillis, int noteLengthInMillis)
        {
            byte velocity = (byte)midiVelocity;
            MidiEvent noteOnEvent = MidiFileUtils.CreateNoteOnEvent(noteOnDeltaTimeInMillis, 0, pitch, velocity);
            midiEvents.Add(noteOnEvent);
            
            MidiEvent noteOffEvent = MidiFileUtils.CreateNoteOffEvent(noteLengthInMillis, 0, pitch, velocity);
            midiEvents.Add(noteOffEvent);
        }

        public void Update()
        {
            if (Input.GetMouseButton(0)
                && !isPlayingNote)
            {
                synthesizer.NoteOn(0, midiNote, midiVelocity);
                isPlayingNote = true;
            }
            else if(!Input.GetMouseButton(0)
                    && isPlayingNote)
            {
                synthesizer.NoteOff(0, midiNote);
                isPlayingNote = false;
            }

            if (restart)
            {
                restart = false;
                LoadMidiFile(loadedMidiFile);
                Play();
            }

            if (setPosition)
            {
                setPosition = false;
                
                if (useAudioClip
                    && audioSource.clip != null)
                {
                    audioSource.time = setPositionTimeInSeconds;
                }
                else
                {
                    sequencer.Seek(TimeSpan.FromSeconds(setPositionTimeInSeconds));
                }
            }
        }

        public void LoadBank()
        {
            bank = new PatchBank(bankFile);
            synthesizer.UnloadBank();
            synthesizer.LoadBank(bank);
        }

        public void StreamMidiFile(byte[] midiFileBytes)
        {
            LoadMidiFile(new MidiFile(midiFileBytes));
            Play();
        }

        public void LoadMidiFile(MidiFile midiFile)
        {
            Debug.Log("Loading midi");
            
            // ManipulateMidiFile(midiFile);
            
            synthesizer.NoteOffAll(true);
            sequencer.Stop();
            sequencer.UnloadMidi();
            sequencer.LoadMidi(midiFile);

            loadedMidiFile = midiFile;
        }

        private void ManipulateMidiFile(MidiFile midiFile)
        {
            int trackIndex = 0;
            int channelIndex = 3;
            SoloTrackAndChannel(midiFile, trackIndex, channelIndex);
            SetFirstDeltaTimeToZero(midiFile, trackIndex);
        }

        private void SetFirstDeltaTimeToZero(MidiFile midiFile, int trackIndex)
        {
            // Set delta time of fist note 0 to to start immediately.
            MidiTrack midiTrack = midiFile.Tracks[trackIndex];
            MidiEvent firstNoteOnEvent = midiTrack.MidiEvents.FirstOrDefault(midiEvent =>
                midiEvent.TryGetMidiEventTypeEnum(out MidiEventTypeEnum midiEventTypeEnum)
                && midiEventTypeEnum is MidiEventTypeEnum.NoteOn);
            MidiEvent firstNoteOffEvent = midiTrack.MidiEvents.FirstOrDefault(midiEvent =>
                midiEvent.TryGetMidiEventTypeEnum(out MidiEventTypeEnum midiEventTypeEnum)
                && midiEventTypeEnum is MidiEventTypeEnum.NoteOff);
            if (firstNoteOnEvent != null)
            {
                firstNoteOnEvent.DeltaTime = 0;
            }
        }

        private void SoloTrackAndChannel(MidiFile midiFile, int trackIndex, int channelIndex)
        {
            MidiTrack midiTrack = this.loadedMidiFile.Tracks[trackIndex];
            int deltaTimeOfRemovedMidiEvents = 0;
            List<MidiEvent> midiEventsToBeRemoved = new();
            midiTrack.MidiEvents.ToList().ForEach(midiEvent =>
            {
                bool isNoteOnOrNoteOffEvent = midiEvent.TryGetMidiEventTypeEnum(out MidiEventTypeEnum midiEventTypeEnum)
                                              && midiEventTypeEnum is MidiEventTypeEnum.NoteOn or MidiEventTypeEnum.NoteOff;

                if (!isNoteOnOrNoteOffEvent)
                {
                    return;
                }

                if (midiEvent.Channel == channelIndex)
                {
                    midiEvent.DeltaTime += deltaTimeOfRemovedMidiEvents;
                    deltaTimeOfRemovedMidiEvents = 0;
                } 
                else
                {
                    deltaTimeOfRemovedMidiEvents += midiEvent.DeltaTime;
                    midiEventsToBeRemoved.Add(midiEvent);
                }
            });
            
            midiTrack.MidiEvents = midiTrack.MidiEvents
                .Except(midiEventsToBeRemoved)
                .ToArray();

            this.loadedMidiFile.Tracks = new MidiTrack[] { midiTrack };
        }

        public void Play()
        {
            Debug.Log("Playing midi");

            if (useAudioClip)
            {
                int audioClipSampleRate = audioFilterReadSampleRate;
                double midiFileLengthInMillis = MidiFileUtils.GetMidiFileLengthInMillis(loadedMidiFile);
                Debug.Log("Midi file length in millis: " + midiFileLengthInMillis);
                int audioClipLengthInSamples = (int)((midiFileLengthInMillis / 1000.0) * audioClipSampleRate);
                audioSource.clip = AudioClip.Create("Midi", audioClipLengthInSamples, midiSynthesizerChannelCount, audioClipSampleRate, true, OnAudioClipRead, OnAudioClipSetPosition);
            }
            audioSource.Play();
            sequencer.Play();
        }

        private void OnAudioClipRead(float[] data)
        {
            FillOutputBuffer(data, midiSynthesizerChannelCount);
        }

        private void OnAudioClipSetPosition(int positionInSamples)
        {
            sequencer.SeekSampleTime(positionInSamples);
        }

        public void Stop()
        {
            audioSource.Stop();
            sequencer.Stop();
            sequencer.ResetMidi();
            sequencer.UnloadMidi();
        }

        void OnAudioFilterRead(float[] data, int outputChannelCount)
        {
            if (useAudioClip
                || loadedMidiFile == null)
            {
                return;
            }

            FillOutputBuffer(data, outputChannelCount);
        }

        private void FillOutputBuffer(float[] data, int outputChannelCount)
        {
            if (data == null)
            {
                return;
            }
            
            // Synthesize new samples from the Midi instrument until there is enough to fill the data array.
            int neededSingleChannelSamples = data.Length / outputChannelCount;
            if (neededSingleChannelSamples >= availableSingleChannelOutputSamples.Capacity)
            {
                Debug.LogWarning($"available sample capacity is too small. Samples needed: {neededSingleChannelSamples}, capacity: {availableSingleChannelOutputSamples.Capacity}");
                neededSingleChannelSamples = availableSingleChannelOutputSamples.Capacity - 1;
            }
            while (availableSingleChannelOutputSamples.Size < neededSingleChannelSamples)
            {
                sequencer.FillMidiEventQueue(loop);
                synthesizer.GetNext();
                for (int i = 0; i < synthesizer.sampleBuffer.Length; i++)
                {
                    availableSingleChannelOutputSamples.PushBack(synthesizer.sampleBuffer[i]);
                }
            }

            // The Midi stream is generated in mono (1 channel).
            // These samples are written to every channel of the output data array.
            for (int outputSampleIndex = 0; outputSampleIndex < data.Length && !availableSingleChannelOutputSamples.IsEmpty; outputSampleIndex += outputChannelCount)
            {
                float sampleValue = availableSingleChannelOutputSamples.Front();
                availableSingleChannelOutputSamples.PopFront();

                for (int outputChannelIndex = 0; outputChannelIndex < outputChannelCount; outputChannelIndex++)
                {
                    data[outputSampleIndex + outputChannelIndex] = sampleValue;
                }
            }
        }

        private static string AddFileExtensionIfNone(string path, string fileExtension)
        {
            if (!path.ToLowerInvariant().EndsWith(fileExtension.ToLowerInvariant()))
            {
                return path + fileExtension;
            }

            return path;
        }

        private void OnDestroy()
        {
            Stop();

            // Destroy manually created AudioClip
            Destroy(audioSource.clip);
        }
    }
}