// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Provides the primitive operations used to read, write, and initialize JPEG XL header fields.
/// </summary>
internal abstract class JxlVisitor
{
    /// <summary>
    /// Extension flags for active nested bundles. Each level occupies one bit in the state masks.
    /// </summary>
    private JxlExtensionStates extensionStates;

    /// <summary>
    /// Number of active bundles in the extension-state masks.
    /// </summary>
    private int depth;

    /// <summary>
    /// Gets whether this visitor reads fields from the input bitstream.
    /// </summary>
    public virtual bool IsReading => false;

    /// <summary>
    /// Visits the fields in one bundle.
    /// </summary>
    /// <param name="fields">The fields to process.</param>
    /// <returns>Whether the field operation succeeded.</returns>
    public bool Visit(IJxlFields fields)
    {
        if (this.depth >= JxlBundle.MaxExtensions)
        {
            return false;
        }

        this.depth++;
        this.extensionStates.Push();

        try
        {
            bool visited = fields.Visit(this);

            if (visited && this.extensionStates.IsBegun && !this.extensionStates.IsEnded)
            {
                throw new InvalidOperationException("A completed bundle did not end its extension fields.");
            }

            return visited;
        }
        finally
        {
            // A truncated primitive can throw. Restore the parent scope even when this bundle cannot finish.
            this.extensionStates.Pop();
            this.depth--;
        }
    }

    /// <summary>
    /// Visits a one-bit Boolean field.
    /// </summary>
    /// <param name="defaultValue">The default field value.</param>
    /// <param name="value">The field to process.</param>
    /// <returns>Whether the field operation succeeded.</returns>
    public virtual bool Boolean(bool defaultValue, ref bool value)
    {
        uint bits = value ? 1U : 0U;

        if (!this.Bits(1, defaultValue ? 1U : 0U, ref bits))
        {
            return false;
        }

        value = bits != 0;
        return true;
    }

    /// <summary>
    /// Visits an unsigned integer using the supplied four-alternative encoding.
    /// </summary>
    /// <param name="enc">The wire alternatives.</param>
    /// <param name="defaultValue">The default field value.</param>
    /// <param name="value">The field to process.</param>
    /// <returns>Whether the field operation succeeded.</returns>
    public abstract bool U32(JxlU32Enc enc, uint defaultValue, ref uint value);

    /// <summary>
    /// Visits an unsigned integer using four explicit wire alternatives.
    /// </summary>
    /// <param name="d0">The first wire alternative.</param>
    /// <param name="d1">The second wire alternative.</param>
    /// <param name="d2">The third wire alternative.</param>
    /// <param name="d3">The fourth wire alternative.</param>
    /// <param name="defaultValue">The default field value.</param>
    /// <param name="value">The field to process.</param>
    /// <returns>Whether the field operation succeeded.</returns>
    public bool U32(
        JxlU32Distribution d0,
        JxlU32Distribution d1,
        JxlU32Distribution d2,
        JxlU32Distribution d3,
        uint defaultValue,
        ref uint value)
        => this.U32(new JxlU32Enc(d0, d1, d2, d3), defaultValue, ref value);

    /// <summary>
    /// Visits an enumeration using the JPEG XL enumeration distribution.
    /// </summary>
    /// <typeparam name="T">A byte-sized or 32-bit enumeration used by the codec's headers.</typeparam>
    /// <param name="defaultValue">The enumeration's default field value.</param>
    /// <param name="value">The field to read, write, or compare.</param>
    /// <returns>Whether the field operation succeeded and its value is defined.</returns>
    public virtual bool Enum<T>(T defaultValue, ref T value)
        where T : unmanaged, System.Enum
    {
        // Header enums use byte storage. The shared ICC intent uses UInt32 storage. Visit a UInt32 local
        // so the reader never writes four bytes through a reference to a one-byte enum field.
        uint encoded = Unsafe.SizeOf<T>() == sizeof(byte) ? Unsafe.BitCast<T, byte>(value) : Unsafe.BitCast<T, uint>(value);
        uint encodedDefault = Unsafe.SizeOf<T>() == sizeof(byte) ? Unsafe.BitCast<T, byte>(defaultValue) : Unsafe.BitCast<T, uint>(defaultValue);

        if (!this.U32(
            JxlFieldExpressions.Value(0),
            JxlFieldExpressions.Value(1),
            JxlFieldExpressions.BitsOffset(4, 2),
            JxlFieldExpressions.BitsOffset(6, 18),
            encodedDefault,
            ref encoded))
        {
            return false;
        }

        // The four wire alternatives cover 0 through 81, so every decoded value fits in byte storage.
        // Validate before publishing it. An invalid wire value must not overwrite the previous field value.
        T decoded = Unsafe.SizeOf<T>() == sizeof(byte) ? Unsafe.BitCast<byte, T>((byte)encoded) : Unsafe.BitCast<uint, T>(encoded);

        if (!System.Enum.IsDefined(decoded))
        {
            return false;
        }

        value = decoded;
        return true;
    }

