using System;
using UnityEngine;

namespace NightDuty.PrologueSample
{
    // "Not quite human" dispatcher voice treatment, applied in-engine so the source WAVs stay clean.
    // Put this on the same GameObject as the voice AudioSource. Unity runs audio filters in component order,
    // recommended order: AudioSource -> UncannyVoiceFX -> AudioDistortionFilter -> AudioChorusFilter
    // -> AudioHighPassFilter (~300 Hz) -> AudioLowPassFilter (~3400 Hz, telephone band).
    // Every parameter below can be tuned live in Play Mode from the Inspector.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class UncannyVoiceFX : MonoBehaviour
    {
        public enum GlitchType { PitchWobble, Dropout, DistortionSpike, Stutter }

        [Header("Intensity")]
        [Tooltip("Overall strength of the treatment. 1 = original tuning; 1.2 (default, raised 1.1 -> 1.2 on 10/5) = about 20% more broken: "
               + "distortion, doubling mix, ring-mod flutter, wobble depth, spike level x intensity, dropouts deeper (/ intensity), "
               + "glitches more often and longer by the same factor, and pitch lower by 10% of (intensity-1). Inspector values below stay the 1.0 base.")]
        [Range(0.5f, 2f)] public float intensity = 1.2f;

        [Header("Base")]
        [Tooltip("AudioSource pitch. Resampling, so playback is also slightly slower. 1 = original.")]
        [Range(0.8f, 1.05f)] public float basePitch = 0.95f;
        [Tooltip("Resting level for the AudioDistortionFilter (0..1).")]
        [Range(0f, 1f)] public float baseDistortion = 0.38f;
        [Tooltip("Optional. Found on this GameObject when empty.")]
        public AudioDistortionFilter distortion;

        [Header("Inhuman doubling (time-preserving pitch-shifted copy, stays in sync)")]
        public bool doublingEnabled = true;
        [Tooltip("Pitch ratio of the doubled layer. 0.75 = about -5 semitones, 0.5 = one octave down.")]
        [Range(0.4f, 1f)] public float doublingRatio = 0.72f;
        [Tooltip("Level of the doubled layer mixed under the voice.")]
        [Range(0f, 1f)] public float doublingMix = 0.32f;
        [Tooltip("Pitch-shifter grain window in ms. Larger = smoother but more smeared.")]
        [Range(20f, 120f)] public float doublingWindowMs = 70f;

        [Header("Ring modulation (metallic, synthetic flutter)")]
        [Range(0f, 200f)] public float ringModFrequency = 42f;
        [Range(0f, 0.6f)] public float ringModMix = 0.14f;

        [Header("Random glitches (only while the voice is playing)")]
        public bool glitchesEnabled = true;
        [Tooltip("Random wait between glitches, in seconds.")]
        [Min(0.2f)] public float glitchIntervalMin = 2.2f;
        [Min(0.2f)] public float glitchIntervalMax = 5.5f;
        [Tooltip("Glitch length, in seconds (50-150 ms by default).")]
        [Range(0.02f, 0.4f)] public float glitchDurationMin = 0.05f;
        [Range(0.02f, 0.4f)] public float glitchDurationMax = 0.15f;
        [Tooltip("No glitch during the first part of each clip so the first word stays clear.")]
        [Min(0f)] public float clipStartGrace = 0.45f;

        [Header("Random glitch types")]
        [Tooltip("Random distortion spikes jump the distortion to near max for a moment = the sudden '치직' crackle. Off by default (10/5). "
               + "Scripted calls to TriggerGlitch(DistortionSpike) (e.g. ending line 17 break) still work.")]
        public bool enableDistortionSpikes = false;
        [Tooltip("Random stutters loop a ~40 ms slice of the voice; the repeated slice edges sound like a buzzy click/crackle. Off by default (10/5). "
               + "Scripted calls to TriggerGlitch(Stutter) (e.g. ending line 17 break) still work.")]
        public bool enableStutter = false;
        [Tooltip("Fade time (ms) into/out of a RANDOM dropout so it never clicks. Scripted dropouts keep the original 4 ms.")]
        [Range(4f, 40f)] public float randomDropoutFadeMs = 15f;

        [Header("Glitch type weights (0 disables a type)")]
        [Min(0f)] public float pitchWobbleWeight = 1f;
        [Min(0f)] public float dropoutWeight = 1f;
        [Min(0f)] public float distortionSpikeWeight = 1f;
        [Min(0f)] public float stutterWeight = 0.8f;

        [Header("Glitch strength")]
        [Tooltip("Pitch wobble depth as a fraction of basePitch.")]
        [Range(0f, 0.4f)] public float pitchWobbleDepth = 0.12f;
        [Range(5f, 60f)] public float pitchWobbleRate = 28f;
        [Tooltip("Volume multiplier during a dropout.")]
        [Range(0f, 1f)] public float dropoutLevel = 0.12f;
        [Range(0f, 1f)] public float distortionSpikeLevel = 0.85f;
        [Tooltip("Length of the repeated slice during a stutter, in ms.")]
        [Range(15f, 120f)] public float stutterSliceMs = 40f;

        public GlitchType LastGlitch { get; private set; }
        // Values actually applied after the intensity scale (other scripts should use these instead of basePitch/baseDistortion).
        public float EffectivePitch => basePitch * Mathf.Max(0.5f, 1f - 0.1f * (intensity - 1f));
        public float EffectiveDistortion => Mathf.Clamp01(baseDistortion * intensity);
        private float Scale => Mathf.Max(0.01f, intensity);
        public int GlitchCount { get; private set; }

        private const int MaxChannels = 8;
        private AudioSource source;
        private readonly System.Random random = new System.Random();
        private AudioClip lastClip;
        private float lastClipTime, clipStartTime, nextGlitchTime, glitchStart, glitchEnd;
        private bool glitchActive;
        private GlitchType activeGlitch;

        // Audio-thread state.
        private int sampleRate = 48000;
        private float[] ring;
        private int ringLength, writeIndex;
        private float shifterPhase;
        private double ringModPhase;
        private int stutterRemaining, stutterSliceLength, stutterStart, stutterPosition;
        private int dropoutRemaining;
        private float dropoutGain = 1f;

        // Main thread -> audio thread.
        private volatile int requestStutterSamples, requestDropoutSamples;
        private volatile float pDropoutFadeSamples = 192f;
        private volatile bool pDoubling;
        private volatile float pDoublingMix, pRatio, pWindowSamples, pRingFrequency, pRingMix, pDropoutLevel, pStutterSamples;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            if (distortion == null) distortion = GetComponent<AudioDistortionFilter>();
            sampleRate = AudioSettings.outputSampleRate;
            ringLength = Mathf.Max(1024, sampleRate);
            ring = new float[ringLength * MaxChannels];
            ApplyBase();
            PushParameters();
            ScheduleNext(Time.unscaledTime);
        }

