// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Noise;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Encoder.Noise;

/// <summary>
/// Tests for the simulation of photon noise in the JPEG XL encoder routines.
/// </summary>
public class PhotonNoiseTests
{
    [Fact]
    public void TestPhotonNoiseEncoder()
    {
        ApproximateFloatComparer comparer = new(1e-6f);

        Assert.Equal(
            JxlPhotonNoise.SimulatePhotonNoise(xSize: 6000, ySize: 4000, iso: 100).Lookup,
            [0.00259652f, 0.0139648f, 0.00681551f, 0.00632582f,
             0.00694917f, 0.00803922f, 0.00934574f, 0.0107607f],
            comparer);

        Assert.Equal(
            JxlPhotonNoise.SimulatePhotonNoise(xSize: 6000, ySize: 4000, iso: 800).Lookup,
            [0.02077220f, 0.0420923f, 0.01820690f, 0.01439020f,
             0.01293670f, 0.01254030f, 0.01277390f, 0.0134161f],
            comparer);

        Assert.Equal(
            JxlPhotonNoise.SimulatePhotonNoise(xSize: 6000, ySize: 4000, iso: 6400).Lookup,
            [0.1661770f, 0.1691120f, 0.05309080f, 0.03963960f,
             0.03357410f, 0.03001650f, 0.02776740f, 0.0263478f],
            comparer);

        Assert.Equal(
            JxlPhotonNoise.SimulatePhotonNoise(xSize: 4000, ySize: 3000, iso: 6400).Lookup,
            [0.0830886f, 0.1008720f, 0.0367748f, 0.0280305f, 0.0240236f,
             0.0218040f, 0.0205771f, 0.0200058f],
            comparer);
    }
}
