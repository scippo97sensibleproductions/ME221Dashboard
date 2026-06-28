using System.Buffers.Binary;
using System.Globalization;
using ME221.Comms.Cli.Results;
using ME221.Comms.Cli.ui;
using ME221.Comms.Messages;
using ME221.Data.Models;

namespace ME221.Comms.Cli.Commands;

public static class ReadCommands
{
    public static async Task<IReadOnlyList<CommandResult>> ExecuteAllAsync(
        CommandExecutor executor,
        CalibrationData? calibration = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CommandResult>();

        results.AddRange(await ExecutePreReportingAsync(executor, calibration, cancellationToken).ConfigureAwait(false));
        results.AddRange(await ExecutePostReportingAsync(executor, calibration, cancellationToken).ConfigureAwait(false));

        return results;
    }

    public static async Task<IReadOnlyList<CommandResult>> ExecutePreReportingAsync(
        CommandExecutor executor,
        CalibrationData? calibration = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CommandResult>();

        results.AddRange(await ExecuteDeviceInformationAsync(executor, cancellationToken).ConfigureAwait(false));
        results.AddRange(await ExecuteDriverCommandsAsync(executor, calibration, cancellationToken).ConfigureAwait(false));
        results.AddRange(await ExecuteTableCommandsAsync(executor, calibration, cancellationToken).ConfigureAwait(false));

        return results;
    }

    public static async Task<IReadOnlyList<CommandResult>> ExecutePostReportingAsync(
        CommandExecutor executor,
        CalibrationData? calibration = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CommandResult>();
        var dataLinkLookup = calibration?.DataLinks
            .ToDictionary(dl => dl.Id) ?? [];

        results.AddRange(await ExecuteReportingCommandsAsync(executor, dataLinkLookup, cancellationToken).ConfigureAwait(false));
        results.AddRange(await ExecuteLoggingCommandsAsync(executor, cancellationToken).ConfigureAwait(false));
        results.AddRange(await ExecuteTriggerLoggerCommandsAsync(executor, cancellationToken).ConfigureAwait(false));
        results.AddRange(await ExecuteFirmwareUpdateCommandsAsync(executor, cancellationToken).ConfigureAwait(false));

        return results;
    }

    private static async Task<IReadOnlyList<CommandResult>> ExecuteDeviceInformationAsync(
        CommandExecutor executor, CancellationToken ct)
    {
        var results = new List<CommandResult>();

        var ecuInfo = await ExecuteWithResultAsync("GetECUInfo",
            () => executor.SendAsync<GetEcuInfoResponse>(new GetEcuInfoRequest(), ct),
            r => $"ProductName: {r.ProductName}, Version: {r.Version}");
        results.Add(ecuInfo);

        var hash = await ExecuteWithResultAsync("GetHash (Overall)",
            () => executor.SendAsync<GetHashResponse>(new GetHashRequest(HashRequestMode.Overall), ct),
            r => $"Hash: 0x{r.OverallHash:X4}");
        results.Add(hash);

        var pwState = await ExecuteWithResultAsync("PwLockGetState",
            () => executor.SendAsync<PwLockGetStateResponse>(new PwLockGetStateRequest(), ct),
            r => $"Locked: {r.Locked}, TunerContact: {r.TunerContact}");
        results.Add(pwState);

        return results;
    }

    private static async Task<IReadOnlyList<CommandResult>> ExecuteDriverCommandsAsync(
        CommandExecutor executor, CalibrationData? calibration, CancellationToken ct)
    {
        var results = new List<CommandResult>();

        var driverIds = calibration?.Drivers
            .Where(d => d.ViewInTree)
            .Select(d => d.Id)
            .Take(5)
            .ToList();

        if (driverIds is null || driverIds.Count == 0)
            driverIds = [0x0001];

        foreach (var id in driverIds)
        {
            var name = calibration?.Drivers.FirstOrDefault(d => d.Id == id)?.Name ?? "";
            var label = string.IsNullOrEmpty(name) ? $"0x{id:X4}" : $"{name} (0x{id:X4})";
            var result = await ExecuteWithResultAsync($"GetDriver {label}",
                () => executor.SendAsync<GetDriverResponse>(new GetDriverRequest(id), ct),
                r => r.Status == MessageStatus.Success
                    ? $"Configs: {r.Payload.Length} bytes"
                    : $"Status: {r.Status}");
            results.Add(result);
        }

        return results;
    }