        private void OnValidate()
        {
            glitchIntervalMax = Mathf.Max(glitchIntervalMin, glitchIntervalMax);
            glitchDurationMax = Mathf.Max(glitchDurationMin, glitchDurationMax);
            if (Application.isPlaying && source != null && !glitchActive) ApplyBase();
        }

        private void ApplyBase()
        {
            if (source != null) source.pitch = EffectivePitch;
            if (distortion != null) distortion.distortionLevel = EffectiveDistortion;
        }

        private void PushParameters()
        {
            pDoubling = doublingEnabled;
            pDoublingMix = Mathf.Clamp01(doublingMix * Scale);
            pRatio = doublingRatio;
            pWindowSamples = doublingWindowMs * .001f * sampleRate;
            pRingFrequency = ringModFrequency;
            pRingMix = Mathf.Clamp(ringModMix * Scale, 0f, 0.9f);
            pDropoutLevel = Mathf.Clamp01(dropoutLevel / Scale);
            pStutterSamples = stutterSliceMs * .001f * sampleRate;
        }

        private void Update()
        {
            PushParameters();
            float now = Time.unscaledTime;
            if (source == null || !source.isPlaying)
            {
                if (glitchActive) EndGlitch(now);
                lastClip = null;
                return;
            }
            if (source.clip != lastClip || source.time + .05f < lastClipTime)
            {
                // New line: keep the running glitch timer (so short lines can glitch too), just honour the grace period.
                lastClip = source.clip;
                clipStartTime = now;
                if (glitchActive) EndGlitch(now);
            }
            lastClipTime = source.time;

            if (glitchActive)
            {
                if (now >= glitchEnd) EndGlitch(now);
                else if (activeGlitch == GlitchType.PitchWobble)
                {
                    // Depth envelope sin(pi*t/T): the wobble fades in and out, so the pitch never jumps at the start or end.
                    float t01 = Mathf.Clamp01((now - glitchStart) / Mathf.Max(0.001f, glitchEnd - glitchStart));
                    float envelope = Mathf.Sin(t01 * Mathf.PI);
                    source.pitch = EffectivePitch * (1f + pitchWobbleDepth * Scale * envelope * Mathf.Sin((now - glitchStart) * pitchWobbleRate * 2f * Mathf.PI));
                }
                return;
            }
            bool clipHasRoom = source.clip == null || source.clip.length - source.time > glitchDurationMax * Scale + .1f;
            if (glitchesEnabled && now >= nextGlitchTime && now - clipStartTime >= clipStartGrace && clipHasRoom)
                TriggerGlitch(PickGlitch(), true);
        }

