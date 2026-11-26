// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <content>
/// Contains the run operators of the RLE compressions. The RLE decode loop takes an operator as a type argument.
/// </content>
internal ref partial struct BmpRleWriter<TPixel>
{
    /// <summary>
    /// Writes the runs of one RLE compression.
    /// </summary>
    /// <remarks>
    /// RLE4, RLE8 and RLE24 use the same commands. They differ only in how a run stores its pixels. The decode loop has a type argument with a struct
    /// constraint for the operator. Thus the JIT compiles one loop for each operator, and the calls to the operator are direct calls that it can inline.
    /// </remarks>
    public interface IRunOperator
    {
        /// <summary>
        /// Writes an encoded run: <paramref name="count"/> pixels of the color that <paramref name="value"/> starts.
        /// </summary>
        /// <param name="reader">The reader, after the two command bytes.</param>
        /// <param name="writer">The writer.</param>
        /// <param name="count">The pixel count, 1 to 255.</param>
        /// <param name="value">The second command byte.</param>
        /// <returns>
        /// <see langword="true"/> if the data has the color of the run. <see langword="false"/> if the data ends before the color.
        /// </returns>
        bool WriteEncoded(ref BmpRleReader reader, ref BmpRleWriter<TPixel> writer, int count, int value);

        /// <summary>
        /// Gets the byte count of an absolute run, without the padding byte.
        /// </summary>
        /// <param name="count">The pixel count, 3 to 255.</param>
        /// <returns>The byte count.</returns>
        int GetAbsoluteByteCount(int count);

        /// <summary>
        /// Writes an absolute run.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="data">The run data. It has <see cref="GetAbsoluteByteCount(int)"/> bytes.</param>
        /// <param name="count">The pixel count, 3 to 255.</param>
        void WriteAbsolute(ref BmpRleWriter<TPixel> writer, scoped ReadOnlySpan<byte> data, int count);
    }

    /// <summary>
    /// Writes RLE8 runs. Each pixel is one 8-bit palette index.
    /// </summary>
    public readonly ref struct Rle8Operator : IRunOperator
    {
        private readonly ReadOnlySpan<TPixel> palette;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rle8Operator"/> struct.
        /// </summary>
        /// <param name="palette">The palette, converted to <typeparamref name="TPixel"/>, with 256 entries.</param>
        public Rle8Operator(ReadOnlySpan<TPixel> palette) => this.palette = palette;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteEncoded(ref BmpRleReader reader, ref BmpRleWriter<TPixel> writer, int count, int value)
        {
            // An encoded RLE8 run has one color, the palette entry of 'value'.
            writer.Fill(this.palette[value], count);
            return true;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAbsoluteByteCount(int count) => count;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAbsolute(ref BmpRleWriter<TPixel> writer, scoped ReadOnlySpan<byte> data, int count)
            => writer.Write(data, this.palette);
    }

    /// <summary>
    /// Writes RLE4 runs. Each pixel is one 4-bit palette index. A byte holds two indices, high nibble first.
    /// </summary>
    public readonly ref struct Rle4Operator : IRunOperator
    {
        private readonly ReadOnlySpan<TPixel> palette;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rle4Operator"/> struct.
        /// </summary>
        /// <param name="palette">The palette, converted to <typeparamref name="TPixel"/>, with 256 entries.</param>
        public Rle4Operator(ReadOnlySpan<TPixel> palette) => this.palette = palette;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteEncoded(ref BmpRleReader reader, ref BmpRleWriter<TPixel> writer, int count, int value)
        {
            // The pixels of an encoded RLE4 run alternate. Even pixels take the high nibble (value >> 4), odd pixels take the low nibble (value & 0xF).
            writer.FillPair(this.palette[value >> 4], this.palette[value & 0xF], count);
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// A byte stores two indices, so the run stores (count + 1) / 2 bytes. When the count is odd, the last byte has one index, in its high nibble.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAbsoluteByteCount(int count) => (count + 1) >> 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAbsolute(ref BmpRleWriter<TPixel> writer, scoped ReadOnlySpan<byte> data, int count)
            => writer.Write4(data, count, this.palette);
    }

    /// <summary>
    /// Writes RLE24 runs. Each pixel is a B, G, R color. An encoded run is [count, B, G, R].
    /// </summary>
    public readonly struct Rle24Operator : IRunOperator
    {
        private readonly Configuration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rle24Operator"/> struct.
        /// </summary>
        /// <param name="configuration">The configuration of the bulk pixel conversion.</param>
        public Rle24Operator(Configuration configuration) => this.configuration = configuration;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteEncoded(ref BmpRleReader reader, ref BmpRleWriter<TPixel> writer, int count, int value)
        {
            // 'value' is the blue byte. The green byte and the red byte follow.
            if (!reader.TryRead(2, out ReadOnlySpan<byte> greenRed))
            {
                return false;
            }

            writer.Fill(TPixel.FromBgr24(new Bgr24(greenRed[1], greenRed[0], (byte)value)), count);
            return true;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAbsoluteByteCount(int count) => count * 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAbsolute(ref BmpRleWriter<TPixel> writer, scoped ReadOnlySpan<byte> data, int count)
            => writer.WriteBgr24(this.configuration, data);
    }
}
