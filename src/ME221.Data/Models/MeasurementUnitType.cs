namespace ME221.Data.Models;

[Flags]
public enum MeasurementUnitType
{
    Unknown = 0,
    Raw = 1,
    Volt = 2,
    Ohm = 4,
    KPa = 8,
    PSI = 16,
    Celsius = 32,
    Fahrenheit = 64,
    Percent = 128,
    Rpm = 256,
    Deg = 512,
    Ms = 1024,
    Bar = 2048,
}
