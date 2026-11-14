using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MusicTool.App.Models;

namespace MusicTool.App.Services;

public sealed class AutosaveRecoveryService : IDisposable
{
    private const string ManifestFileName = "recovery.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string recoveryRoot;
    private readonly string recoveryDirectory;
    private readonly string originalProjectPath;
    private string? currentSnapshotProjectPath;
    private string? currentTakeSnapshotPath;

    public AutosaveRecoveryService(string originalProjectPath, string? recoveryRoot = null)
    {
        this.originalProjectPath = Path.GetFullPath(originalProjectPath);
        this.recoveryRoot = Path.GetFullPath(recoveryRoot ?? GetRecoveryRoot());
        recoveryDirectory = Path.Combine(this.recoveryRoot, GetProjectKey(this.originalProjectPath));
        Directory.CreateDirectory(recoveryDirectory);
    }

    public static IReadOnlyList<RecoveryCheckpoint> FindAvailable(string? recoveryRoot = null)
    {
        var root = Path.GetFullPath(recoveryRoot ?? GetRecoveryRoot());
        if (!Directory.Exists(root))
        {
            return [];
        }

        var checkpoints = new List<RecoveryCheckpoint>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var manifestPath = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifest = JsonSerializer.Deserialize<RecoveryManifest>(File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new InvalidDataException($"Recovery information in '{manifestPath}' is empty or invalid.");
            var snapshotProjectPath = Path.Combine(directory, manifest.SnapshotProjectFile);
            var takeSnapshotPath = manifest.IncompleteTakeFile is null
                ? null
                : Path.Combine(directory, manifest.IncompleteTakeFile);
            checkpoints.Add(new RecoveryCheckpoint(
                directory,
                manifest.OriginalProjectPath,
                snapshotProjectPath,
                manifest.SavedAtUtc,
                manifest.IncompleteTake,
                takeSnapshotPath));
        }

        return checkpoints.OrderByDescending(checkpoint => checkpoint.SavedAtUtc).ToArray();
    }

    public RecoveryCheckpoint SaveCheckpoint(
        ProjectStore store,
        RecoveryTakeInfo? incompleteTake,
        Action<string>? writeIncompleteTakeSnapshot)
    {
        if ((incompleteTake is null) != (writeIncompleteTakeSnapshot is null))
        {
            throw new ArgumentException("Incomplete-take details and snapshot writer must be provided together.");
        }

        var generation = Guid.NewGuid().ToString("N");
        var projectFileName = $"checkpoint-{generation}.musictool";
        var projectTemporaryPath = Path.Combine(recoveryDirectory, $"{projectFileName}.tmp");
        var projectSnapshotPath = Path.Combine(recoveryDirectory, projectFileName);
        string? takeFileName = null;
        string? takeTemporaryPath = null;
        string? takeSnapshotPath = null;

        try
        {
            var snapshot = CloneProject(store.Project);
            foreach (var clip in snapshot.Tracks.SelectMany(track => track.Clips))
            {
                if (string.IsNullOrWhiteSpace(clip.RelativePath))
                {
                    continue;
                }

                var sourcePath = store.ResolveAudioPath(clip);
                var assetFileName = $"{GetProjectKey(sourcePath)}{Path.GetExtension(sourcePath)}";
                CopyAssetIfChanged(sourcePath, Path.Combine(recoveryDirectory, "Media", assetFileName));
                clip.RelativePath = Path.Combine("Media", assetFileName);
            }

            File.WriteAllText(projectTemporaryPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            File.Move(projectTemporaryPath, projectSnapshotPath);

            if (incompleteTake is not null && writeIncompleteTakeSnapshot is not null)
            {
                takeFileName = $"incomplete-{generation}.wav";
                takeTemporaryPath = Path.Combine(recoveryDirectory, $"{takeFileName}.tmp");
                takeSnapshotPath = Path.Combine(recoveryDirectory, takeFileName);
                writeIncompleteTakeSnapshot(takeTemporaryPath);
                File.Move(takeTemporaryPath, takeSnapshotPath);
            }

            var manifest = new RecoveryManifest(
                originalProjectPath,
                projectFileName,
                DateTime.UtcNow,
                incompleteTake,
                takeFileName);
            var manifestTemporaryPath = Path.Combine(recoveryDirectory, $"{ManifestFileName}.tmp");
            File.WriteAllText(manifestTemporaryPath, JsonSerializer.Serialize(manifest, JsonOptions));
            File.Move(manifestTemporaryPath, Path.Combine(recoveryDirectory, ManifestFileName), overwrite: true);

            DeletePreviousGeneration(currentSnapshotProjectPath, projectSnapshotPath);
            DeletePreviousGeneration(currentTakeSnapshotPath, takeSnapshotPath);
            currentSnapshotProjectPath = projectSnapshotPath;
            currentTakeSnapshotPath = takeSnapshotPath;

            return new RecoveryCheckpoint(
                recoveryDirectory,
                originalProjectPath,
                projectSnapshotPath,
                manifest.SavedAtUtc,
                incompleteTake,
                takeSnapshotPath);
        }
        catch
        {
            DeleteFileIfExists(projectTemporaryPath);
            DeleteFileIfExists(projectSnapshotPath);
            if (takeTemporaryPath is not null)
            {
                DeleteFileIfExists(takeTemporaryPath);
            }

            if (takeSnapshotPath is not null)
            {
                DeleteFileIfExists(takeSnapshotPath);
            }

            throw;
        }
    }

    public static void Discard(RecoveryCheckpoint checkpoint, string? recoveryRoot = null)
    {
        var root = Path.GetFullPath(recoveryRoot ?? GetRecoveryRoot());
        var target = Path.GetFullPath(checkpoint.RecoveryDirectory);
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The recovery data is outside Music Tool's recovery directory.");
        }

        Directory.Delete(target, recursive: true);
    }

