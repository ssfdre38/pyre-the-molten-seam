using System;
using System.Numerics;

namespace PyreGame.Entities;

public enum EnemyType
{
    AshScuttler,
    MagmaSpitter,
    ObsidianBrute,
    SeamWarden
}

public sealed class Enemy
{
    public Vector2 Position;
    public Vector2 Velocity;
    public EnemyType Type;
    public float MaxHealth;
    public float CurrentHealth;
    public float Speed;
    public float Radius;
    public float AttackCooldown = 0.0f;
    public float AttackWindup = 0.0f;
    public float StunTimer = 0.0f;
    public bool IsDead => CurrentHealth <= 0.0f;
    public bool IsBoss => Type == EnemyType.SeamWarden;
    public bool IsEnraged => IsBoss && CurrentHealth < MaxHealth * 0.5f;
    public bool HasTriggeredEnrage = false;

    public bool IsTelegraphing => AttackWindup > 0.0f;
    public Vector2 TelegraphTarget;

    public Enemy(Vector2 pos, EnemyType type)
    {
        Position = pos;
        Type = type;

        switch (type)
        {
            case EnemyType.AshScuttler:
                MaxHealth = 25.0f;
                Speed = 160.0f;
                Radius = 14.0f;
                break;
            case EnemyType.MagmaSpitter:
                MaxHealth = 45.0f;
                Speed = 110.0f;
                Radius = 16.0f;
                break;
            case EnemyType.ObsidianBrute:
                MaxHealth = 160.0f;
                Speed = 75.0f;
                Radius = 26.0f;
                break;
            case EnemyType.SeamWarden:
                MaxHealth = 1200.0f;
                Speed = 100.0f;
                Radius = 45.0f;
                break;
        }
        CurrentHealth = MaxHealth;
    }

    public void Update(Vector2 playerPos, float dt, Action<Vector2, Vector2, float>? spawnProjectileCallback = null)
    {
        if (StunTimer > 0.0f)
        {
            StunTimer -= dt;
            return;
        }

        if (AttackCooldown > 0.0f) AttackCooldown -= dt;

        Vector2 toPlayer = playerPos - Position;
        float dist = toPlayer.Length();
        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.UnitX;

        if (AttackWindup > 0.0f)
        {
            AttackWindup -= dt;
            Velocity = Vector2.Zero;

            if (AttackWindup <= 0.0f)
            {
                if (Type == EnemyType.MagmaSpitter)
                {
                    spawnProjectileCallback?.Invoke(Position, dir * 280.0f, 15.0f);
                    AttackCooldown = 2.4f;
                }
                else if (Type == EnemyType.SeamWarden)
                {
                    if (IsEnraged)
                    {
                        // Phase 2: 10-way full 360° magma starburst
                        for (int a = 0; a < 10; a++)
                        {
                            float ang = a * (MathF.PI * 2f / 10f);
                            Vector2 pDir = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 290.0f;
                            spawnProjectileCallback?.Invoke(Position, pDir, 18.0f);
                        }
                        AttackCooldown = 1.35f;
                    }
                    else
                    {
                        for (int a = -2; a <= 2; a++)
                        {
                            float ang = MathF.Atan2(dir.Y, dir.X) + a * 0.25f;
                            Vector2 pDir = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 260.0f;
                            spawnProjectileCallback?.Invoke(Position, pDir, 20.0f);
                        }
                        AttackCooldown = 2.0f;
                    }
                }
            }
            return;
        }

        switch (Type)
        {
            case EnemyType.AshScuttler:
                Velocity = Vector2.Lerp(Velocity, dir * Speed, 1.0f - MathF.Exp(-10.0f * dt));
                break;

            case EnemyType.MagmaSpitter:
                if (dist < 220.0f)
                {
                    Velocity = Vector2.Lerp(Velocity, -dir * Speed, 1.0f - MathF.Exp(-8.0f * dt));
                }
                else if (dist > 320.0f)
                {
                    Velocity = Vector2.Lerp(Velocity, dir * Speed, 1.0f - MathF.Exp(-8.0f * dt));
                }
                else
                {
                    Velocity = Vector2.Zero;
                    if (AttackCooldown <= 0.0f)
                    {
                        AttackWindup = 0.6f;
                        TelegraphTarget = playerPos;
                    }
                }
                break;

            case EnemyType.ObsidianBrute:
                if (dist < 140.0f && AttackCooldown <= 0.0f)
                {
                    AttackWindup = 0.5f;
                    TelegraphTarget = playerPos;
                }
                else
                {
                    Velocity = Vector2.Lerp(Velocity, dir * Speed, 1.0f - MathF.Exp(-6.0f * dt));
                }
                break;

            case EnemyType.SeamWarden:
                float bossSpeed = IsEnraged ? Speed * 1.45f : Speed;
                Velocity = Vector2.Lerp(Velocity, dir * bossSpeed, 1.0f - MathF.Exp(-5.5f * dt));
                if (AttackCooldown <= 0.0f)
                {
                    AttackWindup = IsEnraged ? 0.45f : 0.65f;
                    TelegraphTarget = playerPos;
                }
                break;
        }

        Position += Velocity * dt;
    }

    public void Stun(float duration)
    {
        StunTimer = duration;
        AttackWindup = 0.0f;
    }
}
