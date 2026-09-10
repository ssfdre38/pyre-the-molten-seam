using System;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;

namespace PyreGame.Engine;

/// <summary>
/// Sovereign Procedural Sound & Music Synthesizer for Windows.
/// Synthesizes retro-futuristic sound effects and dynamic dark-synth BGM in RAM.
/// 100% offline, zero external audio assets, works on all Windows systems via standard OS APIs.
/// </summary>
public sealed class SoundSynth : IDisposable
{
    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern long mciSendString(string command, StringBuilder? returnString, int returnLength, IntPtr hwndCallback);

    private readonly SoundPlayer? _sndSlash;
    private readonly SoundPlayer? _sndHit;
    private readonly SoundPlayer? _sndParry;
    private readonly SoundPlayer? _sndDash;
    private readonly SoundPlayer? _sndMolt;
    private readonly SoundPlayer? _sndSpark;

    private readonly string _exploreWavPath;
    private readonly string _bossWavPath;
    private string _activeBgm = "";
    private bool _isMuted = false;

    public bool IsMuted => _isMuted;

    public SoundSynth()
    {
        _exploreWavPath = Path.Combine(Path.GetTempPath(), "pyre_bgm_explore.wav");
        _bossWavPath = Path.Combine(Path.GetTempPath(), "pyre_bgm_boss.wav");

        try
        {
            _sndSlash = GenerateSlashSound();
            _sndHit   = GenerateHitSound();
            _sndParry = GenerateParrySound();
            _sndDash  = GenerateDashSound();
            _sndMolt  = GenerateMoltSound();
            _sndSpark = GenerateSparkSound();

            GenerateExploreBgmFile(_exploreWavPath);
            GenerateBossBgmFile(_bossWavPath);
        }
        catch { }
    }

    public void ToggleMute()
    {
        _isMuted = !_isMuted;
        if (_isMuted)
        {
            StopMusic();
        }
        else
        {
            if (_activeBgm == "boss") PlayBossMusic(force: true);
            else PlayExploreMusic(force: true);
        }
    }

    public void PlayExploreMusic(bool force = false)
    {
        if (_isMuted) return;
        if (_activeBgm == "explore" && !force) return;

        try
        {
            mciSendString("close pyre_bgm", null, 0, IntPtr.Zero);
            mciSendString($"open \"{_exploreWavPath}\" type waveaudio alias pyre_bgm", null, 0, IntPtr.Zero);
            mciSendString("play pyre_bgm repeat", null, 0, IntPtr.Zero);
            _activeBgm = "explore";
        }
        catch { }
    }

    public void PlayBossMusic(bool force = false)
    {
        if (_isMuted) return;
        if (_activeBgm == "boss" && !force) return;

        try
        {
            mciSendString("close pyre_bgm", null, 0, IntPtr.Zero);
            mciSendString($"open \"{_bossWavPath}\" type waveaudio alias pyre_bgm", null, 0, IntPtr.Zero);
            mciSendString("play pyre_bgm repeat", null, 0, IntPtr.Zero);
            _activeBgm = "boss";
        }
        catch { }
    }

    public void StopMusic()
    {
        try
        {
            mciSendString("stop pyre_bgm", null, 0, IntPtr.Zero);
            mciSendString("close pyre_bgm", null, 0, IntPtr.Zero);
            _activeBgm = "";
        }
        catch { }
    }

    public void PlaySlash() { if (!_isMuted) try { _sndSlash?.Play(); } catch { } }
    public void PlayHit()   { if (!_isMuted) try { _sndHit?.Play(); } catch { } }
    public void PlayParry() { if (!_isMuted) try { _sndParry?.Play(); } catch { } }
    public void PlayDash()  { if (!_isMuted) try { _sndDash?.Play(); } catch { } }
    public void PlayMolt()  { if (!_isMuted) try { _sndMolt?.Play(); } catch { } }
    public void PlaySpark() { if (!_isMuted) try { _sndSpark?.Play(); } catch { } }

    private static SoundPlayer CreateWavPlayer(short[] samples, int sampleRate = 44100)
    {
        var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            WriteWavHeader(bw, samples.Length, sampleRate);
            foreach (var s in samples) bw.Write(s);
        }

