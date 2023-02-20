using System;
using System.Collections.Generic;
using System.Linq;
using AudioSynthesis.Bank;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Synthesis;
using CircularBuffer;
using UnityEngine;

namespace UnityMidi
{
    [RequireComponent(typeof(AudioSource))]
    public class MidiPlayer : MonoBehaviour
    {
	    public bool playCreatedMidi;
	    public bool restart;

	    public string banksourcefile = "soundfonts/Scc1t2.sf2";
		public string midiFilePath;
		[SerializeField] bool loadOnAwake = true;
		[SerializeField] bool loop = true;
		[SerializeField] bool playOnStart = true;
        [SerializeField] int bufferSize = 1024;
        PatchBank bank;
        MidiFile midi;
        Synthesizer synthesizer;
        AudioSource audioSource;
        MidiFileSequencer sequencer;
        
        public AudioSource AudioSource { get { return audioSource; } }

        public MidiFileSequencer Sequencer { get { return sequencer; } }

        public PatchBank Bank { get { return bank; } }

        public MidiFile MidiFile { get { return midi; } }

        public int midiNote = 60;
        public int midiVelocity = 80;
        
        private bool isPlayingNote;

        private bool isInitialized;
        
        private int audioFilterReadSampleRateHz; 
        private CircularBuffer<float> availableSingleChannelOutputSamples;
        
        public void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            
            audioFilterReadSampleRateHz = AudioSettings.outputSampleRate;
            // Synthesize samples in mono.
            int synthesizerChannelCount = 1;
            synthesizer = new Synthesizer(audioFilterReadSampleRateHz, synthesizerChannelCount, bufferSize, 1);
            sequencer = new MidiFileSequencer(synthesizer);

            availableSingleChannelOutputSamples = new CircularBuffer<float>(bufferSize * 2);

			if (loadOnAwake)
			{
				LoadBank();
				if (playCreatedMidi)
				{
					LoadMidi(CreateMidiFile());
				}
				else
				{
					LoadMidi(new MidiFile(new StreamingAssetResource(midiFilePath)));
				}
			}

			restart = false;
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
	        MidiEvent noteOnEvent = MidiEventUtils.CreateNoteOnEvent(noteOnDeltaTimeInMillis, 0, pitch, velocity);
	        midiEvents.Add(noteOnEvent);
	        
	        MidiEvent noteOffEvent = MidiEventUtils.CreateNoteOffEvent(noteLengthInMillis, 0, pitch, velocity);
	        midiEvents.Add(noteOffEvent);
        }
        
        public void Start()
		{
			if (playOnStart)
			{
				Play();
			}
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
				LoadMidi(midi);
				Play();
			}
		}

		public void LoadBank()
		{
			bank = new PatchBank(banksourcefile);
			synthesizer.UnloadBank();
			synthesizer.LoadBank(bank);
		}

		public void StreamMidi(byte[] Midisong)
		{
			LoadMidi(new MidiFile(Midisong));
			Play();
		}

        public void LoadMidi(MidiFile midi)
        {
	        Debug.Log("Loading midi");
            
	        this.midi = midi;

            int trackIndex = 0;
            int channelIndex = 3;
            // SoloTrackAndChannel(this.midi, trackIndex, channelIndex);
            // SetFirstDeltaTimeToZero(midi, trackIndex);
            
            synthesizer.NoteOffAll(true);
            sequencer.Stop();
            sequencer.UnloadMidi();
            sequencer.LoadMidi(midi);

            isInitialized = true;
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
	        MidiTrack midiTrack = midi.Tracks[trackIndex];
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

	        midi.Tracks = new MidiTrack[] { midiTrack };
        }

        public void Play()
        {
			Debug.Log("Playing midi");
			
			// audioSource.clip = AudioClip.Create("Midi", bufferSize, channel, sampleRate, true, OnAudioRead);
            audioSource.Play();
			sequencer.Play();
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
	        if (!isInitialized)
	        {
	            return;
	        }

	        // Synthesize new samples from the Midi instrument until there is enough to fill the data array.
	        int neededSingleChannelSamples = data.Length / outputChannelCount;
	        if (neededSingleChannelSamples >= availableSingleChannelOutputSamples.Capacity)
	        {
		        Debug.LogWarning("available sample capacity is too small.");
		        neededSingleChannelSamples = availableSingleChannelOutputSamples.Capacity - 1;
	        }
	        while (availableSingleChannelOutputSamples.Count < neededSingleChannelSamples)
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
    }
}