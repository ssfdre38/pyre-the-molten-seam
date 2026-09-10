using System;
using System.Collections.Generic;
using System.Numerics;

namespace PyreGame.World;

public struct Pillar
{
    public Vector2 Position;
    public float Radius;
}

public sealed class Arena
{
    public float Width = 1800.0f;
    public float Height = 1400.0f;
    public List<Pillar> Pillars { get; } = new();

    public Arena()
    {
        Pillars.Add(new Pillar { Position = new Vector2(-450, -250), Radius = 42.0f });
        Pillars.Add(new Pillar { Position = new Vector2(450, -250), Radius = 42.0f });
        Pillars.Add(new Pillar { Position = new Vector2(-450, 250), Radius = 42.0f });
        Pillars.Add(new Pillar { Position = new Vector2(450, 250), Radius = 42.0f });

        Pillars.Add(new Pillar { Position = new Vector2(0, -400), Radius = 36.0f });
        Pillars.Add(new Pillar { Position = new Vector2(0, 400), Radius = 36.0f });
    }

    public Vector2 ClampPosition(Vector2 pos, float radius)
    {
        float minX = -Width / 2f + radius + 20f;
        float maxX = Width / 2f - radius - 20f;
        float minY = -Height / 2f + radius + 20f;
        float maxY = Height / 2f - radius - 20f;

        pos.X = Math.Clamp(pos.X, minX, maxX);
        pos.Y = Math.Clamp(pos.Y, minY, maxY);

        foreach (var p in Pillars)
        {
            Vector2 toEntity = pos - p.Position;
            float dist = toEntity.Length();
            float minDist = radius + p.Radius;

            if (dist < minDist && dist > 0.001f)
            {
                Vector2 normal = toEntity / dist;
                pos = p.Position + normal * minDist;
            }
        }

        return pos;
    }
}
