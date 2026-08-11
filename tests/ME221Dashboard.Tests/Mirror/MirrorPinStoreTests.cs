using FluentAssertions;
using ME221Dashboard.Services.Mirror;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

public sealed class MirrorPinStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "me221-pins-" + Guid.NewGuid().ToString("N"));
    private readonly MirrorPinStore _store;

    public MirrorPinStoreTests()
    {
        _store = new MirrorPinStore();
        _store.SetBasePath(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task SaveSurvivesRestart()
    {
        await _store.SaveAsync(new MirrorPinEntry { InstanceId = "inst-1", Token = "tok-1", CertThumbprint = "ABC", DisplayName = "A" });

        var reloaded = new MirrorPinStore();
        reloaded.SetBasePath(_tempDir);
        var entry = await reloaded.GetAsync("inst-1");

        entry.Should().NotBeNull();
        entry!.Token.Should().Be("tok-1");
        entry.CertThumbprint.Should().Be("ABC");
    }

    [Fact]
    public async Task SaveReplacesSameInstance()
    {
        await _store.SaveAsync(new MirrorPinEntry { InstanceId = "inst-1", Token = "tok-1", CertThumbprint = "A" });
        await _store.SaveAsync(new MirrorPinEntry { InstanceId = "inst-1", Token = "tok-2", CertThumbprint = "B" });

        var entry = await _store.GetAsync("inst-1");
        entry!.Token.Should().Be("tok-2");
        (await _store.GetAsync("inst-2")).Should().BeNull();
    }

    [Fact]
    public async Task RemoveForgetsPairing()
    {
        await _store.SaveAsync(new MirrorPinEntry { InstanceId = "inst-1", Token = "tok-1" });
        await _store.RemoveAsync("inst-1");
        (await _store.GetAsync("inst-1")).Should().BeNull();
    }

    [Fact]
    public async Task CorruptedStoreFallsBackEmpty()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "companion-pins.json"), "{ nope !!");

        (await _store.GetAsync("inst-1")).Should().BeNull();
    }
}