        // Can also be called from other scripts (e.g. on a specific line) to force a glitch.
        public void TriggerGlitch(GlitchType type) => TriggerGlitch(type, false);

        private void TriggerGlitch(GlitchType type, bool fromRandom)
        {
            float now = Time.unscaledTime;
            float duration = Mathf.Lerp(glitchDurationMin, glitchDurationMax, (float)random.NextDouble()) * Scale;
            activeGlitch = type;
            LastGlitch = type;
            GlitchCount++;
            glitchActive = true;
            glitchStart = now;
            glitchEnd = now + duration;
            int samples = Mathf.Max(1, Mathf.RoundToInt(duration * sampleRate));
            switch (type)
            {
                case GlitchType.Dropout:
                    pDropoutFadeSamples = (fromRandom ? randomDropoutFadeMs : 4f) * .001f * sampleRate;
                    requestDropoutSamples = samples;
                    break;
                case GlitchType.Stutter: requestStutterSamples = samples; break;
                case GlitchType.DistortionSpike: if (distortion != null) distortion.distortionLevel = Mathf.Clamp01(distortionSpikeLevel * Scale); break;
            }
        }

        [ContextMenu("Test: Trigger Random Glitch")]
        private void TestGlitch() { if (Application.isPlaying) TriggerGlitch(PickGlitch(), true); }

        private GlitchType PickGlitch()
        {
            float spike = enableDistortionSpikes ? distortionSpikeWeight : 0f;
            float stutter = enableStutter ? stutterWeight : 0f;
            float total = pitchWobbleWeight + dropoutWeight + spike + stutter;
            if (total <= 0f) return GlitchType.PitchWobble;
            float pick = (float)random.NextDouble() * total;
            if ((pick -= pitchWobbleWeight) < 0f) return GlitchType.PitchWobble;
            if ((pick -= dropoutWeight) < 0f) return GlitchType.Dropout;
            if ((pick -= spike) < 0f) return GlitchType.DistortionSpike;
            return stutter > 0f ? GlitchType.Stutter : GlitchType.Dropout;
        }

        private void EndGlitch(float now)
        {
            glitchActive = false;
            ApplyBase();
            ScheduleNext(now);
        }

        private void ScheduleNext(float now)
        {
            nextGlitchTime = now + Mathf.Lerp(glitchIntervalMin, glitchIntervalMax, (float)random.NextDouble()) / Scale;
        }

