using System.Globalization;
using System.Text;
using ME221.Comms.Cli.ui;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Data.Models;

namespace ME221.Comms.Cli.Commands;

public static class StreamCommands
{
    public static async Task ExecuteAsync(
        CommandExecutor executor,
        Dictionary<ushort, DataLinkDefinition>? dataLinkLookup,
        string outputDirectory = "results",
        CancellationToken ct = default)
    {
        ConsoleDisplay.WriteSection("Live Data Stream");
        ConsoleDisplay.WriteLine("Press Ctrl+C to stop.");
        ConsoleDisplay.WriteLine();

        // ── Enable reporting and get entity map ──────────────────────────
        SetStateResponse? setStateResponse;
        try
        {
            ConsoleDisplay.WriteLine("  ... SetState (Enable Reporting)");
            setStateResponse = await executor.SendAsync<SetStateResponse>(
                new SetStateRequest(true), ct).ConfigureAwait(false);

            if (setStateResponse.Status != MessageStatus.Success)
            {
                ConsoleDisplay.WriteCommandResult("SetState (Enable Reporting)", false,
                    $"Status: {setStateResponse.Status}");
                return;
            }
            ConsoleDisplay.WriteCommandResult("SetState (Enable Reporting)", true,
                $"Version: {setStateResponse.ProtoVersion}, Entities: {setStateResponse.Entities.Count}");
        }
        catch (TimeoutException)
        {
            ConsoleDisplay.WriteCommandResult("SetState (Enable Reporting)", false, "Timeout");
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // ── Build entity map ────────────────────────────────────────────
        var entityMap = new Dictionary<ushort, (ReportingType Type, int Size)>();
        if (setStateResponse.ProtoVersion == ReportingVersion.V2 && setStateResponse.Entities.Count > 0)
        {
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

        // ── Set up CSV logging ──────────────────────────────────────────
        var lookup = dataLinkLookup ?? [];
        var sortedEntities = entityMap.Keys.OrderBy(id => id).ToList();

        Directory.CreateDirectory(outputDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var csvPath = Path.Combine(outputDirectory, $"stream-{timestamp}.csv");

        await using var csvFile = new StreamWriter(csvPath, append: false, encoding: Encoding.UTF8);
        await csvFile.WriteLineAsync(BuildCsvHeader(lookup, sortedEntities)).ConfigureAwait(false);

        ConsoleDisplay.WriteLine($"  Logging to: {Path.GetFullPath(csvPath)}");
        ConsoleDisplay.WriteLine();

        // ── Print console header ────────────────────────────────────────
        PrintConsoleHeader(lookup, sortedEntities);

        // ── Stream loop ─────────────────────────────────────────────────
        var protocolService = executor.GetProtocolService();
        var frameCount = 0;
        var startTime = DateTime.UtcNow;
        var lastPrintTime = DateTime.UtcNow;
        const int printIntervalMs = 100;

        try
        {
            await foreach (var frame in protocolService.UncorrelatedFrames(ct).ConfigureAwait(false))
            {
                if (frame.Type != WireFormat.ResponseType ||
                    frame.Class != WireFormat.ClassReporting ||
                    frame.Command != WireFormat.ReportingSendReport)
                    continue;

                frameCount++;

                if (entityMap.Count > 0 && frame.Payload.Span.Length > 1)
                {
                    var report = new SendReportResponse(frame.Payload.Span, ReportingVersion.V2, entityMap);
                    var now = DateTime.UtcNow;
                    var elapsed = (now - startTime).TotalSeconds;

                    // Build value lookup
                    var values = new Dictionary<ushort, float>();
                    foreach (var entity in report.Entities.Span)
                        values[entity.Id] = entity.Value;

                    // Write CSV row (every frame)
                    await csvFile.WriteLineAsync(BuildCsvRow(elapsed, lookup, sortedEntities, values)).ConfigureAwait(false);

                    // Throttle console print
                    if ((now - lastPrintTime).TotalMilliseconds >= printIntervalMs)
                    {
                        PrintConsoleValues(lookup, sortedEntities, values, frameCount);
                        lastPrintTime = now;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit
        }
        finally
        {
            await csvFile.FlushAsync(CancellationToken.None).ConfigureAwait(false);

            // ── Disable reporting ───────────────────────────────────────
            ConsoleDisplay.WriteLine();
            try
            {
                ConsoleDisplay.WriteLine("  ... SetState (Disable Reporting)");
                var off = await executor.SendAsync<SetStateResponse>(
                    new SetStateRequest(false),
                    CancellationToken.None).ConfigureAwait(false);
                ConsoleDisplay.WriteCommandResult("SetState (Disable Reporting)",
                    off.Status == MessageStatus.Success,
                    off.Status == MessageStatus.Success ? "Reporting disabled" : $"Status: {off.Status}");
            }
            catch
            {
                // Best-effort disable
            }

            ConsoleDisplay.WriteLine($"  Streamed {frameCount} frames.");
            ConsoleDisplay.WriteSuccess($"CSV saved to: {Path.GetFullPath(csvPath)}");
        }
    }

    private static string BuildCsvHeader(
        Dictionary<ushort, DataLinkDefinition> lookup,
        List<ushort> sortedEntities)
    {
        var sb = new StringBuilder();
        sb.Append("timestamp_s,frame");
        foreach (var id in sortedEntities)
        {
            var name = lookup.TryGetValue(id, out var dl) ? dl.Name : $"0x{id:X4}";
            var unit = dl?.MeasureUnit ?? "";
        sb.Append(',');
        sb.Append(EscapeCsvField(name));
        if (!string.IsNullOrEmpty(unit))
        {
            sb.Append(" [");
            sb.Append(unit);
            sb.Append(']');
        }
        }
        return sb.ToString();
    }

    private static string BuildCsvRow(
        double elapsedSeconds,
        Dictionary<ushort, DataLinkDefinition> lookup,
        List<ushort> sortedEntities,
        Dictionary<ushort, float> values)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(elapsedSeconds.ToString("F3", ci));
        sb.Append(',');
        sb.Append(values.Count > 0 ? "1" : "0"); // frame valid marker

        foreach (var id in sortedEntities)
        {
            sb.Append(',');
            if (values.TryGetValue(id, out var val))
                sb.Append(val.ToString("G", ci));
        }
        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    private static void PrintConsoleHeader(
        Dictionary<ushort, DataLinkDefinition> lookup,
        List<ushort> sortedEntities)
    {
        var sb = new StringBuilder();
        sb.Append(string.Format(CultureInfo.InvariantCulture, "{0,-5}", "#"));
        foreach (var id in sortedEntities)
        {
            var name = lookup.TryGetValue(id, out var dl) ? dl.Name : $"0x{id:X4}";
            if (name.Length > 12) name = name[..12];
            sb.Append(string.Format(CultureInfo.InvariantCulture, " {0,-14}", name));
        }
        ConsoleDisplay.WriteLine(sb.ToString());
        ConsoleDisplay.WriteLine(new string('─', sb.Length));
    }

    private static void PrintConsoleValues(
        Dictionary<ushort, DataLinkDefinition> lookup,
        List<ushort> sortedEntities,
        Dictionary<ushort, float> values,
        int frameCount)
    {
        var sb = new StringBuilder();
        sb.Append(string.Format(CultureInfo.InvariantCulture, "{0,-5}", frameCount));
        foreach (var id in sortedEntities)
        {
            var name = lookup.TryGetValue(id, out var dl) ? dl.Name : $"0x{id:X4}";
            var unit = dl?.MeasureUnit ?? "";

            if (values.TryGetValue(id, out var val))
            {
                var formatted = FormatValue(val, name, unit);
                sb.Append(string.Format(CultureInfo.InvariantCulture, " {0,-14}", formatted));
            }
            else
            {
                sb.Append(string.Format(CultureInfo.InvariantCulture, " {0,-14}", "\u2014"));
            }
        }

        Console.Write($"\r{sb}\x1b[K");
    }

    private static string FormatValue(float val, string name, string unit)
    {
        if (unit.Contains('%'))
            return string.Format(CultureInfo.InvariantCulture, "{0,7:F1}%", val);
        if (name.Contains("RPM", StringComparison.OrdinalIgnoreCase))
            return string.Format(CultureInfo.InvariantCulture, "{0,8:F0}rpm", val);
        if (unit.Contains('V'))
            return string.Format(CultureInfo.InvariantCulture, "{0,7:F2}V", val);
        if (unit.Contains("degC", StringComparison.OrdinalIgnoreCase) ||
            unit.Contains("\u00b0C", StringComparison.OrdinalIgnoreCase))
            return string.Format(CultureInfo.InvariantCulture, "{0,7:F1}\u00b0C", val);
        if (name.Contains("Speed", StringComparison.OrdinalIgnoreCase))
            return string.Format(CultureInfo.InvariantCulture, "{0,7:F1}km/h", val);
        if (name.Contains("Press", StringComparison.OrdinalIgnoreCase))
            return string.Format(CultureInfo.InvariantCulture, "{0,7:F1}{1}", val, unit);

        return string.Format(CultureInfo.InvariantCulture, "{0,8:F2}", val);
    }
}
