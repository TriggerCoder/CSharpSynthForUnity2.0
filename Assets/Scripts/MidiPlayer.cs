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
		private const int BufferSize = 1024;

	    public bool restart;
		public bool playOnAwake = true;
		public bool loop = true;
	    public bool playCreatedMidi;
		public string midiFilePath;
	    public string bankFile = "soundfonts/Scc1t2.sf2";
        public int midiNote = 60;
        public int midiVelocity = 80;
        
        private AudioSource audioSource;
		private PatchBank bank;
        private MidiFile loadedMidiFile;
        private Synthesizer synthesizer;
        private MidiFileSequencer sequencer;

        private bool isPlayingNote;
    
        private int audioFilterReadSampleRateHz; 
        private CircularBuffer<float> availableSingleChannelOutputSamples;
        
        public void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            
            audioFilterReadSampleRateHz = AudioSettings.outputSampleRate;
            // Synthesize samples in mono.
            int synthesizerChannelCount = 1;
            synthesizer = new Synthesizer(audioFilterReadSampleRateHz, synthesizerChannelCount, BufferSize, 1);
            sequencer = new MidiFileSequencer(synthesizer);

            availableSingleChannelOutputSamples = new CircularBuffer<float>(BufferSize * 2);

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
	        if (loadedMidiFile == null)
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
		
		private static string AddFileExtensionIfNone(string path, string fileExtension)
		{
			if (!path.ToLowerInvariant().EndsWith(fileExtension.ToLowerInvariant()))
			{
				return path + fileExtension;
			}

			return path;
		}
    }
}