    private static async Task<IReadOnlyList<CommandResult>> ExecuteTableCommandsAsync(
        CommandExecutor executor, CalibrationData? calibration, CancellationToken ct)
    {
        var results = new List<CommandResult>();

        ushort[] tableIds = [4610, 4352]; // CLT HRT (1×16), Inj. Target AFR (16×16)

        var tableLookup = calibration?.Tables.ToDictionary(t => t.Id) ?? [];

        foreach (var id in tableIds)
        {
            var name = tableLookup.TryGetValue(id, out var td) ? td.Name : "";
            var label = string.IsNullOrEmpty(name) ? $"0x{id:X4}" : $"{name} (0x{id:X4})";
            var result = await ExecuteWithResultAsync($"GetTable {label}",
                () => executor.SendAsync<GetTableResponse>(new GetTableRequest(id), ct),
                r => r.Status == MessageStatus.Success
                    ? FormatTableData(r, td)
                    : $"Status: {r.Status}");
            results.Add(result);
        }

        return results;
    }

    private static async Task<IReadOnlyList<CommandResult>> ExecuteReportingCommandsAsync(
        CommandExecutor executor,
        Dictionary<ushort, DataLinkDefinition> dataLinkLookup,
        CancellationToken ct)
    {
        var results = new List<CommandResult>();

        // ── SetState (Enable) — get entity map ────────────────────────────
        SetStateResponse? setStateResponse = null;
        try
        {
            ConsoleDisplay.WriteLine("  ... SetState (Enable Reporting)");
            setStateResponse = await executor.SendAsync<SetStateResponse>(new SetStateRequest(true), ct);
            var success = setStateResponse.Status == MessageStatus.Success;
            var summary = success
                ? FormatSetStateSummary(setStateResponse, dataLinkLookup)
                : $"Status: {setStateResponse.Status}";
            ConsoleDisplay.WriteCommandResult("SetState (Enable Reporting)", success, summary);
            results.Add(new CommandResult
            {
                CommandName = "SetState (Enable Reporting)",
                Status = success ? "success" : setStateResponse.Status.ToString(),
                ResponseType = nameof(SetStateResponse),
                DataSize = setStateResponse.Payload.Length,
                Summary = summary
            });
        }
        catch (TimeoutException ex)
        {
            ConsoleDisplay.WriteCommandResult("SetState (Enable Reporting)", false, "Timeout");
            results.Add(new CommandResult { CommandName = "SetState (Enable Reporting)", Status = "timeout", ResponseType = nameof(SetStateResponse), Error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            ConsoleDisplay.WriteCommandResult("SetState (Enable Reporting)", false, "Cancelled");
            results.Add(new CommandResult { CommandName = "SetState (Enable Reporting)", Status = "cancelled", ResponseType = nameof(SetStateResponse) });
        }
        catch (Exception ex)
        {
            ConsoleDisplay.WriteCommandResult("SetState (Enable Reporting)", false, ex.GetType().Name);
            results.Add(new CommandResult { CommandName = "SetState (Enable Reporting)", Status = "error", ResponseType = nameof(SetStateResponse), Error = ex.Message });
        }

        // ── Build entity map from SetState V2 response ────────────────────
        Dictionary<ushort, (ReportingType Type, int Size)>? entityMap = null;
        if (setStateResponse?.ProtoVersion == ReportingVersion.V2 && setStateResponse.Entities.Count > 0)
        {
            entityMap = new Dictionary<ushort, (ReportingType, int)>();
            foreach (var e in setStateResponse.Entities)
            {
                var size = e.Type switch
                {
                    ReportingType.Float4B => 4,
                    ReportingType.Int2B => 2,
                    ReportingType.Uint2B => 2,
                    ReportingType.Int1B => 1,
                    ReportingType.Uint1B => 1,
                    ReportingType.Bool1B => 1,
                    _ => 4,
                };
                entityMap[e.Id] = (e.Type, size);
            }
        }

        // ── SendReportRequest — get sensor values ─────────────────────────
        try
        {
            ConsoleDisplay.WriteLine("  ... SendReportRequest");
            var raw = await executor.SendAsync<SendReportResponse>(new SendReportRequest(), ct);
            var success = raw.Status == MessageStatus.Success;
            string summary;

            switch (success)
            {
                case true when entityMap is { Count: > 0 } && raw.Payload.Span.Length > 1:
                {
                    // Re-parse V2 using entity map from SetState
                    var parsed = new SendReportResponse(raw.Payload.Span, ReportingVersion.V2, entityMap);
                    summary = FormatReportSummary(parsed, dataLinkLookup);
                    success = parsed.Entities.Length > 0;
                    break;
                }
                case true:
                    summary = raw.Entities.Length > 0
                        ? FormatReportSummary(raw, dataLinkLookup)
                        : "No entities in report";
                    success = raw.Entities.Length > 0;
                    break;
                default:
                    summary = $"Status: {raw.Status}";
                    break;
            }

            ConsoleDisplay.WriteCommandResult("SendReportRequest", success, summary);
            results.Add(new CommandResult
            {
                CommandName = "SendReportRequest",
                Status = success ? "success" : raw.Status.ToString(),
                ResponseType = nameof(SendReportResponse),
                DataSize = raw.Payload.Length,
                Summary = summary
            });
        }
        catch (TimeoutException ex)
        {
            ConsoleDisplay.WriteCommandResult("SendReportRequest", false, "Timeout");
            results.Add(new CommandResult { CommandName = "SendReportRequest", Status = "timeout", ResponseType = nameof(SendReportResponse), Error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            ConsoleDisplay.WriteCommandResult("SendReportRequest", false, "Cancelled");
            results.Add(new CommandResult { CommandName = "SendReportRequest", Status = "cancelled", ResponseType = nameof(SendReportResponse) });
        }
        catch (Exception ex)
        {
            ConsoleDisplay.WriteCommandResult("SendReportRequest", false, ex.GetType().Name);
            results.Add(new CommandResult { CommandName = "SendReportRequest", Status = "error", ResponseType = nameof(SendReportResponse), Error = ex.Message });
        }

        // ── SetState (Disable) ────────────────────────────────────────────
        try
        {
            ConsoleDisplay.WriteLine("  ... SetState (Disable Reporting)");
            var off = await executor.SendAsync<SetStateResponse>(new SetStateRequest(false), ct);
            var success = off.Status == MessageStatus.Success;
            var summary = success ? "Reporting disabled" : $"Status: {off.Status}";
            ConsoleDisplay.WriteCommandResult("SetState (Disable Reporting)", success, summary);
            results.Add(new CommandResult
            {
                CommandName = "SetState (Disable Reporting)",
                Status = success ? "success" : off.Status.ToString(),
                ResponseType = nameof(SetStateResponse),
                DataSize = off.Payload.Length,
                Summary = summary
            });
        }
        catch (TimeoutException ex)
        {
            ConsoleDisplay.WriteCommandResult("SetState (Disable Reporting)", false, "Timeout (expected — ECU may not respond to disable)");
            results.Add(new CommandResult { CommandName = "SetState (Disable Reporting)", Status = "timeout", ResponseType = nameof(SetStateResponse), Error = ex.Message });
        }
        catch (OperationCanceledException)
        {
            ConsoleDisplay.WriteCommandResult("SetState (Disable Reporting)", false, "Cancelled");
            results.Add(new CommandResult { CommandName = "SetState (Disable Reporting)", Status = "cancelled", ResponseType = nameof(SetStateResponse) });
        }
        catch (Exception ex)
        {
            ConsoleDisplay.WriteCommandResult("SetState (Disable Reporting)", false, ex.GetType().Name);
            results.Add(new CommandResult { CommandName = "SetState (Disable Reporting)", Status = "error", ResponseType = nameof(SetStateResponse), Error = ex.Message });
        }

        return results;
    }

    private static string FormatSetStateSummary(SetStateResponse r, Dictionary<ushort, DataLinkDefinition> lookup)
    {
        var info = $"Version: {r.ProtoVersion}, Entities: {r.Entities.Count}";
        if (r.Entities.Count > 0)
        {
            var sample = r.Entities.Take(5).Select(e =>
            {
                var name = lookup.TryGetValue(e.Id, out var dl) ? dl.Name : $"0x{e.Id:X4}";
                var typeName = (byte)e.Type switch
                {
                    0 => "float",
                    1 => "int16",
                    2 => "uint16",
                    3 => "int8",
                    4 => "uint8",
                    5 => "bool",
                    _ => $"type{(byte)e.Type}"
                };
                return $"{name}({typeName})";
            });
            info += $"  [{string.Join(", ", sample)}]";
        }
        return info;
    }

    private static string FormatReportSummary(SendReportResponse r, Dictionary<ushort, DataLinkDefinition> lookup)
    {
        if (r.Entities.Length == 0)
            return "No entities in report";

        var items = new List<string>();
        foreach (var entity in r.Entities.Span)
        {
            var name = lookup.TryGetValue(entity.Id, out var dl) ? dl.Name : $"0x{entity.Id:X4}";
            var unit = dl?.MeasureUnit ?? "";
            var unitStr = string.IsNullOrEmpty(unit) ? "" : $" {unit}";
            items.Add($"{name} = {entity.Value:F2}{unitStr}");
        }
        return string.Join(", ", items);
    }

    private static int GetReportingTypeSize(ReportingType type) => type switch
    {
        ReportingType.Float4B => 4,
        ReportingType.Int2B => 2,
        ReportingType.Uint2B => 2,
        ReportingType.Int1B => 1,
        ReportingType.Uint1B => 1,
        ReportingType.Bool1B => 1,
        _ => 4,
    };

    private static string FormatTableData(GetTableResponse r, TableDefinition? td)
    {
        var serialized = r.SerializedTable.Span;
        if (serialized.Length < 4)
            return $"SerializedTable: {serialized.Length} bytes";

        var enabled = serialized[1] != 0;
        var rows = serialized[2];
        var cols = serialized[3];

        // Body = [type:1][enabled:1][rows:1][cols:1] + Input1(Rows>1) + Input0 + Output
        var hasInput1 = rows > 1;
        var bodyHeader = 4;
        var expectedLen = bodyHeader + (hasInput1 ? rows * 4 : 0) + cols * 4 + rows * cols * 4;

        var info = $"{rows}×{cols}, {(enabled ? "ON" : "OFF")} ";

        if (serialized.Length < expectedLen)
            return info + $"(truncated: {serialized.Length}/{expectedLen} bytes)";

        var offset = bodyHeader;

        float[] input1;
        if (hasInput1)
        {
            input1 = new float[rows];
            for (var i = 0; i < rows; i++, offset += 4)
                input1[i] = BinaryPrimitives.ReadSingleLittleEndian(serialized[offset..]);
        }
        else
        {
            input1 = [];
        }

        var input0 = new float[cols];
        for (var i = 0; i < cols; i++, offset += 4)
            input0[i] = BinaryPrimitives.ReadSingleLittleEndian(serialized[offset..]);

        if (hasInput1)
            info += $"      In1: [{string.Join(", ", input1.Select(v => v.ToString("F1", CultureInfo.InvariantCulture)))}]\n";
        info += $"      In0: [{string.Join(", ", input0.Select(v => v.ToString("F1", CultureInfo.InvariantCulture)))}]\n";

        var previewCols = cols <= 8 ? cols : 8;
        var previewRows = rows <= 8 ? rows : 4;
        info += "      Output:\n";
        for (var rIdx = 0; rIdx < previewRows; rIdx++)
        {
            var rowVals = new List<string>();
            for (var cIdx = 0; cIdx < previewCols; cIdx++)
            {
                var val = BinaryPrimitives.ReadSingleLittleEndian(serialized[offset..]);
                rowVals.Add(val.ToString("F2", CultureInfo.InvariantCulture));
                offset += 4;
            }
            info += $"      [{string.Join(", ", rowVals)}]\n";
            offset += (cols - previewCols) * 4; // skip to next row
        }

        return info.TrimEnd();
    }

    private static async Task<IReadOnlyList<CommandResult>> ExecuteLoggingCommandsAsync(
        CommandExecutor executor, CancellationToken ct)
    {
        var results = new List<CommandResult>();

        var isSupported = await ExecuteFireAndForgetWithResultAsync(executor, "IsSupported",
            new IsSupportedRequest(), ct);
        results.Add(isSupported);

        var config = await ExecuteWithResultAsync("GetConfig",
            () => executor.SendAsync<GetConfigResponse>(new GetConfigRequest(), ct),
            r => $"Config status: {r.Status}");
        results.Add(config);

        var summary = await ExecuteWithResultAsync("GetLogsSummary",
            () => executor.SendAsync<GetLogsSummaryResponse>(new GetLogsSummaryRequest(), ct),
            r => $"Summary status: {r.Status}");
        results.Add(summary);

        return results;
    }

    private static async Task<IReadOnlyList<CommandResult>> ExecuteTriggerLoggerCommandsAsync(
        CommandExecutor executor, CancellationToken ct)
    {
        var results = new List<CommandResult>();

        var isSupported = await ExecuteFireAndForgetWithResultAsync(executor, "TriggerLoggerIsSupported",
            new TriggerLoggerIsSupportedRequest(), ct);
        results.Add(isSupported);

        return results;
    }

    private static async Task<IReadOnlyList<CommandResult>> ExecuteFirmwareUpdateCommandsAsync(
        CommandExecutor executor, CancellationToken ct)
    {
        var results = new List<CommandResult>();

        var regionInfo = await ExecuteWithResultAsync("RegionInfoGet",
            () => executor.SendAsync<RegionInfoGetResponse>(new RegionInfoGetRequest(0), ct),
            r => $"Region info status: {r.Status}");
        results.Add(regionInfo);

        return results;
    }

    private static async Task<CommandResult> ExecuteWithResultAsync<TResponse>(
        string commandName,
        Func<Task<TResponse>> execute,
        Func<TResponse, string>? summaryFactory = null)
        where TResponse : Response
    {
        ConsoleDisplay.WriteLine($"  ... {commandName}");
        try
        {
            var response = await execute().ConfigureAwait(false);
            var summary = summaryFactory?.Invoke(response);
            var success = response.Status == MessageStatus.Success;

            ConsoleDisplay.WriteCommandResult(commandName, success, summary);
            return new CommandResult
            {
                CommandName = commandName,
                Status = success ? "success" : response.Status.ToString(),
                ResponseType = typeof(TResponse).Name,
                DataSize = response.Payload.Length,
                Summary = summary
            };
        }
        catch (TimeoutException ex)
        {
            ConsoleDisplay.WriteCommandResult(commandName, false, "Timeout");
            return new CommandResult
            {
                CommandName = commandName,
                Status = "timeout",
                ResponseType = typeof(TResponse).Name,
                DataSize = 0,
                Summary = ex.Message,
                Error = ex.Message
            };
        }
        catch (OperationCanceledException)
        {
            ConsoleDisplay.WriteCommandResult(commandName, false, "Cancelled");
            return new CommandResult
            {
                CommandName = commandName,
                Status = "cancelled",
                ResponseType = typeof(TResponse).Name,
                DataSize = 0,
                Summary = "Operation cancelled"
            };
        }
        catch (Exception ex)
        {
            ConsoleDisplay.WriteCommandResult(commandName, false, ex.GetType().Name);
            return new CommandResult
            {
                CommandName = commandName,
                Status = "error",
                ResponseType = typeof(TResponse).Name,
                DataSize = 0,
                Summary = ex.GetType().Name,
                Error = ex.Message
            };
        }
    }

    private static async Task<CommandResult> ExecuteFireAndForgetWithResultAsync(
        CommandExecutor executor, string commandName, MessageFrame frame, CancellationToken ct = default)
    {
        ConsoleDisplay.WriteLine($"  ... {commandName}");
        try
        {
            await executor.SendAsync(frame, ct).ConfigureAwait(false);
            await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
            ConsoleDisplay.WriteCommandResult(commandName, true, "Sent");
            return new CommandResult
            {
                CommandName = commandName,
                Status = "sent",
                ResponseType = "fire-and-forget",
                DataSize = 0,
                Summary = "Command sent (no response expected)"
            };
        }
        catch (Exception ex)
        {
            ConsoleDisplay.WriteCommandResult(commandName, false, ex.GetType().Name);
            return new CommandResult
            {
                CommandName = commandName,
                Status = "error",
                ResponseType = "fire-and-forget",
                DataSize = 0,
                Summary = ex.GetType().Name,
                Error = ex.Message
            };
        }
    }
}
