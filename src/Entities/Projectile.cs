using System;
using System.Numerics;

namespace PyreGame.Entities;

public sealed class Projectile
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Damage;
    public float Radius = 6.0f;
    public float Life = 3.5f;
    public bool IsDead => Life <= 0.0f;

    public Projectile(Vector2 pos, Vector2 vel, float damage)
    {
        Position = pos;
        Velocity = vel;
        Damage = damage;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Life -= dt;
    }
}

public sealed class SparkItem
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Life = 15.0f;
    public bool IsDead => Life <= 0.0f;

    public SparkItem(Vector2 pos)
    {
        Position = pos;
        Random rng = new();
        float ang = (float)rng.NextDouble() * MathF.PI * 2.0f;
        float speed = 30.0f + (float)rng.NextDouble() * 50.0f;
        Velocity = new Vector2(MathF.Cos(ang) * speed, MathF.Sin(ang) * speed);
    }

    public void Update(Vector2 playerPos, float dt)
    {
        Life -= dt;
        Vector2 toPlayer = playerPos - Position;
        float dist = toPlayer.Length();

        if (dist < 220.0f && dist > 1.0f)
        {
            Vector2 dir = toPlayer / dist;
            float pullSpeed = 400.0f * (1.0f - dist / 240.0f) + 120.0f;
            Velocity = Vector2.Lerp(Velocity, dir * pullSpeed, 1.0f - MathF.Exp(-8.0f * dt));
        }
        else
        {
            Velocity *= MathF.Exp(-3.0f * dt);
        }

        Position += Velocity * dt;
    }
}
