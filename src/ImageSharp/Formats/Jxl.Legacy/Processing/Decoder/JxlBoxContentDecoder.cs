// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.IO.Compression;
using SixLabors.ImageSharp.Formats.Jxl.IO;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Allows decoding and decompressing box data in JPEG XL
/// container format.
/// </summary>
internal sealed class JxlBoxContentDecoder
{
    /// <summary>
    /// Specifies how many bytes to read to fetch box data. This is ignored
    /// if the box extends till EOF.
    /// </summary>
    private ulong boxSize;

    /// <summary>
    /// When true the box size is ignored and is unbounded - that is, keeps going
    /// till the end of the file or stream.
    /// </summary>
    private bool boxExtendsTillEnd;

    /// <summary>
    /// This contains flags that determine whether the box is Brotli-compressed or
    /// not.
    /// </summary>
    private JxlBoxCodingMode codingMode;

    /// <summary>
    /// Prepares parsing the box.
    /// </summary>
    /// <param name="codingMode">Specifies box compression.</param>
    /// <param name="isUnbounded">Specifies whether or not the box size keeps going till the end of stream.</param>
    /// <param name="size">Specifies the fixed size of the box when it is not unbounded.</param>
    public void Initialize(JxlBoxCodingMode codingMode, bool isUnbounded, ulong size)
    {
        this.boxSize = size;
        this.codingMode = codingMode;
        this.boxExtendsTillEnd = isUnbounded;
    }

    /// <summary>
    /// Prepares parsing the box.
    /// </summary>
    /// <param name="isBrotliCompressed">True if the box is compressed with Brotli. If uncompressed - false.</param>
    /// <param name="isUnbounded">Specifies whether or not the box size keeps going till the end of stream.</param>
    /// <param name="size">Specifies the fixed size of the box when it is not unbounded.</param>
    public void Initialize(bool isBrotliCompressed, bool isUnbounded, ulong size)
        => this.Initialize(
            isBrotliCompressed ? JxlBoxCodingMode.Brotli : JxlBoxCodingMode.Uncompressed,
            isUnbounded,
            size);

    public void Process(Stream stream, Stream writer)
    {
        byte[] cache = ArrayPool<byte>.Shared.Rent(16384);

        if (this.codingMode == JxlBoxCodingMode.Brotli)
        {
            using BrotliStream brotli = new(stream, CompressionMode.Decompress, leaveOpen: true);

            int bytesRead;
            while ((bytesRead = brotli.Read(cache, 0, cache.Length)) > 0)
            {
                writer.Write(cache.AsSpan(0, bytesRead));
            }
        }
        else
        {
            if (this.boxExtendsTillEnd)
            {
                int bytesRead;
                while ((bytesRead = stream.Read(cache, 0, cache.Length)) > 0)
                {
                    writer.Write(cache.AsSpan(0, bytesRead));
                }
            }
            else
            {
                ulong bytesLeft = this.boxSize;
                while (bytesLeft > 0)
                {
                    int toRead = (int)Math.Min((ulong)cache.Length, bytesLeft);
                    int bytesRead = stream.Read(cache, 0, toRead);

                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException("Unexpected EOF while reading box content");
                    }

                    writer.Write(cache.AsSpan(0, bytesRead));
                    bytesLeft -= (ulong)bytesRead;
                }
            }
        }

        ArrayPool<byte>.Shared.Return(cache);
    }
}
