// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Tests.ColorProfiles.Icc;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.IO;

public class ImageBundleTests
{
    /// <summary>
    /// Checks that color transfers and removal retain the bundle's wrapper while moving or releasing sample allocations.
    /// </summary>
    [Fact]
    public void ColorTransfer_KeepsWrapperAcrossReplacementAndRemoval()
    {
        using JxlImageBundle bundle = new(new JxlImageMetadata());
        JxlImage3<float> retained = bundle.Color;
        JxlColorEncoding encoding = new();
        using JxlImage3<float> first = new(TestEnvironment.Configuration, 1, 1);
        JxlImageOperations.FillImage(7F, first);

        Assert.True(bundle.SetFromImage(first, encoding));
        Assert.Same(retained, bundle.Color);
        Assert.Equal(0, first.XSize);
        Assert.Equal(7F, bundle.Color.PlaneRow(0, 0)[0]);

        // The input wrappers stay caller-owned. Replacing the bundle moves the next allocation into the same retained wrapper.
        using JxlImage3<float> replacement = new(TestEnvironment.Configuration, 2, 1);
        JxlImageOperations.FillImage(5F, replacement);
        Assert.True(bundle.SetFromImage(replacement, encoding));
        Assert.Same(retained, bundle.Color);
        Assert.Equal(0, replacement.XSize);
        Assert.Equal(2, bundle.Color.XSize);
        Assert.Equal(5F, bundle.Color.PlaneRow(0, 0)[0]);

        bundle.RemoveColor();

        Assert.Same(retained, bundle.Color);
        Assert.False(bundle.HasColor);
        Assert.Same(encoding, bundle.CurrentColorEncoding);

        for (int c = 0; c < 3; c++)
        {
            Assert.True(bundle.Color.PlaneRowMemory(c, 0).IsEmpty);
        }
    }

    /// <summary>
    /// Checks that rejecting a gray/RGB model change leaves both the bundle and caller-owned samples intact.
    /// </summary>
    [Fact]
    public void ColorTransfer_IncompatibleEncodingLeavesBothOwnersUnchanged()
    {
        using JxlImageBundle bundle = new(new JxlImageMetadata());
        JxlColorEncoding encoding = new();
        using JxlImage3<float> first = new(TestEnvironment.Configuration, 1, 1);
        JxlImageOperations.FillImage(3F, first);
        Assert.True(bundle.SetFromImage(first, encoding));

        using JxlImage3<float> rejected = new(TestEnvironment.Configuration, 2, 1);
        JxlImageOperations.FillImage(9F, rejected);
        JxlColorEncoding gray = new() { ColorSpace = JxlColorSpace.Gray };

        Assert.False(bundle.SetFromImage(rejected, gray));
        Assert.Equal(1, bundle.Color.XSize);
        Assert.Equal(2, rejected.XSize);
        Assert.Same(encoding, bundle.CurrentColorEncoding);

        for (int c = 0; c < 3; c++)
        {
            Assert.Equal(3F, bundle.Color.PlaneRow(c, 0)[0]);
            Assert.Equal(9F, rejected.PlaneRow(c, 0)[0]);
        }
    }

    /// <summary>
    /// Checks that released plane storage cannot be reused through its previous dimensions.
    /// </summary>
    [Fact]
    public void DisposedPlane_CannotReuseReleasedCapacity()
    {
        using JxlPlane<float> plane = new(TestEnvironment.Configuration, 16, 2);

        // Frame initialization uses ShrinkTo before allocating replacement storage.
        // After disposal, that check must reject the dimensions of the released allocation.
        plane.Dispose();

        Assert.False(plane.ShrinkTo(16, 2));
        Assert.Equal(0, plane.XSize);
        Assert.Equal(0, plane.YSize);
        Assert.Equal(0, plane.PixelsPerRow);
        Assert.True(plane.GetRowMemory(0).IsEmpty);
    }

