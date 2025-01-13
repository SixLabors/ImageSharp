// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1PredictorTests
{
    private static string[] Digests4x4 = [
              "7b1c762e28747f885d2b7d83cb8aa75c", "73353f179207f1432d40a132809e3a50",
              "80c9237c838b0ec0674ccb070df633d5", "1cd79116b41fda884e7fa047f5eb14df",
              "33211425772ee539a59981a2e9dc10c1", "d6f5f65a267f0e9a2752e8151cc1dcd7",
              "7ff8c762cb766eb0665682152102ce4b", "2276b861ae4599de15938651961907ec",
              "766982bc69f4aaaa8e71014c2dc219bc", "04401B397D702F853B12407EBFB91027",
          ];

    private static string[] Digests4x8 = [
              "0a0d8641ecfa0e82f541acdc894d5574", "1a40371af6cff9c278c5b0def9e4b3e7",
              "3631a7a99569663b514f15b590523822", "646c7b592136285bd31501494e7393e7",
              "ecbe89cc64dc2688123d3cfe865b5237", "79048e70ecbb7d43a4703f62718588c0",
              "f3de11bf1198a00675d806d29c41d676", "32bb6cd018f6e871c342fcc21c7180cf",
              "6f076a1e5ab3d69cf08811d62293e4be", "F94348B0D3E2B1F2FC061232831E8B9B",
          ];

    private static string[] Digests4x16 = [
              "cb8240be98444ede5ae98ca94afc1557", "460acbcf825a1fa0d8f2aa6bf2d6a21c",
              "7896fdbbfe538dce1dc3a5b0873d74b0", "504aea29c6b27f21555d5516b8de2d8a",
              "c5738e7fa82b91ea0e39232120da56ea", "19abbd934c243a6d9df7585d81332dd5",
              "9e42b7b342e45c842dfa8aedaddbdfaa", "0e9eb07a89f8bf96bc219d5d1c3d9f6d",
              "659393c31633e0f498bae384c9df5c7b", "29241129BC91B354D04CA6C09A7CF1E1",
          ];

    private static string[] Digests8x4 = [
              "5950744064518f77867c8e14ebd8b5d7", "46b6cbdc76efd03f4ac77870d54739f7",
              "efe21fd1b98cb1663950e0bf49483b3b", "3c647b64760b298092cbb8e2f5c06bfd",
              "c3595929687ffb04c59b128d56e2632f", "d89ad2ddf8a74a520fdd1d7019fd75b4",
              "53907cb70ad597ee5885f6c58201f98b", "09d2282a29008b7fb47eb60ed6653d06",
              "e341fc1c910d7cb2dac5dbc58b9c9af9", "93D67CD49B07CE3870526E5C12F00EBF",
          ];

    private static string[] Digests8x8 = [
              "06fb7cb52719855a38b4883b4b241749", "2013aafd42a4303efb553e42264ab8b0",
              "2f070511d5680c12ca73a20e47fd6e23", "9923705af63e454392625794d5459fe0",
              "04007a0d39778621266e2208a22c4fac", "2d296c202d36b4a53f1eaddda274e4a1",
              "c87806c220d125c7563c2928e836fbbd", "339b49710a0099087e51ab5afc8d8713",
              "c90fbc020afd9327bf35dccae099bf77", "BAB0E5918AB2F7354D9BCA4E9A927C0F",
          ];

    private static string[] Digests8x16 = [
              "3c5a4574d96b5bb1013429636554e761", "8cf56b17c52d25eb785685f2ab48b194",
              "7911e2e02abfbe226f17529ac5db08fc", "064e509948982f66a14293f406d88d42",
              "5c443aa713891406d5be3af4b3cf67c6", "5d2cb98e532822ca701110cda9ada968",
              "3d58836e17918b8890012dd96b95bb9d", "20e8d61ddc451b9e553a294073349ffd",
              "a9aa6cf9d0dcf1977a1853ccc264e40b", "FEDA9D1554325ED2A243FC3DD6ADD4E3",
          ];

    private static string[] Digests8x32 = [
              "b393a2db7a76acaccc39e04d9dc3e8ac", "bbda713ee075a7ef095f0f479b5a1f82",
              "f337dce3980f70730d6f6c2c756e3b62", "796189b05dc026e865c9e95491b255d1",
              "ea932c21e7189eeb215c1990491320ab", "a9fffdf9455eba5e3b01317cae140289",
              "9525dbfdbf5fba61ef9c7aa5fe887503", "8c6a7e3717ff8a459f415c79bb17341c",
              "3761071bfaa2363a315fe07223f95a2d", "506D691319D1AEF38DF5C1E060408BDD",
          ];

    private static string[] Digests16x4 = [
              "1c0a950b3ac500def73b165b6a38467c", "95e7f7300f19da280c6a506e40304462",
              "28a6af15e31f76d3ff189012475d78f5", "e330d67b859bceef62b96fc9e1f49a34",
              "36eca3b8083ce2fb5f7e6227dfc34e71", "08f567d2abaa8e83e4d9b33b3f709538",
              "dc2d0ba13aa9369446932f03b53dc77d", "9ab342944c4b1357aa79d39d7bebdd3a",
              "77ec278c5086c88b91d68eef561ed517", "7B8ECDFBF449908E9C96664582C05BFE",
          ];

    private static string[] Digests16x8 = [
              "053a2bc4b5b7287fee524af4e77f077a", "619b720b13f14f32391a99ea7ff550d5",
              "728d61c11b06baf7fe77881003a918b9", "889997b89a44c9976cb34f573e2b1eea",
              "b43bfc31d1c770bb9ca5ca158c9beec4", "9d3fe9f762e0c6e4f114042147c50c7f",
              "c74fdd7c9938603b01e7ecf9fdf08d61", "870c7336db1102f80f74526bd5a7cf4e",
              "3fd5354a6190903d6a0b661fe177daf6", "EA586FE5C31B38A547D18332141665E9",
          ];

    private static string[] Digests16x16 = [
              "1fa9e2086f6594bda60c30384fbf1635", "2098d2a030cd7c6be613edc74dc2faf8",
              "f3c72b0c8e73f1ddca04d14f52d194d8", "6b31f2ee24cf88d3844a2fc67e1f39f3",
              "d91a22a83575e9359c5e4871ab30ddca", "24c32a0d38b4413d2ef9bf1f842c8634",
              "6e9e47bf9da9b2b9ae293e0bbd8ff086", "968b82804b5200b074bcdba9718140d4",
              "4e6d7e612c5ae0bbdcc51a453cd1db3f", "D182AFFEC5DBD0C7C0BD945AF090C48D",
          ];

    private static string[] Digests16x32 = [
              "01afd04432026ff56327d6226b720be2", "a6e7be906cc6f1e7a520151bfa7c303d",
              "bc05c46f18d0638f0228f1de64f07cd5", "204e613e429935f721a5b29cec7d44bb",
              "aa0a7c9a7482dfc06d9685072fc5bafd", "ffb60f090d83c624bb4f7dc3a630ac4f",
              "36bcb9ca9bb5eac520b050409de25da5", "34d9a5dd3363668391bc3bd05b468182",
              "1e149c28db8b234e43931c347a523794", "DD9FCEB000F2B2F1F4910AE9856BCF4C",
          ];

    private static string[] Digests16x64 = [
              "727797ef15ccd8d325476fe8f12006a3", "f77c544ac8035e01920deae40cee7b07",
              "12b0c69595328c465e0b25e0c9e3e9fc", "3b2a053ee8b05a8ac35ad23b0422a151",
              "f3be77c0fe67eb5d9d515e92bec21eb7", "f1ece6409e01e9dd98b800d49628247d",
              "efd2ec9bfbbd4fd1f6604ea369df1894", "ec703de918422b9e03197ba0ed60a199",
              "739418efb89c07f700895deaa5d0b3e3", "AE9A0FBC3EB929B82E20E8C45001E7BE",
          ];

    private static string[] Digests32x8 = [
              "4da55401331ed98acec0c516d7307513", "0ae6f3974701a5e6c20baccd26b4ca52",
              "79b799f1eb77d5189535dc4e18873a0e", "90e943adf3de4f913864dce4e52b4894",
              "5e1b9cc800a89ef45f5bdcc9e99e4e96", "3103405df20d254cbf32ac30872ead4b",
              "648550e369b77687bff3c7d6f249b02f", "f9f73bcd8aadfc059fa260325df957a1",
              "204cef70d741c25d4fe2b1d10d2649a5", "8CB3C74FFFF9975F3DE5178311486350",
          ];

    private static string[] Digests32x16 = [
              "86ad1e1047abaf9959150222e8f19593", "1908cbe04eb4e5c9d35f1af7ffd7ee72",
              "6ad3bb37ebe8374b0a4c2d18fe3ebb6a", "08d3cfe7a1148bff55eb6166da3378c6",
              "656a722394764d17b6c42401b9e0ad3b", "4aa00c192102efeb325883737e562f0d",
              "9881a90ca88bca4297073e60b3bb771a", "8cd74aada398a3d770fc3ace38ecd311",
              "0a927e3f5ff8e8338984172cc0653b13", "91ABCB84EB86746DEF31AF8F96BAA0CF",
          ];

    private static string[] Digests32x32 = [
              "1303ca680644e3d8c9ffd4185bb2835b", "2a4d9f5cc8da307d4cf7dc021df10ba9",
              "ced60d3f4e4b011a6a0314dd8a4b1fd8", "ced60d3f4e4b011a6a0314dd8a4b1fd8",
              "1464b01aa928e9bd82c66bad0f921693", "90deadfb13d7c3b855ba21b326c1e202",
              "af96a74f8033dff010e53a8521bc6f63", "9f1039f2ef082aaee69fcb7d749037c2",
              "3f82893e478e204f2d254b34222d14dc", "9B2A16A6331AA6EC41636B8F709B39C7",
          ];

    private static string[] Digests32x64 = [
              "e1e8ed803236367821981500a3d9eebe", "0f46d124ba9f48cdd5d5290acf786d6d",
              "4e2a2cfd8f56f15939bdfc753145b303", "0ce332b343934b34cd4417725faa85cb",
              "1d2f8e48e3adb7c448be05d9f66f4954", "9fb2e176636a5689b26f73ca73fcc512",
              "e720ebccae7e25e36f23da53ae5b5d6a", "86fe4364734169aaa4520d799890d530",
              "b1870290764bb1b100d1974e2bd70f1d", "B652BBE65C345A7153A1F3E1C801633F",
          ];

    private static string[] Digests64x16 = [
              "de1b736e9d99129609d6ef3a491507a0", "516d8f6eb054d74d150e7b444185b6b9",
              "69e462c3338a9aaf993c3f7cfbc15649", "821b76b1494d4f84d20817840f719a1a",
              "fd9b4276e7affe1e0e4ce4f428058994", "cd82fd361a4767ac29a9f406b480b8f3",
              "2792c2f810157a4a6cb13c28529ff779", "1220442d90c4255ba0969d28b91e93a6",
              "c7253e10b45f7f67dfee3256c9b94825", "F0DC6EE8E9291452AC361E08AEB53DB5",
          ];

    private static string[] Digests64x32 = [
              "e48e1ac15e97191a8fda08d62fff343e", "80c15b303235f9bc2259027bb92dfdc4",
              "538424b24bd0830f21788e7238ca762f", "a6c5aeb722615089efbca80b02951ceb",
              "12604b37875533665078405ef4582e35", "0048afa17bd3e1632d68b96048836530",
              "07a0cfcb56a5eed50c4bd6c26814336b", "529d8a070de5bc6531fa3ee8f450c233",
              "33c50a11c7d78f72434064f634305e95", "28E560D9C16C5ED6055A66AB16EC6900",
          ];

    private static string[] Digests64x64 = [
              "a1650dbcd56e10288c3e269eca37967d", "be91585259bc37bf4dc1651936e90b3e",
              "afe020786b83b793c2bbd9468097ff6e", "6e1094fa7b50bc813aa2ba29f5df8755",
              "9e5c34f3797e0cdd3cd9d4c05b0d8950", "bc87be7ac899cc6a28f399d7516c49fe",
              "9811fd0d2dd515f06122f5d1bd18b784", "3c140e466f2c2c0d9cb7d2157ab8dc27",
              "9543de76c925a8f6adc884cc7f98dc91", "73BBEC1CDFFF6D66318FB43628EA42C2",
          ];

    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifyDcFill(int _, int width, int height)
    {
        // Assign
        byte[] destination = new byte[width * height];
        byte[] left = new byte[1];
        byte[] above = new byte[1];
        byte expected = 0x80;

        // Act
        Av1DcFillPredictor predictor = new(new Size(width, height));
        predictor.PredictScalar(destination, (nuint)width, above, left);

        // Assert
        Assert.All(destination, (b) => AssertValue(expected, b));
    }

    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifyDc(int _, int width, int height)
    {
        // Assign
        byte[] destination = new byte[width * height];
        byte[] left = new byte[height];
        byte[] above = new byte[width];
        Array.Fill(left, (byte)5);
        Array.Fill(above, (byte)28);
        int count = width + height;
        int sum = Sum(left, height) + Sum(above, width);
        byte expected = (byte)((sum + (count >> 1)) / count);

        // Act
        Av1DcPredictor predictor = new(new Size(width, height));
        predictor.PredictScalar(destination, (nuint)width, above, left);

        // Assert
        Assert.Equal((5 * height) + (28 * width), sum);
        Assert.All(destination, (b) => AssertValue(expected, b));
    }

    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifyDcLeft(int _, int width, int height)
    {
        // Assign
        byte[] destination = new byte[width * height];
        byte[] left = new byte[height];
        byte[] above = new byte[width];
        Array.Fill(left, (byte)5);
        Array.Fill(above, (byte)28);
        byte expected = left[0];

        // Act
        Av1DcLeftPredictor predictor = new(new Size(width, height));
        predictor.PredictScalar(destination, (nuint)width, above, left);

        // Assert
        Assert.All(destination, (b) => AssertValue(expected, b));
    }

    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifyDcTop(int _, int width, int height)
    {
        // Assign
        byte[] destination = new byte[width * height];
        byte[] left = new byte[height];
        byte[] above = new byte[width];
        Array.Fill(left, (byte)5);
        Array.Fill(above, (byte)28);
        byte expected = above[0];

        // Act
        Av1DcTopPredictor predictor = new(new Size(width, height));
        predictor.PredictScalar(destination, (nuint)width, above, left);

        // Assert
        Assert.All(destination, (b) => AssertValue(expected, b));
    }

    [Theory]
    [MemberData(nameof(GetTransformSizes))]
    public void VerifySmooth(int index, int width, int height)
    {
        // Arrange
        string expectedDigest = GetExpectedDigext((Av1TransformSize)index, Av1PredictionMode.Smooth);
        Av1IntraPredictionMemory predictorMemory = new(8);
        predictorMemory.Scramble(new Random(42));
        predictorMemory.CopySourceToDestination();
        const int stride = 1 << (Av1Constants.MaxSuperBlockSizeLog2 - 1);
        Av1SmoothPredictor predictor = new(new Size(width, height));

        // Act
        predictor.PredictScalar(predictorMemory.Destination, stride, predictorMemory.Top, predictorMemory.Left);

        // Assert
        Assert.Equal(expectedDigest, predictorMemory.GetDestinationDigest());
    }

    private static void AssertValue(byte expected, byte actual)
    {
        Assert.NotEqual(0, actual);
        Assert.Equal(expected, actual);
    }

    private static int Sum(Span<byte> values, int length)
    {
        int sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += values[i];
        }

        return sum;
    }

    public static TheoryData<int, int, int> GetTransformSizes()
    {
        TheoryData<int, int, int> combinations = [];
        for (int s = 0; s < (int)Av1TransformSize.AllSizes; s++)
        {
            Av1TransformSize size = (Av1TransformSize)s;
            int width = size.GetWidth();
            int height = size.GetHeight();
            combinations.Add(s, width, height);
        }

        return combinations;
    }

    private static string GetExpectedDigext(Av1TransformSize size, Av1PredictionMode mode) => size switch
    {
        Av1TransformSize.Size4x4 => Digests4x4[(int)mode],
        Av1TransformSize.Size8x4 => Digests8x4[(int)mode],
        Av1TransformSize.Size4x8 => Digests4x8[(int)mode],
        Av1TransformSize.Size8x8 => Digests8x8[(int)mode],
        Av1TransformSize.Size4x16 => Digests4x16[(int)mode],
        Av1TransformSize.Size16x4 => Digests16x4[(int)mode],
        Av1TransformSize.Size8x16 => Digests8x16[(int)mode],
        Av1TransformSize.Size16x8 => Digests16x8[(int)mode],
        Av1TransformSize.Size16x16 => Digests16x16[(int)mode],
        Av1TransformSize.Size8x32 => Digests8x32[(int)mode],
        Av1TransformSize.Size32x8 => Digests32x8[(int)mode],
        Av1TransformSize.Size16x32 => Digests16x32[(int)mode],
        Av1TransformSize.Size32x16 => Digests32x16[(int)mode],
        Av1TransformSize.Size32x32 => Digests32x32[(int)mode],
        Av1TransformSize.Size16x64 => Digests16x64[(int)mode],
        Av1TransformSize.Size64x16 => Digests64x16[(int)mode],
        Av1TransformSize.Size32x64 => Digests32x64[(int)mode],
        Av1TransformSize.Size64x32 => Digests64x32[(int)mode],
        Av1TransformSize.Size64x64 => Digests64x64[(int)mode],
        _ => string.Empty,
    };
}
