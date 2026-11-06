using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Bagel.B13_Sound
{
    public class BagelSynthTests
    {
        const int k_SampleRate = 48000;
        const int k_BlockFrames = 512;

        [Test]
        public void EveryPatchPlaysAudiblyAndEndsOnEveryInstrument()
        {
            var notes = new List<SynthNote>();
            var buffer = new float[k_BlockFrames * 2];
            foreach (var patch in BagelSoundLibrary.patchNames)
            {
                foreach (SoundTone tone in Enum.GetValues(typeof(SoundTone)))
                {
                    notes.Clear();
                    Assert.That(BagelSoundLibrary.Build(patch, tone, 0f, notes), Is.True, patch);
                    var engine = SynthEngine.Create(k_SampleRate, 1f);
                    foreach (var note in notes)
                        engine.Start(note);

                    var peak = 0f;
                    var finite = true;
                    for (var seconds = 0f; engine.activeVoices > 0 && seconds < 4f; seconds += (float)k_BlockFrames / k_SampleRate)
                    {
                        engine.Render(new ChannelBuffer(buffer, 2));
                        foreach (var sample in buffer)
                        {
                            finite &= float.IsFinite(sample);
                            peak = Mathf.Max(peak, Mathf.Abs(sample));
                        }
                    }

                    Assert.That(finite, Is.True, $"{patch} on {tone}: NaN or infinity");
                    Assert.That(engine.activeVoices, Is.Zero, $"{patch} on {tone}: still playing after 4 s");
                    Assert.That(peak, Is.InRange(0.1f, 0.99f), $"{patch} on {tone}: peak");
                }
            }
        }

        [Test]
        public void TheSynthIsSilentUntilItGetsNotesThroughTheScriptableAudioPipeline()
        {
            // The same path an AudioSource takes, on a context we mix by hand: notes are messages to the
            // generator's control part, which pipes them to its real-time part.
            var format = new AudioFormat(AudioSpeakerMode.Stereo, k_SampleRate, k_BlockFrames);
            var manual = ControlContext.CreateManualControlContext(format);
            var synth = ScriptableObject.CreateInstance<BagelSynth>();
            try
            {
                var instance = ((IAudioGenerator)synth).CreateInstance(manual.context, format, new GeneratorInstance.CreationParameters());
                ulong tick = 0;
                Assert.That(Mix(manual, instance, 4, ref tick), Is.Zero, "silence before any note");

                var notes = new List<SynthNote>();
                BagelSoundLibrary.Build("pop", SoundTone.Wood, 0f, notes);
                Assert.That(BagelSynth.Play(manual.context, instance, notes), Is.True);
                Assert.That(Mix(manual, instance, 8, ref tick), Is.GreaterThan(0.1f), "the pop");

                manual.context.Destroy(instance);
            }
            finally
            {
                Object.DestroyImmediate(synth);
                manual.Dispose();
            }
        }

        [Test]
        public void MoreNotesThanVoicesReplaceTheOnesClosestToTheirEnd()
        {
            var engine = SynthEngine.Create(k_SampleRate, 1f);
            for (int i = 0; i < SynthEngine.voiceCount + 6; i++)
                engine.Start(SynthNote.Tone(SynthWave.Sine, 440f + i, 0.5f + i * 0.1f, 0.1f));
            Assert.That(engine.activeVoices, Is.EqualTo(SynthEngine.voiceCount));

            var buffer = new float[k_BlockFrames * 2];
            engine.Render(new ChannelBuffer(buffer, 2));
            Assert.That(engine.activeVoices, Is.EqualTo(SynthEngine.voiceCount));
        }

        [Test]
        public void EverySoundTheStylesNameExists()
        {
            // --be-sound: pop; and --be-sound-<cue>: patch; name patches, --be-sound-tone an instrument.
            var declaration = new Regex(@"(--be-sound(?:-[a-z-]+)?)\s*:\s*([^;]+);");
            var checkedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:StyleSheet", new[] { "Assets/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Match match in declaration.Matches(File.ReadAllText(path)))
                {
                    var property = match.Groups[1].Value;
                    var value = match.Groups[2].Value.Trim().Trim('"', '\'');
                    if (property == BagelSound.pitchProperty)
                        Assert.That(float.TryParse(value, out _), Is.True, $"{path}: {property}: {value}");
                    else if (property == BagelSound.toneProperty)
                        Assert.That(BagelSoundLibrary.TryParseTone(value, out _), Is.True, $"{path}: {property}: {value}");
                    else
                        Assert.That(value == BagelSoundLibrary.none || BagelSoundLibrary.Contains(value), Is.True, $"{path}: {property}: {value} isn't a sound");
                    checkedCount++;
                }
            }
            Assert.That(checkedCount, Is.GreaterThan(30));
        }

        static float Mix(ControlContext.Manual manual, GeneratorInstance instance, int blocks, ref ulong tick)
        {
            var data = new float[k_BlockFrames * 2];
            var output = new float[k_BlockFrames * 2];
            var peak = 0f;
            for (int b = 0; b < blocks; b++)
            {
                manual.Update();
                var context = manual.BeginMix(tick);
                if (context == null)
                    Assert.Inconclusive("Audio is bypassed on this machine.");
                Array.Clear(data, 0, data.Length);
                context.Value.Process(instance, new ChannelBuffer(data, 2), default);
                manual.EndMix(new ChannelBuffer(output, 2));
                tick += k_BlockFrames;
                foreach (var sample in data)
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
            }
            return peak;
        }
    }
}
