using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using MusicTool.App.Models;
using MusicTool.App.Services;
using NAudio.Wave;
using Path = System.IO.Path;

namespace MusicTool.App;

public partial class MainWindow : Window
{
    private const double HeaderWidth = 222;
    private const double TrackHeight = 84;
    private readonly AudioPlaybackService playback = new();
    private readonly AudioRecordingService recording = new();
    private readonly DispatcherTimer playheadTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly Stopwatch playbackClock = new();
    private readonly List<Line> lanePlayheadLines = [];
    private readonly string[] trackColors = ["#55C8A0", "#7AABFF", "#E6AE62", "#D98CB7", "#A98BFA", "#E17878"];
    private ProjectStore projectStore;
    private AudioTrack? selectedTrack;
    private AudioTrack? recordingTrack;
    private string? recordingPath;
    private Line? rulerPlayheadLine;
    private double playheadSeconds;
    private double playbackStartSeconds;
    private double pixelsPerBar = 75;
    private double timelineWidth = 2400;

    public MainWindow()
    {
        InitializeComponent();
        projectStore = ProjectStore.CreateDefault();
        UpdateTimingControls();
        TempoTextBox.LostKeyboardFocus += (_, _) => CommitTempo();
        TempoTextBox.KeyDown += TempoTextBox_KeyDown;
        TimeSignatureComboBox.SelectionChanged += TimeSignatureComboBox_SelectionChanged;
        playback.PlaybackCompleted += Playback_Completed;
        playheadTimer.Tick += PlayheadTimer_Tick;
        TimelineRuler.MouseLeftButtonDown += Ruler_MouseLeftButtonDown;
        UpdateProjectName();
        RefreshTimeline();
    }

