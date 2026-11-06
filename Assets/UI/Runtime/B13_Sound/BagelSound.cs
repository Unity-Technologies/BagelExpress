using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>What the UI asked to hear, raised for every sound (also when muted or when no synth is listening).</summary>
    public readonly struct SoundEvent
    {
        public readonly string patch;
        public readonly string cue;
        public readonly SoundTone tone;
        public readonly float semitones;
        public readonly VisualElement element;

        public SoundEvent(string patch, string cue, SoundTone tone, float semitones, VisualElement element)
        {
            this.patch = patch;
            this.cue = cue;
            this.tone = tone;
            this.semitones = semitones;
            this.element = element;
        }

        public override string ToString() => cue == null ? patch : $"{cue}: {patch}";
    }

    /// <summary>
    /// Sound effects, chosen in USS. The code only says what happened to an element; its styles say what that
    /// sounds like, so a designer can retune a press or an event in the Style Sheets window or UI Builder:
    /// <list type="bullet">
    /// <item><c>--be-sound: pop;</c> the sound of clicking the element (<see cref="ListenForPresses"/>), or
    /// <c>none</c>.</item>
    /// <item><c>--be-sound-&lt;cue&gt;: register;</c> the sound of an event, e.g. <c>--be-sound-collect</c> or
    /// <c>--be-sound-reveal</c> (one per Lucky Bag rarity).</item>
    /// <item><c>--be-sound-pitch: 4;</c> semitones up (the nav items play a scale).</item>
    /// <item><c>--be-sound-tone: wood;</c> the instrument of the melodic sounds; each season's theme sets
    /// one (<see cref="SoundTone"/>).</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// UI Toolkit custom properties aren't inherited, so each lookup walks up from the element to the root,
    /// like the mascot's <c>--be-mascot-accessory</c>: a value on <c>:root</c> is the default for every element.
    /// The patches themselves are code (<see cref="BagelSoundLibrary"/>), rendered live by
    /// <see cref="BagelSynth"/>; <see cref="BakeryAppDriver"/> plugs the synth in as <see cref="output"/>.
    /// Without it (UI Builder, editor windows, tests) sounds are only raised as <see cref="played"/>.
    /// </remarks>
    [NoAutoStaticsCleanup] // ResetStatics starts every Play session with sound on and no stale listeners
    public static class BagelSound
    {
        public const string pressProperty = "--be-sound";
        public const string pitchProperty = "--be-sound-pitch";
        public const string toneProperty = "--be-sound-tone";

        static readonly CustomStyleProperty<string> s_Press = new(pressProperty);
        static readonly CustomStyleProperty<float> s_Pitch = new(pitchProperty);
        static readonly CustomStyleProperty<string> s_Tone = new(toneProperty);
        static readonly Dictionary<string, CustomStyleProperty<string>> s_CueProperties = new(StringComparer.Ordinal);
        static readonly List<SynthNote> s_Notes = new();
        static readonly EventCallback<ClickEvent> s_OnClick = OnClick;

        /// <summary>Settings › Sound.</summary>
        public static bool enabled { get; set; } = true;

        /// <summary>Plays notes (the driver's <see cref="BagelSynth.Play(AudioSource, IReadOnlyList{SynthNote})"/>).</summary>
        public static Func<IReadOnlyList<SynthNote>, bool> output { get; set; }

        public static event Action<SoundEvent> played;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            enabled = true;
            output = null;
            played = null;
        }

        /// <summary>
        /// Plays the sound <paramref name="element"/>'s styles give <paramref name="cue"/> (null: a press).
        /// </summary>
        /// <param name="semitones">Added to the element's <c>--be-sound-pitch</c>.</param>
        /// <param name="pan">-1 (left) .. 1 (right).</param>
        /// <returns>The patch played, or null when the styles name none.</returns>
        public static string Play(VisualElement element, string cue = null, float semitones = 0f, float pan = 0f)
        {
            if (element == null)
                return null;
            var patch = FindString(element, cue == null ? s_Press : CueProperty(cue));
            if (patch == null || patch == BagelSoundLibrary.none)
                return null;

            var tone = BagelSoundLibrary.TryParseTone(FindString(element, s_Tone), out var parsed) ? parsed : SoundTone.Wood;
            if (FindFloat(element, s_Pitch, out var pitch))
                semitones += pitch;

            played?.Invoke(new SoundEvent(patch, cue, tone, semitones, element));
            if (!enabled || output == null)
                return patch;

            s_Notes.Clear();
            if (BagelSoundLibrary.Build(patch, tone, semitones, s_Notes, pan))
                output(s_Notes);
            else
                Debug.LogWarning($"[BagelSound] No sound named \"{patch}\" ({(cue == null ? pressProperty : "--be-sound-" + cue)} on {element.name ?? element.GetType().Name}).");
            return patch;
        }

        /// <summary>Plays the press sound of whatever is clicked inside <paramref name="root"/> (disabled elements: the <c>disabled</c> cue).</summary>
        public static void ListenForPresses(VisualElement root)
        {
            root.UnregisterCallback(s_OnClick, TrickleDown.TrickleDown);
            root.RegisterCallback(s_OnClick, TrickleDown.TrickleDown);
        }

        static void OnClick(ClickEvent evt)
        {
            if (evt.target is VisualElement element)
                Play(element, element.enabledInHierarchy ? null : "disabled");
        }

        static CustomStyleProperty<string> CueProperty(string cue)
        {
            if (!s_CueProperties.TryGetValue(cue, out var property))
            {
                property = new CustomStyleProperty<string>(pressProperty + "-" + cue);
                s_CueProperties.Add(cue, property);
            }
            return property;
        }

        static string FindString(VisualElement element, CustomStyleProperty<string> property)
        {
            for (var e = element; e != null; e = e.hierarchy.parent)
            {
                if (e.customStyle.TryGetValue(property, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value.Trim().Trim('"', '\'').ToLowerInvariant();
            }
            return null;
        }

        static bool FindFloat(VisualElement element, CustomStyleProperty<float> property, out float value)
        {
            for (var e = element; e != null; e = e.hierarchy.parent)
            {
                if (e.customStyle.TryGetValue(property, out value))
                    return true;
            }
            value = 0f;
            return false;
        }
    }
}
