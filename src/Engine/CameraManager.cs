using System;
using System.Numerics;

namespace PyreGame.Engine;

public sealed class CameraManager
{
    public Vector2 Target;
    public Vector2 ShakeOffset;
    public float Zoom = 1.0f;
    private float _trauma;
    private readonly Random _rng = new();

    public CameraManager()
    {
        Target = Vector2.Zero;
    }

    public void AddTrauma(float amount)
    {
        _trauma = Math.Clamp(_trauma + amount, 0.0f, 1.0f);
    }

    public void Update(Vector2 targetPos, float dt)
    {
        // Smooth lerp follow
        float lerpFactor = 1.0f - MathF.Exp(-8.0f * dt);
        Target = Vector2.Lerp(Target, targetPos, lerpFactor);

        // Screenshake using trauma decay
        if (_trauma > 0.0f)
        {
            float shake = _trauma * _trauma;
            float maxOffset = 18.0f;
            ShakeOffset.X = ((float)_rng.NextDouble() * 2.0f - 1.0f) * maxOffset * shake;
            ShakeOffset.Y = ((float)_rng.NextDouble() * 2.0f - 1.0f) * maxOffset * shake;
            _trauma = MathF.Max(0.0f, _trauma - 1.8f * dt);
        }
        else
        {
            ShakeOffset = Vector2.Zero;
        }
    }

    public Vector2 WorldToScreen(Vector2 worldPos, float screenWidth, float screenHeight)
    {
        Vector2 screenCenter = new Vector2(screenWidth / 2f, screenHeight / 2f);
        return (worldPos - Target) * Zoom + screenCenter + ShakeOffset;
    }

    public Vector2 ScreenToWorld(Vector2 screenPos, float screenWidth, float screenHeight)
    {
        Vector2 screenCenter = new Vector2(screenWidth / 2f, screenHeight / 2f);
        return (screenPos - screenCenter - ShakeOffset) / Zoom + Target;
    }
}