    public void DiscardCurrent()
    {
        if (Directory.Exists(recoveryDirectory))
        {
            Directory.Delete(recoveryDirectory, recursive: true);
        }
    }

    public void Dispose()
    {
    }

    private static MusicProject CloneProject(MusicProject project)
    {
        var json = JsonSerializer.Serialize(project, JsonOptions);
        return JsonSerializer.Deserialize<MusicProject>(json, JsonOptions)
            ?? throw new InvalidDataException("Couldn't create an autosave snapshot of the project.");
    }

    private static void CopyAssetIfChanged(string sourcePath, string destinationPath)
    {
        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException("Project audio is missing and cannot be included in recovery.", sourcePath);
        }

        var destinationInfo = new FileInfo(destinationPath);
        if (destinationInfo.Exists
            && destinationInfo.Length == sourceInfo.Length
            && destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var temporaryPath = $"{destinationPath}.tmp";
        File.Copy(sourcePath, temporaryPath, overwrite: true);
        File.SetLastWriteTimeUtc(temporaryPath, sourceInfo.LastWriteTimeUtc);
        File.Move(temporaryPath, destinationPath, overwrite: true);
    }

    private static void DeletePreviousGeneration(string? previousPath, string? retainedPath)
    {
        if (previousPath is not null && !string.Equals(previousPath, retainedPath, StringComparison.OrdinalIgnoreCase))
        {
            DeleteFileIfExists(previousPath);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string GetProjectKey(string path)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())));
    }

    private static string GetRecoveryRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicTool", "Recovery");

    private sealed record RecoveryManifest(
        string OriginalProjectPath,
        string SnapshotProjectFile,
        DateTime SavedAtUtc,
        RecoveryTakeInfo? IncompleteTake,
        string? IncompleteTakeFile);
}

public sealed record RecoveryTakeInfo(string TrackName, int TrackIndex, double StartSeconds);

public sealed record RecoveryCheckpoint(
    string RecoveryDirectory,
    string OriginalProjectPath,
    string SnapshotProjectPath,
    DateTime SavedAtUtc,
    RecoveryTakeInfo? IncompleteTake,
    string? IncompleteTakePath);
