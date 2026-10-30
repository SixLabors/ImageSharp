// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Stores the begun and ended extension flags for nested bundles in two UInt64 masks.
/// </summary>
internal struct JxlExtensionStates
{
    private ulong begun;
    private ulong ended;

    /// <summary>
    /// Gets whether the current bundle began its extension fields.
    /// </summary>
    public readonly bool IsBegun => (this.begun & 1) != 0;

    /// <summary>
    /// Gets whether the current bundle ended its extension fields.
    /// </summary>
    public readonly bool IsEnded => (this.ended & 1) != 0;

    /// <summary>
    /// Enters a nested bundle with no extension flags set.
    /// </summary>
    public void Push()
    {
        // The least significant bit represents the current bundle. Preserve the parent bits above it.
        this.begun <<= 1;
        this.ended <<= 1;
    }

    /// <summary>
    /// Restores the parent bundle's extension flags.
    /// </summary>
    public void Pop()
    {
        this.begun >>= 1;
        this.ended >>= 1;
    }

    /// <summary>
    /// Marks the current bundle's extension fields as begun.
    /// </summary>
    public void Begin()
    {
        DebugGuard.IsFalse(this.IsBegun, nameof(this.IsBegun), "This must be false.");
        DebugGuard.IsFalse(this.IsEnded, nameof(this.IsEnded), "This must be false.");

        this.begun++;
    }

    /// <summary>
    /// Marks the current bundle's extension fields as ended.
    /// </summary>
    public void End()
    {
        DebugGuard.IsTrue(this.IsBegun, nameof(this.IsBegun), "This must be true.");
        DebugGuard.IsFalse(this.IsEnded, nameof(this.IsEnded), "This must be false.");

        this.ended++;
    }
}
