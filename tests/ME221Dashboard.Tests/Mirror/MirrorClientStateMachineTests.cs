using FluentAssertions;
using ME221Dashboard.Services.Mirror;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

public sealed class MirrorClientStateMachineTests
{
    private static (MirrorSessionState State, List<(MirrorClientState, MirrorClientState)> Log) Create()
    {
        var state = new MirrorSessionState();
        var log = new List<(MirrorClientState, MirrorClientState)>();
        state.Transitioned += (from, to) => log.Add((from, to));
        return (state, log);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public void FullWalkSearchingToConnectedToMirrored()
    {
        var (state, log) = Create();

        state.StartSearch();
        state.SelectDevice("A");
        state.Approved("tok-1");

        state.State.Should().Be(MirrorClientState.Connected);
        state.Token.Should().Be("tok-1");
        state.ServerDisplayName.Should().Be("A");
        log.Should().Contain((MirrorClientState.AwaitingApproval, MirrorClientState.Connected));
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void NoBeaconThenRetry()
    {
        var (state, _) = Create();

        state.NoBeaconFound();
        state.State.Should().Be(MirrorClientState.NoA);

        state.StartSearch();
        state.State.Should().Be(MirrorClientState.Searching);
    }

    [Fact]
    public void VersionMismatchIsIncompatibleAndNotConnectable()
    {
        var (state, _) = Create();

        state.BeaconMismatched();
        state.State.Should().Be(MirrorClientState.Incompatible);
        // No transition to AwaitingApproval exists from Incompatible — never connectable.
    }

    [Fact]
    public void RefusalIsPerSessionRetryable()
    {
        var (state, _) = Create();

        state.SelectDevice();
        state.ApprovalDeniedOrTimeout();
        state.State.Should().Be(MirrorClientState.Refused);

        state.ReRequest();
        state.State.Should().Be(MirrorClientState.AwaitingApproval);
    }

    [Fact]
    public void ExhaustionThresholdIsThreeDrops()
    {
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok");

        state.LinkDropped().Should().BeTrue("attempt 1 → retry remains");
        state.LinkDropped().Should().BeTrue("attempt 2 → retry remains");
        state.LinkDropped().Should().BeFalse("attempt 3 → exhausted");
        state.State.Should().Be(MirrorClientState.Disconnected);
    }

    [Fact]
    public void HostDeathDuringApprovalExhaustsToDisconnected()
    {
        // Regression: a host dying mid-approval must NOT strand the session in
        // AwaitingApproval — the reconnect counter works from AwaitingApproval too.
        var (state, _) = Create();
        state.SelectDevice();

        state.LinkDropped().Should().BeTrue("attempt 1 → retry remains");
        state.LinkDropped().Should().BeTrue("attempt 2 → retry remains");
        state.LinkDropped().Should().BeFalse("attempt 3 → exhausted");
        state.State.Should().Be(MirrorClientState.Disconnected);
    }

    [Fact]
    public void ManualRetryAfterExhaustionReturnsToSearch()
    {
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok");
        state.LinkDropped();
        state.LinkDropped();
        state.LinkDropped();

        state.ManualRetry();
        state.State.Should().Be(MirrorClientState.Searching);
        state.ReconnectAttempts.Should().Be(0);
    }

    [Fact]
    public void ReconnectSucceededResetsAttempts()
    {
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok");
        state.LinkDropped();

        state.ReconnectSucceeded();
        state.State.Should().Be(MirrorClientState.Connected);
        state.ReconnectAttempts.Should().Be(0);
        state.Error.Should().BeNull();
    }

    [Fact]
    public void ReconnectLoopConsumesOneAttemptPerDrop()
    {
        // Regression (review): the reconnect loop previously ran `while (!LinkDropped())`
        // which skipped the first two drops entirely and looped forever after the third.
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok");

        // Drop 1 → retry permitted (attempt 1 counted).
        state.LinkDropped().Should().BeTrue();
        state.ReconnectAttempts.Should().Be(1);
        state.State.Should().Be(MirrorClientState.Connected);

        // Drop 2 → retry permitted (attempt 2 counted).
        state.LinkDropped().Should().BeTrue();
        state.ReconnectAttempts.Should().Be(2);

        // Drop 3 → exhausted → Disconnected (the loop must terminate here).
        state.LinkDropped().Should().BeFalse();
        state.State.Should().Be(MirrorClientState.Disconnected);
    }

    // ── Error paths ──────────────────────────────────────────────────────────

    [Fact]
    public void RevocationStopsAutoReconnectUntilUserActs()
    {
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok");

        state.Revoked();
        state.State.Should().Be(MirrorClientState.Revoked);
        // LinkDropped from Revoked does nothing (no auto-reconnect):
        state.LinkDropped().Should().BeFalse();
        state.State.Should().Be(MirrorClientState.Revoked);
    }

    [Fact]
    public void CertChangeRoutesToRepairWithForgetAndRequestAgain()
    {
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok");

        state.CertificateChanged();
        state.State.Should().Be(MirrorClientState.RePair);
        state.Error.Should().NotBeNullOrWhiteSpace();

        state.ForgetAndReRequest();
        state.State.Should().Be(MirrorClientState.Searching);
        state.Token.Should().BeNull("the old pairing credential is forgotten");
    }

    [Fact]
    public void ConfigUpdateEndsSessionWithoutAutoReconnect()
    {
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok");

        state.ConfigUpdateRequired();
        state.State.Should().Be(MirrorClientState.Disconnected);
        state.Error.Should().Be("update made, reconnect required");
        state.LinkDropped().Should().BeFalse("no auto-reconnect after a config update");
    }

    [Fact]
    public void DisconnectButtonExitsToStopped()
    {
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok");

        state.DisconnectRequested();
        state.State.Should().Be(MirrorClientState.Stopped);
    }

    // ── Integration ──────────────────────────────────────────────────────────

    [Fact]
    public void ReconnectAfterDropResumesFromCachedState()
    {
        var (state, _) = Create();
        state.SelectDevice();
        state.Approved("tok-42");

        // Drop once, reconnect in place, succeed — cached state survives.
        state.LinkDropped().Should().BeTrue();
        state.ReconnectSucceeded();
        state.State.Should().Be(MirrorClientState.Connected);
        state.Token.Should().Be("tok-42");
    }

    [Fact]
    public void StateMachineIsReusableAfterStop()
    {
        var (state, _) = Create();
        state.DisconnectRequested();
        state.StartSearch();
        state.State.Should().Be(MirrorClientState.Searching);
    }
}