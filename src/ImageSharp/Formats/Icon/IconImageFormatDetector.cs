// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// Detects ico file headers.
/// </summary>
public class IconImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize { get; } = IconDir.Size + IconDirEntry.Size;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) switch
        {
            true => Ico.IcoFormat.Instance,
            false => Cur.CurFormat.Instance,
            null => default
        };

        return format is not null;
    }

    private bool? IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        // There are no magic bytes in the first few bytes of a tga file,
        // so we try to figure out if its a valid tga by checking for valid tga header bytes.
        if (header.Length < this.HeaderSize)
        {
            return null;
        }

        IconDir dir = IconDir.Parse(header);
        if (dir is not { Reserved: 0 } // Should be 0.
            or not { Type: IconFileType.ICO or IconFileType.CUR } // Unknown Type.
            or { Count: 0 })
        {
            return null;
        }

        IconDirEntry entry = IconDirEntry.Parse(header[IconDir.Size..]);
        if (entry is not { Reserved: 0 } // Should be 0.
            or { BytesInRes: 0 } // Should not be 0.
            || entry.ImageOffset < IconDir.Size + (dir.Count * IconDirEntry.Size))
        {
            return null;
        }

        if (dir.Type is IconFileType.ICO)
        {
            if (entry is not { BitCount: 1 or 4 or 8 or 16 or 24 or 32 } or not { Planes: 0 or 1 })
            {
                return null;
            }

            return true;
        }
        else
        {
            return false;
        }
    }
}
