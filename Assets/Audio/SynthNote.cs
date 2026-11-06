using System;
using UnityEngine;

namespace Bagel
{
    public enum SynthWave : byte
    {
        Sine,
        Triangle,
        Square,
        Saw,
        Noise
    }

    public enum SynthFilter : byte
    {
        None,
        LowPass,
        BandPass,
        HighPass
    }

    /// <summary>
    /// One voice of a sound: an oscillator (or noise), an optional FM modulator, pitch glide and vibrato, an
    /// optional resonant filter with a cutoff sweep, and an envelope (attack, then an exponential decay that
    /// reaches -60 dB after <see cref="decay"/> seconds). A sound is a few notes, each with its own delay.
    /// </summary>
    /// <remarks>
    /// Plain unmanaged data, so a note travels as a message from the main thread to the audio thread, where
    /// <see cref="SynthEngine"/> renders it. Build notes with <see cref="Tone"/> or <see cref="Noise"/> and the
    /// chained setters, e.g. <c>SynthNote.Tone(SynthWave.Sine, 400f, 0.08f, 0.5f).GlideTo(900f, 0.035f)</c>.
    /// </remarks>
    [Serializable]
    public struct SynthNote
    {
        /// <summary>Seconds after the start of the sound.</summary>
        public float delay;
        public SynthWave wave;
        /// <summary>Hz (ignored by noise).</summary>
        public float frequency;
        /// <summary>Hz the pitch glides to, exponentially, over <see cref="glide"/> seconds (0: no glide).</summary>
        public float frequencyEnd;
        public float glide;
        /// <summary>Vibrato speed (Hz) and depth (semitones).</summary>
        public float vibratoRate;
        public float vibratoDepth;
        /// <summary>FM: the modulator runs at <see cref="fmRatio"/> times the pitch (0: no FM). Its index starts at
        /// <see cref="fmIndex"/> and falls by e every <see cref="fmDecay"/> seconds, so bells start bright.</summary>
        public float fmRatio;
        public float fmIndex;
        public float fmDecay;
        public float attack;
        /// <summary>Seconds from the end of the attack to -60 dB; the voice ends there.</summary>
        public float decay;
        public float gain;
        public SynthFilter filter;
        /// <summary>Filter cutoff (Hz) at the start, and where it sweeps to over the note (0: fixed).</summary>
        public float cutoff;
        public float cutoffEnd;
        /// <summary>Filter Q (0.707: no resonance).</summary>
        public float resonance;
        /// <summary>0..1: chops the note into random grains, like paper rustling or a sleigh bell's jingle.</summary>
        public float crackle;
        /// <summary>-1 (left) .. 1 (right).</summary>
        public float pan;

        /// <summary>Seconds from the start of the sound to the end of this note.</summary>
        public readonly float end => delay + attack + decay;

        public static SynthNote Tone(SynthWave wave, float frequency, float decay, float gain) => new()
        {
            wave = wave,
            frequency = frequency,
            attack = 0.002f,
            decay = decay,
            gain = gain,
            resonance = 0.707f,
        };

        public static SynthNote Noise(float decay, float gain) => Tone(SynthWave.Noise, 0f, decay, gain);

        public SynthNote At(float seconds) { delay = seconds; return this; }
        public SynthNote Attack(float seconds) { attack = seconds; return this; }
        public SynthNote GlideTo(float hz, float seconds) { frequencyEnd = hz; glide = seconds; return this; }
        public SynthNote Vibrato(float rate, float semitones) { vibratoRate = rate; vibratoDepth = semitones; return this; }
        public SynthNote Fm(float ratio, float index, float decaySeconds) { fmRatio = ratio; fmIndex = index; fmDecay = decaySeconds; return this; }
        public SynthNote Crackle(float amount) { crackle = amount; return this; }
        public SynthNote Pan(float value) { pan = value; return this; }

        public SynthNote Filter(SynthFilter type, float cutoffHz, float cutoffEndHz = 0f, float q = 0.707f)
        {
            filter = type;
            cutoff = cutoffHz;
            cutoffEnd = cutoffEndHz;
            resonance = q;
            return this;
        }

        /// <summary>Shifts the pitch (not the filter) by <paramref name="semitones"/>.</summary>
        public SynthNote Transpose(float semitones)
        {
            if (semitones == 0f)
                return this;
            var ratio = Mathf.Pow(2f, semitones / 12f);
            frequency *= ratio;
            frequencyEnd *= ratio;
            return this;
        }
    }
}
