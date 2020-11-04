using UnityEngine;
using System.IO;
using System.Collections;
using AudioSynthesis;
using AudioSynthesis.Bank;
using AudioSynthesis.Synthesis;
using AudioSynthesis.Sequencer;
using AudioSynthesis.Midi;
using System;
using System.Collections.Generic;

namespace UnityMidi
{
    [RequireComponent(typeof(AudioSource))]
    public class MidiPlayer : MonoBehaviour
    {
		public string banksourcefile = "soundfonts/Scc1t2.sf2";
		[SerializeField] StreamingAssetResouce midiSource;
		[SerializeField] bool loadOnAwake = true;
		[SerializeField] bool loop = true;
		[SerializeField] bool playOnStart = true;
        [SerializeField] int channel = 2;
        [SerializeField] int sampleRate = 44100;
        [SerializeField] int bufferSize = 1024;
        PatchBank bank;
        MidiFile midi;
        Synthesizer synthesizer;
        AudioSource audioSource;
        MidiFileSequencer sequencer;
        int bufferHead = 0;
        float[] currentBuffer;

        public AudioSource AudioSource { get { return audioSource; } }

        public MidiFileSequencer Sequencer { get { return sequencer; } }

        public PatchBank Bank { get { return bank; } }

        public MidiFile MidiFile { get { return midi; } }

        public void Awake()
        {
            synthesizer = new Synthesizer(sampleRate, channel, bufferSize, 1);
            sequencer = new MidiFileSequencer(synthesizer);
            audioSource = GetComponent<AudioSource>();

			if (loadOnAwake)
			{
				LoadBank();
				LoadMidi(new MidiFile(midiSource));
			}
        }

		public void Start()
		{
			if (playOnStart)
			{
				Play();
			}
		}

		public void LoadBank()
		{
			LoadBank(new PatchBank(banksourcefile));
		}

        public void LoadBank(PatchBank bank)
        {
            this.bank = bank;
            synthesizer.UnloadBank();
            synthesizer.LoadBank(bank);
        }

		public void StreamMidi (byte[] Midisong)
		{
			LoadMidi(new MidiFile(Midisong));
			Play();
		}
        public void LoadMidi(MidiFile midi)
        {
            this.midi = midi;
            sequencer.Stop();
            sequencer.UnloadMidi();
            sequencer.LoadMidi(midi);
        }

        public void Play()
        {
			audioSource.clip = AudioClip.Create("Midi", bufferSize, channel, sampleRate, true, OnAudioRead);
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
			
		void FillBuffer()
		{					
			sequencer.FillMidiEventQueue(loop);
			synthesizer.GetNext();
			currentBuffer = synthesizer.WorkingBuffer;
			bufferHead = 0;
		}
			
		void OnAudioRead(float[] data)
        {
            int count = 0;

			while (count < data.Length)
			{
				if (currentBuffer == null)
					FillBuffer();
				else if (bufferHead >= currentBuffer.Length)
					FillBuffer();
				var length = Mathf.Min(currentBuffer.Length - bufferHead, data.Length - count);
				Array.Copy(currentBuffer, bufferHead, data, count, length);
				bufferHead += length;
				count += length;
			}
        }
    }
}
