using System.Collections.Generic;
using Unity.Burst;
using Unity.IntegerTime;
using UnityEngine;
using UnityEngine.Audio;
using static UnityEngine.Audio.ProcessorInstance;

namespace Bagel
{
    /// <summary>
    /// The game's only sound source: a small polyphonic synth that renders every sound effect live, so the
    /// project has no audio files. It's a Scriptable Audio Pipeline generator: assign this asset as the
    /// <b>Generator</b> of an <see cref="AudioSource"/> and the source plays it, rendering in Burst-compiled C#
    /// on the audio thread. The synth never ends; it outputs silence until it gets notes.
    /// </summary>
    /// <remarks>
    /// <see cref="Play"/> sends a sound's notes (built by <see cref="BagelSoundLibrary"/>) from the main thread:
    /// each note is a message to the generator's control part, which passes it through the pipe to the
    /// real-time part, where <see cref="SynthEngine"/> starts a voice for it.
    /// </remarks>
    [CreateAssetMenu(fileName = "BagelSynth", menuName = "Bagel/Bagel Synth")]
    public class BagelSynth : ScriptableObject, IAudioGenerator
    {
        [SerializeField, Range(0f, 2f), Tooltip("Gain of the mix before the soft limiter.")]
        float m_Volume = 1f;

        public float volume
        {
            get => m_Volume;
            set => m_Volume = value;
        }

        public bool isFinite => false;
        public bool isRealtime => false;
        public DiscreteTime? length => null;

        public GeneratorInstance CreateInstance(ControlContext context, AudioFormat? nestedFormat, GeneratorInstance.CreationParameters creationParameters)
        {
            var realtime = new Realtime { engine = SynthEngine.Create(AudioSettings.outputSampleRate, m_Volume, (uint)System.Environment.TickCount) };
            return context.AllocateGenerator(realtime, new Control(), nestedFormat, creationParameters);
        }

        /// <summary>Starts <paramref name="notes"/> on the synth that <paramref name="source"/> is playing.</summary>
        /// <returns>False if the source isn't playing a synth.</returns>
        public static bool Play(AudioSource source, IReadOnlyList<SynthNote> notes)
        {
            if (source == null || !source.isPlaying)
                return false;
            return Play(ControlContext.builtIn, source.generatorInstance, notes);
        }

        /// <summary>Starts <paramref name="notes"/> on a synth instance (also one created on a manual context, in tests).</summary>
        public static bool Play(ControlContext context, ProcessorInstance instance, IReadOnlyList<SynthNote> notes)
        {
            if (!context.Exists(instance))
                return false;
            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                context.SendMessage(instance, ref note);
            }
            return true;
        }

        /// <summary>Stops every voice at once.</summary>
        public static void StopAll(ControlContext context, ProcessorInstance instance)
        {
            if (!context.Exists(instance))
                return;
            var stop = new StopMessage();
            context.SendMessage(instance, ref stop);
        }

        struct StopMessage { }

        struct Control : GeneratorInstance.IControl<Realtime>
        {
            public void Configure(ControlContext context, ref Realtime realtime, in AudioFormat format,
                out GeneratorInstance.Setup setup, ref GeneratorInstance.Properties properties)
            {
                realtime.engine.Configure(format.sampleRate);
                setup = new GeneratorInstance.Setup(format.speakerMode, format.sampleRate);
            }

            public void Update(ControlContext context, Pipe pipe) { }

            public Response OnMessage(ControlContext context, Pipe pipe, Message message)
            {
                if (message.Is<SynthNote>())
                {
                    pipe.SendData(context, message.Get<SynthNote>());
                    return Response.Handled;
                }
                if (message.Is<StopMessage>())
                {
                    pipe.SendData(context, new StopMessage());
                    return Response.Handled;
                }
                return Response.Unhandled;
            }

            public void Dispose(ControlContext context, ref Realtime realtime) { }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct Realtime : GeneratorInstance.IRealtime
        {
            public SynthEngine engine;

            public bool isFinite => false;
            public bool isRealtime => false;
            public DiscreteTime? length => null;

            public void Update(UpdatedDataContext context, Pipe pipe)
            {
                foreach (var element in pipe.GetAvailableData(context))
                {
                    if (element.TryGetData(out SynthNote note))
                        engine.Start(note);
                    else if (element.TryGetData(out StopMessage _))
                        engine.StopAll();
                }
            }

            public GeneratorInstance.Result Process(in RealtimeContext context, Pipe pipe, ChannelBuffer buffer, GeneratorInstance.Arguments args)
            {
                engine.Render(buffer);
                return buffer.frameCount;
            }
        }
    }
}
