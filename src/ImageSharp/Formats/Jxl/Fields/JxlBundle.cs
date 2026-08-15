// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// An <see cref="IJxlFields"/> helper.
/// </summary>
internal static class JxlBundle
{
    /// <summary>
    /// Initializes the specified JXL fields.
    /// </summary>
    /// <param name="fields">The JXL fields.</param>
    public static void Init(IJxlFields fields)
    {
        JxlInitVisitor initVisitor = new();

        if (!initVisitor.Visit(fields))
        {
            DebugGuard.IsTrue(false, "Init should never fail");
        }
    }

    /// <summary>
    /// Sets all JXL fields provided by the input value to their defaults.
    /// </summary>
    /// <param name="fields">The JXL fields.</param>
    public static void SetDefault(IJxlFields fields)
    {
        JxlSetDefaultVisitor visitor = new();

        if (!visitor.Visit(fields))
        {
            DebugGuard.IsTrue(false, "SetDefault should never fail");
        }
    }

    /// <summary>
    /// Returns a value indicating whether every value provided by this
    /// field is a default value. If at least one field isn't a default
    /// value, the method returns false.
    /// </summary>
    /// <param name="fields">The JXL fields.</param>
    /// <returns>A boolean indicating whether or not are all values initialized to their default values.</returns>
    public static bool AllDefault(IJxlFields fields)
    {
        JxlAllDefaultVisitor allDefaultVisitor = new();

        if (!allDefaultVisitor.Visit(fields))
        {
            DebugGuard.IsTrue(false, "AllDefault should never fail");
        }

        return allDefaultVisitor.IsAllDefault;
    }

    /// <summary>
    /// Reads the fields from a bit-reader.
    /// </summary>
    /// <param name="reader">The bit-reader.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>Status of the read operation.</returns>
    public static bool Read(JxlBitReader reader, IJxlFields fields)
    {
        JxlReadVisitor visitor = new(reader);
        if (!visitor.Visit(fields))
        {
            return false;
        }

        return visitor.OK;
    }

    /// <summary>
    /// Tries to read the fields from a bit-reader.
    /// </summary>
    /// <param name="reader">The bit-reader.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>Status of the read operation.</returns>
    public static bool CanRead(JxlBitReader reader, IJxlFields fields)
    {
        JxlReadVisitor visitor = new(reader);
        _ = visitor.Visit(fields);
        return visitor.OK;
    }
}
