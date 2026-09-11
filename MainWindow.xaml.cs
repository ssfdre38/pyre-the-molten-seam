using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PyreGame;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _isPlayingCinematic = false;

    public MainWindow()
    {
        InitializeComponent();

        Surface.PlayCinematicRequested += OnPlayCinematic;
        CinematicPlayer.MediaEnded += (s, e) => CloseCinematic();
        CinematicPlayer.MediaFailed += (s, e) => CloseCinematic();

        KeyDown += OnWindowKeyDown;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_isPlayingCinematic && (e.Key == Key.Space || e.Key == Key.Escape || e.Key == Key.Enter))
        {
            CloseCinematic();
            e.Handled = true;
        }
    }

    private void OnPlayCinematic()
    {
        string assetPath = Path.Combine(AppContext.BaseDirectory, "assets", "sovereign_cinematic_cathedral.mp4");
        if (!File.Exists(assetPath))
        {
            assetPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "sovereign_cinematic_cathedral.mp4");
        }

        if (!File.Exists(assetPath)) return;

        _isPlayingCinematic = true;
        Surface.PauseAudio();

        CinematicPlayer.Source = new Uri(Path.GetFullPath(assetPath));
        CinematicOverlay.Visibility = Visibility.Visible;
        CinematicPlayer.Play();
        CinematicOverlay.Focus();
    }

    private void CloseCinematic()
    {
        if (!_isPlayingCinematic) return;

        _isPlayingCinematic = false;
        CinematicPlayer.Stop();
        CinematicPlayer.Source = null;
        CinematicOverlay.Visibility = Visibility.Collapsed;

        Surface.ResumeAudio();
        Surface.Focus();
    }
}
