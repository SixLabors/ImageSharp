// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.IPTC;

/// <summary>
/// Enum for the different record types of a IPTC value.
/// </summary>
internal enum IptcRecordNumber : byte
{
    /// <summary>
    /// An Envelope Record.
    /// </summary>
    Envelope = 0x01,

    /// <summary>
    /// An Application Record.
    /// </summary>
    Application = 0x02
}
