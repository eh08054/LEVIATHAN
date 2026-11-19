using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sector.UI
{
    public enum Cue
    {
        Click,
        Select,
        Assign,
        Deny,
        Alert,
        Choose,
        Type,
        EndCycle,
        Reveal,
        Success,
        Fail,
        Glitch,
        Concept,
        Settle,
    }

    // Placeholder sound effects synthesized at startup, so the prototype needs no audio files.
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        const float Tau = Mathf.PI * 2f;

        [SerializeField, Range(0f, 1f)] float volume = 0.5f;

        readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
        AudioSource source;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            var noise = new System.Random(1);
            float Noise() => (float)noise.NextDouble() * 2f - 1f;

            // Soft set (2026-11-17): sine tones only, gentle 4-8 ms attacks, low-to-mid pitches, quiet levels.
            // Click: a low wooden "tock" that drops slightly in pitch.
            clips[Cue.Click] = Synth("click", 0.09f, t =>
                (Sine(Mathf.Lerp(300f, 240f, t / 0.09f), t) + 0.25f * Sine(600f, t) * Mathf.Exp(-t / 0.01f)) * Soft(t, 0.004f, 0.03f) * 0.2f);
            clips[Cue.Select] = Synth("select", 0.16f, t => Sine(Mathf.Lerp(392f, 466f, t / 0.16f), t) * Soft(t, 0.005f, 0.05f) * 0.22f);
            clips[Cue.Assign] = Synth("assign", 0.5f, t => (Bell(523.25f, t, 0f, 0.14f) + Bell(659.25f, t, 0.07f, 0.16f)) * 0.17f);
            clips[Cue.Deny] = Synth("deny", 0.25f, t => Sine(Mathf.Lerp(196f, 160f, t / 0.25f), t) * Soft(t, 0.006f, 0.08f) * 0.26f);
            // Alert (an event arrives): two calm marimba-like notes, a fourth apart.
            clips[Cue.Alert] = Synth("alert", 0.8f, t => (Bell(587.33f, t, 0f, 0.2f) + Bell(440f, t, 0.14f, 0.25f)) * 0.2f);
            clips[Cue.Choose] = Synth("choose", 0.4f, t => (Bell(440f, t, 0f, 0.12f) + Bell(587.33f, t, 0.06f, 0.16f)) * 0.17f);
            clips[Cue.Type] = Synth("type", 0.02f, t => Noise() * Soft(t, 0.002f, 0.004f) * 0.06f);
            clips[Cue.EndCycle] = Synth("end-cycle", 0.6f, t => Sine(Mathf.Lerp(392f, 262f, t / 0.6f), t) * Soft(t, 0.008f, 0.25f) * 0.2f);
            clips[Cue.Reveal] = Synth("reveal", 0.9f, t => (Bell(659.25f, t, 0f, 0.25f) + Bell(987.77f, t, 0.09f, 0.3f)) * 0.14f);
            // Success: a soft rising arpeggio of bells (C5 E5 G5 C6).
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            clips[Cue.Success] = Synth("success", 1.2f, t =>
            {
                float sum = 0f;
                for (int i = 0; i < notes.Length; i++)
                    sum += Bell(notes[i], t, i * 0.085f, 0.32f) * (i == 3 ? 0.8f : 1f);
                return sum * 0.12f;
            });
            // Fail: two low, falling notes (E4 → C4), never buzzy.
            clips[Cue.Fail] = Synth("fail", 0.9f, t => (Bell(329.63f, t, 0f, 0.22f) + Bell(261.63f, t, 0.16f, 0.35f)) * 0.2f);
            float phase = 0f;
            clips[Cue.Glitch] = Synth("glitch", 0.4f, t =>
            {
                // A low tone that sags out of tune, like a tape slowing down, over a soft thud.
                float f = Mathf.Lerp(330f, 150f, t / 0.4f) * (1f + 0.025f * Mathf.Sin(Tau * 14f * t));
                phase += f / Rate;
                float detuned = (Mathf.Sin(Tau * phase) + Mathf.Sin(Tau * phase * 1.012f)) * 0.5f;
                float thud = Sine(70f, t) * Decay(t, 0.06f);
                return (detuned * Decay(t, 0.16f) * 0.4f + thud * 0.3f);
            });
            clips[Cue.Concept] = Synth("concept", 0.9f, t =>
            {
                float shimmer = 0.7f + 0.3f * Mathf.Sin(Tau * 7f * t);
                return (Sine(659.25f, t) + Sine(987.77f, t) + Sine(1318.5f, t)) / 3f * shimmer * Decay(t, 0.5f) * 0.45f;
            });
            // Monthly settlement (operations game): two soft bell notes (G5, then C6) with gentle overtones.
            clips[Cue.Settle] = Synth("settle", 1.6f, t =>
            {
                float Bell(float f, float at)
                {
                    float u = t - at;
                    if (u < 0f)
                        return 0f;
                    float attack = Mathf.Min(1f, u / 0.012f);
                    float body = Sine(f, u) + 0.35f * Sine(f * 2.01f, u) * Mathf.Exp(-u / 0.25f) + 0.12f * Sine(f * 3.02f, u) * Mathf.Exp(-u / 0.12f);
                    return body * attack * Mathf.Exp(-u / 0.55f);
                }
                return (Bell(783.99f, 0f) * 0.7f + Bell(1046.5f, 0.16f)) * 0.22f;
            });
        }

        public void Play(Cue cue, float scale = 1f)
        {
            if (source != null && clips.TryGetValue(cue, out var clip))
                source.PlayOneShot(clip, volume * scale);
        }

        static float Sine(float frequency, float t) => Mathf.Sin(Tau * frequency * t);

        static float Square(float frequency, float t) => Mathf.Sin(Tau * frequency * t) >= 0f ? 1f : -1f;

        // Smooth attack over `attack` seconds, then an exponential tail: no clicks at the start.
        static float Soft(float t, float attack, float seconds) =>
            t < 0f ? 0f : Mathf.Sin(Mathf.Min(1f, t / attack) * Mathf.PI * 0.5f) * Mathf.Exp(-t / seconds);

        // A soft bell (or marimba) note starting at `at`: the fundamental with quickly fading overtones.
        static float Bell(float frequency, float t, float at, float seconds)
        {
            float u = t - at;
            if (u < 0f)
                return 0f;
            float body = Sine(frequency, u) + 0.3f * Sine(frequency * 2f, u) * Mathf.Exp(-u / 0.08f)
                                            + 0.08f * Sine(frequency * 3f, u) * Mathf.Exp(-u / 0.04f);
            return body * Soft(u, 0.006f, seconds);
        }

        // Quick attack, exponential tail.
        static float Decay(float t, float seconds) =>
            t < 0f ? 0f : Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t / seconds);

        static AudioClip Synth(string name, float seconds, Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
