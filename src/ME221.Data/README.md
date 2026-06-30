# ME221.Data

Pure domain models and parsers for ME221 calibration data. No application logic, no framework dependencies (just Serilog for optional logging).

## What's in here

### Models (`Models/`)

The data structures that describe an ME221 calibration:

- CalibrationData: the root container. Holds device info, data links, tables, and drivers.
- TableDefinition: a tuning table. Either 1D (1x16) or 2D (16x16), with input/output link IDs, axis data, and default values.
- DataLinkDefinition: a sensor or data channel. ID, name, category, unit, optional text-to-value mappings.
- DriverDefinition: ECU driver with configuration parameters (min/max ranges, combo options).
- GaugeConfigEntry: dashboard gauge layout. Shape category, arc/bar/text/digital settings, color stops, needle curve, free-form positioning.
- SensorCustomization: user-customized name, unit, min/max for a sensor.
- TextValueMapping: maps a numeric value to display text (e.g., "Off" = 0, "On" = 1).
- ColorStop, NeedleCurvePoint, ArcPosition, DigitalStyle: gauge visual config types.

### Parsers (`Infrastructure/`)

- MefwReader: reads `.mefw` firmware files. Validates the "MEFW" magic bytes, extracts embedded DEF XML from an offset table.
- DefXmlParser: parses the DEF XML into `CalibrationData`. Extracts metadata, data links (with units and text mappings), tables (with axis/output data), and drivers (with config parameters and combo options).
- TableSerializer: binary serialization for tuning table data. Handles the wire format: type byte, enabled flag, dimensions, input/output floats in little-endian.

### JSON persistence

`JsonContext.cs` contains source-generated `JsonSerializerContext` for all calibration model types. This enables AOT-friendly JSON serialization without reflection.

## Usage

Reading a firmware file:

```csharp
var mefw = MefwReader.Read(firmwarePath);
var calibration = DefXmlParser.Parse(mefw.DefXml);
// calibration.Tables contains all tuning tables
// calibration.DataLinks contains all sensor definitions
```

Serializing table data:

```csharp
// Write table values to bytes for sending to the ECU
var bytes = TableSerializer.Serialize(tableDefinition, enabled: true);

// Read table values from ECU response
var table = TableSerializer.Deserialize(responseBytes);
```

## Building

Targets `net11.0`. Only dependency is Serilog.

```powershell
dotnet build src\ME221.Data\ME221.Data.csproj
```

## Notes

- The `.mefw` file format is specific to ME221 firmware definitions. It's basically a container for DEF XML with some header metadata.
- Table dimensions are fixed at 1x16, 1x32, or 16x16. The ME221 ECU doesn't support other sizes.
- All numeric values use IEEE 754 float32 (4 bytes) in the wire format.
