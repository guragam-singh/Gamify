using UnityEngine;

namespace Gamify.Racing
{
    public class GameAudio : MonoBehaviour
    {
        private AudioSource _sfxSource;
        private AudioSource _engineSource;

        private AudioClip _selectClip;
        private AudioClip _correctClip;
        private AudioClip _wrongClip;

        private void Awake()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            _engineSource = gameObject.AddComponent<AudioSource>();
            _engineSource.loop = true;
            _engineSource.playOnAwake = false;
            _engineSource.volume = 0.15f;

            _selectClip = CreateToneClip("Select", 880f, 0.08f, 0.4f, ToneShape.Decay);
            _correctClip = CreateMajorChime("Correct");
            _wrongClip = CreateCrashTone("Wrong");
            _engineSource.clip = CreateEngineHum("EngineHum");
            _engineSource.Play();
        }

        public void PlaySelect()
        {
            if (_sfxSource != null && _selectClip != null)
                _sfxSource.PlayOneShot(_selectClip, 0.6f);
        }

        public void PlayCorrect()
        {
            if (_sfxSource != null && _correctClip != null)
                _sfxSource.PlayOneShot(_correctClip, 0.8f);
        }

        public void PlayWrong()
        {
            if (_sfxSource != null && _wrongClip != null)
                _sfxSource.PlayOneShot(_wrongClip, 0.9f);
        }

        public void SetEnginePitch(float pitch)
        {
            if (_engineSource != null)
                _engineSource.pitch = Mathf.Clamp(pitch, 0.7f, 1.8f);
        }

        private enum ToneShape { Constant, Decay }

        private AudioClip CreateToneClip(string name, float freq, float duration, float volume, ToneShape shape)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = shape == ToneShape.Decay ? (1f - ((float)i / samples)) : 1f;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * volume * env;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateMajorChime(string name)
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.15f ? 523.25f : (t < 0.3f ? 659.25f : 783.99f); // C5 -> E5 -> G5
                float env = 1f - (t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f * env;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateCrashTone(string name)
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                float noise = (Random.value * 2f - 1f) * 0.4f;
                float lowBuzz = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.6f;
                data[i] = (noise + lowBuzz) * env * 0.7f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateEngineHum(string name)
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Harmonic hum
                float val = Mathf.Sin(2f * Mathf.PI * 75f * t) * 0.5f
                          + Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.3f;
                data[i] = val * 0.3f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
