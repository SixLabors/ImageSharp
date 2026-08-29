// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC context initialization, arithmetic decoding, bypass decoding, termination, and PCM restart.
/// </summary>
[Trait("Format", "Heic")]
public class HevcCabacDecoderTests
{
    /// <summary>
    /// Verifies every intra-slice context initialization against the table in the pinned HM ContextTables.h.
    /// </summary>
    /// <param name="quantizationParameter">The luma quantization parameter used to initialize the contexts.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(22)]
    [InlineData(51)]
    public void IntraContextInitializationMatchesPinnedHmTable(int quantizationParameter)
    {
        ReadOnlySpan<byte> initializationValues =
        [
            154,
            139, 141, 157,
            184,
            184,
            63, 139,
            154, 154, 154,
            154,
            154,
            111, 141, 154, 154, 154,
            94, 138, 182, 154, 154,
            110, 110, 124, 125, 140, 153, 125, 127, 140, 109, 111, 143, 127, 111, 79,
            108, 123, 63, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154,
            110, 110, 124, 125, 140, 153, 125, 127, 140, 109, 111, 143, 127, 111, 79,
            108, 123, 63, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154, 154,
            91, 171, 134, 141,
            111, 111, 125, 110, 110, 94, 124, 108, 124, 107, 125, 141, 179, 153,
            125, 107, 125, 141, 179, 153, 125, 107, 125, 141, 179, 153, 125, 141,
            140, 139, 182, 182, 152, 136, 152, 136, 153, 136, 139, 111, 136, 139, 111, 111,
            140, 92, 137, 138, 140, 152, 138, 139, 153, 74, 149, 92, 139, 107, 122, 152,
            140, 179, 166, 182, 140, 227, 122, 197,
            138, 153, 136, 167, 152, 152,
            153,
            200,
            153, 138, 138,
            139, 139,
            154, 154, 154, 154, 154, 154, 154, 154, 154, 154
        ];

        Assert.Equal(HevcCabacContexts.ContextCount, initializationValues.Length);
        HevcCabacContexts contexts = new(quantizationParameter);
        Span<HevcCabacContext> actual = stackalloc HevcCabacContext[HevcCabacContexts.ContextCount];
        contexts.CopyTo(actual);

        for (int index = 0; index < actual.Length; index++)
        {
            int expectedState = GetInitializedPackedState(quantizationParameter, initializationValues[index]);
            Assert.Equal(expectedState, GetPackedState(actual[index]));
        }
    }

    /// <summary>
    /// Verifies every reachable probability-state transition against the pinned HM ContextModel.cpp tables.
    /// </summary>
    [Fact]
    public void ReachableContextTransitionsMatchPinnedHmTables()
    {
        ReadOnlySpan<byte> mostProbableTransitions =
        [
            2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
            18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33,
            34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49,
            50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65,
            66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81,
            82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97,
            98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113,
            114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 124, 125, 126, 127
        ];

        ReadOnlySpan<byte> leastProbableTransitions =
        [
            1, 0, 0, 1, 2, 3, 4, 5, 4, 5, 8, 9, 8, 9, 10, 11,
            12, 13, 14, 15, 16, 17, 18, 19, 18, 19, 22, 23, 22, 23, 24, 25,
            26, 27, 26, 27, 30, 31, 30, 31, 32, 33, 32, 33, 36, 37, 36, 37,
            38, 39, 38, 39, 42, 43, 42, 43, 44, 45, 44, 45, 46, 47, 48, 49,
            48, 49, 50, 51, 52, 53, 52, 53, 54, 55, 54, 55, 56, 57, 58, 59,
            58, 59, 60, 61, 60, 61, 60, 61, 62, 63, 64, 65, 64, 65, 66, 67,
            66, 67, 66, 67, 68, 69, 68, 69, 70, 71, 70, 71, 70, 71, 72, 73,
            72, 73, 72, 73, 74, 75, 74, 75, 74, 75, 76, 77, 76, 77, 126, 127
        ];

        Span<bool> visited = stackalloc bool[128];
        int visitedCount = 0;
        for (int quantizationParameter = 0; quantizationParameter <= 51; quantizationParameter++)
        {
            for (int initializationValue = 0; initializationValue <= byte.MaxValue; initializationValue++)
            {
                HevcCabacContext context = new(quantizationParameter, (byte)initializationValue);
                int packedState = GetPackedState(context);
                if (visited[packedState])
                {
                    continue;
                }

                visited[packedState] = true;
                visitedCount++;

                HevcCabacContext mostProbableContext = context;
                mostProbableContext.UpdateMostProbableSymbol();
                Assert.Equal(mostProbableTransitions[packedState], GetPackedState(mostProbableContext));

                HevcCabacContext leastProbableContext = context;
                leastProbableContext.UpdateLeastProbableSymbol();
                Assert.Equal(leastProbableTransitions[packedState], GetPackedState(leastProbableContext));
            }
        }

        // The clipped initialization equation reaches packed states 0 through 125. HM's terminal states 126 and 127
        // cannot be entered from those states, so they are not transitions of a conforming decoder execution.
        Assert.Equal(126, visitedCount);
        Assert.False(visited[126]);
        Assert.False(visited[127]);
    }

