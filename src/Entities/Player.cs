using System;
using System.Numerics;

namespace PyreGame.Entities;

public enum PlayerPersona
{
    Sovereign, // Heavy Ash Knight (Greatsword + Aegis Shield)
    Cinder     // Hyper-speed Emberborn (Dual Flame Daggers + Wings)
}

public sealed class Player
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float FacingAngle; // Radians
    public PlayerPersona Persona = PlayerPersona.Sovereign;

    // Health & Vitals
    public float MaxHealth = 100.0f;
    public float CurrentHealth = 100.0f;
    public float PyreCharge = 0.0f; // 0 to 100
    public float RebirthMeter = 0.0f; // 0 to 100 (while in Cinder form)

    // Timers & Cooldowns
    public float AttackCooldown = 0.0f;
    public float DashCooldown = 0.0f;
    public float DashDuration = 0.0f;
    public float InvulnerabilityTimer = 0.0f;
    public float BlockTimer = 0.0f;
    public bool IsBlocking => BlockTimer > 0.0f;
    public bool IsDashing => DashDuration > 0.0f;

    // Movement Stats
    public float SovereignSpeed = 240.0f;
    public float CinderSpeed = 380.0f;
    public float Radius = 18.0f;

    // Attack State
    public float SlashVisualTimer = 0.0f;
    public float SlashArcAngle = 0.0f;

    public bool IsDead => CurrentHealth <= 0.0f && Persona != PlayerPersona.Sovereign;

    public Player(Vector2 startPos)
    {
        Position = startPos;
    }

    public void Update(Vector2 moveInput, Vector2 lookTarget, float dt)
    {
        // 1. Aiming / Rotation
        Vector2 aimDir = lookTarget - Position;
        if (aimDir.LengthSquared() > 0.001f)
        {
            FacingAngle = MathF.Atan2(aimDir.Y, aimDir.X);
        }

        // 2. Cooldowns & Timers
        if (AttackCooldown > 0.0f) AttackCooldown -= dt;
        if (DashCooldown > 0.0f) DashCooldown -= dt;
        if (InvulnerabilityTimer > 0.0f) InvulnerabilityTimer -= dt;
        if (BlockTimer > 0.0f) BlockTimer -= dt;
        if (SlashVisualTimer > 0.0f) SlashVisualTimer -= dt;

        // 3. Movement
        float currentSpeed = Persona == PlayerPersona.Sovereign ? SovereignSpeed : CinderSpeed;
        if (IsBlocking) currentSpeed *= 0.4f;

        if (IsDashing)
        {
            DashDuration -= dt;
            currentSpeed *= 2.8f;
        }

        if (moveInput.LengthSquared() > 0.01f)
        {
            moveInput = Vector2.Normalize(moveInput);
            Velocity = Vector2.Lerp(Velocity, moveInput * currentSpeed, 1.0f - MathF.Exp(-16.0f * dt));
        }
        else
        {
            Velocity = Vector2.Lerp(Velocity, Vector2.Zero, 1.0f - MathF.Exp(-12.0f * dt));
        }

        Position += Velocity * dt;

        // 4. Persona Special Rules
        if (Persona == PlayerPersona.Cinder)
        {
            CurrentHealth -= 5.0f * dt;
            if (CurrentHealth <= 1.0f) CurrentHealth = 1.0f;
        }
    }

    public bool TryAttack()
    {
        if (AttackCooldown > 0.0f || IsDashing) return false;

        if (Persona == PlayerPersona.Sovereign)
        {
            AttackCooldown = 0.38f;
            SlashVisualTimer = 0.18f;
            SlashArcAngle = FacingAngle;
            PyreCharge = MathF.Min(100.0f, PyreCharge + 6.0f);
        }
        else
        {
            AttackCooldown = 0.14f;
            SlashVisualTimer = 0.10f;
            SlashArcAngle = FacingAngle;
            RebirthMeter = MathF.Min(100.0f, RebirthMeter + 8.0f);
        }

        return true;
    }

    public bool TryBlockOrDash()
    {
        if (Persona == PlayerPersona.Sovereign)
        {
            BlockTimer = 0.45f;
            InvulnerabilityTimer = 0.45f;
            return true;
        }
        else
        {
            if (DashCooldown > 0.0f) return false;
            DashCooldown = 0.75f;
            DashDuration = 0.22f;
            InvulnerabilityTimer = 0.25f;
            return true;
        }
    }

    public bool TriggerMolt()
    {
        if (Persona == PlayerPersona.Sovereign)
        {
            Persona = PlayerPersona.Cinder;
            CurrentHealth = MaxHealth * 0.85f;
            PyreCharge = 0.0f;
            RebirthMeter = 0.0f;
            InvulnerabilityTimer = 1.2f;
            return true;
        }
        return false;
    }

    public bool TriggerRebirth()
    {
        if (Persona == PlayerPersona.Cinder && RebirthMeter >= 100.0f)
        {
            Persona = PlayerPersona.Sovereign;
            CurrentHealth = MaxHealth;
            RebirthMeter = 0.0f;
            PyreCharge = 20.0f;
            InvulnerabilityTimer = 1.2f;
            return true;
        }
        return false;
    }

    public bool TakeDamage(float amount)
    {
        if (InvulnerabilityTimer > 0.0f) return false;

        if (IsBlocking && Persona == PlayerPersona.Sovereign)
        {
            PyreCharge = MathF.Min(100.0f, PyreCharge + 25.0f);
            return false;
        }

        CurrentHealth -= amount;
        InvulnerabilityTimer = 0.35f;

        if (CurrentHealth <= 0.0f && Persona == PlayerPersona.Sovereign)
        {
            TriggerMolt();
            return false;
        }

        return true;
    }

    public void AddSpark()
    {
        if (Persona == PlayerPersona.Cinder)
        {
            CurrentHealth = MathF.Min(MaxHealth, CurrentHealth + 12.0f);
            RebirthMeter = MathF.Min(100.0f, RebirthMeter + 15.0f);
        }
        else
        {
            PyreCharge = MathF.Min(100.0f, PyreCharge + 10.0f);
            CurrentHealth = MathF.Min(MaxHealth, CurrentHealth + 5.0f);
        }
    }
}
