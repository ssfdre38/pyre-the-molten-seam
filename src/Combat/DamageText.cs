using System;
using System.Collections.Generic;
using System.Numerics;
using PyreGame.Engine;

namespace PyreGame.Combat;

public struct DamageNumber
{
    public Vector2 Position;
    public string Text;
    public GameColor Color;
    public float Life;
    public float MaxLife;
}

public sealed class DamageTextManager
{
    private readonly List<DamageNumber> _numbers = new(64);

    public IReadOnlyList<DamageNumber> Numbers => _numbers;

    public void Spawn(Vector2 pos, string text, GameColor color, float duration = 0.65f)
    {
        _numbers.Add(new DamageNumber
        {
            Position = pos + new Vector2(((float)Random.Shared.NextDouble() - 0.5f) * 14f, -10f),
            Text = text,
            Color = color,
            Life = duration,
            MaxLife = duration
        });
    }

    public void Update(float dt)
    {
        for (int i = _numbers.Count - 1; i >= 0; i--)
        {
            var d = _numbers[i];
            d.Life -= dt;
            if (d.Life <= 0.0f)
            {
                _numbers.RemoveAt(i);
                continue;
            }

            d.Position.Y -= 35.0f * dt;
            _numbers[i] = d;
        }
    }
}
