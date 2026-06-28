namespace ME221.Comms.Messages;

/// <summary>
/// Data type encoding used in V2 reporting payloads.
/// Each type determines how many bytes to read for a single entity value.
/// </summary>
public enum ReportingType : byte
{
    /// <summary>4-byte single-precision floating point.</summary>
    Float4B = 0,

    /// <summary>2-byte signed integer.</summary>
    Int2B = 1,

    /// <summary>2-byte unsigned integer.</summary>
    Uint2B = 2,

    /// <summary>1-byte signed integer.</summary>
    Int1B = 3,

    /// <summary>1-byte unsigned integer.</summary>
    Uint1B = 4,

    /// <summary>1-byte boolean.</summary>
    Bool1B = 5,
}
