using System.Buffers.Binary;
using System.Globalization;
using ME221.Comms.Cli.Results;
using ME221.Comms.Cli.ui;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
using ME221.Data.Models;

namespace ME221.Comms.Cli.Commands;

public static class DriverCommands
{
    /// <summary>
    /// Read all visible drivers, deserialize their wire data, and display detailed info.
    /// </summary>
    public static async Task<IReadOnlyList<CommandResult>> ReadAllDriversAsync(
        CommandExecutor executor,
        CalibrationData? calibration,
        CancellationToken ct = default)
    {
        var results = new List<CommandResult>();
        var drivers = calibration?.Drivers.Where(d => d.ViewInTree).ToList() ?? [];

        if (drivers.Count == 0)
        {
            ConsoleDisplay.WriteInfo("No drivers in calibration. Using fallback IDs.");
            drivers = [new DriverDefinition { Id = 0x0001, Name = "Fallback", NumberOfConfigs = 0 }];
        }

        foreach (var driver in drivers)
        {
            var label = $"{driver.Name} (0x{driver.Id:X4})";
            ConsoleDisplay.WriteLine($"  ... GetDriver {label}");

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var response = await executor.SendAsync<GetDriverResponse>(
                    new GetDriverRequest(driver.Id), cts.Token);

                if (response.Status != MessageStatus.Success)
                {
                    ConsoleDisplay.WriteCommandResult($"GetDriver {label}", false, $"Status: {response.Status}");
                    results.Add(new CommandResult
                    {
                        CommandName = $"GetDriver {label}",
                        Status = response.Status.ToString(),
                        ResponseType = nameof(GetDriverResponse),
                        DataSize = response.Payload.Length,
                        Summary = $"Status: {response.Status}"
                    });
                    continue;
                }

                var serialized = response.SerializedDriver.Span;
                var wireData = DriverSerializer.Deserialize(serialized);

                var summary = FormatDriverWireData(driver, wireData);
                ConsoleDisplay.WriteCommandResult($"GetDriver {label}", true, summary);
                results.Add(new CommandResult
                {
                    CommandName = $"GetDriver {label}",
                    Status = "success",
                    ResponseType = nameof(GetDriverResponse),
                    DataSize = serialized.Length,
                    Summary = summary
                });
            }
            catch (TimeoutException)
            {
                ConsoleDisplay.WriteCommandResult($"GetDriver {label}", false, "Timeout");
                results.Add(new CommandResult
                {
                    CommandName = $"GetDriver {label}",
                    Status = "timeout",
                    ResponseType = nameof(GetDriverResponse),
                    Summary = "Timeout"
                });
            }
            catch (Exception ex)
            {
                ConsoleDisplay.WriteCommandResult($"GetDriver {label}", false, ex.GetType().Name);
                results.Add(new CommandResult
                {
                    CommandName = $"GetDriver {label}",
                    Status = "error",
                    ResponseType = nameof(GetDriverResponse),
                    Summary = ex.Message,
                    Error = ex.Message
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Write a driver config back and read it to verify round-trip.
    /// </summary>
    public static async Task<CommandResult> VerifyDriverRoundTripAsync(
        CommandExecutor executor,
        DriverDefinition driver,
        CancellationToken ct = default)
    {
        var label = $"{driver.Name} (0x{driver.Id:X4})";
        ConsoleDisplay.WriteLine($"  ... VerifyRoundTrip {label}");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Read original
            var getResponse = await executor.SendAsync<GetDriverResponse>(
                new GetDriverRequest(driver.Id), cts.Token);

            if (getResponse.Status != MessageStatus.Success)
                return Fail($"GetDriver failed: {getResponse.Status}");

            var original = DriverSerializer.Deserialize(getResponse.SerializedDriver.Span);

            // Write it back unchanged
            var serialized = DriverSerializer.Serialize(
                original.Configs, original.OutputIds, original.InputIds);
            ReadOnlyMemory<byte> serializedMem = serialized;
            var setResponse = await executor.SendAsync<SetDriverResponse>(
                new SetDriverRequest(driver.Id, serializedMem), cts.Token);

            if (setResponse.Status != MessageStatus.Success)
                return Fail($"SetDriver failed: {setResponse.Status}");

            // Read again
            var reRead = await executor.SendAsync<GetDriverResponse>(
                new GetDriverRequest(driver.Id), cts.Token);

            if (reRead.Status != MessageStatus.Success)
                return Fail($"Re-read failed: {reRead.Status}");

            var roundTripped = DriverSerializer.Deserialize(reRead.SerializedDriver.Span);

            // Compare
            var configsMatch = original.Configs.Length == roundTripped.Configs.Length
                && original.Configs.Zip(roundTripped.Configs).All(p => p.First == p.Second);
            var outputsMatch = original.OutputIds.SequenceEqual(roundTripped.OutputIds);
            var inputsMatch = original.InputIds.SequenceEqual(roundTripped.InputIds);

            if (configsMatch && outputsMatch && inputsMatch)
            {
                var msg = $"Round-trip OK: {original.Configs.Length} configs, {original.OutputIds.Count} outputs, {original.InputIds.Count} inputs";
                ConsoleDisplay.WriteCommandResult($"VerifyRoundTrip {label}", true, msg);
                return new CommandResult
                {
                    CommandName = $"VerifyRoundTrip {label}",
                    Status = "success",
                    ResponseType = "round-trip",
                    Summary = msg
                };
            }
            else
            {
                var msg = $"MISMATCH: configs={configsMatch} outputs={outputsMatch} inputs={inputsMatch}";
                ConsoleDisplay.WriteCommandResult($"VerifyRoundTrip {label}", false, msg);
                return new CommandResult
                {
                    CommandName = $"VerifyRoundTrip {label}",
                    Status = "failure",
                    ResponseType = "round-trip",
                    Summary = msg
                };
            }
        }
        catch (TimeoutException)
        {
            return Fail("Timeout");
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }

        CommandResult Fail(string msg)
        {
            ConsoleDisplay.WriteCommandResult($"VerifyRoundTrip {label}", false, msg);
            return new CommandResult
            {
                CommandName = $"VerifyRoundTrip {label}",
                Status = "error",
                ResponseType = "round-trip",
                Summary = msg
            };
        }
    }

    /// <summary>
    /// Test SetDriver with a modified config value, then read back to verify ECU accepted it.
    /// WARNING: This modifies ECU state. Use only for testing.
    /// </summary>
    public static async Task<CommandResult> TestSetDriverAsync(
        CommandExecutor executor,
        DriverDefinition driver,
        float testConfigValue,
        CancellationToken ct = default)
    {
        var label = $"{driver.Name} (0x{driver.Id:X4})";
        ConsoleDisplay.WriteLine($"  ... TestSetDriver {label} (config={testConfigValue:F2})");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Read original
            var getResponse = await executor.SendAsync<GetDriverResponse>(
                new GetDriverRequest(driver.Id), cts.Token);

            if (getResponse.Status != MessageStatus.Success)
                return Fail($"GetDriver failed: {getResponse.Status}");

            var original = DriverSerializer.Deserialize(getResponse.SerializedDriver.Span);

            if (original.Configs.Length == 0)
                return Fail("Driver has no config params");

            // Modify first config
            var modifiedConfigs = (float[])original.Configs.Clone();
            modifiedConfigs[0] = testConfigValue;

            // Write
            var serialized = DriverSerializer.Serialize(
                modifiedConfigs, original.OutputIds, original.InputIds);
            ReadOnlyMemory<byte> serializedMem2 = serialized;
            var setResponse = await executor.SendAsync<SetDriverResponse>(
                new SetDriverRequest(driver.Id, serializedMem2), cts.Token);

            if (setResponse.Status != MessageStatus.Success)
                return Fail($"SetDriver failed: {setResponse.Status}");

            // Read back
            var reRead = await executor.SendAsync<GetDriverResponse>(
                new GetDriverRequest(driver.Id), cts.Token);

            if (reRead.Status != MessageStatus.Success)
                return Fail($"Re-read failed: {reRead.Status}");

            var after = DriverSerializer.Deserialize(reRead.SerializedDriver.Span);
            var actual = after.Configs[0];

            // Restore original
            var restore = DriverSerializer.Serialize(
                original.Configs, original.OutputIds, original.InputIds);
            ReadOnlyMemory<byte> restoreMem = restore;
            await executor.SendAsync<SetDriverResponse>(
                new SetDriverRequest(driver.Id, restoreMem), cts.Token);

            var match = Math.Abs(actual - testConfigValue) < 0.001f;
            var msg = match
                ? $"SetDriver OK: wrote {testConfigValue:F2}, read back {actual:F2}"
                : $"MISMATCH: wrote {testConfigValue:F2}, read back {actual:F2}";

            ConsoleDisplay.WriteCommandResult($"TestSetDriver {label}", match, msg);
            return new CommandResult
            {
                CommandName = $"TestSetDriver {label}",
                Status = match ? "success" : "mismatch",
                ResponseType = "set-read-verify",
                Summary = msg
            };
        }
        catch (TimeoutException)
        {
            return Fail("Timeout");
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }

        CommandResult Fail(string msg)
        {
            ConsoleDisplay.WriteCommandResult($"TestSetDriver {label}", false, msg);
            return new CommandResult
            {
                CommandName = $"TestSetDriver {label}",
                Status = "error",
                ResponseType = "set-read-verify",
                Summary = msg
            };
        }
    }

    private static string FormatDriverWireData(DriverDefinition driver, DriverWireData wire)
    {
        var parts = new List<string>
        {
            $"{wire.Configs.Length} configs",
            $"{wire.OutputIds.Count} outputs",
            $"{wire.InputIds.Count} inputs"
        };

        if (wire.Configs.Length > 0)
        {
            var preview = wire.Configs.Take(5).Select(v => v.ToString("F2", CultureInfo.InvariantCulture));
            parts.Add($"[{string.Join(", ", preview)}{(wire.Configs.Length > 5 ? ", ..." : "")}]");
        }

        if (wire.OutputIds.Count > 0)
        {
            var preview = wire.OutputIds.Take(5).Select(id => $"0x{id:X4}");
            parts.Add($"out:[{string.Join(", ", preview)}{(wire.OutputIds.Count > 5 ? ", ..." : "")}]");
        }

        if (wire.InputIds.Count > 0)
        {
            var preview = wire.InputIds.Take(5).Select(id => $"0x{id:X4}");
            parts.Add($"in:[{string.Join(", ", preview)}{(wire.InputIds.Count > 5 ? ", ..." : "")}]");
        }

        return string.Join(", ", parts);
    }
}
