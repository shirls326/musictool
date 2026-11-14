using MusicTool.App.Services;
using NAudio.Wave;
using Xunit;

namespace MusicTool.App.Tests;

public sealed class AutosaveRecoveryTests
{
    [Fact]
    public void CheckpointCopiesProjectMediaAndCanBeDiscarded()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var recoveryRoot = Path.Combine(root, "Recovery");
            var sourceAudioPath = Path.Combine(root, "backing.wav");
            var audioBytes = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(sourceAudioPath, audioBytes);
            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");
            store.AddAudioFiles([sourceAudioPath]);
            using var recovery = new AutosaveRecoveryService(store.ProjectFilePath, recoveryRoot);

            var checkpoint = recovery.SaveCheckpoint(store, null, null);
            var reopened = ProjectStore.Open(checkpoint.SnapshotProjectPath);

            Assert.Equal("Song", reopened.Project.Name);
            Assert.Equal(audioBytes, File.ReadAllBytes(reopened.ResolveAudioPath(
                Assert.Single(Assert.Single(reopened.Project.Tracks).Clips))));
            Assert.Single(AutosaveRecoveryService.FindAvailable(recoveryRoot));

            AutosaveRecoveryService.Discard(checkpoint, recoveryRoot);
            Assert.Empty(AutosaveRecoveryService.FindAvailable(recoveryRoot));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CheckpointIncludesIncompleteTakeMetadataAndSnapshot()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var recoveryRoot = Path.Combine(root, "Recovery");
            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");
            using var recovery = new AutosaveRecoveryService(store.ProjectFilePath, recoveryRoot);
            var takeInfo = new RecoveryTakeInfo("Lead vocal", 0, 3.5);

            var checkpoint = recovery.SaveCheckpoint(
                store,
                takeInfo,
                path => File.WriteAllBytes(path, [5, 6, 7]));
            var reopenedCheckpoint = Assert.Single(AutosaveRecoveryService.FindAvailable(recoveryRoot));

            Assert.Equal(takeInfo, reopenedCheckpoint.IncompleteTake);
            Assert.Equal(new byte[] { 5, 6, 7 }, File.ReadAllBytes(reopenedCheckpoint.IncompleteTakePath!));
            Assert.Equal(checkpoint.SnapshotProjectPath, reopenedCheckpoint.SnapshotProjectPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ActivePcmRecordingCanBeCopiedIntoAReadablePartialWaveFile()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourcePath = Path.Combine(root, "active.wav");
            var snapshotPath = Path.Combine(root, "snapshot.wav");
            const int recordedBytes = 4410;
            using (var stream = new FileStream(sourcePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (var writer = new WaveFileWriter(stream, new WaveFormat(44100, 16, 1)))
            {
                writer.Write(new byte[recordedBytes], 0, recordedBytes);
                writer.Flush();

                AudioRecordingService.CopyRecoverablePcmSnapshot(sourcePath, snapshotPath, recordedBytes);

                using var reader = new AudioFileReader(snapshotPath);
                Assert.Equal(TimeSpan.FromMilliseconds(50), reader.TotalTime);
                Assert.Equal(46 + recordedBytes, new FileInfo(snapshotPath).Length);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RecoveredIncompleteTakeIsAddedSeparatelyWithoutChangingExistingClips()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = ProjectStore.Create(Path.Combine(root, "Recovered", "Song.musictool"), "Song");
            var backingPath = Path.Combine(root, "backing.wav");
            var interruptedTakePath = Path.Combine(root, "interrupted.wav");
            WriteSilence(backingPath);
            WriteSilence(interruptedTakePath);
            store.AddAudioFiles([backingPath]);
            var originalTrack = store.Project.Tracks[0];
            originalTrack.Clips[0].StartSeconds = 1.25;
            originalTrack.Volume = 0.6;
            originalTrack.IsMuted = true;
            store.Save();

            var incompleteTrack = store.AddRecoveredIncompleteTake(interruptedTakePath, originalTrack.Name, 3.5);
            var reopened = ProjectStore.Open(store.ProjectFilePath);

            Assert.Equal("INCOMPLETE - backing", incompleteTrack.Name);
            Assert.Equal(2, reopened.Project.Tracks.Count);
            var reopenedOriginal = reopened.Project.Tracks[0];
            var originalClip = Assert.Single(reopenedOriginal.Clips);
            Assert.Equal(1.25, originalClip.StartSeconds);
            Assert.Equal(0.6, reopenedOriginal.Volume);
            Assert.True(reopenedOriginal.IsMuted);
            var recoveredClip = Assert.Single(reopened.Project.Tracks[1].Clips);
            Assert.Equal(3.5, recoveredClip.StartSeconds);
            Assert.True(File.Exists(reopened.ResolveAudioPath(recoveredClip)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteSilence(string path)
    {
        using var writer = new WaveFileWriter(path, new WaveFormat(44100, 16, 1));
        writer.Write(new byte[4410], 0, 4410);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"MusicToolTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
