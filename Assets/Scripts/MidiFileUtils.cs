﻿using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;

namespace UnityMidi
{
    public static class MidiFileUtils
    {
        public static int GetMidiFileLengthInMillis(MidiFile midiFile)
        {
            int lengthInMillis = midiFile.Tracks
                .Select(track => track.EndTime)
                .Max();
            return lengthInMillis;
        }

        public static MidiEvent CreateNoteOnEvent(int deltaTime, byte channel, byte pitch, byte velocity)
        {
            int status = 0x90 | channel;
            return new MidiEvent(deltaTime, (byte)status, pitch, velocity);
        }

        public static MidiEvent CreateNoteOffEvent(int deltaTime, byte channel, byte pitch, byte velocity)
        {
            int status = 0x80 | channel;
            return new MidiEvent(deltaTime, (byte)status, pitch, velocity);
        }
    }
}