    /// <summary>
    /// Verifies most- and least-probable arithmetic decisions against hand-calculated pinned-HM register vectors.
    /// </summary>
    [Fact]
    public void DecisionDecodingMatchesPinnedHmVectors()
    {
        ReadOnlySpan<byte> mostProbableData = [0x00, 0x00, 0x00];

        HevcCabacDecoder mostProbableDecoder = new(mostProbableData);
        HevcCabacContext mostProbableContext = new(22, 154);
        Assert.True(mostProbableDecoder.ReadDecision(ref mostProbableContext));
        Assert.Equal(3, GetPackedState(mostProbableContext));
        Assert.Equal(2, mostProbableDecoder.BytesConsumed);

        ReadOnlySpan<byte> leastProbableData = [0xFF, 0xFF, 0x00];

        HevcCabacDecoder leastProbableDecoder = new(leastProbableData);
        HevcCabacContext leastProbableContext = new(22, 154);
        Assert.False(leastProbableDecoder.ReadDecision(ref leastProbableContext));
        Assert.Equal(0, GetPackedState(leastProbableContext));
        Assert.Equal(2, leastProbableDecoder.BytesConsumed);
    }

    /// <summary>
    /// Verifies aligned bypass extraction and terminating decisions against pinned-HM register vectors.
    /// </summary>
    [Fact]
    public void BypassAndTerminationMatchPinnedHmVectors()
    {
        ReadOnlySpan<byte> bypassData = [0x2A, 0x80, 0xCC];

        HevcCabacDecoder bypassDecoder = new(bypassData);
        bypassDecoder.AlignBypass();
        Assert.Equal(0x55U, bypassDecoder.ReadBypassBits(8));
        Assert.Equal(3, bypassDecoder.BytesConsumed);

        ReadOnlySpan<byte> terminatingData = [0xFF, 0xFF];

        HevcCabacDecoder terminatingDecoder = new(terminatingData);
        Assert.True(terminatingDecoder.ReadTerminate());

        ReadOnlySpan<byte> continuingData = [0x00, 0x00];

        HevcCabacDecoder continuingDecoder = new(continuingData);
        Assert.False(continuingDecoder.ReadTerminate());
    }

    /// <summary>
    /// Verifies the required stop bit and zero padding after a terminating CABAC value.
    /// </summary>
    [Fact]
    public void TerminationAlignmentRejectsInvalidPattern()
    {
        ReadOnlySpan<byte> validData = [0x00, 0x80];

        HevcCabacDecoder validDecoder = new(validData);
        validDecoder.ValidateTerminationAlignment();

        Assert.Throws<InvalidImageContentException>(ValidateInvalidTerminationAlignment);
    }

    /// <summary>
    /// Verifies that PCM samples begin after the terminating arithmetic bytes and that arithmetic decoding resumes after the raw payload.
    /// </summary>
    [Fact]
    public void PcmPayloadSuspendsAndRestartsArithmeticDecoding()
    {
        ReadOnlySpan<byte> data = [0xFF, 0xFF, 0xAB, 0xFF, 0xFF];
        HevcCabacDecoder decoder = new(data);

        Assert.True(decoder.ReadPcmFlag());
        Assert.Equal((ushort)0xA, decoder.ReadPcmSample(4));
        Assert.Equal((ushort)0xB, decoder.ReadPcmSample(4));

        decoder.RestartAfterPcm();

        Assert.True(decoder.ReadTerminate());
    }

    /// <summary>
    /// Verifies that a PCM sample cannot read beyond its bounded entropy substream.
    /// </summary>
    [Fact]
    public void PcmPayloadRejectsTruncatedSample()
    {
        Assert.Throws<InvalidImageContentException>(ReadTruncatedPcmSample);
    }

    /// <summary>
    /// Attempts to read a sample wider than the remaining raw PCM payload.
    /// </summary>
    private static void ReadTruncatedPcmSample()
    {
        ReadOnlySpan<byte> data = [0xFF, 0xFF, 0x80];
        HevcCabacDecoder decoder = new(data);

        Assert.True(decoder.ReadPcmFlag());
        decoder.ReadPcmSample(16);
    }

    /// <summary>
    /// Validates an entropy substream without the required termination stop bit.
    /// </summary>
    private static void ValidateInvalidTerminationAlignment()
    {
        ReadOnlySpan<byte> data = [0x00, 0x00];

        HevcCabacDecoder decoder = new(data);
        decoder.ValidateTerminationAlignment();
    }

    /// <summary>
    /// Calculates the packed context state prescribed by the HEVC initialization equation.
    /// </summary>
    /// <param name="quantizationParameter">The luma quantization parameter.</param>
    /// <param name="initializationValue">The context initialization byte.</param>
    /// <returns>The probability-state index and most-probable symbol packed into one integer.</returns>
    private static int GetInitializedPackedState(int quantizationParameter, byte initializationValue)
    {
        int clippedQuantizationParameter = Math.Clamp(quantizationParameter, 0, 51);
        int slope = ((initializationValue >> 4) * 5) - 45;
        int offset = ((initializationValue & 15) << 3) - 16;
        int initializationState = Math.Clamp(
            ((slope * clippedQuantizationParameter) >> 4) + offset,
            1,
            126);

        bool mostProbableSymbol = initializationState >= 64;
        return ((mostProbableSymbol ? initializationState - 64 : 63 - initializationState) << 1)
            + (mostProbableSymbol ? 1 : 0);
    }

    /// <summary>
    /// Packs a decoded context's observable probability state for table comparison.
    /// </summary>
    /// <param name="context">The context to inspect.</param>
    /// <returns>The probability-state index and most-probable symbol packed into one integer.</returns>
    private static int GetPackedState(HevcCabacContext context)
        => (context.StateIndex << 1) + (context.MostProbableSymbol ? 1 : 0);
}
