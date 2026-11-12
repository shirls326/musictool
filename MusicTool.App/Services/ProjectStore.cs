using System.IO;
using System.Text.Json;
using MusicTool.App.Models;

namespace MusicTool.App.Services;

public sealed class ProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private ProjectStore(string projectFilePath, MusicProject project)
    {
        ProjectFilePath = projectFilePath;
        Project = project;
        Directory.CreateDirectory(MediaDirectory);
    }

    public string ProjectFilePath { get; private set; }
    public string ProjectDirectory => Path.GetDirectoryName(ProjectFilePath)!;
    public string MediaDirectory => Path.Combine(ProjectDirectory, "Media");
    public MusicProject Project { get; private set; }

    public static ProjectStore Open(string projectFilePath)
    {
        var fullPath = Path.GetFullPath(projectFilePath);
        var project = JsonSerializer.Deserialize<MusicProject>(File.ReadAllText(fullPath), JsonOptions)
            ?? throw new InvalidDataException("The selected project file is empty or invalid.");
        var store = new ProjectStore(fullPath, project);
        store.MigrateLegacyTracks();
        return store;
    }

    public static ProjectStore CreateDefault()
    {
        var projectsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Music Tool Projects");
        var projectPath = Path.Combine(projectsDirectory, "My First Project.musictool");
        Directory.CreateDirectory(projectsDirectory);
        return File.Exists(projectPath)
            ? Open(projectPath)
            : Create(projectPath, "My First Project");
    }

    public static ProjectStore Create(string projectFilePath, string name)
    {
        var store = new ProjectStore(Path.GetFullPath(projectFilePath), new MusicProject { Name = name });
        store.Save();
        return store;
    }

    public AudioTrack AddEmptyTrack()
    {
        var track = new AudioTrack { Name = $"Audio {Project.Tracks.Count + 1}" };
        Project.Tracks.Add(track);
        Save();
        return track;
    }

    public void AddAudioFiles(IEnumerable<string> sourcePaths)
    {
        foreach (var sourcePath in sourcePaths)
        {
            var extension = Path.GetExtension(sourcePath);
            if (!IsSupportedAudioFile(extension))
            {
                throw new NotSupportedException($"The file type '{extension}' is not supported. Choose a WAV or MP3 file.");
            }

            var destinationPath = Path.Combine(MediaDirectory, $"{Guid.NewGuid():N}{extension}");
            File.Copy(sourcePath, destinationPath);
            Project.Tracks.Add(new AudioTrack
            {
                Name = Path.GetFileNameWithoutExtension(sourcePath),
                Clips =
                [
                    new AudioClip
                    {
                        RelativePath = Path.GetRelativePath(ProjectDirectory, destinationPath)
                    }
                ]
            });
        }

        Save();
    }

    public void AddRecordedAudio(string audioPath, AudioTrack? targetTrack = null)
    {
        var track = targetTrack ?? new AudioTrack { Name = Path.GetFileNameWithoutExtension(audioPath) };
        if (track.Clips.Count == 0)
        {
            track.Name = Path.GetFileNameWithoutExtension(audioPath);
        }

        track.Clips.Add(new AudioClip
        {
            RelativePath = Path.GetRelativePath(ProjectDirectory, audioPath)
        });
        if (targetTrack is null)
        {
            Project.Tracks.Add(track);
        }

        Save();
    }

    public void PunchInRecordedAudio(AudioTrack track, string audioPath, double punchInSeconds)
    {
        if (!Project.Tracks.Contains(track))
        {
            throw new InvalidOperationException("The selected recording track is not part of this project.");
        }

        if (!double.IsFinite(punchInSeconds) || punchInSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(punchInSeconds));
        }

        using var takeReader = new NAudio.Wave.AudioFileReader(audioPath);
        var takeDuration = takeReader.TotalTime.TotalSeconds;
        if (takeDuration <= 0)
        {
            throw new InvalidDataException("The recorded take contains no audio.");
        }

        var punchEndSeconds = punchInSeconds + takeDuration;
        var replacementClips = new List<AudioClip>();
        foreach (var clip in track.Clips)
        {
            var duration = GetClipDuration(clip);
            var clipEnd = clip.StartSeconds + duration;
            if (duration <= 0 || clipEnd <= punchInSeconds || clip.StartSeconds >= punchEndSeconds)
            {
                replacementClips.Add(clip);
                continue;
            }

            if (clip.StartSeconds < punchInSeconds)
            {
                var leftClip = CloneClip(clip);
                leftClip.TrimEndSeconds += clipEnd - punchInSeconds;
                replacementClips.Add(leftClip);
            }

            if (clipEnd > punchEndSeconds)
            {
                var rightClip = CloneClip(clip);
                rightClip.StartSeconds = punchEndSeconds;
                rightClip.TrimStartSeconds += punchEndSeconds - clip.StartSeconds;
                replacementClips.Add(rightClip);
            }
        }

        replacementClips.Add(new AudioClip
        {
            RelativePath = Path.GetRelativePath(ProjectDirectory, Path.GetFullPath(audioPath)),
            StartSeconds = punchInSeconds
        });
        var originalClips = track.Clips;
        track.Clips = replacementClips.OrderBy(clip => clip.StartSeconds).ToList();
        ClearLegacyTrackFields(track);
        try
        {
            Save();
        }
        catch
        {
            track.Clips = originalClips;
            throw;
        }
    }

    public void RemoveTrack(AudioTrack track)
    {
        Project.Tracks.Remove(track);
        Save();
    }

    public void RemoveClip(AudioTrack track, AudioClip clip)
    {
        if (!Project.Tracks.Contains(track))
        {
            throw new InvalidOperationException("The selected track is not part of this project.");
        }

        var clipIndex = track.Clips.IndexOf(clip);
        if (clipIndex < 0)
        {
            throw new InvalidOperationException("The selected clip is not part of this track.");
        }

        track.Clips.RemoveAt(clipIndex);
        try
        {
            Save();
        }
        catch
        {
            track.Clips.Insert(clipIndex, clip);
            throw;
        }
    }

    public string ResolveAudioPath(AudioClip clip) =>
        Path.GetFullPath(Path.Combine(ProjectDirectory, clip.RelativePath));

    public double GetClipDuration(AudioClip clip)
    {
        using var reader = new NAudio.Wave.AudioFileReader(ResolveAudioPath(clip));
        return Math.Max(0, reader.TotalTime.TotalSeconds - clip.TrimStartSeconds - clip.TrimEndSeconds);
    }

    public void Save()
    {
        Directory.CreateDirectory(ProjectDirectory);
        Directory.CreateDirectory(MediaDirectory);
        File.WriteAllText(ProjectFilePath, JsonSerializer.Serialize(Project, JsonOptions));
    }

    public void SaveAs(string newProjectFilePath)
    {
        var newPath = Path.GetFullPath(newProjectFilePath);
        var newDirectory = Path.GetDirectoryName(newPath)!;
        var newMediaDirectory = Path.Combine(newDirectory, "Media");
        Directory.CreateDirectory(newMediaDirectory);

        foreach (var track in Project.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (string.IsNullOrWhiteSpace(clip.RelativePath))
                {
                    continue;
                }

                var currentPath = ResolveAudioPath(clip);
                var destinationPath = Path.Combine(newMediaDirectory, Path.GetFileName(currentPath));
                if (!string.Equals(currentPath, destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(currentPath, destinationPath, overwrite: true);
                }

                clip.RelativePath = Path.GetRelativePath(newDirectory, destinationPath);
            }
        }

        Project.Name = Path.GetFileNameWithoutExtension(newPath);
        ProjectFilePath = newPath;
        Save();
    }

    public static bool IsSupportedAudioFile(string extension) =>
        extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase);

    private void MigrateLegacyTracks()
    {
        foreach (var track in Project.Tracks)
        {
            track.Clips ??= [];
            if (track.Clips.Count == 0 && !string.IsNullOrWhiteSpace(track.RelativePath))
            {
                track.Clips.Add(new AudioClip
                {
                    RelativePath = track.RelativePath,
                    StartSeconds = track.StartSeconds,
                    TrimStartSeconds = track.TrimStartSeconds,
                    TrimEndSeconds = track.TrimEndSeconds
                });
                ClearLegacyTrackFields(track);
            }
        }
    }

    private static AudioClip CloneClip(AudioClip clip) => new()
    {
        RelativePath = clip.RelativePath,
        StartSeconds = clip.StartSeconds,
        TrimStartSeconds = clip.TrimStartSeconds,
        TrimEndSeconds = clip.TrimEndSeconds
    };

    private static void ClearLegacyTrackFields(AudioTrack track)
    {
        track.RelativePath = string.Empty;
        track.StartSeconds = 0;
        track.TrimStartSeconds = 0;
        track.TrimEndSeconds = 0;
    }
}
