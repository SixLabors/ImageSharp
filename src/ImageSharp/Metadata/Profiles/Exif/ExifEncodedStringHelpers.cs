// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using static SixLabors.ImageSharp.Metadata.Profiles.Exif.EncodedString;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

internal static class ExifEncodedStringHelpers
{
    public const int CharacterCodeBytesLength = 8;

    private const ulong AsciiCode = 0x_00_00_00_49_49_43_53_41;
    private const ulong JISCode = 0x_00_00_00_00_00_53_49_4A;
    private const ulong UnicodeCode = 0x_45_44_4F_43_49_4E_55;
    private const ulong UndefinedCode = 0x_00_00_00_00_00_00_00_00;

    private static ReadOnlySpan<byte> AsciiCodeBytes => [0x41, 0x53, 0x43, 0x49, 0x49, 0, 0, 0];

    private static ReadOnlySpan<byte> JISCodeBytes => [0x4A, 0x49, 0x53, 0, 0, 0, 0, 0];

    private static ReadOnlySpan<byte> UnicodeCodeBytes => [0x55, 0x4E, 0x49, 0x43, 0x4F, 0x44, 0x45, 0];

    private static ReadOnlySpan<byte> UndefinedCodeBytes => [0, 0, 0, 0, 0, 0, 0, 0];

    // 20932 EUC-JP Japanese (JIS 0208-1990 and 0212-1990)
    // https://docs.microsoft.com/en-us/dotnet/api/system.text.encoding?view=net-6.0
    private static Encoding JIS0208Encoding
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(20932);
        }
    }

    public static bool IsEncodedString(ExifTagValue tag) => tag switch
    {
        ExifTagValue.UserComment or ExifTagValue.GPSProcessingMethod or ExifTagValue.GPSAreaInformation => true,
        _ => false
    };

    public static ReadOnlySpan<byte> GetCodeBytes(CharacterCode code) => code switch
    {
        CharacterCode.ASCII => AsciiCodeBytes,
        CharacterCode.JIS => JISCodeBytes,
        CharacterCode.Unicode => UnicodeCodeBytes,
        CharacterCode.Undefined => UndefinedCodeBytes,
        _ => UndefinedCodeBytes
    };

    public static Encoding GetEncoding(CharacterCode code, ByteOrder order) => code switch
    {
        CharacterCode.ASCII => Encoding.ASCII,
        CharacterCode.JIS => JIS0208Encoding,
        CharacterCode.Unicode => order is ByteOrder.BigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode,
        CharacterCode.Undefined => Encoding.UTF8,
        _ => Encoding.UTF8
    };

    public static bool TryParse(ReadOnlySpan<byte> buffer, ByteOrder order, out EncodedString encodedString)
    {
        if (TryDetect(buffer, out CharacterCode code))
        {
            ReadOnlySpan<byte> textBuffer = buffer[CharacterCodeBytesLength..];
            if (code == CharacterCode.Unicode && textBuffer.Length >= 2)
            {
                // Check BOM
                if (textBuffer[0] == 0xFF && textBuffer[1] == 0xFE)
                {
                    // Little-endian BOM
                    string text = Encoding.Unicode.GetString(textBuffer[2..]);
                    encodedString = new EncodedString(code, text);
                    return true;
                }
                else if (textBuffer[0] == 0xFE && textBuffer[1] == 0xFF)
                {
                    // Big-endian BOM
                    string text = Encoding.BigEndianUnicode.GetString(textBuffer[2..]);
                    encodedString = new EncodedString(code, text);
                    return true;
                }
                else
                {
                    // No BOM, use EXIF byte order
                    string text = GetEncoding(code, order).GetString(textBuffer);
                    encodedString = new EncodedString(code, text);
                    return true;
                }
            }
            else
            {
                string text = GetEncoding(code, order).GetString(textBuffer);
                encodedString = new EncodedString(code, text);
                return true;
            }
        }

        encodedString = default;
        return false;
    }

    public static uint GetDataLength(EncodedString encodedString, ByteOrder order) =>
        (uint)GetEncoding(encodedString.Code, order).GetByteCount(encodedString.Text) + CharacterCodeBytesLength;

    public static int Write(EncodedString encodedString, Span<byte> destination)
    {
        GetCodeBytes(encodedString.Code).CopyTo(destination);

        string text = encodedString.Text;
        int count = Write(GetEncoding(encodedString.Code, ByteOrder.LittleEndian), text, destination[CharacterCodeBytesLength..]);

        return CharacterCodeBytesLength + count;
    }

    public static unsafe int Write(Encoding encoding, string value, Span<byte> destination)
        => encoding.GetBytes(value.AsSpan(), destination);

    private static bool TryDetect(ReadOnlySpan<byte> buffer, out CharacterCode code)
    {
        if (buffer.Length >= CharacterCodeBytesLength)
        {
            switch (BinaryPrimitives.ReadUInt64LittleEndian(buffer))
            {
                case AsciiCode:
                    code = CharacterCode.ASCII;
                    return true;
                case JISCode:
                    code = CharacterCode.JIS;
                    return true;
                case UnicodeCode:
                    code = CharacterCode.Unicode;
                    return true;
                case UndefinedCode:
                    code = CharacterCode.Undefined;
                    return true;
                default:
                    code = default;
                    return false;
            }
        }

        code = default;
        return false;
    }
}
