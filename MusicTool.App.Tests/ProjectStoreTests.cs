using MusicTool.App.Services;
using Xunit;

namespace MusicTool.App.Tests;

public sealed class ProjectStoreTests
{
    [Fact]
    public void ImportedAudioIsCopiedAndAvailableAfterReopeningProject()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourcePath = Path.Combine(root, "karaoke.wav");
            File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);
            var projectPath = Path.Combine(root, "Project", "Song.musictool");
            var store = ProjectStore.Create(projectPath, "Song");

            store.AddAudioFiles([sourcePath]);
            store.Project.Tracks[0].Clips[0].StartSeconds = 2.5;
            store.Project.Tracks[0].Clips[0].TrimStartSeconds = 0.5;
            store.Project.Tracks[0].Clips[0].TrimEndSeconds = 1;
            store.Project.Tracks[0].Volume = 0.75;
            store.Project.Tracks[0].IsMuted = true;
            store.Save();
            var importedPath = store.ResolveAudioPath(store.Project.Tracks[0].Clips[0]);
            var reopened = ProjectStore.Open(projectPath);

            Assert.NotEqual(sourcePath, importedPath);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(importedPath));
            Assert.Single(reopened.Project.Tracks);
            Assert.Equal("karaoke", reopened.Project.Tracks[0].Name);
            Assert.Equal(2.5, reopened.Project.Tracks[0].Clips[0].StartSeconds);
            Assert.Equal(0.5, reopened.Project.Tracks[0].Clips[0].TrimStartSeconds);
            Assert.Equal(1, reopened.Project.Tracks[0].Clips[0].TrimEndSeconds);
            Assert.Equal(0.75, reopened.Project.Tracks[0].Volume);
            Assert.True(reopened.Project.Tracks[0].IsMuted);
            Assert.True(File.Exists(reopened.ResolveAudioPath(reopened.Project.Tracks[0].Clips[0])));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveAsCopiesProjectMediaAndKeepsTracksResolvable()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourcePath = Path.Combine(root, "vocal.mp3");
            File.WriteAllBytes(sourcePath, [5, 6, 7]);
            var store = ProjectStore.Create(Path.Combine(root, "Original", "Song.musictool"), "Song");
            store.AddAudioFiles([sourcePath]);
            var originalMediaPath = store.ResolveAudioPath(store.Project.Tracks[0].Clips[0]);
            var copyPath = Path.Combine(root, "Copy", "Song Copy.musictool");

            store.SaveAs(copyPath);

            Assert.True(File.Exists(copyPath));
            Assert.True(File.Exists(originalMediaPath));
            Assert.True(File.Exists(store.ResolveAudioPath(store.Project.Tracks[0].Clips[0])));
            Assert.Equal("Song Copy", store.Project.Name);
            Assert.Single(ProjectStore.Open(copyPath).Project.Tracks);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EmptyTracksSurviveSaveAsAndRecordingCanFillAnExistingTrack()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = ProjectStore.Create(Path.Combine(root, "Original", "Song.musictool"), "Song");
            var emptyTrack = store.AddEmptyTrack();
            var recordedAudioPath = Path.Combine(store.MediaDirectory, "Vocal.wav");
            File.WriteAllBytes(recordedAudioPath, [8, 9, 10]);
            store.AddRecordedAudio(recordedAudioPath, emptyTrack);

            var copyPath = Path.Combine(root, "Copy", "Song Copy.musictool");
            store.SaveAs(copyPath);
            var reopened = ProjectStore.Open(copyPath);

            Assert.Single(reopened.Project.Tracks);
            Assert.Equal("Vocal", reopened.Project.Tracks[0].Name);
            Assert.True(File.Exists(reopened.ResolveAudioPath(reopened.Project.Tracks[0].Clips[0])));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RenamingTrackTrimsNameAndPersistsAcrossReopen()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");
            var track = store.AddEmptyTrack();
            track.Clips.Add(new MusicTool.App.Models.AudioClip
            {
                RelativePath = "Media/backing.wav",
                StartSeconds = 12.5
            });
            track.Volume = 0.6;
            track.IsMuted = true;

            store.RenameTrack(track, "  Lead vocal  ");

            var reopened = ProjectStore.Open(store.ProjectFilePath);
            var savedTrack = Assert.Single(reopened.Project.Tracks);
            Assert.Equal("Lead vocal", savedTrack.Name);
            Assert.Equal(12.5, Assert.Single(savedTrack.Clips).StartSeconds);
            Assert.Equal("Media/backing.wav", savedTrack.Clips[0].RelativePath);
            Assert.Equal(0.6, savedTrack.Volume);
            Assert.True(savedTrack.IsMuted);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \t ")]
    public void RenamingTrackRejectsBlankName(string name)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");
            var track = store.AddEmptyTrack();

            Assert.Throws<ArgumentException>(() => store.RenameTrack(track, name));
            Assert.Equal("Audio 1", track.Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnsupportedAudioTypeIsRejected()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourcePath = Path.Combine(root, "notes.txt");
            File.WriteAllText(sourcePath, "not audio");
            var store = ProjectStore.Create(Path.Combine(root, "Song.musictool"), "Song");

            Assert.Throws<NotSupportedException>(() => store.AddAudioFiles([sourcePath]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TempoAndTimeSignatureAreSavedAndReopened()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var projectPath = Path.Combine(root, "Song.musictool");
            var store = ProjectStore.Create(projectPath, "Song");
            Assert.Equal(128, store.Project.TempoBpm);
            Assert.Equal(4, store.Project.TimeSignatureNumerator);
            Assert.Equal(4, store.Project.TimeSignatureDenominator);
            store.Project.TempoBpm = 92;
            store.Project.TimeSignatureNumerator = 6;
            store.Project.TimeSignatureDenominator = 8;
            store.Save();

            var reopened = ProjectStore.Open(projectPath);

            Assert.Equal(92, reopened.Project.TempoBpm);
            Assert.Equal(6, reopened.Project.TimeSignatureNumerator);
            Assert.Equal(8, reopened.Project.TimeSignatureDenominator);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OlderProjectWithoutTimingPropertiesUsesDefaultTempoAndMeter()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var projectPath = Path.Combine(root, "Legacy.musictool");
            File.WriteAllText(projectPath, """{"Name":"Legacy","Tracks":[]}""");

            var reopened = ProjectStore.Open(projectPath);

            Assert.Equal(128, reopened.Project.TempoBpm);
            Assert.Equal(4, reopened.Project.TimeSignatureNumerator);
            Assert.Equal(4, reopened.Project.TimeSignatureDenominator);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"MusicToolTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