        private float ReadDelayed(int channelBase, float delay)
        {
            float position = writeIndex - delay;
            while (position < 0f) position += ringLength;
            int i0 = (int)position;
            float fraction = position - i0;
            if (i0 >= ringLength) i0 -= ringLength;
            int i1 = i0 + 1 == ringLength ? 0 : i0 + 1;
            return ring[channelBase + i0] + (ring[channelBase + i1] - ring[channelBase + i0]) * fraction;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (ring == null || channels <= 0 || channels > MaxChannels) return;
            int frames = data.Length / channels;

            int request = requestStutterSamples;
            if (request > 0)
            {
                requestStutterSamples = 0;
                stutterSliceLength = Mathf.Clamp((int)pStutterSamples, 64, ringLength / 2);
                stutterRemaining = request;
                stutterStart = (writeIndex - stutterSliceLength + ringLength) % ringLength;
                stutterPosition = 0;
            }
            request = requestDropoutSamples;
            if (request > 0) { requestDropoutSamples = 0; dropoutRemaining = request; }

            bool doubling = pDoubling && pDoublingMix > 0f;
            float mix = pDoublingMix, ringMix = pRingMix, dropLevel = pDropoutLevel;
            float window = Mathf.Clamp(pWindowSamples, 64f, ringLength * .25f);
            float phaseStep = (1f - pRatio) / window;
            double ringStep = 2.0 * Math.PI * pRingFrequency / sampleRate;
            float rampStep = 1f / Mathf.Max(16f, pDropoutFadeSamples);
            float edgeSamples = .003f * sampleRate;

            for (int f = 0; f < frames; f++)
            {
                int offset = f * channels;
                for (int c = 0; c < channels; c++) ring[c * ringLength + writeIndex] = data[offset + c];

                float p1 = shifterPhase, p2 = p1 + .5f;
                if (p2 >= 1f) p2 -= 1f;
                float d1 = 1f + p1 * window, d2 = 1f + p2 * window;
                float s1 = Mathf.Sin(Mathf.PI * p1), s2 = Mathf.Sin(Mathf.PI * p2);
                float g1 = s1 * s1, g2 = s2 * s2;
                float ringGain = 1f - ringMix + ringMix * (float)Math.Sin(ringModPhase);

                float target = dropoutRemaining > 0 ? dropLevel : 1f;
                if (dropoutGain < target) dropoutGain = Mathf.Min(target, dropoutGain + rampStep);
                else if (dropoutGain > target) dropoutGain = Mathf.Max(target, dropoutGain - rampStep);
                if (dropoutRemaining > 0) dropoutRemaining--;

                bool stutter = stutterRemaining > 0;
                int stutterIndex = 0;
                float stutterEnvelope = 1f;
                if (stutter)
                {
                    stutterIndex = (stutterStart + stutterPosition) % ringLength;
                    int edge = Math.Min(stutterPosition, stutterSliceLength - 1 - stutterPosition);
                    stutterEnvelope = Mathf.Clamp01(edge / edgeSamples);
                    if (++stutterPosition >= stutterSliceLength) stutterPosition = 0;
                    stutterRemaining--;
                }

                for (int c = 0; c < channels; c++)
                {
                    int channelBase = c * ringLength;
                    float x;
                    if (stutter) x = ring[channelBase + stutterIndex] * stutterEnvelope;
                    else
                    {
                        x = data[offset + c];
                        if (doubling)
                        {
                            float shifted = ReadDelayed(channelBase, d1) * g1 + ReadDelayed(channelBase, d2) * g2;
                            x = x * (1f - .35f * mix) + shifted * mix;
                        }
                    }
                    data[offset + c] = x * ringGain * dropoutGain;
                }

                if (++writeIndex >= ringLength) writeIndex = 0;
                shifterPhase += phaseStep;
                if (shifterPhase >= 1f) shifterPhase -= 1f;
                else if (shifterPhase < 0f) shifterPhase += 1f;
                ringModPhase += ringStep;
                if (ringModPhase > 2.0 * Math.PI) ringModPhase -= 2.0 * Math.PI;
            }
        }
    }
}
