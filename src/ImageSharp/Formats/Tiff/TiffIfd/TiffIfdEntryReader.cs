// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal static class TiffIfdEntryReader
    {
        public static object ReadValue(this TiffIfdEntry entry, TiffStream stream)
        {
            DebugGuard.MustBeNull(entry.Value, "Value");

            switch (entry.Type)
            {
                case TiffTagType.Byte:
                case TiffTagType.Undefined:
                {
                    return stream.ReadBytes(entry.Count);
                }

                case TiffTagType.SByte:
                {
                    sbyte[] res = new sbyte[entry.Count];
                    byte[] buf = stream.ReadBytes(entry.Count);
                    Array.Copy(buf, res, buf.Length);
                    return res;
                }

                case TiffTagType.Short:
                {
                    uint[] buf = new uint[entry.Count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = stream.ReadUInt16();
                    }

                    return buf;
                }

                case TiffTagType.SShort:
                {
                    short[] buf = new short[entry.Count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = stream.ReadInt16();
                    }

                    return buf;
                }

                case TiffTagType.Long:
                {
                    uint[] buf = new uint[entry.Count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = stream.ReadUInt32();
                    }

                    return buf;
                }

                case TiffTagType.SLong:
                {
                    int[] buf = new int[entry.Count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = stream.ReadInt32();
                    }

                    return buf;
                }

                case TiffTagType.Ascii:
                {
                    byte[] buf = stream.ReadBytes(entry.Count);

                    if (buf[buf.Length - 1] != 0)
                    {
                        throw new ImageFormatException("The retrieved string is not null terminated.");
                    }

                    return Encoding.UTF8.GetString(buf, 0, buf.Length - 1);
                }

                case TiffTagType.Float:
                {
                    float[] buf = new float[entry.Count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = stream.ReadSingle();
                    }

                    return buf;
                }

                case TiffTagType.Double:
                {
                    double[] buf = new double[entry.Count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = stream.ReadDouble();
                    }

                    return buf;
                }

                case TiffTagType.Rational:
                {
                    var buf = new Rational[entry.Count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        uint numerator = stream.ReadUInt32();
                        uint denominator = stream.ReadUInt32();
                        buf[i] = new Rational(numerator, denominator);
                    }

                    return buf;
                }

                case TiffTagType.SRational:
                {
                    var buf = new SignedRational[entry.Count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        int numerator = stream.ReadInt32();
                        int denominator = stream.ReadInt32();
                        buf[i] = new SignedRational(numerator, denominator);
                    }

                    return buf;
                }

                case TiffTagType.Ifd:
                {
                    return stream.ReadUInt32();
                }

                default:
                    ////throw new ImageFormatException($"A value of type '{entry.Type}' cannot be read.");
                    return null;
            }
        }
    }
}
