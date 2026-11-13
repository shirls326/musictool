using MusicTool.App.Services;
using NAudio.Wave;
using Xunit;

namespace MusicTool.App.Tests;

public sealed class ProjectStorePunchInTests
{
    [Fact]
    public void PunchInKeepsGapsAndSplitsOverlappedClipAroundTheTake()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var firstTakePath = Path.Combine(root, "first.wav");
            var secondTakePath = Path.Combine(root, "second.wav");
            CreateWaveFile(firstTakePath, 0.4, 0.3f);
            CreateWaveFile(secondTakePath, 0.25, 0.8f);

            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");
            var track = store.AddEmptyTrack();
            store.PunchInRecordedAudio(track, firstTakePath, punchInSeconds: 1);
            store.PunchInRecordedAudio(track, secondTakePath, punchInSeconds: 4);

            var reopened = ProjectStore.Open(store.ProjectFilePath);
            var savedTrack = Assert.Single(reopened.Project.Tracks);
            Assert.Equal(2, savedTrack.Clips.Count);
            Assert.Equal(1, savedTrack.Clips[0].StartSeconds, 5);
            Assert.Equal(4, savedTrack.Clips[1].StartSeconds, 5);
            Assert.All(savedTrack.Clips, clip => Assert.True(File.Exists(reopened.ResolveAudioPath(clip))));

