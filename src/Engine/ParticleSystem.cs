using System;
using System.Collections.Generic;
using System.Numerics;

namespace PyreGame.Engine;

public enum ParticleType
{
    Ember,
    Spark,
    Flame,
    Smoke,
    Shockwave
}

public readonly struct GameColor
{
    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
    public readonly byte A;

    public GameColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r; G = g; B = b; A = a;
    }

    public static GameColor Lerp(GameColor a, GameColor b, float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        return new GameColor(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t)
        );
    }

    public static readonly GameColor Gold = new(255, 215, 0);
    public static readonly GameColor Orange = new(255, 140, 0);
    public static readonly GameColor Red = new(230, 40, 20);
    public static readonly GameColor Yellow = new(255, 255, 50);
    public static readonly GameColor SkyBlue = new(135, 206, 235);
    public static readonly GameColor White = new(255, 255, 255);
}

public struct Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public GameColor StartColor;
    public GameColor EndColor;
    public float Size;
    public float StartSize;
    public float Life;
    public float MaxLife;
    public ParticleType Type;
}

public sealed class ParticleSystem
{
    private readonly List<Particle> _particles = new(1024);
    private readonly Random _rng = new();

    public IReadOnlyList<Particle> Particles => _particles;

    public void EmitEmber(Vector2 pos)
    {
        float angle = -MathF.PI * 0.5f + ((float)_rng.NextDouble() - 0.5f) * 0.8f;
        float speed = 25.0f + (float)_rng.NextDouble() * 50.0f;
        float life = 1.0f + (float)_rng.NextDouble() * 1.5f;

        _particles.Add(new Particle
        {
            Position = pos,
            Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
            StartColor = new GameColor(255, 180, 50, 240),
            EndColor = new GameColor(220, 50, 20, 0),
            Size = 2.5f + (float)_rng.NextDouble() * 2.5f,
            StartSize = 3.0f,
            Life = life,
            MaxLife = life,
            Type = ParticleType.Ember
        });
    }

    public void EmitBurst(Vector2 pos, int count, GameColor startColor, GameColor endColor, float speedMult = 1.0f)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)_rng.NextDouble() * MathF.PI * 2.0f;
            float speed = (40.0f + (float)_rng.NextDouble() * 160.0f) * speedMult;
            float life = 0.3f + (float)_rng.NextDouble() * 0.5f;
            float size = 3.0f + (float)_rng.NextDouble() * 4.0f;

            _particles.Add(new Particle
            {
                Position = pos,
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                StartColor = startColor,
                EndColor = endColor,
                Size = size,
                StartSize = size,
                Life = life,
                MaxLife = life,
                Type = ParticleType.Spark
            });
        }
    }

    public void EmitFlameTrail(Vector2 pos)
    {
        float angle = (float)_rng.NextDouble() * MathF.PI * 2.0f;
        float speed = 10.0f + (float)_rng.NextDouble() * 25.0f;
        float life = 0.4f + (float)_rng.NextDouble() * 0.3f;
        float size = 6.0f + (float)_rng.NextDouble() * 6.0f;

        _particles.Add(new Particle
        {
            Position = pos + new Vector2(((float)_rng.NextDouble() - 0.5f) * 12f, ((float)_rng.NextDouble() - 0.5f) * 12f),
            Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
            StartColor = new GameColor(255, 140, 0, 200),
            EndColor = new GameColor(180, 20, 0, 0),
            Size = size,
            StartSize = size,
            Life = life,
            MaxLife = life,
            Type = ParticleType.Flame
        });
    }

    public void EmitShockwave(Vector2 pos, float maxRadius = 120.0f)
    {
        _particles.Add(new Particle
        {
            Position = pos,
            Velocity = Vector2.Zero,
            StartColor = new GameColor(255, 120, 30, 230),
            EndColor = new GameColor(255, 60, 0, 0),
            Size = 10.0f,
            StartSize = maxRadius,
            Life = 0.4f,
            MaxLife = 0.4f,
            Type = ParticleType.Shockwave
        });
    }

    public void Update(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;

            if (p.Life <= 0.0f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;

            if (p.Type == ParticleType.Spark)
            {
                p.Velocity *= MathF.Exp(-4.0f * dt);
            }
            else if (p.Type == ParticleType.Ember)
            {
                p.Velocity.X += MathF.Sin(p.Life * 8.0f) * 15.0f * dt;
            }
            else if (p.Type == ParticleType.Shockwave)
            {
                float t = 1.0f - (p.Life / p.MaxLife);
                p.Size = 10.0f + (p.StartSize - 10.0f) * t;
            }

            _particles[i] = p;
        }
    }
}