    private void AddTrack_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            selectedTrack = projectStore.AddEmptyTrack();
            RefreshTimeline();
            StatusText.Text = "Empty audio track added. Record a vocal or import audio into the project.";
        }
        catch (Exception exception)
        {
            ShowError("Couldn't add a track", exception);
        }
    }

    private void AddAudio_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add audio to your project",
            Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            ImportAudio(dialog.FileNames);
        }
    }

    private void Timeline_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Timeline_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            ImportAudio(paths);
        }
    }

    private void ImportAudio(IEnumerable<string> paths)
    {
        try
        {
            var supportedPaths = paths.Where(path => ProjectStore.IsSupportedAudioFile(Path.GetExtension(path))).ToArray();
            if (supportedPaths.Length == 0)
            {
                StatusText.Text = "Choose a WAV or MP3 file.";
                return;
            }

            projectStore.AddAudioFiles(supportedPaths);
            selectedTrack = projectStore.Project.Tracks[^1];
            RefreshTimeline();
            StatusText.Text = $"Added {supportedPaths.Length} audio track(s). Drag clips to arrange them.";
        }
        catch (Exception exception)
        {
            ShowError("Couldn't add the audio files", exception);
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (playback.IsPlaying)
        {
            UpdatePlayheadFromClock();
            playback.Pause();
            playbackClock.Stop();
            playheadTimer.Stop();
            UpdatePlaybackButtonState(isPlaying: false);
            StatusText.Text = $"Paused at {FormatTimelinePosition(playheadSeconds)}.";
            return;
        }

        try
        {
            if (playback.IsPaused)
            {
                playback.Resume();
            }
            else
            {
                playback.Play(projectStore.Project.Tracks.Select(track =>
                    (Track: track, Path: projectStore.ResolveAudioPath(track))), playheadSeconds);
            }

            playbackStartSeconds = playheadSeconds;
            playbackClock.Restart();
            playheadTimer.Start();
            UpdatePlaybackButtonState(isPlaying: true);
            StatusText.Text = $"Playing from {FormatTimelinePosition(playheadSeconds)}.";
        }
        catch (Exception exception)
        {
            UpdatePlaybackButtonState(isPlaying: false);
            ShowError("Couldn't play the tracks", exception);
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        StatusText.Text = recording.IsRecording ? "Recording vocal." : "Playback stopped.";
    }

    private void StopPlayback()
    {
        UpdatePlayheadFromClock();
        playheadTimer.Stop();
        playbackClock.Stop();
        playback.Stop();
        UpdatePlaybackButtonState(isPlaying: false);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !playback.IsPlaying
            || Keyboard.FocusedElement is TextBox or ComboBox)
        {
            return;
        }

        Stop_Click(sender, e);
        e.Handled = true;
    }

    private void UpdatePlaybackButtonState(bool isPlaying)
    {
        PlayIcon.Visibility = isPlaying ? Visibility.Collapsed : Visibility.Visible;
        PauseIcon.Visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
        var name = isPlaying ? "Pause" : playback.IsPaused ? "Resume" : "Play";
        var tooltip = isPlaying ? "Pause playback" : playback.IsPaused ? "Resume playback" : "Play from playhead";
        AutomationProperties.SetName(PlayButton, name);
        PlayButton.ToolTip = tooltip;
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (recording.IsRecording)
            {
                FinishRecording();
                StatusText.Text = "Vocal take added to the selected track.";
                return;
            }

            if (selectedTrack is null || !string.IsNullOrWhiteSpace(selectedTrack.RelativePath))
            {
                selectedTrack = projectStore.AddEmptyTrack();
            }

            RefreshTimeline();
            recordingPath = Path.Combine(
                projectStore.MediaDirectory,
                $"Vocal Take {DateTime.Now:yyyy-MM-dd HH-mm-ss-fff}.wav");
            recordingTrack = selectedTrack;
            recording.Start(recordingPath, MonitorCheckBox.IsChecked == true);
            UpdateRecordButtonState(isRecording: true);
            StatusText.Text = "Recording vocal. Use headphones to prevent feedback.";
        }
        catch (Exception exception)
        {
            recording.Stop();
            UpdateRecordButtonState(isRecording: false);
            recordingTrack = null;
            recordingPath = null;
            ShowError("Couldn't record audio", exception);
        }
    }

    private void UpdateRecordButtonState(bool isRecording)
    {
        RecordDot.Visibility = isRecording ? Visibility.Collapsed : Visibility.Visible;
        RecordingStopIcon.Visibility = isRecording ? Visibility.Visible : Visibility.Collapsed;
        RecordButton.ToolTip = isRecording ? "Stop recording" : "Record vocal";
        AutomationProperties.SetName(RecordButton, isRecording ? "Stop recording" : "Record vocal");
    }

    private void Monitor_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            var enabled = MonitorCheckBox.IsChecked == true;
            recording.SetMonitoring(enabled);
            if (enabled)
            {
                StatusText.Text = recording.IsRecording
                    ? "Input monitoring is on while recording. Use headphones."
                    : "Live input monitoring is on. Use headphones.";
            }
            else
            {
                StatusText.Text = recording.IsRecording
                    ? "Input monitoring is off. Recording continues."
                    : "Input monitoring is off.";
            }
        }
        catch (Exception exception)
        {
            MonitorCheckBox.IsChecked = false;
            ShowError("Couldn't enable input monitoring", exception);
        }
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        pixelsPerBar = Math.Max(30, pixelsPerBar / 1.25);
        RefreshTimeline();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        pixelsPerBar = Math.Min(400, pixelsPerBar * 1.25);
        RefreshTimeline();
    }

    private void TempoTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTempo();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void CommitTempo()
    {
        if (!int.TryParse(TempoTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var tempo)
            || tempo is < 20 or > 300)
        {
            TempoTextBox.Text = projectStore.Project.TempoBpm.ToString(CultureInfo.CurrentCulture);
            StatusText.Text = "Tempo must be a whole number from 20 to 300 BPM.";
            return;
        }

        ApplyProjectTimingChange(tempo, projectStore.Project.TimeSignatureNumerator,
            projectStore.Project.TimeSignatureDenominator);
    }

    private void TimeSignatureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeSignatureComboBox.SelectedItem is not ComboBoxItem item
            || item.Content is not string signature)
        {
            return;
        }

        var parts = signature.Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var numerator)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var denominator))
        {
            return;
        }

        ApplyProjectTimingChange(projectStore.Project.TempoBpm, numerator, denominator);
    }

    private void ApplyProjectTimingChange(int tempo, int numerator, int denominator)
    {
        var project = projectStore.Project;
        if (project.TempoBpm == tempo
            && project.TimeSignatureNumerator == numerator
            && project.TimeSignatureDenominator == denominator)
        {
            return;
        }

        var previousTempo = project.TempoBpm;
        var previousNumerator = project.TimeSignatureNumerator;
        var previousDenominator = project.TimeSignatureDenominator;
        var visibleStartSeconds = TimelineXToSeconds(TimelineScroll.HorizontalOffset);
        project.TempoBpm = tempo;
        project.TimeSignatureNumerator = numerator;
        project.TimeSignatureDenominator = denominator;
        try
        {
            projectStore.Save();
            UpdateTimingControls();
            RefreshTimeline();
            TimelineScroll.ScrollToHorizontalOffset(SecondsToTimelineX(visibleStartSeconds));
            StatusText.Text = $"Timeline updated to {tempo} BPM, {numerator}/{denominator}. Audio playback speed is unchanged.";
        }
        catch (Exception exception)
        {
            project.TempoBpm = previousTempo;
            project.TimeSignatureNumerator = previousNumerator;
            project.TimeSignatureDenominator = previousDenominator;
            UpdateTimingControls();
            RefreshTimeline();
            ShowError("Couldn't update the project timing", exception);
        }
    }

    private void UpdateTimingControls()
    {
        TempoTextBox.Text = projectStore.Project.TempoBpm.ToString(CultureInfo.CurrentCulture);
        var signature = $"{projectStore.Project.TimeSignatureNumerator}/{projectStore.Project.TimeSignatureDenominator}";
        TimeSignatureComboBox.SelectedItem = TimeSignatureComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), signature, StringComparison.Ordinal));
    }

    private double GetSecondsPerBar() =>
        MusicalTimeline.GetSecondsPerBar(
            projectStore.Project.TempoBpm,
            projectStore.Project.TimeSignatureNumerator,
            projectStore.Project.TimeSignatureDenominator);

    private double TimelineXToSeconds(double x) => x / pixelsPerBar * GetSecondsPerBar();

    private double SecondsToTimelineX(double seconds) => seconds / GetSecondsPerBar() * pixelsPerBar;

    private string FormatTimelinePosition(double seconds) =>
        MusicalTimeline.FormatBarBeat(
            seconds,
            projectStore.Project.TempoBpm,
            projectStore.Project.TimeSignatureNumerator,
            projectStore.Project.TimeSignatureDenominator);

    private void TimelineScroll_Changed(object sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.HorizontalChange) > 0.1)
        {
            RulerScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        }

        if (Math.Abs(e.VerticalChange) > 0.1)
        {
            HeaderScroll.ScrollToVerticalOffset(e.VerticalOffset);
        }
    }

    private void Ruler_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SetPlayheadFromViewport(e.GetPosition(RulerScroll).X);
        e.Handled = true;
    }

    private void SetPlayheadFromViewport(double viewportX)
    {
        if (playback.IsPlaying)
        {
            StopPlayback();
        }

        playheadSeconds = Math.Max(0, TimelineXToSeconds(TimelineScroll.HorizontalOffset + viewportX));
        UpdatePlayheadVisual();
        StatusText.Text = $"Playhead at {FormatTimelinePosition(playheadSeconds)}. Press Play to start here.";
    }

    private void PlayheadTimer_Tick(object? sender, EventArgs e)
    {
        UpdatePlayheadFromClock();
        EnsurePlayheadVisible();
    }

    private void UpdatePlayheadFromClock()
    {
        if (playbackClock.IsRunning)
        {
            playheadSeconds = playbackStartSeconds + playbackClock.Elapsed.TotalSeconds;
            UpdatePlayheadVisual();
        }
    }

    private void EnsurePlayheadVisible()
    {
        var viewportWidth = TimelineScroll.ViewportWidth;
        if (viewportWidth <= 0)
        {
            return;
        }

        var playheadX = SecondsToTimelineX(playheadSeconds);
        var rightEdge = TimelineScroll.HorizontalOffset + viewportWidth;
        if (playheadX >= rightEdge - 40)
        {
            TimelineScroll.ScrollToHorizontalOffset(playheadX - viewportWidth + 80);
        }
    }

    private void RefreshTimeline()
    {
        var previousHorizontalOffset = TimelineScroll.HorizontalOffset;
        var previousVerticalOffset = TimelineScroll.VerticalOffset;
        var longestTrack = projectStore.Project.Tracks
            .Where(track => !string.IsNullOrWhiteSpace(track.RelativePath))
            .Select(track => GetClipDuration(track) + Math.Max(0, track.StartSeconds))
            .DefaultIfEmpty(0)
            .Max();
        timelineWidth = Math.Max(2400, (longestTrack / GetSecondsPerBar() + 16) * pixelsPerBar);
        ZoomText.Text = $"{pixelsPerBar:0} px/bar";
        TimelineRuler.Width = timelineWidth;
        TrackHeaders.Children.Clear();
        TrackLanes.Children.Clear();
        lanePlayheadLines.Clear();
        DrawRuler();

        for (var index = 0; index < projectStore.Project.Tracks.Count; index++)
        {
            var track = projectStore.Project.Tracks[index];
            var color = trackColors[index % trackColors.Length];
            TrackHeaders.Children.Add(CreateTrackHeader(track, color));
            TrackLanes.Children.Add(CreateTrackLane(track, index, color));
        }

        UpdateTrackHeaderSelection();
        TimelineScroll.ScrollToHorizontalOffset(previousHorizontalOffset);
        TimelineScroll.ScrollToVerticalOffset(previousVerticalOffset);
        UpdatePlayheadVisual();
    }

    private Border CreateTrackHeader(AudioTrack track, string color)
    {
        var header = new Border
        {
            Height = TrackHeight,
            Padding = new Thickness(12, 8, 8, 8),
            Background = new SolidColorBrush(Color.FromRgb(37, 38, 47)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(48, 49, 59)),
            BorderThickness = new Thickness(0, 0, 1, 1),
            ClipToBounds = true,
            Tag = track
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(29) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock
        {
            Text = track.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Brush("#F0EFF5")
        };
        var removeButton = new Button
        {
            Content = "×",
            Width = 25,
            Height = 23,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Foreground = Brush("#AAA9B5"),
            Background = Brushes.Transparent,
            Tag = track,
            ToolTip = "Remove track"
        };
        removeButton.Click += RemoveTrack_Click;
        Grid.SetColumn(removeButton, 1);
        layout.Children.Add(name);
        layout.Children.Add(removeButton);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var muteButton = new Button
        {
            Content = track.IsMuted ? "M" : "M",
            Width = 27,
            Height = 22,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 5, 0),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Background = track.IsMuted ? Brush(color) : Brush("#343540"),
            Foreground = track.IsMuted ? Brush("#15151A") : Brush("#C3C2CD"),
            Tag = track,
            ToolTip = "Mute track"
        };
        muteButton.Click += MuteTrack_Click;
        var volumeText = new TextBlock
        {
            Text = $"{track.Volume * 100:0}%",
            Width = 31,
            FontSize = 10,
            Foreground = Brush("#B8B7C2"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var volume = new Slider
        {
            Width = 105,
            Height = 20,
            Value = Math.Clamp(track.Volume, 0, 1),
            Tag = track,
            ToolTip = "Track volume"
        };
        volume.ValueChanged += (_, args) =>
        {
            track.Volume = args.NewValue;
            volumeText.Text = $"{args.NewValue * 100:0}%";
        };
        controls.Children.Add(muteButton);
        controls.Children.Add(volumeText);
        controls.Children.Add(volume);
        Grid.SetRow(controls, 1);
        Grid.SetColumnSpan(controls, 2);
        layout.Children.Add(controls);
        header.Child = layout;
        header.MouseLeftButtonDown += (_, _) => SelectTrack(track);
        return header;
    }

    private Border CreateTrackLane(AudioTrack track, int index, string color)
    {
        var lane = new Canvas
        {
            Width = timelineWidth,
            Height = TrackHeight,
            Background = Brush(index % 2 == 0 ? "#1D1E25" : "#202129"),
            ClipToBounds = true,
            Tag = track
        };
        lane.MouseLeftButtonDown += (_, _) => SelectTrack(track);
        DrawLaneGrid(lane);

        var duration = GetClipDuration(track);
        if (duration > 0)
        {
            var clip = CreateAudioClip(track, color, Math.Max(0.1, duration));
            Canvas.SetLeft(clip, SecondsToTimelineX(Math.Max(0, track.StartSeconds)));
            Canvas.SetTop(clip, 9);
            lane.Children.Add(clip);
        }
        else
        {
            var hint = new TextBlock
            {
                Text = "Empty track  ·  select and record a vocal",
                Foreground = Brush("#62636F"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(hint, 14);
            Canvas.SetTop(hint, 32);
            lane.Children.Add(hint);
        }

        var playheadLine = new Line
        {
            Y1 = 0,
            Y2 = TrackHeight,
            Stroke = Brush("#FF6D75"),
            StrokeThickness = 2,
            IsHitTestVisible = false
        };
        lanePlayheadLines.Add(playheadLine);
        lane.Children.Add(playheadLine);

        lane.MouseLeftButtonDown += (_, e) =>
        {
            SetPlayheadFromViewport(e.GetPosition(TimelineScroll).X);
            e.Handled = true;
        };

        return new Border
        {
            Height = TrackHeight,
            BorderBrush = Brush("#30313A"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = lane
        };
    }

    private Border CreateAudioClip(AudioTrack track, string color, double duration)
    {
        var clipWidth = Math.Max(90, duration / GetSecondsPerBar() * pixelsPerBar);
        var clip = new Border
        {
            Width = clipWidth,
            Height = TrackHeight - 18,
            CornerRadius = new CornerRadius(6),
            Background = Brush(color, 44),
            BorderBrush = Brush(color, 210),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Cursor = Cursors.SizeAll,
            Tag = track
        };
        var layout = new Grid { Margin = new Thickness(8, 5, 8, 4) };
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var name = new TextBlock
        {
            Text = track.Name,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F5F2FA"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        layout.Children.Add(name);
        var waveform = CreateWaveform(track, color, clipWidth - 18, 38);
        Grid.SetRow(waveform, 1);
        layout.Children.Add(waveform);
        clip.Child = layout;
        var trimMenuItem = new MenuItem { Header = "Set trim in / out..." };
        trimMenuItem.Click += (_, _) => EditClipTrim(track);
        clip.ContextMenu = new ContextMenu();
        clip.ContextMenu.Items.Add(trimMenuItem);
        clip.ToolTip = "Drag to move. Right-click to trim.";

        Point dragOrigin = default;
        double startAtMouseDown = 0;
        var didDrag = false;
        clip.MouseLeftButtonDown += (_, e) =>
        {
            SelectTrack(track);
            dragOrigin = e.GetPosition(TimelineScroll);
            startAtMouseDown = track.StartSeconds;
            didDrag = false;
            clip.CaptureMouse();
            e.Handled = true;
        };
        clip.MouseMove += (_, e) =>
        {
            if (!clip.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var pointer = e.GetPosition(TimelineScroll);
            if (!didDrag && Math.Abs(pointer.X - dragOrigin.X) < 4)
            {
                return;
            }

            didDrag = true;
            track.StartSeconds = Math.Max(0, startAtMouseDown + TimelineXToSeconds(pointer.X - dragOrigin.X));
            Canvas.SetLeft(clip, SecondsToTimelineX(track.StartSeconds));
        };
        clip.MouseLeftButtonUp += (_, e) =>
        {
            if (!clip.IsMouseCaptured)
            {
                return;
            }

            if (!didDrag)
            {
                SetPlayheadFromViewport(e.GetPosition(TimelineScroll).X);
                clip.ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            clip.ReleaseMouseCapture();
            projectStore.Save();
            StatusText.Text = $"Moved {track.Name} to {FormatTimelinePosition(track.StartSeconds)}.";
            e.Handled = true;
        };
        return clip;
    }

    private Polyline CreateWaveform(AudioTrack track, string color, double width, double height)
    {
        var peaks = GetWaveformPeaks(track);
        var center = height / 2;
        var points = new PointCollection();
        for (var index = 0; index < peaks.Length; index++)
        {
            var x = peaks.Length <= 1 ? 0 : index * width / (peaks.Length - 1);
            var amplitude = Math.Max(2, peaks[index] * center * 0.9);
            points.Add(new Point(x, center - amplitude));
            points.Add(new Point(x, center + amplitude));
        }

        return new Polyline
        {
            Points = points,
            Stroke = Brush(color),
            StrokeThickness = 1.2,
            Opacity = 0.95
        };
    }

    private float[] GetWaveformPeaks(AudioTrack track)
    {
        const int bucketCount = 130;
        var peaks = new float[bucketCount];
        using var reader = new AudioFileReader(projectStore.ResolveAudioPath(track));
        var totalSamples = Math.Max(1L,
            (long)(reader.TotalTime.TotalSeconds * reader.WaveFormat.SampleRate * reader.WaveFormat.Channels));
        var bucketSize = Math.Max(1L, totalSamples / bucketCount);
        var buffer = new float[8192];
        long sampleIndex = 0;
        int samplesRead;
        while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var offset = 0; offset < samplesRead; offset++)
            {
                var bucket = (int)Math.Min(bucketCount - 1, sampleIndex / bucketSize);
                peaks[bucket] = Math.Max(peaks[bucket], Math.Abs(buffer[offset]));
                sampleIndex++;
            }
        }

        return peaks;
    }

    private double GetTrackDuration(AudioTrack track)
    {
        if (string.IsNullOrWhiteSpace(track.RelativePath))
        {
            return 0;
        }

        using var reader = new AudioFileReader(projectStore.ResolveAudioPath(track));
        return reader.TotalTime.TotalSeconds;
    }

    private double GetClipDuration(AudioTrack track) =>
        Math.Max(0, GetTrackDuration(track) - track.TrimStartSeconds - track.TrimEndSeconds);

    private void DrawLaneGrid(Canvas lane)
    {
        var measureCount = (int)Math.Ceiling(timelineWidth / pixelsPerBar);
        for (var measure = 0; measure < measureCount; measure++)
        {
            for (var beat = 0; beat < projectStore.Project.TimeSignatureNumerator; beat++)
            {
                var x = (measure + beat / (double)projectStore.Project.TimeSignatureNumerator) * pixelsPerBar;
                var line = new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 0,
                    Y2 = TrackHeight,
                    Stroke = Brush(beat == 0 ? "#454650" : "#33343F"),
                    StrokeThickness = beat == 0 ? 1.2 : 1,
                    IsHitTestVisible = false
                };
                lane.Children.Add(line);
            }
        }
    }

    private void DrawRuler()
    {
        TimelineRuler.Children.Clear();
        rulerPlayheadLine = null;
        var measureCount = (int)Math.Ceiling(timelineWidth / pixelsPerBar);
        for (var measure = 0; measure <= measureCount; measure++)
        {
            for (var beat = 0; beat < projectStore.Project.TimeSignatureNumerator; beat++)
            {
                var x = (measure + beat / (double)projectStore.Project.TimeSignatureNumerator) * pixelsPerBar;
                var isMeasure = beat == 0;
                var tick = new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = isMeasure ? 19 : 29,
                    Y2 = 40,
                    Stroke = Brush(isMeasure ? "#777884" : "#555662"),
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                TimelineRuler.Children.Add(tick);
            }

            if (measure < measureCount)
            {
                var label = new TextBlock
                {
                    Text = (measure + 1).ToString(CultureInfo.InvariantCulture),
                    FontSize = 10,
                    Foreground = Brush("#A9A8B5"),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, measure * pixelsPerBar + 5);
                Canvas.SetTop(label, 3);
                TimelineRuler.Children.Add(label);
            }
        }

        rulerPlayheadLine = new Line
        {
            Y1 = 0,
            Y2 = 40,
            Stroke = Brush("#FF6D75"),
            StrokeThickness = 2,
            IsHitTestVisible = false
        };
        TimelineRuler.Children.Add(rulerPlayheadLine);
        UpdatePlayheadVisual();
    }

    private void UpdatePlayheadVisual()
    {
        var x = SecondsToTimelineX(playheadSeconds);
        if (rulerPlayheadLine is not null)
        {
            rulerPlayheadLine.X1 = x;
            rulerPlayheadLine.X2 = x;
        }

        foreach (var line in lanePlayheadLines)
        {
            line.X1 = x;
            line.X2 = x;
        }
    }

    private void EditClipTrim(AudioTrack track)
    {
        var dialog = new Window
        {
            Title = $"Trim {track.Name}",
            Width = 340,
            Height = 210,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = Brush("#202129"),
            Foreground = Brush("#F6F4FA")
        };
        var layout = new Grid { Margin = new Thickness(18) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var description = new TextBlock
        {
            Text = $"Leave seconds off the beginning/end of this clip. Full length: {GetTrackDuration(track):0.0}s",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#B9B8C4"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var trimIn = new TextBox
        {
            Text = track.TrimStartSeconds.ToString(CultureInfo.CurrentCulture),
            Margin = new Thickness(0, 2, 0, 7)
        };
        var trimOut = new TextBox
        {
            Text = track.TrimEndSeconds.ToString(CultureInfo.CurrentCulture),
            Margin = new Thickness(0, 2, 0, 7)
        };
        layout.Children.Add(description);

        var trimInRow = new StackPanel { Orientation = Orientation.Horizontal };
        trimInRow.Children.Add(new TextBlock
        {
            Text = "Trim in (seconds)",
            Width = 112,
            VerticalAlignment = VerticalAlignment.Center
        });
        trimInRow.Children.Add(trimIn);
        Grid.SetRow(trimInRow, 1);
        layout.Children.Add(trimInRow);
        var trimOutRow = new StackPanel { Orientation = Orientation.Horizontal };
        trimOutRow.Children.Add(new TextBlock
        {
            Text = "Trim out (seconds)",
            Width = 112,
            VerticalAlignment = VerticalAlignment.Center
        });
        trimOutRow.Children.Add(trimOut);
        Grid.SetRow(trimOutRow, 2);
        layout.Children.Add(trimOutRow);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true });
        var apply = new Button
        {
            Content = "Apply",
            IsDefault = true,
            Background = Brush("#B69CFF"),
            Foreground = Brush("#17131F")
        };
        apply.Click += (_, _) =>
        {
            if (!double.TryParse(trimIn.Text, CultureInfo.CurrentCulture, out var inSeconds)
                || !double.TryParse(trimOut.Text, CultureInfo.CurrentCulture, out var outSeconds)
                || !double.IsFinite(inSeconds) || !double.IsFinite(outSeconds)
                || inSeconds < 0 || outSeconds < 0
                || inSeconds + outSeconds >= GetTrackDuration(track))
            {
                MessageBox.Show(dialog, "Enter non-negative trim values that leave some audio in the clip.",
                    "Invalid trim", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            track.TrimStartSeconds = inSeconds;
            track.TrimEndSeconds = outSeconds;
            projectStore.Save();
            RefreshTimeline();
            StatusText.Text = $"Trimmed {track.Name}.";
            dialog.DialogResult = true;
        };
        buttons.Children.Add(apply);
        Grid.SetRow(buttons, 4);
        layout.Children.Add(buttons);
        dialog.Content = layout;
        dialog.ShowDialog();
    }

    private void RemoveTrack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AudioTrack track })
        {
            return;
        }

        if (recording.IsRecording && ReferenceEquals(track, recordingTrack))
        {
            StatusText.Text = "Stop the recording before removing its track.";
            return;
        }

        try
        {
            StopPlayback();
            projectStore.RemoveTrack(track);
            if (ReferenceEquals(selectedTrack, track))
            {
                selectedTrack = null;
            }

            RefreshTimeline();
            StatusText.Text = "Track removed from the project.";
        }
        catch (Exception exception)
        {
            ShowError("Couldn't remove the track", exception);
        }
    }

    private void MuteTrack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AudioTrack track })
        {
            return;
        }

        track.IsMuted = !track.IsMuted;
        StopPlayback();
        projectStore.Save();
        RefreshTimeline();
        StatusText.Text = track.IsMuted
            ? $"{track.Name} muted. Press Play to hear the updated mix."
            : $"{track.Name} unmuted. Press Play to hear the updated mix.";
    }

    private void SelectTrack(AudioTrack track)
    {
        selectedTrack = track;
        UpdateTrackHeaderSelection();
        StatusText.Text = $"{track.Name} selected.";
    }

    private void UpdateTrackHeaderSelection()
    {
        foreach (var header in TrackHeaders.Children.OfType<Border>())
        {
            if (header.Tag is AudioTrack current)
            {
                header.Background = ReferenceEquals(current, selectedTrack)
                    ? Brush("#332A45")
                    : Brush("#25262F");
            }
        }
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Create a new Music Tool project",
            Filter = "Music Tool project (*.musictool)|*.musictool",
            FileName = "New Project.musictool",
            AddExtension = true,
            DefaultExt = ".musictool"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (File.Exists(dialog.FileName))
        {
            MessageBox.Show(this, "Choose a new file name so an existing project isn't replaced.", "Project already exists",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            FinishRecording();
            projectStore.Save();
            StopPlayback();
            projectStore = ProjectStore.Create(dialog.FileName, Path.GetFileNameWithoutExtension(dialog.FileName));
            UpdateTimingControls();
            selectedTrack = null;
            RefreshTimeline();
            UpdateProjectName();
            StatusText.Text = "New project created.";
        }
        catch (Exception exception)
        {
            ShowError("Couldn't create the project", exception);
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open a Music Tool project",
            Filter = "Music Tool project (*.musictool)|*.musictool"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            FinishRecording();
            projectStore.Save();
            StopPlayback();
            projectStore = ProjectStore.Open(dialog.FileName);
            UpdateTimingControls();
            selectedTrack = null;
            RefreshTimeline();
            UpdateProjectName();
            StatusText.Text = "Project opened.";
        }
        catch (Exception exception)
        {
            ShowError("Couldn't open the project", exception);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            projectStore.Save();
            StatusText.Text = "Project saved.";
        }
        catch (Exception exception)
        {
            ShowError("Couldn't save the project", exception);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save a copy of this project",
            Filter = "Music Tool project (*.musictool)|*.musictool",
            FileName = $"{projectStore.Project.Name}.musictool",
            AddExtension = true,
            DefaultExt = ".musictool"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            FinishRecording();
            projectStore.SaveAs(dialog.FileName);
            UpdateProjectName();
            StatusText.Text = "Project saved.";
            RefreshTimeline();
        }
        catch (Exception exception)
        {
            ShowError("Couldn't save the project", exception);
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            playback.Dispose();
            FinishRecording();
            recording.Dispose();
            projectStore.Save();
        }
        catch (Exception exception)
        {
            e.Cancel = true;
            ShowError("Couldn't finish saving the project", exception);
        }
    }

    private void FinishRecording()
    {
        if (!recording.IsRecording)
        {
            return;
        }

        recording.Stop();
        if (recordingPath is not null && File.Exists(recordingPath))
        {
            projectStore.AddRecordedAudio(recordingPath, recordingTrack);
        }

        recordingPath = null;
        recordingTrack = null;
        UpdateRecordButtonState(isRecording: false);
        RefreshTimeline();
    }

    private void UpdateProjectName()
    {
        ProjectNameText.Text = projectStore.Project.Name;
        Title = $"Music Tool - {projectStore.Project.Name}";
    }

    private void ShowError(string title, Exception exception)
    {
        StatusText.Text = title;
        MessageBox.Show(this, exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static SolidColorBrush Brush(string color, byte? opacity = null)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        if (opacity.HasValue)
        {
            brush.Opacity = opacity.Value / 255d;
        }

        return brush;
    }

    private void Playback_Completed(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdatePlayheadFromClock();
            playheadTimer.Stop();
            playbackClock.Stop();
            playback.Stop();
            UpdatePlaybackButtonState(isPlaying: false);
            playheadSeconds = Math.Max(playheadSeconds, playbackStartSeconds);
            UpdatePlayheadVisual();
            if (!recording.IsRecording)
            {
                StatusText.Text = "Playback finished.";
            }
        }));
    }
}
