using AudioSynthesis.Midi.Event;

public static class MidiEventUtils
{
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