using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Bagel
{
    /// <summary>The instrument the melodic sounds use. Each season picks one (<c>--be-sound-tone</c> in its theme).</summary>
    public enum SoundTone
    {
        /// <summary>Maple: a maple-wood marimba with a woodblock knock.</summary>
        Wood,
        /// <summary>Sakura: a plucked koto string.</summary>
        Koto,
        /// <summary>Matsuri: a glass furin (wind chime).</summary>
        Furin,
        /// <summary>Snow: a celesta with sleigh-bell jingle.</summary>
        Bell
    }

    /// <summary>
    /// Every sound effect in the game, as code: each patch is a handful of <see cref="SynthNote"/>s. Bakery
    /// sounds (a puff of flour, the register's "ka-ching", the shop's door bell, a paper bag) and cute ones
    /// (pops, a squeaky mascot, little arpeggios), all in C major pentatonic so overlapping sounds agree.
    /// </summary>
    /// <remarks>
    /// The UI picks patches by name from USS (<c>--be-sound: pop;</c>), so a designer can retune any press or
    /// event without code. Melodic patches play on the season's <see cref="SoundTone"/>.
    /// </remarks>
    [NoAutoStaticsCleanup] // a read-only patch table; s_Jitter only lives inside one Build call
    public static class BagelSoundLibrary
    {
        public const string none = "none";

        // C major pentatonic, from C5.
        const float C5 = 523.25f, D5 = 587.33f, E5 = 659.26f, G5 = 783.99f, A5 = 880f;
        const float C6 = 1046.5f, D6 = 1174.66f, E6 = 1318.51f, G6 = 1567.98f, A6 = 1760f;
        const float C7 = 2093f, D7 = 2349.32f, E7 = 2637.02f, G7 = 3135.96f, A7 = 3520f;
        static readonly float[] s_Sparkle = { C7, E7, G7, D7, A7, E7, G7, C7 };
        static readonly float[] s_Glissando = { C6, D6, E6, G6, A6, C7, D7, E7, G7, A7 };
        static readonly float[] s_DoorBellGains = { 0.32f, 0.24f, 0.2f, 0.14f };

        static readonly Dictionary<string, Action<Builder>> s_Patches = new(StringComparer.Ordinal)
        {
            // Presses
            ["pop"] = b => { b.Jitter(0.6f); b.Add(Sine(420f, 0.085f, 0.55f).GlideTo(980f, 0.035f)); b.Add(Click(4000f, 0.12f)); },
            ["tab"] = b => { b.Tone(C5, 0f, 0.5f); b.Add(Sine(300f, 0.04f, 0.15f).GlideTo(600f, 0.02f)); },
            ["click"] = b =>
            {
                b.Add(SynthNote.Tone(SynthWave.Square, 1900f, 0.03f, 0.18f).GlideTo(1300f, 0.02f).Filter(SynthFilter.LowPass, 5000f));
                b.Add(Click(4000f, 0.25f, SynthFilter.BandPass));
            },
            ["back"] = b => b.Add(Sine(880f, 0.13f, 0.45f).GlideTo(420f, 0.08f)),
            ["nope"] = b =>
            {
                b.Add(SynthNote.Tone(SynthWave.Triangle, 392f, 0.1f, 0.45f).Filter(SynthFilter.LowPass, 2000f));
                b.Add(SynthNote.Tone(SynthWave.Triangle, 311.13f, 0.16f, 0.45f).Filter(SynthFilter.LowPass, 1800f).At(0.1f));
            },

            // Overlays
            ["open"] = b => { b.Add(Sine(330f, 0.16f, 0.4f).GlideTo(880f, 0.09f).Attack(0.005f)); b.Tone(E6, 0.06f, 0.22f); },
            ["close"] = b => b.Add(Sine(780f, 0.14f, 0.35f).GlideTo(300f, 0.1f)),
            ["hint"] = b => { b.Tone(G6, 0f, 0.35f); b.Tone(E6, 0.12f, 0.35f); },

            // Home
            ["bake"] = b =>
            {
                // A puff of flour and a soft dough "boop", a little different every tap.
                b.Jitter(1.2f);
                b.Add(SynthNote.Noise(0.11f, 0.32f).Attack(0.004f).Filter(SynthFilter.LowPass, 1400f, 350f, 0.9f));
                b.Add(Sine(330f, 0.14f, 0.5f).GlideTo(190f, 0.07f));
                b.Add(Sine(G6, 0.06f, 0.05f).At(0.02f));
            },
            ["register"] = b =>
            {
                // Ka: the drawer's latch and a rattle of coins. Ching: two bells.
                b.Add(SynthNote.Tone(SynthWave.Square, 700f, 0.025f, 0.14f).GlideTo(500f, 0.02f).Filter(SynthFilter.LowPass, 3000f));
                b.Add(SynthNote.Noise(0.05f, 0.5f).Filter(SynthFilter.BandPass, 4500f, 0f, 1.2f).Crackle(0.4f));
                b.Add(SynthNote.Noise(0.25f, 0.3f).Filter(SynthFilter.HighPass, 5000f).Crackle(0.85f).At(0.02f));
                b.Add(Bell(E6, 0.9f, 0.28f).At(0.07f).Pan(-0.2f));
                b.Add(Bell(A6, 0.8f, 0.24f).At(0.075f).Pan(0.2f));
            },
            ["squeak"] = b =>
            {
                b.Jitter(1f);
                b.Add(Sine(820f, 0.16f, 0.3f).GlideTo(1450f, 0.09f).Vibrato(22f, 0.5f));
                b.Add(Sine(1100f, 0.1f, 0.2f).GlideTo(1700f, 0.05f).At(0.1f));
            },

            // Currency landing in its HUD pill (the UI raises the pitch coin by coin)
            ["coin"] = b => b.Add(Sine(C7, 0.22f, 0.3f).Fm(2.41f, 1.8f, 0.03f).Pan(b.RandomRange(-0.2f, 0.2f))),
            ["gem"] = b =>
            {
                b.Add(Sine(E7, 0.45f, 0.26f).Fm(2.76f, 1.2f, 0.12f));
                b.Add(SynthNote.Noise(0.08f, 0.12f).Filter(SynthFilter.HighPass, 7000f).Crackle(0.6f));
            },
            ["ticket"] = b =>
            {
                b.Add(SynthNote.Noise(0.04f, 0.7f).Filter(SynthFilter.BandPass, 2600f, 0f, 1.8f));
                b.Add(SynthNote.Tone(SynthWave.Triangle, 1200f, 0.03f, 0.15f));
            },

            // Shops
            ["upgrade"] = b =>
            {
                b.Tone(C5, 0f, 0.45f);
                b.Tone(E5, 0.075f, 0.45f);
                b.Tone(G5, 0.15f, 0.45f);
                b.Tone(C6, 0.225f, 0.55f, 1.5f);
                b.Add(Shimmer(0.3f, 0.05f).At(0.22f));
            },
            ["doorbell"] = b => DoorBell(b),
            ["opening"] = b =>
            {
                DoorBell(b);
                b.Tone(G5, 0.28f, 0.4f);
                b.Tone(C6, 0.36f, 0.5f, 1.4f);
            },
            ["fanfare"] = b =>
            {
                b.Tone(C5, 0f, 0.5f);
                b.Tone(E5, 0.09f, 0.5f);
                b.Tone(G5, 0.18f, 0.5f);
                b.Tone(C6, 0.27f, 0.5f);
                b.Add(Sine(130f, 0.3f, 0.3f).GlideTo(90f, 0.2f).At(0.4f));
                b.Tone(E6, 0.4f, 0.3f, 2f);
                b.Tone(G6, 0.4f, 0.28f, 2f);
                b.Tone(C7, 0.4f, 0.26f, 2f);
                Sparkles(b, 0.5f, s_Sparkle.Length, 0.07f);
            },

            // Lucky Bag
            ["rustle"] = b =>
            {
                b.Add(PaperBag(0.65f, 1.2f).Attack(0.03f));
                for (int i = 0; i < 4; i++)
                    b.Add(SynthNote.Noise(0.07f, 0.5f).Filter(SynthFilter.LowPass, 900f).At(i * 0.17f).Pan(i % 2 == 0 ? -0.3f : 0.3f));
            },
            ["bag"] = b => { b.Add(PaperBag(0.25f, 1.1f).Attack(0.01f)); b.Add(SynthNote.Noise(0.06f, 0.45f).Filter(SynthFilter.LowPass, 900f)); },
            ["reveal-n"] = b => { Flip(b); b.Tone(G5, 0.05f, 0.35f); },
            ["reveal-r"] = b => { Flip(b); b.Tone(G5, 0.05f, 0.35f); b.Tone(C6, 0.12f, 0.38f); },
            ["reveal-sr"] = b => { Flip(b); b.Tone(C6, 0.05f, 0.35f); b.Tone(E6, 0.11f, 0.35f); b.Tone(G6, 0.17f, 0.4f); },
            ["reveal-ssr"] = b =>
            {
                Flip(b);
                b.Tone(C6, 0.05f, 0.35f);
                b.Tone(E6, 0.1f, 0.35f);
                b.Tone(G6, 0.15f, 0.35f);
                b.Tone(C7, 0.2f, 0.4f, 1.5f);
                b.Add(Shimmer(0.5f, 0.07f).At(0.1f));
                Sparkles(b, 0.25f, 4, 0.06f);
            },
            ["reveal-ur"] = b =>
            {
                Flip(b);
                for (int i = 0; i < s_Glissando.Length; i++)
                    b.Tone(s_Glissando[i], 0.05f + i * 0.04f, 0.25f);
                b.Add(Sine(130f, 0.35f, 0.3f).GlideTo(90f, 0.25f).At(0.45f));
                b.Tone(C7, 0.45f, 0.3f, 2f);
                b.Tone(E7, 0.45f, 0.25f, 2f);
                b.Add(Shimmer(0.9f, 0.08f).At(0.3f));
                Sparkles(b, 0.5f, s_Sparkle.Length, 0.06f);
            },

            // Pages and Settings
            ["swish"] = b => b.Add(SynthNote.Noise(0.22f, 1.2f).Attack(0.03f).Filter(SynthFilter.BandPass, 500f, 3800f, 1.3f)),
            ["settle"] = b => b.Add(SynthNote.Noise(0.14f, 0.6f).Attack(0.02f).Filter(SynthFilter.BandPass, 1600f, 600f, 1.1f)),
            ["season"] = b => { b.Tone(G5, 0f, 0.32f); b.Tone(C6, 0.05f, 0.32f); b.Tone(E6, 0.1f, 0.32f); b.Tone(G6, 0.15f, 0.34f, 1.5f); },
            ["chime"] = b => { b.Tone(E6, 0f, 0.32f); b.Tone(G6, 0.08f, 0.32f); },
        };

        /// <summary>Every patch name, for tools and tests.</summary>
        public static IEnumerable<string> patchNames => s_Patches.Keys;

        public static bool Contains(string patch) => patch != null && s_Patches.ContainsKey(patch);

        /// <summary>
        /// Appends the notes of <paramref name="patch"/> to <paramref name="notes"/>, played on
        /// <paramref name="tone"/> and transposed by <paramref name="semitones"/>.
        /// </summary>
        /// <returns>False for an unknown patch (or <c>none</c>).</returns>
        public static bool Build(string patch, SoundTone tone, float semitones, List<SynthNote> notes, float pan = 0f)
        {
            if (patch == null || !s_Patches.TryGetValue(patch, out var build))
                return false;
            var start = notes.Count;
            build(new Builder(tone, notes));

            // Transpose and pan after building, so a patch's own jitter and the caller's pitch add up.
            for (int i = start; i < notes.Count; i++)
            {
                var note = notes[i].Transpose(semitones + s_Jitter);
                if (pan != 0f)
                    note.pan = Mathf.Clamp(note.pan + pan, -1f, 1f);
                notes[i] = note;
            }
            s_Jitter = 0f;
            return true;
        }

        public static bool TryParseTone(string value, out SoundTone tone) =>
            Enum.TryParse(value?.Trim().Trim('"', '\''), ignoreCase: true, out tone);

        // ---------------------------------------------------------------- Building blocks

        static float s_Jitter;

        readonly struct Builder
        {
            readonly SoundTone m_Tone;
            readonly List<SynthNote> m_Notes;

            public Builder(SoundTone tone, List<SynthNote> notes)
            {
                m_Tone = tone;
                m_Notes = notes;
            }

            public void Add(SynthNote note) => m_Notes.Add(note);

            /// <summary>Detunes the whole sound by up to ±<paramref name="semitones"/>, so repeats don't sound identical.</summary>
            public void Jitter(float semitones) => s_Jitter = UnityEngine.Random.Range(-semitones, semitones);

            public float RandomRange(float min, float max) => UnityEngine.Random.Range(min, max);

            /// <summary>One note on the season's instrument.</summary>
            public void Tone(float frequency, float delay, float gain, float length = 1f)
            {
                switch (m_Tone)
                {
                    case SoundTone.Koto:
                        // A plucked string: a bright saw whose filter closes, bent slightly into pitch.
                        Add(SynthNote.Tone(SynthWave.Saw, frequency * 1.012f, 0.9f * length, 0.5f * gain).Attack(0.001f)
                            .GlideTo(frequency, 0.05f).Filter(SynthFilter.LowPass, frequency * 7f, frequency * 1.3f, 1.1f).At(delay));
                        Add(SynthNote.Noise(0.01f, 0.15f * gain).Filter(SynthFilter.HighPass, 2500f).At(delay));
                        break;
                    case SoundTone.Furin:
                        // Glass: inharmonic FM (an octave up for low notes), a long ring and a tiny "tink".
                        Add(Sine(frequency * Octave(frequency), 1.4f * length, 0.42f * gain).Fm(2.76f, 1.6f, 0.25f).At(delay));
                        Add(Sine(frequency * 5.4f, 0.05f, 0.12f * gain).At(delay));
                        break;
                    case SoundTone.Bell:
                        // Celesta and sleigh bells: a bell (an octave up for low notes) with a short jingle.
                        Add(Bell(frequency * Octave(frequency), 0.7f * length, 0.42f * gain).At(delay));
                        Add(SynthNote.Noise(0.14f, 0.08f * gain).Filter(SynthFilter.HighPass, 6500f, 0f, 0.9f).Crackle(0.75f).At(delay));
                        break;
                    default:
                        // Wood: a marimba bar (bright FM attack, pure ring) and a woodblock knock.
                        Add(Sine(frequency, 0.3f * length, gain).Fm(4f, 1.4f, 0.03f).At(delay));
                        Add(SynthNote.Noise(0.012f, 0.18f * gain).Filter(SynthFilter.BandPass, 3000f, 0f, 1.5f).At(delay));
                        break;
                }
            }
        }

        // The furin and the celesta sound an octave up, except notes that are already high.
        static float Octave(float frequency) => frequency < 1000f ? 2f : 1f;

        static SynthNote Sine(float frequency, float decay, float gain) => SynthNote.Tone(SynthWave.Sine, frequency, decay, gain);

        static SynthNote Bell(float frequency, float decay, float gain) => Sine(frequency, decay, gain).Fm(3.5f, 2.5f, 0.1f);

        static SynthNote Click(float cutoff, float gain, SynthFilter filter = SynthFilter.LowPass) =>
            SynthNote.Noise(0.008f, gain).Filter(filter, cutoff);

        static SynthNote Shimmer(float decay, float gain) =>
            SynthNote.Noise(decay, gain).Filter(SynthFilter.HighPass, 7000f).Crackle(0.8f);

        static SynthNote PaperBag(float decay, float gain) =>
            SynthNote.Noise(decay, gain).Filter(SynthFilter.BandPass, 2400f, 0f, 0.9f).Crackle(0.85f);

        static void Flip(Builder b)
        {
            b.Add(SynthNote.Noise(0.05f, 0.7f).Filter(SynthFilter.BandPass, 1500f, 4500f, 1.2f));
            b.Add(Sine(900f, 0.04f, 0.12f).GlideTo(1300f, 0.03f));
        }

        /// <summary>The bell over the bakery's door: four quick strikes and a jingle.</summary>
        static void DoorBell(Builder b)
        {
            for (int i = 0; i < s_DoorBellGains.Length; i++)
                b.Add(Bell(i % 2 == 0 ? 1975.53f : G6, 0.7f, s_DoorBellGains[i]).At(i * 0.075f).Pan(i % 2 == 0 ? -0.15f : 0.15f));
            b.Add(SynthNote.Noise(0.35f, 0.1f).Filter(SynthFilter.HighPass, 6000f).Crackle(0.8f));
        }

        static void Sparkles(Builder b, float start, int count, float gain)
        {
            for (int i = 0; i < count; i++)
                b.Add(Sine(s_Sparkle[i % s_Sparkle.Length], 0.12f, gain).At(start + i * 0.07f).Pan(i % 2 == 0 ? -0.5f : 0.5f));
        }
    }
}