            var originalClip = savedTrack.Clips[0];
            Assert.Equal(0.4, reopened.GetClipDuration(originalClip), 2);
            var secondClip = savedTrack.Clips[1];
            Assert.Equal(0.25, reopened.GetClipDuration(secondClip), 2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PunchInInsideAnExistingClipPreservesBothSidesAsSeparateClips()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var originalPath = Path.Combine(root, "original.wav");
            var takePath = Path.Combine(root, "take.wav");
            CreateWaveFile(originalPath, 2, 0.1f);
            CreateWaveFile(takePath, 0.5, 0.8f);
            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");
            store.AddAudioFiles([originalPath]);
            var track = store.Project.Tracks[0];
            track.Name = "Lead vocal";
            track.Clips[0].StartSeconds = 2;
            track.Volume = 0.65;
            track.IsMuted = true;
            store.PunchInRecordedAudio(track, takePath, punchInSeconds: 2.75);

            var reopened = ProjectStore.Open(store.ProjectFilePath);
            var savedTrack = Assert.Single(reopened.Project.Tracks);
            Assert.Equal("Lead vocal", savedTrack.Name);
            Assert.Equal(0.65, savedTrack.Volume);
            Assert.True(savedTrack.IsMuted);
            Assert.Equal(3, savedTrack.Clips.Count);
            Assert.Equal(2, savedTrack.Clips[0].StartSeconds, 5);
            Assert.Equal(2.75, savedTrack.Clips[1].StartSeconds, 5);
            Assert.Equal(3.25, savedTrack.Clips[2].StartSeconds, 5);
            Assert.Equal(0.75, reopened.GetClipDuration(savedTrack.Clips[0]), 2);
            Assert.Equal(0.5, reopened.GetClipDuration(savedTrack.Clips[1]), 2);
            Assert.Equal(0.75, reopened.GetClipDuration(savedTrack.Clips[2]), 2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OlderSingleAudioTrackProjectsLoadAsOneClip()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var mediaDirectory = Path.Combine(root, "Media");
            Directory.CreateDirectory(mediaDirectory);
            var mediaPath = Path.Combine(mediaDirectory, "legacy.wav");
            CreateWaveFile(mediaPath, 1, 0.2f);
            var projectPath = Path.Combine(root, "Legacy.musictool");
            File.WriteAllText(projectPath,
                """{"Name":"Legacy","Tracks":[{"Name":"Old vocal","RelativePath":"Media/legacy.wav","StartSeconds":2,"TrimStartSeconds":0.1,"TrimEndSeconds":0.2,"Volume":0.75,"IsMuted":false}]}""");

            var store = ProjectStore.Open(projectPath);

            var track = Assert.Single(store.Project.Tracks);
            var clip = Assert.Single(track.Clips);
            Assert.Equal(2, clip.StartSeconds);
            Assert.Equal(0.1, clip.TrimStartSeconds);
            Assert.Equal(0.2, clip.TrimEndSeconds);
            Assert.True(File.Exists(store.ResolveAudioPath(clip)));
            store.Save();

            var reopened = ProjectStore.Open(projectPath);
            Assert.Single(Assert.Single(reopened.Project.Tracks).Clips);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RemoveClipPersistsWithoutDeletingMediaSharedByAnotherClip()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourcePath = Path.Combine(root, "shared.wav");
            CreateWaveFile(sourcePath, 2, 0.2f);
            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");
            store.AddAudioFiles([sourcePath]);
            var track = Assert.Single(store.Project.Tracks);
            var removedClip = Assert.Single(track.Clips);
            var sharedMediaPath = store.ResolveAudioPath(removedClip);
            var retainedClip = new MusicTool.App.Models.AudioClip
            {
                RelativePath = removedClip.RelativePath,
                StartSeconds = 3
            };
            track.Clips.Add(retainedClip);
            store.Save();

            store.RemoveClip(track, removedClip);

            var reopened = ProjectStore.Open(store.ProjectFilePath);
            var savedTrack = Assert.Single(reopened.Project.Tracks);
            var savedClip = Assert.Single(savedTrack.Clips);
            Assert.Equal(3, savedClip.StartSeconds);
            Assert.Equal(removedClip.RelativePath, savedClip.RelativePath);
            Assert.True(File.Exists(sharedMediaPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RestoreTrackClipsUndoesPunchInAndPreservesTrackSettings()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var originalPath = Path.Combine(root, "original.wav");
            var takePath = Path.Combine(root, "take.wav");
            CreateWaveFile(originalPath, 2, 0.2f);
            CreateWaveFile(takePath, 0.5, 0.8f);
            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");
            store.AddAudioFiles([originalPath]);
            var track = Assert.Single(store.Project.Tracks);
            track.Name = "Lead vocal";
            track.Clips[0].StartSeconds = 1;
            track.Volume = 0.65;
            track.IsMuted = true;
            store.Save();

            var originalClips = store.CaptureTrackClips(track);
            store.PunchInRecordedAudio(track, takePath, punchInSeconds: 1.5);
            store.RestoreTrackClips(originalClips);

            var reopened = ProjectStore.Open(store.ProjectFilePath);
            var restoredTrack = Assert.Single(reopened.Project.Tracks);
            var restoredClip = Assert.Single(restoredTrack.Clips);
            Assert.Equal("Lead vocal", restoredTrack.Name);
            Assert.Equal(1, restoredClip.StartSeconds);
            Assert.Equal(0, restoredClip.TrimStartSeconds);
            Assert.Equal(0, restoredClip.TrimEndSeconds);
            Assert.Equal(0.65, restoredTrack.Volume);
            Assert.True(restoredTrack.IsMuted);
            Assert.True(File.Exists(reopened.ResolveAudioPath(restoredClip)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RestoreTrackClipsAfterSaveAsCopiesOriginalMediaNoLongerInProjectCopy()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var originalPath = Path.Combine(root, "original.wav");
            var takePath = Path.Combine(root, "take.wav");
            CreateWaveFile(originalPath, 1, 0.2f);
            CreateWaveFile(takePath, 1.5, 0.8f);
            var store = ProjectStore.Create(Path.Combine(root, "Original", "Song.musictool"), "Song");
            store.AddAudioFiles([originalPath]);
            var track = Assert.Single(store.Project.Tracks);
            var originalClips = store.CaptureTrackClips(track);
            store.PunchInRecordedAudio(track, takePath, punchInSeconds: 0);

            store.SaveAs(Path.Combine(root, "Copy", "Song.musictool"));
            store.RestoreTrackClips(originalClips);

            var reopened = ProjectStore.Open(store.ProjectFilePath);
            var restoredClip = Assert.Single(Assert.Single(reopened.Project.Tracks).Clips);
            Assert.Equal(1, reopened.GetClipDuration(restoredClip), 2);
            Assert.True(File.Exists(reopened.ResolveAudioPath(restoredClip)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateWaveFile(string path, double durationSeconds, float sampleValue)
    {
        var sampleCount = (int)(durationSeconds * 44100);
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(44100, 1));
        var samples = Enumerable.Repeat(sampleValue, sampleCount).ToArray();
        writer.WriteSamples(samples, 0, samples.Length);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"MusicToolTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
