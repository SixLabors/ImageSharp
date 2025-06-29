// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class Vp8ResidualTests
{
    private static void WriteVp8Residual(string filename, Vp8Residual residual)
    {
        using FileStream stream = File.Open(filename, FileMode.Create);
        using BinaryWriter writer = new(stream, Encoding.UTF8, false);

        writer.Write(residual.First);
        writer.Write(residual.Last);
        writer.Write(residual.CoeffType);

        for (int i = 0; i < residual.Coeffs.Length; i++)
        {
            writer.Write(residual.Coeffs[i]);
        }

        for (int i = 0; i < residual.Prob.Length; i++)
        {
            for (int j = 0; j < residual.Prob[i].Probabilities.Length; j++)
            {
                writer.Write(residual.Prob[i].Probabilities[j].Probabilities);
            }
        }

        for (int i = 0; i < residual.Costs.Length; i++)
        {
            Vp8Costs costs = residual.Costs[i];
            Vp8CostArray[] costsArray = costs.Costs;
            for (int j = 0; j < costsArray.Length; j++)
            {
                for (int k = 0; k < costsArray[j].Costs.Length; k++)
                {
                    writer.Write(costsArray[j].Costs[k]);
                }
            }
        }

        for (int i = 0; i < residual.Stats.Length; i++)
        {
            for (int j = 0; j < residual.Stats[i].Stats.Length; j++)
            {
                for (int k = 0; k < residual.Stats[i].Stats[j].Stats.Length; k++)
                {
                    writer.Write(residual.Stats[i].Stats[j].Stats[k]);
                }
            }
        }

        writer.Flush();
    }

    private static Vp8Residual ReadVp8Residual(string fileName)
    {
        using FileStream stream = File.Open(fileName, FileMode.Open);
        using BinaryReader reader = new(stream, Encoding.UTF8, false);

        Vp8Residual residual = new()
        {
            First = reader.ReadInt32(),
            Last = reader.ReadInt32(),
            CoeffType = reader.ReadInt32()
        };

        for (int i = 0; i < residual.Coeffs.Length; i++)
        {
            residual.Coeffs[i] = reader.ReadInt16();
        }

        Vp8BandProbas[] bandProbas = new Vp8BandProbas[8];
        for (int i = 0; i < bandProbas.Length; i++)
        {
            bandProbas[i] = new Vp8BandProbas();
            for (int j = 0; j < bandProbas[i].Probabilities.Length; j++)
            {
                for (int k = 0; k < 11; k++)
                {
                    bandProbas[i].Probabilities[j].Probabilities[k] = reader.ReadByte();
                }
            }
        }

        residual.Prob = bandProbas;

        residual.Costs = new Vp8Costs[16];
        for (int i = 0; i < residual.Costs.Length; i++)
        {
            residual.Costs[i] = new Vp8Costs();
            Vp8CostArray[] costsArray = residual.Costs[i].Costs;
            for (int j = 0; j < costsArray.Length; j++)
            {
                for (int k = 0; k < costsArray[j].Costs.Length; k++)
                {
                    costsArray[j].Costs[k] = reader.ReadUInt16();
                }
            }
        }

        residual.Stats = new Vp8Stats[8];
        for (int i = 0; i < residual.Stats.Length; i++)
        {
            residual.Stats[i] = new Vp8Stats();
            for (int j = 0; j < residual.Stats[i].Stats.Length; j++)
            {
                for (int k = 0; k < residual.Stats[i].Stats[j].Stats.Length; k++)
                {
                    residual.Stats[i].Stats[j].Stats[k] = reader.ReadUInt32();
                }
            }
        }

        return residual;
    }

    [Fact]
    public void Vp8Residual_Serialization_Works()
    {
        // arrange
        Vp8Residual expected = new();
        Vp8EncProba encProb = new();
        Random rand = new(439);
        CreateRandomProbas(encProb, rand);
        CreateCosts(encProb, rand);
        expected.Init(1, 0, encProb);
        for (int i = 0; i < expected.Coeffs.Length; i++)
        {
            expected.Coeffs[i] = (byte)rand.Next(255);
        }

        // act
        string fileName = "Vp8SerializationTest.bin";
        WriteVp8Residual(fileName, expected);
        Vp8Residual actual = ReadVp8Residual(fileName);
        File.Delete(fileName);

        // assert
        Assert.Equal(expected.CoeffType, actual.CoeffType);
        Assert.Equal(expected.Coeffs, actual.Coeffs);
        Assert.Equal(expected.Costs.Length, actual.Costs.Length);
        Assert.Equal(expected.First, actual.First);
        Assert.Equal(expected.Last, actual.Last);
        Assert.Equal(expected.Stats.Length, actual.Stats.Length);
        for (int i = 0; i < expected.Stats.Length; i++)
        {
            Vp8StatsArray[] expectedStats = expected.Stats[i].Stats;
            Vp8StatsArray[] actualStats = actual.Stats[i].Stats;
            Assert.Equal(expectedStats.Length, actualStats.Length);
            for (int j = 0; j < expectedStats.Length; j++)
            {
                Assert.Equal(expectedStats[j].Stats, actualStats[j].Stats);
            }
        }

        Assert.Equal(expected.Prob.Length, actual.Prob.Length);
        for (int i = 0; i < expected.Prob.Length; i++)
        {
            Vp8ProbaArray[] expectedProbabilities = expected.Prob[i].Probabilities;
            Vp8ProbaArray[] actualProbabilities = actual.Prob[i].Probabilities;
            Assert.Equal(expectedProbabilities.Length, actualProbabilities.Length);
            for (int j = 0; j < expectedProbabilities.Length; j++)
            {
                Assert.Equal(expectedProbabilities[j].Probabilities, actualProbabilities[j].Probabilities);
            }
        }

        for (int i = 0; i < expected.Costs.Length; i++)
        {
            Vp8CostArray[] expectedCosts = expected.Costs[i].Costs;
            Vp8CostArray[] actualCosts = actual.Costs[i].Costs;
            Assert.Equal(expectedCosts.Length, actualCosts.Length);
            for (int j = 0; j < expectedCosts.Length; j++)
            {
                Assert.Equal(expectedCosts[j].Costs, actualCosts[j].Costs);
            }
        }
    }

    [Fact]
    public void GetResidualCost_Works()
    {
        // arrange
        int ctx0 = 0;
        int expected = 20911;
        Vp8Residual residual = ReadVp8Residual(Path.Combine("TestDataWebp", "Vp8Residual.bin"));

        // act
        int actual = residual.GetResidualCost(ctx0);

        // assert
        Assert.Equal(expected, actual);
    }

    private static void CreateRandomProbas(Vp8EncProba probas, Random rand)
    {
        for (int t = 0; t < WebpConstants.NumTypes; ++t)
        {
            for (int b = 0; b < WebpConstants.NumBands; ++b)
            {
                for (int c = 0; c < WebpConstants.NumCtx; ++c)
                {
                    for (int p = 0; p < WebpConstants.NumProbas; ++p)
                    {
                        probas.Coeffs[t][b].Probabilities[c].Probabilities[p] = (byte)rand.Next(255);
                    }
                }
            }
        }
    }

    private static void CreateCosts(Vp8EncProba probas, Random rand)
    {
        for (int i = 0; i < probas.RemappedCosts.Length; i++)
        {
            for (int j = 0; j < probas.RemappedCosts[i].Length; j++)
            {
                for (int k = 0; k < probas.RemappedCosts[i][j].Costs.Length; k++)
                {
                    ushort[] costs = probas.RemappedCosts[i][j].Costs[k].Costs;
                    for (int m = 0; m < costs.Length; m++)
                    {
                        costs[m] = (byte)rand.Next(255);
                    }
                }
            }
        }
    }

    private static void RunSetCoeffsTest()
    {
        // arrange
        Vp8Residual residual = new();
        short[] coeffs = { 110, 0, -2, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0, 0, 0 };

        // act
        residual.SetCoeffs(coeffs);

        // assert
        Assert.Equal(9, residual.Last);
    }

    [Fact]
    public void SetCoeffsTest_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSetCoeffsTest, HwIntrinsics.AllowAll);

    [Fact]
    public void SetCoeffsTest_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSetCoeffsTest, HwIntrinsics.DisableSSE2);
}
