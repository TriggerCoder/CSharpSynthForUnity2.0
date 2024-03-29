﻿using System;
using AudioSynthesis.Midi.Event;

namespace AudioSynthesis.Midi
{
    public class MidiTrack
    {
        private int notesPlayed;
        private int totalTime;
        private int activeChannels;
        private byte[] instPrograms;
        private byte[] drumPrograms;   
        private MidiEvent[] midiEvents;

        public int NoteOnCount
        {
            get { return notesPlayed; }
            set { notesPlayed = value; }
        }
        public int EndTime
        {
            get { return totalTime; }
            set { totalTime = value; }
        }
        public int ActiveChannels 
        {
            get { return activeChannels; }
            set { activeChannels = value; }
        }
        public MidiEvent[] MidiEvents
        {
            get { return midiEvents; }
            set { midiEvents = value; }
        }
        public byte[] Instruments
        {
            get { return instPrograms; }
        }
        public byte[] DrumInstruments
        {
            get { return drumPrograms; }
        }

        public MidiTrack()
            : this(new byte[] { 0 }, new byte[] { 0 }, Array.Empty<MidiEvent>())
        {
        }
        
        public MidiTrack(byte[] instPrograms, byte[] drumPrograms, MidiEvent[] midiEvents)
        {
            this.instPrograms = instPrograms;
            this.drumPrograms = drumPrograms;
            this.midiEvents = midiEvents;
            this.notesPlayed = 0;
            this.totalTime = 0;
            this.activeChannels = 0;
        }
        public bool isChannelActive(int channel)
        {
            return ((activeChannels >> channel) & 1) == 1;
        }
        public override string ToString()
        {
            return "MessageCount: " + midiEvents.Length + ", TotalTime: " + totalTime;
        }
    }
}