        ms.Position = 0;
        var player = new SoundPlayer(ms);
        try { player.Load(); } catch { }
        return player;
    }

    private static void WriteWavHeader(BinaryWriter bw, int sampleCount, int sampleRate)
    {
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + sampleCount * 2);
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
        bw.Write(sampleCount * 2);
    }

    private static void GenerateExploreBgmFile(string filePath)
    {
        int rate = 22050;
        int count = rate * 8; // 8 seconds seamless loop at 120 BPM
        short[] buf = new short[count];
        Random rng = new(1337);

        float[] arpNotes = { 293.66f, 349.23f, 440.00f, 523.25f, 587.33f, 523.25f, 440.00f, 349.23f };
        float[] bassNotes = { 73.42f, 73.42f, 87.31f, 98.00f };

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float beat = t * 2.0f; // 120 BPM: 2 beats/sec

            // 1. Kick on every beat
            float kickT = beat % 1.0f;
            float kickFreq = 110.0f * MathF.Exp(-18.0f * kickT) + 38.0f;
            float kick = MathF.Sin(2.0f * MathF.PI * kickFreq * kickT) * MathF.Pow(1.0f - kickT, 2.5f) * 0.42f;

            // 2. Hi-hat on 8th notes
            float hatT = (beat * 2.0f) % 1.0f;
            float hat = ((float)rng.NextDouble() * 2.0f - 1.0f) * MathF.Exp(-32.0f * hatT) * 0.16f;

            // 3. Bassline (8th notes, changes chord every 2 beats)
            int bassIdx = ((int)(beat / 2.0f)) % bassNotes.Length;
            float bFreq = bassNotes[bassIdx];
            float bassT = (beat * 2.0f) % 1.0f;
            float bassEnv = MathF.Pow(1.0f - bassT, 1.4f);
            float bass = MathF.Sin(2.0f * MathF.PI * bFreq * t) * 0.32f * bassEnv;

            // 4. Arp Synth (16th notes: 8 notes/sec)
            int arpIdx = ((int)(beat * 4.0f)) % arpNotes.Length;
            float aFreq = arpNotes[arpIdx];
            float arpT = (beat * 4.0f) % 1.0f;
            float arpEnv = MathF.Exp(-9.0f * arpT);
            float arp = MathF.Sin(2.0f * MathF.PI * aFreq * t) * 0.20f * arpEnv;

            float mix = kick + hat + bass + arp;
            buf[i] = (short)Math.Clamp(mix * 28000.0f, -32767f, 32767f);
        }

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var bw = new BinaryWriter(fs, Encoding.ASCII);
        WriteWavHeader(bw, buf.Length, rate);
        foreach (var s in buf) bw.Write(s);
    }

    private static void GenerateBossBgmFile(string filePath)
    {
        int rate = 22050;
        float duration = 6.62f; // ~145 BPM loop (16 beats)
        int count = (int)(rate * duration);
        short[] buf = new short[count];
        Random rng = new(4242);

        float[] bossNotes = { 164.81f, 174.61f, 196.00f, 220.00f, 246.94f, 220.00f, 196.00f, 174.61f };

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float beat = t * (145.0f / 60.0f); // 145 BPM

            // 1. Heavy industrial kick
            float kickT = beat % 1.0f;
            float kickFreq = 160.0f * MathF.Exp(-22.0f * kickT) + 45.0f;
            float kick = MathF.Sin(2.0f * MathF.PI * kickFreq * kickT) * MathF.Pow(1.0f - kickT, 2.8f) * 0.50f;

            // 2. Snare / Clang on beats 1 and 3
            float snareT = ((beat + 0.5f) % 1.0f);
            float snare = ((float)rng.NextDouble() * 2.0f - 1.0f) * MathF.Exp(-18.0f * snareT) * 0.22f;

            // 3. Menacing Sub Bass Pulse
            float bFreq = (beat % 4.0f < 2.0f) ? 82.41f : 73.42f; // E2 or D2
            float bass = MathF.Sin(2.0f * MathF.PI * bFreq * t);
            bass = MathF.Sign(bass) * MathF.Pow(MathF.Abs(bass), 0.7f) * 0.35f; // Overdrive saturation

            // 4. Aggressive Lead Synth
            int leadIdx = ((int)(beat * 4.0f)) % bossNotes.Length;
            float lFreq = bossNotes[leadIdx] * 2.0f;
            float leadT = (beat * 4.0f) % 1.0f;
            float lead = MathF.Sin(2.0f * MathF.PI * lFreq * t) * MathF.Exp(-7.0f * leadT) * 0.25f;

            float mix = kick + snare + bass + lead;
            buf[i] = (short)Math.Clamp(mix * 29000.0f, -32767f, 32767f);
        }

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var bw = new BinaryWriter(fs, Encoding.ASCII);
        WriteWavHeader(bw, buf.Length, rate);
        foreach (var s in buf) bw.Write(s);
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
        StopMusic();
        _sndSlash?.Dispose();
        _sndHit?.Dispose();
        _sndParry?.Dispose();
        _sndDash?.Dispose();
        _sndMolt?.Dispose();
        _sndSpark?.Dispose();

        try { if (File.Exists(_exploreWavPath)) File.Delete(_exploreWavPath); } catch { }
        try { if (File.Exists(_bossWavPath)) File.Delete(_bossWavPath); } catch { }
    }
}
