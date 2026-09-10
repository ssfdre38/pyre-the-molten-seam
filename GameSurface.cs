using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PyreGame.Combat;
using PyreGame.Engine;
using PyreGame.Entities;
using PyreGame.World;

namespace PyreGame;

public enum GameState
{
    Title,
    Playing,
    RelicSelect,
    Victory,
    GameOver
}

public sealed class GameSurface : FrameworkElement
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private double _lastTimeSec;
    private readonly SoundSynth _soundSynth = new();
    private readonly CameraManager _cameraMgr = new();
    private readonly ParticleSystem _particles = new();
    private readonly DamageTextManager _damageTexts = new();
    private readonly Arena _arena = new();

    private Player _player = new(Vector2.Zero);
    private readonly List<Enemy> _enemies = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<SparkItem> _sparks = new();

    private GameState _state = GameState.Title;
    private int _currentWave = 1;
    private float _waveSpawnTimer = 0.0f;
    private int _totalKills = 0;
    private float _hitStopTimer = 0.0f;
    private bool _bossSpawned = false;

    private float _playerDamageMult = 1.0f;
    private float _playerSpeedMult = 1.0f;
    private bool _parryNovaRelic = false;

    // Run telemetry & high score stats
    private float _runDurationSeconds = 0.0f;
    private int _sparksCollected = 0;
    private int _parriesCount = 0;
    private float _totalDamageDealt = 0.0f;
    private int _moltsUsed = 0;

    // Drawing resources cached for performance
    private readonly Typeface _typeface = new("Segoe UI");
    private readonly Typeface _boldTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private readonly Typeface _blackTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

    public GameSurface()
    {
        Focusable = true;
        Loaded += (s, e) =>
        {
            Focus();
            CompositionTarget.Rendering += OnCompositionRendering;
        };
        Unloaded += (s, e) =>
        {
            CompositionTarget.Rendering -= OnCompositionRendering;
            _soundSynth.Dispose();
        };

        // Event Listeners for Input
        MouseDown += OnSurfaceMouseDown;
        MouseUp += OnSurfaceMouseUp;
        KeyDown += OnSurfaceKeyDown;
    }

    private void ResetRun()
    {
        _player = new Player(Vector2.Zero);
        _enemies.Clear();
        _projectiles.Clear();
        _sparks.Clear();
        _currentWave = 1;
        _waveSpawnTimer = 1.5f;
        _totalKills = 0;
        _bossSpawned = false;
        _playerDamageMult = 1.0f;
        _playerSpeedMult = 1.0f;
        _parryNovaRelic = false;
        _runDurationSeconds = 0.0f;
        _sparksCollected = 0;
        _parriesCount = 0;
        _totalDamageDealt = 0.0f;
        _moltsUsed = 0;
        _state = GameState.Playing;
        _soundSynth.PlayExploreMusic(force: true);
    }

    private void SpawnWave(int wave)
    {
        _enemies.Clear();
        var rng = new Random();

        if (wave < 5)
        {
            int scuttlers = 4 + wave * 3;
            int spitters = wave >= 2 ? 1 + wave : 0;
            int brutes = wave >= 3 ? wave - 1 : 0;

            for (int i = 0; i < scuttlers; i++)
            {
                float ang = (float)rng.NextDouble() * MathF.PI * 2f;
                float r = 400f + (float)rng.NextDouble() * 300f;
                _enemies.Add(new Enemy(new Vector2(MathF.Cos(ang) * r, MathF.Sin(ang) * r), EnemyType.AshScuttler));
            }
            for (int i = 0; i < spitters; i++)
            {
                float ang = (float)rng.NextDouble() * MathF.PI * 2f;
                float r = 480f + (float)rng.NextDouble() * 250f;
                _enemies.Add(new Enemy(new Vector2(MathF.Cos(ang) * r, MathF.Sin(ang) * r), EnemyType.MagmaSpitter));
            }
            for (int i = 0; i < brutes; i++)
            {
                float ang = (float)rng.NextDouble() * MathF.PI * 2f;
                float r = 420f + (float)rng.NextDouble() * 200f;
                _enemies.Add(new Enemy(new Vector2(MathF.Cos(ang) * r, MathF.Sin(ang) * r), EnemyType.ObsidianBrute));
            }
        }
        else
        {
            _bossSpawned = true;
            _enemies.Add(new Enemy(new Vector2(0, -350), EnemyType.SeamWarden));
            _cameraMgr.AddTrauma(0.8f);
            _soundSynth.PlayMolt();
            _soundSynth.PlayBossMusic();
        }
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
    {
        double currentTime = _stopwatch.Elapsed.TotalSeconds;
        float dt = (float)(currentTime - _lastTimeSec);
        _lastTimeSec = currentTime;
        if (dt > 0.05f) dt = 0.05f;

        UpdateGame(dt);
        InvalidateVisual();
    }

    private void UpdateGame(float dt)
    {
        if (_hitStopTimer > 0.0f)
        {
            _hitStopTimer -= dt;
            _particles.Update(dt);
            return;
        }

        // Ambient Embers
        if (Random.Shared.NextSingle() < 0.35f && _state == GameState.Playing)
        {
            Vector2 emberSpawn = _player.Position + new Vector2(
                ((float)Random.Shared.NextDouble() - 0.5f) * 1100f,
                ((float)Random.Shared.NextDouble() - 0.5f) * 700f
            );
            _particles.EmitEmber(emberSpawn);
        }

        if (_state == GameState.Playing)
        {
            _runDurationSeconds += dt;

            // 1. Gather Keyboard Input
            Vector2 moveInput = Vector2.Zero;
            if (Keyboard.IsKeyDown(Key.W)) moveInput.Y -= 1f;
            if (Keyboard.IsKeyDown(Key.S)) moveInput.Y += 1f;
            if (Keyboard.IsKeyDown(Key.A)) moveInput.X -= 1f;
            if (Keyboard.IsKeyDown(Key.D)) moveInput.X += 1f;

            // Aiming target in World Coordinates
            Point mousePos = Mouse.GetPosition(this);
            Vector2 mouseScreen = new((float)mousePos.X, (float)mousePos.Y);
            Vector2 mouseWorld = _cameraMgr.ScreenToWorld(mouseScreen, (float)ActualWidth, (float)ActualHeight);

            // Flame Dash Trail
            if (_player.IsDashing)
            {
                _particles.EmitFlameTrail(_player.Position);
            }

            // 2. Update Player
            _player.Update(moveInput, mouseWorld, dt);
            _player.Position = _arena.ClampPosition(_player.Position, _player.Radius);

            if (_player.IsDead)
            {
                _soundSynth.PlayHit();
                _soundSynth.StopMusic();
                _cameraMgr.AddTrauma(0.9f);
                _state = GameState.GameOver;
            }

            // 3. Update Enemies
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];

                // Phase 2: Boss Enrage Trigger
                if (enemy.IsBoss && enemy.IsEnraged && !enemy.HasTriggeredEnrage)
                {
                    enemy.HasTriggeredEnrage = true;
                    _cameraMgr.AddTrauma(0.90f);
                    _soundSynth.PlayMolt();
                    _particles.EmitShockwave(enemy.Position, 280.0f);
                    _damageTexts.Spawn(enemy.Position, "TECTONIC RUPTURE!", GameColor.Orange, 1.4f);
                }

                enemy.Update(_player.Position, dt, (spawnPos, vel, dmg) =>
                {
                    _projectiles.Add(new Projectile(spawnPos, vel, dmg));
                });
                enemy.Position = _arena.ClampPosition(enemy.Position, enemy.Radius);

                float dist = Vector2.Distance(_player.Position, enemy.Position);
                if (dist < _player.Radius + enemy.Radius)
                {
                    float dmg = enemy.Type == EnemyType.AshScuttler ? 14f : (enemy.Type == EnemyType.ObsidianBrute ? 32f : 22f);
                    if (_player.TakeDamage(dmg))
                    {
                        _soundSynth.PlayHit();
                        _cameraMgr.AddTrauma(0.35f);
                        _damageTexts.Spawn(_player.Position, $"-{dmg:F0}", GameColor.Red);
                    }
                    else if (_player.IsBlocking)
                    {
                        _parriesCount++;
                        _soundSynth.PlayParry();
                        enemy.Stun(0.8f);
                        enemy.Velocity = -enemy.Velocity * 1.5f;
                        _cameraMgr.AddTrauma(0.2f);
                        _particles.EmitBurst(_player.Position, 12, GameColor.Gold, GameColor.White);

                        if (_parryNovaRelic)
                        {
                            _particles.EmitShockwave(_player.Position, 120.0f);
                            enemy.CurrentHealth -= 35.0f;
                        }
                    }
                }
            }

            // 4. Update Projectiles
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                p.Update(dt);

                if (Vector2.Distance(p.Position, _player.Position) < p.Radius + _player.Radius)
                {
                    if (_player.TakeDamage(p.Damage))
                    {
                        _soundSynth.PlayHit();
                        _cameraMgr.AddTrauma(0.25f);
                        _damageTexts.Spawn(_player.Position, $"-{p.Damage:F0}", GameColor.Red);
                    }
                    else if (_player.IsBlocking)
                    {
                        _parriesCount++;
                        _soundSynth.PlayParry();
                        _particles.EmitBurst(_player.Position, 10, GameColor.Gold, GameColor.White);
                    }
                    _projectiles.RemoveAt(i);
                    continue;
                }

                if (p.IsDead) _projectiles.RemoveAt(i);
            }

            // 5. Update Sparks
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.Update(_player.Position, dt);

                if (Vector2.Distance(s.Position, _player.Position) < _player.Radius + 14.0f)
                {
                    _sparksCollected++;
                    _player.AddSpark();
                    _soundSynth.PlaySpark();
                    _damageTexts.Spawn(_player.Position, "+SPARK", GameColor.Gold, 0.45f);
                    _particles.EmitBurst(_player.Position, 8, GameColor.Gold, GameColor.Yellow);
                    _sparks.RemoveAt(i);
                    continue;
                }

                if (s.IsDead) _sparks.RemoveAt(i);
            }

            // 6. Wave Progress Check
            if (_enemies.Count == 0 && !_bossSpawned)
            {
                _waveSpawnTimer -= dt;
                if (_waveSpawnTimer <= 0.0f)
                {
                    if (_currentWave < 4)
                    {
                        _currentWave++;
                        _state = GameState.RelicSelect;
                    }
                    else
                    {
                        _currentWave = 5;
                        SpawnWave(5);
                    }
                }
            }

            _cameraMgr.Update(_player.Position, dt);
        }

        _particles.Update(dt);
        _damageTexts.Update(dt);
    }

    private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_state == GameState.Title)
        {
            ResetRun();
            return;
        }

        if (_state == GameState.GameOver || _state == GameState.Victory)
        {
            ResetRun();
            return;
        }

        if (_state == GameState.Playing)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ExecutePlayerAttack();
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                ExecutePlayerDefendOrDash();
            }
        }
    }

    private void OnSurfaceMouseUp(object sender, MouseButtonEventArgs e)
    {
    }

    private void OnSurfaceKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.M)
        {
            _soundSynth.ToggleMute();
            e.Handled = true;
            return;
        }

        if (_state == GameState.Title && (e.Key == Key.Space || e.Key == Key.Enter))
        {
            ResetRun();
            e.Handled = true;
            return;
        }

        if ((_state == GameState.GameOver || _state == GameState.Victory) && (e.Key == Key.Space || e.Key == Key.Enter))
        {
            ResetRun();
            e.Handled = true;
            return;
        }

        if (_state == GameState.RelicSelect)
        {
            if (e.Key == Key.D1 || e.Key == Key.NumPad1)
            {
                _playerDamageMult += 0.30f;
                _state = GameState.Playing;
                _waveSpawnTimer = 1.0f;
                SpawnWave(_currentWave);
                e.Handled = true;
            }
            else if (e.Key == Key.D2 || e.Key == Key.NumPad2)
            {
                _playerSpeedMult += 0.25f;
                _player.SovereignSpeed *= 1.25f;
                _player.CinderSpeed *= 1.25f;
                _state = GameState.Playing;
                _waveSpawnTimer = 1.0f;
                SpawnWave(_currentWave);
                e.Handled = true;
            }
            else if (e.Key == Key.D3 || e.Key == Key.NumPad3)
            {
                _parryNovaRelic = true;
                _player.MaxHealth += 35.0f;
                _player.CurrentHealth = _player.MaxHealth;
                _state = GameState.Playing;
                _waveSpawnTimer = 1.0f;
                SpawnWave(_currentWave);
                e.Handled = true;
            }
            return;
        }

        if (_state == GameState.Playing)
        {
            if (e.Key == Key.Space)
            {
                ExecutePlayerMolt();
                e.Handled = true;
            }
        }
    }

    private void ExecutePlayerAttack()
    {
        if (!_player.TryAttack()) return;

        _soundSynth.PlaySlash();
        _cameraMgr.AddTrauma(0.12f);

        float attackRange = _player.Persona == PlayerPersona.Sovereign ? 78.0f : 55.0f;
        float attackArc = _player.Persona == PlayerPersona.Sovereign ? 1.6f : 1.2f;
        float baseDmg = (_player.Persona == PlayerPersona.Sovereign ? 38.0f : 24.0f) * _playerDamageMult;

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            Vector2 toEnemy = enemy.Position - _player.Position;
            float dist = toEnemy.Length();

            if (dist < attackRange + enemy.Radius)
            {
                float angleToEnemy = MathF.Atan2(toEnemy.Y, toEnemy.X);
                float angleDiff = MathF.Abs(angleToEnemy - _player.FacingAngle);
                while (angleDiff > MathF.PI) angleDiff -= MathF.PI * 2f;
                angleDiff = MathF.Abs(angleDiff);

                if (angleDiff < attackArc)
                {
                    enemy.CurrentHealth -= baseDmg;
                    _totalDamageDealt += baseDmg;
                    enemy.Velocity += Vector2.Normalize(toEnemy) * 220f;
                    _soundSynth.PlayHit();
                    _hitStopTimer = 0.05f;
                    _cameraMgr.AddTrauma(0.18f);

                    _particles.EmitBurst(enemy.Position, 14, GameColor.Yellow, GameColor.Red, 1.2f);
                    _damageTexts.Spawn(enemy.Position, $"{baseDmg:F0}", _player.Persona == PlayerPersona.Sovereign ? GameColor.Orange : GameColor.Gold);

                    if (enemy.IsDead)
                    {
                        _totalKills++;
                        _particles.EmitBurst(enemy.Position, 26, GameColor.Gold, GameColor.Red, 1.8f);
                        _sparks.Add(new SparkItem(enemy.Position));

                        if (enemy.IsBoss)
                        {
                            _state = GameState.Victory;
                            _soundSynth.StopMusic();
                        }

                        _enemies.RemoveAt(i);
                    }
                }
            }
        }
    }

    private void ExecutePlayerDefendOrDash()
    {
        if (!_player.TryBlockOrDash()) return;

        if (_player.Persona == PlayerPersona.Sovereign)
        {
            _soundSynth.PlayParry();
            _particles.EmitBurst(_player.Position, 16, GameColor.Gold, GameColor.Orange);
        }
        else
        {
            _soundSynth.PlayDash();
            _cameraMgr.AddTrauma(0.15f);
            _particles.EmitShockwave(_player.Position, 70.0f);
        }
    }

    private void ExecutePlayerMolt()
    {
        if (_player.Persona == PlayerPersona.Sovereign && _player.PyreCharge >= 100.0f)
        {
            if (_player.TriggerMolt())
            {
                _moltsUsed++;
                _soundSynth.PlayMolt();
                _cameraMgr.AddTrauma(0.7f);
                _particles.EmitShockwave(_player.Position, 220.0f);
                _particles.EmitBurst(_player.Position, 60, GameColor.Gold, GameColor.Red, 2.5f);

                foreach (var enemy in _enemies)
                {
                    Vector2 diff = enemy.Position - _player.Position;
                    if (diff.Length() < 240.0f)
                    {
                        enemy.CurrentHealth -= 65.0f * _playerDamageMult;
                        enemy.Velocity += Vector2.Normalize(diff) * 450.0f;
                        enemy.Stun(1.2f);
                    }
                }
            }
        }
        else if (_player.Persona == PlayerPersona.Cinder && _player.RebirthMeter >= 100.0f)
        {
            if (_player.TriggerRebirth())
            {
                _moltsUsed++;
                _soundSynth.PlayMolt();
                _cameraMgr.AddTrauma(0.6f);
                _particles.EmitShockwave(_player.Position, 180.0f);
                _particles.EmitBurst(_player.Position, 45, GameColor.SkyBlue, GameColor.Gold, 2.0f);
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 10 || h <= 10) return;

        // Clip to window
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));

        // Background
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(10, 12, 17)), null, new Rect(0, 0, w, h));

        if (_state == GameState.Title)
        {
            DrawTitleScreen(dc, w, h);
        }
        else
        {
            DrawGameWorld(dc, (float)w, (float)h);
            DrawHUD(dc, w, h);

            if (_state == GameState.RelicSelect) DrawRelicScreen(dc, w, h);
            else if (_state == GameState.Victory) DrawVictoryScreen(dc, w, h);
            else if (_state == GameState.GameOver) DrawGameOverScreen(dc, w, h);
        }

        dc.Pop();
    }

    private void DrawGameWorld(DrawingContext dc, float sw, float sh)
    {
        // 1. Arena Floor
        Vector2 aMin = _cameraMgr.WorldToScreen(new Vector2(-_arena.Width / 2f, -_arena.Height / 2f), sw, sh);
        Vector2 aMax = _cameraMgr.WorldToScreen(new Vector2(_arena.Width / 2f, _arena.Height / 2f), sw, sh);
        Rect arenaRect = new(new Point(aMin.X, aMin.Y), new Point(aMax.X, aMax.Y));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(14, 16, 22)), new Pen(new SolidColorBrush(Color.FromRgb(40, 45, 60)), 4), arenaRect);

        // Volcanic Pulsing Magma Cracks
        float pulse = 0.65f + MathF.Sin((float)_stopwatch.Elapsed.TotalSeconds * 3.5f) * 0.35f;
        Color lavaCol = Color.FromArgb((byte)(180 * pulse), 255, (byte)(80 + pulse * 60), 0);
        Pen lavaPen = new(new SolidColorBrush(lavaCol), 3.5);

        DrawWorldLine(dc, new Vector2(-200, -100), new Vector2(0, 0), lavaPen, sw, sh);
        DrawWorldLine(dc, new Vector2(0, 0), new Vector2(180, -60), lavaPen, sw, sh);
        DrawWorldLine(dc, new Vector2(0, 0), new Vector2(-50, 180), lavaPen, sw, sh);
        DrawWorldLine(dc, new Vector2(-50, 180), new Vector2(220, 300), lavaPen, sw, sh);

        // Pillars
        foreach (var p in _arena.Pillars)
        {
            Vector2 pScn = _cameraMgr.WorldToScreen(p.Position, sw, sh);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 32, 42)), new Pen(new SolidColorBrush(Color.FromRgb(255, 140, 40)), 2), new Point(pScn.X, pScn.Y), p.Radius, p.Radius);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(160 * pulse), 220, 70, 10)), null, new Point(pScn.X, pScn.Y), p.Radius * 0.35, p.Radius * 0.35);
        }

        // Sparks
        foreach (var s in _sparks)
        {
            Vector2 sScn = _cameraMgr.WorldToScreen(s.Position, sw, sh);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 215, 0)), null, new Point(sScn.X, sScn.Y), 4, 4);
        }

        // Projectiles
        foreach (var p in _projectiles)
        {
            Vector2 pScn = _cameraMgr.WorldToScreen(p.Position, sw, sh);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(160, 255, 60, 0)), null, new Point(pScn.X, pScn.Y), p.Radius + 2, p.Radius + 2);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 215, 0)), null, new Point(pScn.X, pScn.Y), p.Radius, p.Radius);
        }

        // Enemies
        foreach (var e in _enemies)
        {
            Vector2 eScn = _cameraMgr.WorldToScreen(e.Position, sw, sh);

            // Telegraph Indicator
            if (e.IsTelegraphing)
            {
                Vector2 tScn = _cameraMgr.WorldToScreen(e.TelegraphTarget, sw, sh);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 30, 30)), 2), new Point(eScn.X, eScn.Y), new Point(tScn.X, tScn.Y));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 255, 50, 20)), new Pen(Brushes.Red, 1.5), new Point(tScn.X, tScn.Y), 22, 22);
            }

            Brush enemyBrush = e.Type switch
            {
                EnemyType.AshScuttler => new SolidColorBrush(Color.FromRgb(45, 50, 60)),
                EnemyType.MagmaSpitter => new SolidColorBrush(Color.FromRgb(160, 60, 20)),
                EnemyType.ObsidianBrute => new SolidColorBrush(Color.FromRgb(30, 32, 40)),
                _ => new SolidColorBrush(Color.FromRgb(20, 22, 28))
            };

            dc.DrawEllipse(enemyBrush, new Pen(new SolidColorBrush(Color.FromRgb(255, 140, 30)), 1.5), new Point(eScn.X, eScn.Y), e.Radius, e.Radius);

            if (e.IsBoss)
            {
                // Boss pulsing aura
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(120 * pulse), 255, 100, 20)), 3), new Point(eScn.X, eScn.Y), e.Radius + 8, e.Radius + 8);
            }
        }

        // Player
        Vector2 plScn = _cameraMgr.WorldToScreen(_player.Position, sw, sh);
        if (_player.Persona == PlayerPersona.Sovereign)
        {
            // Sovereign Knight
            Brush armBrush = _player.InvulnerabilityTimer > 0.0f ? Brushes.White : new SolidColorBrush(Color.FromRgb(70, 80, 95));
            dc.DrawEllipse(armBrush, new Pen(new SolidColorBrush(Color.FromRgb(255, 170, 50)), 2), new Point(plScn.X, plScn.Y), _player.Radius, _player.Radius);

            // Greatsword Angle
            Vector2 tip = plScn + new Vector2(MathF.Cos(_player.FacingAngle), MathF.Sin(_player.FacingAngle)) * (_player.Radius + 14);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255, 170, 50)), 4), new Point(plScn.X, plScn.Y), new Point(tip.X, tip.Y));

            if (_player.IsBlocking)
            {
                Vector2 shCenter = plScn + new Vector2(MathF.Cos(_player.FacingAngle), MathF.Sin(_player.FacingAngle)) * (_player.Radius + 6);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, 255, 180, 40)), new Pen(Brushes.Gold, 2), new Point(shCenter.X, shCenter.Y), 11, 11);
            }
        }
        else
        {
            // Cinder Emberborn
            Brush cinBrush = _player.InvulnerabilityTimer > 0.0f ? Brushes.White : new SolidColorBrush(Color.FromRgb(255, 80, 20));
            dc.DrawEllipse(cinBrush, null, new Point(plScn.X, plScn.Y), _player.Radius, _player.Radius);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 220, 100)), null, new Point(plScn.X, plScn.Y), _player.Radius * 0.55, _player.Radius * 0.55);

            // Radiant Wings
            Vector2 wingL = plScn + new Vector2(MathF.Cos(_player.FacingAngle - 2.2f), MathF.Sin(_player.FacingAngle - 2.2f)) * (_player.Radius * 2.2f);
            Vector2 wingR = plScn + new Vector2(MathF.Cos(_player.FacingAngle + 2.2f), MathF.Sin(_player.FacingAngle + 2.2f)) * (_player.Radius * 2.2f);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 120, 0)), 4), new Point(plScn.X, plScn.Y), new Point(wingL.X, wingL.Y));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 120, 0)), 4), new Point(plScn.X, plScn.Y), new Point(wingR.X, wingR.Y));
        }

        // Particles
        foreach (var p in _particles.Particles)
        {
            Vector2 ptScn = _cameraMgr.WorldToScreen(p.Position, sw, sh);
            float progress = 1.0f - (p.Life / p.MaxLife);
            var col = GameColor.Lerp(p.StartColor, p.EndColor, progress);
            var brush = new SolidColorBrush(Color.FromArgb(col.A, col.R, col.G, col.B));

            if (p.Type == ParticleType.Shockwave)
            {
                dc.DrawEllipse(null, new Pen(brush, 2), new Point(ptScn.X, ptScn.Y), p.Size, p.Size);
            }
            else
            {
                dc.DrawEllipse(brush, null, new Point(ptScn.X, ptScn.Y), p.Size / 2f, p.Size / 2f);
            }
        }

        // Damage Numbers
        foreach (var d in _damageTexts.Numbers)
        {
            Vector2 dScn = _cameraMgr.WorldToScreen(d.Position, sw, sh);
            float progress = 1.0f - (d.Life / d.MaxLife);
            byte alpha = (byte)(255 * (1.0f - progress));
            var brush = new SolidColorBrush(Color.FromArgb(alpha, d.Color.R, d.Color.G, d.Color.B));
            DrawTextFormatted(dc, d.Text, (int)dScn.X, (int)dScn.Y, 15, brush, _boldTypeface);
        }
    }

    private void DrawWorldLine(DrawingContext dc, Vector2 p1, Vector2 p2, Pen pen, float sw, float sh)
    {
        Vector2 s1 = _cameraMgr.WorldToScreen(p1, sw, sh);
        Vector2 s2 = _cameraMgr.WorldToScreen(p2, sw, sh);
        dc.DrawLine(pen, new Point(s1.X, s1.Y), new Point(s2.X, s2.Y));
    }

    private void DrawHUD(DrawingContext dc, double w, double h)
    {
        // Top-Left: Persona Card & Health
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(230, 14, 17, 24)), new Pen(new SolidColorBrush(Color.FromRgb(45, 55, 75)), 1), new Rect(24, 24, 320, 94), 6, 6);

        string pTitle = _player.Persona == PlayerPersona.Sovereign ? "🛡️ SOVEREIGN — ASH KNIGHT" : "🔥 CINDER — EMBERBORN";
        Brush pBrush = _player.Persona == PlayerPersona.Sovereign ? new SolidColorBrush(Color.FromRgb(255, 170, 50)) : new SolidColorBrush(Color.FromRgb(255, 70, 20));
        DrawTextFormatted(dc, pTitle, 36, 32, 13, pBrush, _boldTypeface);

        // Health Bar
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 10, 10)), null, new Rect(36, 56, 296, 18));
        float hpPct = Math.Clamp(_player.CurrentHealth / _player.MaxHealth, 0f, 1f);
        Brush hpBrush = _player.Persona == PlayerPersona.Sovereign ? new SolidColorBrush(Color.FromRgb(230, 40, 30)) : new SolidColorBrush(Color.FromRgb(255, 100, 0));
        dc.DrawRectangle(hpBrush, null, new Rect(36, 56, 296 * hpPct, 18));
        DrawTextFormatted(dc, $"{_player.CurrentHealth:F0} / {_player.MaxHealth:F0}", 150, 57, 12, Brushes.White, _typeface);

        // Pyre / Rebirth Gauge
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(10, 20, 28)), null, new Rect(36, 82, 296, 14));
        if (_player.Persona == PlayerPersona.Sovereign)
        {
            float pyrePct = Math.Clamp(_player.PyreCharge / 100f, 0f, 1f);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(255, 140, 0)), null, new Rect(36, 82, 296 * pyrePct, 14));
            string txt = _player.PyreCharge >= 100f ? "⚡ THE MOLT READY [SPACE]!" : $"PYRE CHARGE: {_player.PyreCharge:F0}%";
            DrawTextFormatted(dc, txt, 100, 81, 10, Brushes.White, _boldTypeface);
        }
        else
        {
            float rebPct = Math.Clamp(_player.RebirthMeter / 100f, 0f, 1f);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(135, 206, 235)), null, new Rect(36, 82, 296 * rebPct, 14));
            string txt = _player.RebirthMeter >= 100f ? "✨ PHOENIX REBIRTH READY [SPACE]!" : $"REBIRTH METER: {_player.RebirthMeter:F0}%";
            DrawTextFormatted(dc, txt, 80, 81, 10, Brushes.White, _boldTypeface);
        }

        // Top-Right: Wave Counter & Stats
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(230, 14, 17, 24)), new Pen(new SolidColorBrush(Color.FromRgb(45, 55, 75)), 1), new Rect(w - 230, 24, 206, 88), 6, 6);
        string waveTxt = _bossSpawned ? "⚔️ BOSS CHAMBER" : $"WAVE {_currentWave} / 5";
        DrawTextFormatted(dc, waveTxt, (int)w - 216, 32, 15, Brushes.Gold, _boldTypeface);
        int m = (int)(_runDurationSeconds / 60);
        int s = (int)(_runDurationSeconds % 60);
        DrawTextFormatted(dc, $"TIME: {m:D2}:{s:D2}  •  KILLS: {_totalKills}", (int)w - 216, 54, 12, Brushes.White, _typeface);
        string muteTxt = _soundSynth.IsMuted ? "🔇 [M] AUDIO: MUTED" : "🔊 [M] AUDIO: SYNTH ON";
        DrawTextFormatted(dc, muteTxt, (int)w - 216, 74, 11, _soundSynth.IsMuted ? Brushes.Red : Brushes.LightGreen, _boldTypeface);

        // Top-Center: Boss Bar
        if (_bossSpawned)
        {
            foreach (var e in _enemies)
            {
                if (e.IsBoss)
                {
                    double bBarW = 600;
                    double bBarH = 22;
                    double bBarX = w / 2 - bBarW / 2;
                    double bBarY = 32;

                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 20, 10, 10)), null, new Rect(bBarX, bBarY, bBarW, bBarH));
                    float bPct = Math.Clamp(e.CurrentHealth / e.MaxHealth, 0f, 1f);
                    Brush bFill = e.IsEnraged ? new SolidColorBrush(Color.FromRgb(255, 50, 0)) : Brushes.Red;
                    dc.DrawRectangle(bFill, null, new Rect(bBarX, bBarY, bBarW * bPct, bBarH));
                    dc.DrawRectangle(null, new Pen(e.IsEnraged ? Brushes.OrangeRed : Brushes.Gold, e.IsEnraged ? 2.5 : 1.5), new Rect(bBarX, bBarY, bBarW, bBarH));
                    string bTitle = e.IsEnraged ? "🔥 THE SEAM WARDEN — TECTONIC RUPTURE (PHASE 2) 🔥" : "THE SEAM WARDEN — LORD OF CINDERS";
                    DrawTextFormatted(dc, bTitle, (int)bBarX + (e.IsEnraged ? 80 : 160), (int)bBarY - 18, 13, e.IsEnraged ? Brushes.OrangeRed : Brushes.Gold, _boldTypeface);
                    break;
                }
            }
        }
    }

    private void DrawTitleScreen(DrawingContext dc, double w, double h)
    {
        float pulse = 0.85f + MathF.Sin((float)_stopwatch.Elapsed.TotalSeconds * 4.0f) * 0.15f;
        Brush titleBrush = new SolidColorBrush(Color.FromRgb(255, (byte)(110 * pulse), 20));

        DrawTextFormatted(dc, "PYRE: THE MOLTEN SEAM", (int)w / 2 - 320, 140, 44, titleBrush, _blackTypeface);
        DrawTextFormatted(dc, "A SOVEREIGN ACTION ROGUELITE", (int)w / 2 - 180, 205, 18, new SolidColorBrush(Color.FromRgb(255, 180, 80)), _boldTypeface);

        int boxW = 560;
        int boxH = 200;
        int boxX = (int)w / 2 - boxW / 2;
        int boxY = 270;

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 18, 22, 30)), new Pen(new SolidColorBrush(Color.FromRgb(255, 110, 20)), 2), new Rect(boxX, boxY, boxW, boxH), 8, 8);

        DrawTextFormatted(dc, "CONTROLS:", boxX + 24, boxY + 18, 17, Brushes.Gold, _boldTypeface);
        DrawTextFormatted(dc, "• [W, A, S, D]            : Move Character", boxX + 24, boxY + 50, 15, Brushes.White, _typeface);
        DrawTextFormatted(dc, "• [Left Click]            : Greatsword Cleave / Dagger Flurry", boxX + 24, boxY + 78, 15, Brushes.White, _typeface);
        DrawTextFormatted(dc, "• [Right Click]           : Aegis Parry (Sovereign) / Flame Dash (Cinder)", boxX + 24, boxY + 106, 15, Brushes.White, _typeface);
        DrawTextFormatted(dc, "• [SPACE]                 : Trigger 'The Molt' (At 100% Pyre Charge)", boxX + 24, boxY + 136, 15, Brushes.Orange, _boldTypeface);

        DrawTextFormatted(dc, "CLICK OR PRESS [SPACE] TO COMMENCE PURGE", (int)w / 2 - 250, 510, 19, Brushes.White, _boldTypeface);
    }

    private void DrawRelicScreen(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), null, new Rect(0, 0, w, h));
        DrawTextFormatted(dc, "CHOOSE YOUR FLAME ASCENSION", (int)w / 2 - 240, 150, 26, Brushes.Gold, _boldTypeface);

        int cardW = 320;
        int cardH = 220;
        int gap = 30;
        int startX = (int)w / 2 - (cardW * 3 + gap * 2) / 2;
        int cardY = 240;

        // Card 1
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(240, 24, 28, 38)), new Pen(Brushes.Orange, 2), new Rect(startX, cardY, cardW, cardH), 8, 8);
        DrawTextFormatted(dc, "[1] MOLTEN EDGE", startX + 20, cardY + 24, 18, Brushes.Orange, _boldTypeface);
        DrawTextFormatted(dc, "All weapon strikes deal\n+30% bonus damage.", startX + 20, cardY + 70, 16, Brushes.White, _typeface);

        // Card 2
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(240, 24, 28, 38)), new Pen(Brushes.SkyBlue, 2), new Rect(startX + cardW + gap, cardY, cardW, cardH), 8, 8);
        DrawTextFormatted(dc, "[2] PHOENIX PLUMAGE", startX + cardW + gap + 20, cardY + 24, 18, Brushes.SkyBlue, _boldTypeface);
        DrawTextFormatted(dc, "Increases combat and dash\nspeed by +25%.", startX + cardW + gap + 20, cardY + 70, 16, Brushes.White, _typeface);

        // Card 3
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(240, 24, 28, 38)), new Pen(Brushes.Gold, 2), new Rect(startX + (cardW + gap) * 2, cardY, cardW, cardH), 8, 8);
        DrawTextFormatted(dc, "[3] SUN AEGIS NOVA", startX + (cardW + gap) * 2 + 20, cardY + 24, 18, Brushes.Gold, _boldTypeface);
        DrawTextFormatted(dc, "Parries trigger a 360°\nmagma nova shockwave.\n+35 Max Health.", startX + (cardW + gap) * 2 + 20, cardY + 70, 16, Brushes.White, _typeface);

        DrawTextFormatted(dc, "Press [1], [2], or [3] on your keyboard to claim boon", (int)w / 2 - 230, 510, 17, Brushes.White, _boldTypeface);
    }

    private string CalculateRank(bool victory, out Brush rankBrush)
    {
        if (victory)
        {
            if (_runDurationSeconds < 160f && _parriesCount >= 6)
            {
                rankBrush = Brushes.Gold;
                return "S  [SEAM DOMINATOR]";
            }
            rankBrush = Brushes.Orange;
            return "A  [MOLTEN VANGUARD]";
        }
        else
        {
            if (_currentWave >= 4)
            {
                rankBrush = Brushes.SkyBlue;
                return "B  [CINDER SURVIVOR]";
            }
            rankBrush = Brushes.Gray;
            return "C  [ASH DRIFTER]";
        }
    }

    private void DrawStatsCard(DrawingContext dc, double w, double h, bool victory)
    {
        int cardW = 620;
        int cardH = 340;
        int cardX = (int)w / 2 - cardW / 2;
        int cardY = (int)h / 2 - cardH / 2 + 30;

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(245, 14, 18, 26)), new Pen(victory ? Brushes.Gold : Brushes.Red, 2), new Rect(cardX, cardY, cardW, cardH), 10, 10);

        string rank = CalculateRank(victory, out Brush rankBrush);
        DrawTextFormatted(dc, $"COMBAT RANK: {rank}", cardX + 35, cardY + 24, 20, rankBrush, _blackTypeface);

        int min = (int)(_runDurationSeconds / 60);
        int sec = (int)(_runDurationSeconds % 60);

        int col1X = cardX + 40;
        int col2X = cardX + 320;
        int rowY = cardY + 75;
        int rowH = 36;

        DrawTextFormatted(dc, $"⏱️ SURVIVAL TIME : {min:D2}:{sec:D2}", col1X, rowY, 15, Brushes.White, _boldTypeface);
        DrawTextFormatted(dc, $"⚔️ ENEMIES PURGED: {_totalKills}", col2X, rowY, 15, Brushes.White, _boldTypeface);

        DrawTextFormatted(dc, $"🛡️ PERFECT PARRIES: {_parriesCount}", col1X, rowY + rowH, 15, Brushes.White, _boldTypeface);
        DrawTextFormatted(dc, $"✨ SPARKS GATHERED: {_sparksCollected}", col2X, rowY + rowH, 15, Brushes.White, _boldTypeface);

        DrawTextFormatted(dc, $"🔥 TOTAL DAMAGE   : {_totalDamageDealt:F0}", col1X, rowY + rowH * 2, 15, Brushes.White, _boldTypeface);
        DrawTextFormatted(dc, $"⚡ MOLTS EXECUTED : {_moltsUsed}", col2X, rowY + rowH * 2, 15, Brushes.White, _boldTypeface);

        DrawTextFormatted(dc, victory ? "CLICK OR PRESS [SPACE] TO REIGNITE THE RUN" : "CLICK OR PRESS [SPACE] TO ARISE AGAIN", cardX + 90, cardY + cardH - 50, 17, victory ? Brushes.Gold : Brushes.Orange, _boldTypeface);
    }

    private void DrawVictoryScreen(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 10, 15, 24)), null, new Rect(0, 0, w, h));
        DrawTextFormatted(dc, "VICTORY ACHIEVED", (int)w / 2 - 200, 100, 40, Brushes.Gold, _blackTypeface);
        DrawTextFormatted(dc, "THE SEAM WARDEN HAS FALLEN. THE ASH IS CONSECRATED.", (int)w / 2 - 280, 160, 17, Brushes.White, _boldTypeface);
        DrawStatsCard(dc, w, h, victory: true);
    }

    private void DrawGameOverScreen(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 25, 5, 5)), null, new Rect(0, 0, w, h));
        DrawTextFormatted(dc, "THE PYRE EXTINGUISHED", (int)w / 2 - 250, 100, 38, Brushes.Red, _blackTypeface);
        DrawTextFormatted(dc, "YOUR FORM CRUMBLED TO ASH BEFORE THE SEAM.", (int)w / 2 - 240, 160, 17, Brushes.White, _boldTypeface);
        DrawStatsCard(dc, w, h, victory: false);
    }

    private void DrawTextFormatted(DrawingContext dc, string text, int x, int y, double size, Brush brush, Typeface tf)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            tf,
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip
        );
        dc.DrawText(formatted, new Point(x, y));
    }
}