    /// <summary>
    /// Checks that a sample conversion cannot replace the original ICC bytes used for JPEG reconstruction.
    /// </summary>
    [Fact]
    public void JpegReconstruction_UsesOriginalProfileAfterEncodingChange()
    {
        JxlCodecIO io = new();
        IccProfile original = TestIccProfiles.GetProfile(TestIccProfiles.StandardRgbV2);
        io.CodecMetadata.ImageMetadata.ColorEncoding.IccProfile = original;
        ReadOnlyMemory<byte> expected = original.GetData();

        // A JPEG APP2 marker stores a two-byte length, the ICC tag, a sequence number, and a marker count.
        // Reconstruction has already reserved this payload before it asks the bundle for the original profile.
        byte[] marker = new byte[17 + expected.Length];
        marker[0] = 0xE2;
        BinaryPrimitives.WriteUInt16BigEndian(marker.AsSpan(1), checked((ushort)(marker.Length - 1)));
        JpegDataConstants.IccProfileTag.CopyTo(marker.AsSpan(3));
        marker[15] = 1;
        marker[16] = 1;

        JxlToJpegDecoder decoder = new() { Output = Stream.Null };
        JpegData jpeg = decoder.GetJpegData();
        jpeg.AppMarkerTypes.Add(JpegAppMarkerType.Icc);
        jpeg.AppData.Add([.. marker]);

        // The converted samples have another profile. Reconstruction must still use the codestream's original profile.
        io.Main.OverrideProfile(new JxlColorEncoding { IccProfile = TestIccProfiles.GetProfile(TestIccProfiles.RommRgb) });
        decoder.SetImageBundleJpegData(io.Main);

        Assert.Same(jpeg, io.Main.JpegData);
        Assert.True(expected.Span.SequenceEqual(CollectionsMarshal.AsSpan(jpeg.AppData[0])[17..]));
    }

    /// <summary>
    /// Checks that an absent original profile is accepted only when no ICC marker requires a payload.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JpegReconstruction_RequiresProfileForIccMarkers(bool hasIccMarker)
    {
        JxlCodecIO io = new();
        JxlToJpegDecoder decoder = new() { Output = Stream.Null };
        JpegData jpeg = decoder.GetJpegData();

        if (hasIccMarker)
        {
            // APP2 declares length 17, the ICC_PROFILE tag, sequence/count 1, and one payload byte.
            // The codestream has no profile to supply that byte.
            jpeg.AppMarkerTypes.Add(JpegAppMarkerType.Icc);
            jpeg.AppData.Add([0xE2, 0, 17, 73, 67, 67, 95, 80, 82, 79, 70, 73, 76, 69, 0, 1, 1, 0]);

            Assert.Throws<InvalidImageContentException>(() => decoder.SetImageBundleJpegData(io.Main));
            Assert.Null(io.Main.JpegData);
        }
        else
        {
            decoder.SetImageBundleJpegData(io.Main);

            Assert.Same(jpeg, io.Main.JpegData);
        }
    }

    [Fact]
    public void ExtraChannel()
    {
        using MemoryStream ms = new();
        JxlBitWriter bw = new(ms);

        Assert.True(bw.WithMaxBits(99, () =>
        {
            JxlImageMetadata md = new()
            {
                ExtraChannels =
                [
                    new JxlExtraChannelInfo()
                    {
                        Type = JxlExtraChannel.Black,
                        Name = "testK"
                    }
                ],
                ExtraChannelCount = 1
            };

            if (!JxlBundle.Write(md, bw, JxlLayerType.Header, new JxlAuxiliaryOutput()))
            {
                return false;
            }

            bw.ZeroPadToByte();
            return true;
        }));

        ms.Position = 0;
        JxlBitReader reader = new(ms);
        JxlImageMetadata result = new();

        Assert.True(JxlBundle.Read(reader, result));
        Assert.Equal("testK", result.FindExtraChannel(JxlExtraChannel.Black)?.Name);
    }
}
