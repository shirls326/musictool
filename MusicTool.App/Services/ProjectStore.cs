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
        return new ProjectStore(fullPath, project);
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
                RelativePath = Path.GetRelativePath(ProjectDirectory, destinationPath)
            });
        }

        Save();
    }

    public void AddRecordedAudio(string audioPath, AudioTrack? targetTrack = null)
    {
        var track = targetTrack ?? new AudioTrack { Name = Path.GetFileNameWithoutExtension(audioPath) };
        track.Name = Path.GetFileNameWithoutExtension(audioPath);
        track.RelativePath = Path.GetRelativePath(ProjectDirectory, audioPath);
        if (targetTrack is null)
        {
            Project.Tracks.Add(track);
        }

        Save();
    }

    public void RemoveTrack(AudioTrack track)
    {
        Project.Tracks.Remove(track);
        Save();
    }

    public string ResolveAudioPath(AudioTrack track) =>
        Path.GetFullPath(Path.Combine(ProjectDirectory, track.RelativePath));

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
            if (string.IsNullOrWhiteSpace(track.RelativePath))
            {
                continue;
            }

            var currentPath = ResolveAudioPath(track);
            var destinationPath = Path.Combine(newMediaDirectory, Path.GetFileName(currentPath));
            if (!string.Equals(currentPath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentPath, destinationPath, overwrite: true);
            }

            track.RelativePath = Path.GetRelativePath(newDirectory, destinationPath);
        }

        Project.Name = Path.GetFileNameWithoutExtension(newPath);
        ProjectFilePath = newPath;
        Save();
    }

    public static bool IsSupportedAudioFile(string extension) =>
        extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase);
}
