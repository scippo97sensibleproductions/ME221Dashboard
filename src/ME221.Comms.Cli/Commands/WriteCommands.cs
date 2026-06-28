using System.Buffers.Binary;
using System.Globalization;
using ME221.Comms.Cli.Results;
using ME221.Comms.Cli.ui;
using ME221.Comms.Messages;
using ME221.Data.Models;

namespace ME221.Comms.Cli.Commands;

public static class WriteCommands
{
    public static async Task<IReadOnlyList<CommandResult>> ExecuteAsync(
        CommandExecutor executor,
        CalibrationData? calibration,
        CancellationToken ct = default)
    {
        var results = new List<CommandResult>();
        var tableLookup = calibration?.Tables.ToDictionary(t => t.Id) ?? [];

        // GPT 3D 3: id=4542, T16x16 — set output[0] and output[255] to 10
        results.Add(await WriteTableElementsAsync(executor, 4542, tableLookup, [0, 255], 10f, ct).ConfigureAwait(false));

        // GPT 2D 3: id=4545, T1x16 — set output[0] and output[15] to 10
        results.Add(await WriteTableElementsAsync(executor, 4545, tableLookup, [0, 15], 10f, ct).ConfigureAwait(false));

        return results;
    }

    private static async Task<CommandResult> WriteTableElementsAsync(
        CommandExecutor executor,
        ushort tableId,
        Dictionary<ushort, TableDefinition> tableLookup,
        int[] elementIndices,
        float value,
        CancellationToken ct)
    {
        var name = tableLookup.TryGetValue(tableId, out var td) ? td.Name : "";
        var label = string.IsNullOrEmpty(name) ? $"0x{tableId:X4}" : $"{name} (0x{tableId:X4})";
        var commandName = $"SetTable {label}";
        var indicesStr = string.Join(", ", elementIndices.Select(i => $"output[{i}]"));

        ConsoleDisplay.WriteLine($"  ... {commandName}");

        try
        {
            // Step 1: Read current table data
            var readResponse = await executor.SendAsync<GetTableResponse>(new GetTableRequest(tableId), ct).ConfigureAwait(false);
            if (readResponse.Status != MessageStatus.Success)
            {
                var msg = $"Read failed: {readResponse.Status}";
                ConsoleDisplay.WriteCommandResult(commandName, false, msg);
                return new CommandResult
                {
                    CommandName = commandName,
                    Status = msg,
                    ResponseType = nameof(GetTableResponse),
                    DataSize = readResponse.Payload.Length,
                    Summary = msg
                };
            }

            var payload = readResponse.Payload.Span;
            if (payload.Length < 9)
            {
                var msg = $"Payload too short: {payload.Length} bytes";
                ConsoleDisplay.WriteCommandResult(commandName, false, msg);
                return new CommandResult
                {
                    CommandName = commandName,
                    Status = msg,
                    ResponseType = nameof(GetTableResponse),
                    DataSize = payload.Length,
                    Summary = msg
                };
            }

            var serialized = readResponse.SerializedTable.Span;

            var rows = serialized[2];
            var cols = serialized[3];
            var hasInput1 = rows > 1;

            // Clone and modify output elements
            var body = serialized.ToArray();
            var bodyHeader = 4;
            var outputOffset = bodyHeader + (hasInput1 ? rows * 4 : 0) + cols * 4;

            foreach (var idx in elementIndices)
            {
                var byteOff = outputOffset + idx * 4;
                if (byteOff + 4 <= body.Length)
                    BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(byteOff), value);
            }

            // Step 2: Write modified table
            var writeResponse = await executor.SendAsync<SetTableResponse>(new SetTableRequest(tableId, body), ct).ConfigureAwait(false);
            var success = writeResponse.Status == MessageStatus.Success;
            var summary = success
                ? $"{indicesStr} = {value:F2}"
                : $"Write failed: {writeResponse.Status}";

            // Step 3: Read back and verify
            if (success)
            {
                var verifyResponse = await executor.SendAsync<GetTableResponse>(new GetTableRequest(tableId), ct).ConfigureAwait(false);
                if (verifyResponse.Status == MessageStatus.Success && verifyResponse.Payload.Length >= 9)
                {
                    var verifyBody = verifyResponse.Payload.Span[5..];
                    var verifyOffset = bodyHeader + (hasInput1 ? rows * 4 : 0) + cols * 4;
                    var verifyItems = new List<string>();
                    foreach (var idx in elementIndices)
                    {
                        var val = BinaryPrimitives.ReadSingleLittleEndian(verifyBody[(verifyOffset + idx * 4)..]);
                        verifyItems.Add($"{val.ToString("F2", CultureInfo.InvariantCulture)}");
                    }
                    summary += $", readback: [{string.Join(", ", verifyItems)}]";
                }
            }

            ConsoleDisplay.WriteCommandResult(commandName, success, summary);
            return new CommandResult
            {
                CommandName = commandName,
                Status = success ? "success" : writeResponse.Status.ToString(),
                ResponseType = nameof(SetTableResponse),
                DataSize = writeResponse.Payload.Length,
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
                ResponseType = nameof(SetTableResponse),
                Error = ex.Message,
                Summary = "Timeout"
            };
        }
        catch (OperationCanceledException)
        {
            ConsoleDisplay.WriteCommandResult(commandName, false, "Cancelled");
            return new CommandResult
            {
                CommandName = commandName,
                Status = "cancelled",
                ResponseType = nameof(SetTableResponse),
                Summary = "Cancelled"
            };
        }
        catch (Exception ex)
        {
            ConsoleDisplay.WriteCommandResult(commandName, false, ex.GetType().Name);
            return new CommandResult
            {
                CommandName = commandName,
                Status = "error",
                ResponseType = nameof(SetTableResponse),
                Error = ex.Message,
                Summary = ex.GetType().Name
            };
        }
    }
}
