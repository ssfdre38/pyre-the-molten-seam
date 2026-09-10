using System;
using System.IO;
using System.Media;
using System.Text;

namespace PyreGame.Engine;

/// <summary>
/// Sovereign Procedural Sound Synthesizer for Windows.
/// Synthesizes retro-futuristic sound effects mathematically in RAM into WAV streams.
/// 100% offline, zero external audio assets, works on all Windows systems.
/// </summary>
public sealed class SoundSynth : IDisposable
{
    private readonly SoundPlayer? _sndSlash;
    private readonly SoundPlayer? _sndHit;
    private readonly SoundPlayer? _sndParry;
    private readonly SoundPlayer? _sndDash;
    private readonly SoundPlayer? _sndMolt;
    private readonly SoundPlayer? _sndSpark;

    public SoundSynth()
    {
        try
        {
            _sndSlash = GenerateSlashSound();
            _sndHit   = GenerateHitSound();
            _sndParry = GenerateParrySound();
            _sndDash  = GenerateDashSound();
            _sndMolt  = GenerateMoltSound();
            _sndSpark = GenerateSparkSound();
        }
        catch { }
    }

    public void PlaySlash() { try { _sndSlash?.Play(); } catch { } }
    public void PlayHit()   { try { _sndHit?.Play(); } catch { } }
    public void PlayParry() { try { _sndParry?.Play(); } catch { } }
    public void PlayDash()  { try { _sndDash?.Play(); } catch { } }
    public void PlayMolt()  { try { _sndMolt?.Play(); } catch { } }
    public void PlaySpark() { try { _sndSpark?.Play(); } catch { } }

    private static SoundPlayer CreateWavPlayer(short[] samples, int sampleRate = 44100)
    {
        var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + samples.Length * 2);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16); // Subchunk1Size
            bw.Write((short)1); // AudioFormat PCM
            bw.Write((short)1); // NumChannels Mono
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2); // ByteRate
            bw.Write((short)2); // BlockAlign
            bw.Write((short)16); // BitsPerSample
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(samples.Length * 2);

            foreach (var s in samples) bw.Write(s);
        }

        ms.Position = 0;
        var player = new SoundPlayer(ms);
        try { player.Load(); } catch { }
        return player;
    }

    private static SoundPlayer GenerateSlashSound()
    {
        int sampleRate = 44100;
        float duration = 0.15f;
        int count = (int)(sampleRate * duration);
        short[] samples = new short[count];
        Random rng = new();

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = 600f * MathF.Exp(-12.0f * t) + 120f;
            float env = MathF.Sin(t * MathF.PI) * MathF.Pow(1.0f - t, 0.5f);
            float tone = MathF.Sin((float)i / sampleRate * freq * 2.0f * MathF.PI);
            float noise = ((float)rng.NextDouble() * 2.0f - 1.0f) * 0.4f;
            samples[i] = (short)Math.Clamp((tone * 0.6f + noise) * env * 24000f, -32767f, 32767f);
        }
        return CreateWavPlayer(samples, sampleRate);
    }

    private static SoundPlayer GenerateHitSound()
    {
        int sampleRate = 44100;
        float duration = 0.16f;
        int count = (int)(sampleRate * duration);
        short[] samples = new short[count];
        Random rng = new();

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = 160f * MathF.Exp(-15.0f * t) + 40f;
            float env = MathF.Pow(1.0f - t, 2.0f);
            float tone = MathF.Sin((float)i / sampleRate * freq * 2.0f * MathF.PI);
            float noise = ((float)rng.NextDouble() * 2.0f - 1.0f) * 0.6f;
            samples[i] = (short)Math.Clamp((tone * 0.7f + noise * 0.3f) * env * 28000f, -32767f, 32767f);
        }
        return CreateWavPlayer(samples, sampleRate);
    }

    private static SoundPlayer GenerateParrySound()
    {
        int sampleRate = 44100;
        float duration = 0.30f;
        int count = (int)(sampleRate * duration);
        short[] samples = new short[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float env = MathF.Exp(-9.0f * t);
            float tone1 = MathF.Sin((float)i / sampleRate * 1760.0f * 2.0f * MathF.PI);
            float tone2 = MathF.Sin((float)i / sampleRate * 2640.0f * 2.0f * MathF.PI);
            samples[i] = (short)Math.Clamp((tone1 * 0.5f + tone2 * 0.5f) * env * 26000f, -32767f, 32767f);
        }
        return CreateWavPlayer(samples, sampleRate);
    }

    private static SoundPlayer GenerateDashSound()
    {
        int sampleRate = 44100;
        float duration = 0.20f;
        int count = (int)(sampleRate * duration);
        short[] samples = new short[count];
        Random rng = new();

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float env = MathF.Sin(t * MathF.PI) * (1.0f - t * 0.4f);
            float noise = ((float)rng.NextDouble() * 2.0f - 1.0f);
            samples[i] = (short)Math.Clamp(noise * env * 22000f, -32767f, 32767f);
        }
        return CreateWavPlayer(samples, sampleRate);
    }

    private static SoundPlayer GenerateMoltSound()
    {
        int sampleRate = 44100;
        float duration = 0.65f;
        int count = (int)(sampleRate * duration);
        short[] samples = new short[count];
        Random rng = new();

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = 220f * (1.0f - t * 0.7f);
            float env = MathF.Pow(1.0f - t, 1.2f);
            float sub = MathF.Sin((float)i / sampleRate * 55.0f * 2.0f * MathF.PI);
            float sweep = MathF.Sin((float)i / sampleRate * freq * 2.0f * MathF.PI);
            float rumble = ((float)rng.NextDouble() * 2.0f - 1.0f) * 0.5f;
            samples[i] = (short)Math.Clamp((sub * 0.5f + sweep * 0.3f + rumble * 0.2f) * env * 31000f, -32767f, 32767f);
        }
        return CreateWavPlayer(samples, sampleRate);
    }

    private static SoundPlayer GenerateSparkSound()
    {
        int sampleRate = 44100;
        float duration = 0.15f;
        int count = (int)(sampleRate * duration);
        short[] samples = new short[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = t < 0.5f ? 880.0f : 1320.0f;
            float env = MathF.Exp(-16.0f * (t % 0.5f));
            float tone = MathF.Sin((float)i / sampleRate * freq * 2.0f * MathF.PI);
            samples[i] = (short)Math.Clamp(tone * env * 22000f, -32767f, 32767f);
        }
        return CreateWavPlayer(samples, sampleRate);
    }

    public void Dispose()
    {
        _sndSlash?.Dispose();
        _sndHit?.Dispose();
        _sndParry?.Dispose();
        _sndDash?.Dispose();
        _sndMolt?.Dispose();
        _sndSpark?.Dispose();
    }
}