    /// <summary>
    /// Visits a fixed-width unsigned integer field.
    /// </summary>
    /// <param name="bits">The number of encoded bits.</param>
    /// <param name="defaultValue">The default field value.</param>
    /// <param name="value">The field to process.</param>
    /// <returns>Whether the field operation succeeded.</returns>
    public abstract bool Bits(int bits, uint defaultValue, ref uint value);

    /// <summary>
    /// Visits a variable-length unsigned 64-bit integer field.
    /// </summary>
    /// <param name="defaultValue">The default field value.</param>
    /// <param name="value">The field to process.</param>
    /// <returns>Whether the field operation succeeded.</returns>
    public abstract bool U64(ulong defaultValue, ref ulong value);

    /// <summary>
    /// Visits a half-precision field represented as a float in memory.
    /// </summary>
    /// <param name="defaultValue">The default field value.</param>
    /// <param name="value">The field to process.</param>
    /// <returns>Whether the field operation succeeded.</returns>
    public abstract bool F16(float defaultValue, ref float value);

    /// <summary>
    /// Visits the UTF-8 name shared by frame and extra-channel headers.
    /// </summary>
    /// <param name="value">The name to read, write, compare or reset. Null and empty names encode identically.</param>
    /// <returns>Whether the byte length and every name byte can be processed.</returns>
    public bool Name(ref string? value)
    {
        // Name lengths count encoded bytes, not UTF-16 characters. The longest wire alternative stores 48 plus ten bits.
        const int MaximumNameBytes = 1071;
        uint byteLength = this.IsReading ? 0U : (uint)Encoding.UTF8.GetByteCount(value.AsSpan());

        if (!this.U32(
            JxlFieldExpressions.Value(0),
            JxlFieldExpressions.Bits(4),
            JxlFieldExpressions.BitsOffset(5, 16),
            JxlFieldExpressions.BitsOffset(10, 48),
            0,
            ref byteLength)
            || byteLength > MaximumNameBytes)
        {
            return false;
        }

        // Reset visitors also set the byte length to zero. Do not encode an earlier name into their empty destination.
        if (byteLength == 0)
        {
            value = string.Empty;
            return true;
        }

        Span<byte> bytes = stackalloc byte[(int)byteLength];

        if (!this.IsReading)
        {
            Encoding.UTF8.GetBytes(value.AsSpan(), bytes);
        }

        for (int i = 0; i < bytes.Length; i++)
        {
            uint encoded = this.IsReading ? 0U : bytes[i];

            if (!this.Bits(8, 0, ref encoded))
            {
                return false;
            }

            bytes[i] = (byte)encoded;
        }

        if (this.IsReading)
        {
            value = Encoding.UTF8.GetString(bytes);
        }

        return true;
    }

    /// <summary>
    /// Selects whether a conditional group of fields is visited.
    /// </summary>
    /// <param name="condition">Whether the group is present for the current field values.</param>
    /// <returns>Whether to visit the group.</returns>
    public virtual bool Conditional(bool condition) => condition;

    /// <summary>
    /// Visits the flag that replaces all serialized fields with their defaults.
    /// </summary>
    /// <param name="fields">The bundle whose default values the visitor can compare or restore.</param>
    /// <param name="allDefault">The encoded shortcut flag.</param>
    /// <returns>Whether the shortcut flag is set.</returns>
    public virtual bool AllDefault(IJxlFields fields, ref bool allDefault)
    {
        if (!this.Boolean(true, ref allDefault))
        {
            return false;
        }

        return allDefault;
    }

    /// <summary>
    /// Restores a bundle's serialized defaults when required by the visitor's operation.
    /// </summary>
    /// <param name="fields">The bundle to reset.</param>
    public virtual void SetDefault(IJxlFields fields)
    {
        // Size and default comparisons must not reset the fields. The read visitor implements the encoded shortcut.
    }

    /// <summary>
    /// Visits a nested bundle in its own extension scope.
    /// </summary>
    /// <param name="fields">The nested fields.</param>
    /// <returns>Whether the nested operation succeeded.</returns>
    public virtual bool VisitNested(IJxlFields fields) => this.Visit(fields);

    /// <summary>
    /// Starts the extension fields selected by a 64-bit flag set.
    /// </summary>
    /// <param name="extensions">The extension flags to process.</param>
    /// <returns>Whether the field operation succeeded.</returns>
    public virtual bool BeginExtensions(ref ulong extensions)
    {
        if (!this.U64(0, ref extensions))
        {
            return false;
        }

        this.extensionStates.Begin();
        return true;
    }

    /// <summary>
    /// Completes the current bundle's extension fields.
    /// </summary>
    /// <returns>Whether the extension operation succeeded.</returns>
    public virtual bool EndExtensions()
    {
        this.extensionStates.End();
        return true;
    }
}
