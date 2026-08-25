// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the bounded picture-order differences declared by one HEVC short-term reference-picture set.
/// </summary>
internal sealed class HevcShortTermReferencePictureSet
{
    /// <summary>
    /// Stores the bounded signed picture-order differences in HEVC reference order.
    /// </summary>
    private InlineArray16<int> deltaPictureOrders;

    /// <summary>
    /// Stores the bounded current-picture usage flags corresponding to <see cref="deltaPictureOrders"/>.
    /// </summary>
    private InlineArray16<bool> usedByCurrentPicture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcShortTermReferencePictureSet"/> class.
    /// </summary>
    private HevcShortTermReferencePictureSet()
    {
    }

    /// <summary>
    /// Gets the number of pictures declared by the reference-picture set.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets a signed picture-order difference in HEVC reference order.
    /// </summary>
    /// <param name="index">The zero-based reference-picture index.</param>
    /// <returns>The signed picture-order difference.</returns>
    public int GetDeltaPictureOrder(int index) => this.deltaPictureOrders[index];

    /// <summary>
    /// Gets whether a reference picture is used by the current picture.
    /// </summary>
    /// <param name="index">The zero-based reference-picture index.</param>
    /// <returns><see langword="true"/> when the reference is used by the current picture.</returns>
    public bool IsUsedByCurrentPicture(int index) => this.usedByCurrentPicture[index];

    /// <summary>
    /// Reads one SPS short-term reference-picture set.
    /// </summary>
    /// <param name="reader">The sequence-parameter-set raw byte sequence payload reader.</param>
    /// <param name="previousSets">The previously decoded sets available for inter-set prediction.</param>
    /// <param name="index">The zero-based index of the set being decoded.</param>
    /// <returns>The decoded reference-picture set.</returns>
    /// <exception cref="InvalidImageContentException">The set exceeds the HEVC decoded-picture-buffer bound.</exception>
    public static HevcShortTermReferencePictureSet Parse(
        ref HevcBitReader reader,
        IReadOnlyList<HevcShortTermReferencePictureSet> previousSets,
        int index)
    {
        HevcShortTermReferencePictureSet result = new();
        Span<int> deltaPictureOrders = result.deltaPictureOrders;
        Span<bool> usedByCurrentPicture = result.usedByCurrentPicture;
        int pictureCount = 0;
        bool interSetPrediction = index > 0 && reader.ReadFlag();
        if (interSetPrediction)
        {
            HevcShortTermReferencePictureSet referenceSet = previousSets[index - 1];
            bool deltaPictureOrderSign = reader.ReadFlag();
            uint absoluteDeltaPictureOrderMinusOne = reader.ReadUnsignedExpGolomb();
            if (absoluteDeltaPictureOrderMinusOne >= int.MaxValue)
            {
                throw new InvalidImageContentException("The HEVC short-term reference-picture delta is too large.");
            }

            int deltaReferencePictureSet = (deltaPictureOrderSign ? -1 : 1)
                * ((int)absoluteDeltaPictureOrderMinusOne + 1);

            for (int referenceIndex = 0; referenceIndex <= referenceSet.Count; referenceIndex++)
            {
                bool used = reader.ReadFlag();
                bool useDelta = used || reader.ReadFlag();
                if (!useDelta)
                {
                    continue;
                }

                if (pictureCount == deltaPictureOrders.Length)
                {
                    throw new InvalidImageContentException("The HEVC short-term reference-picture set is too large.");
                }

                int referenceDelta = referenceIndex < referenceSet.Count
                    ? referenceSet.GetDeltaPictureOrder(referenceIndex)
                    : 0;

                long deltaPictureOrder = (long)deltaReferencePictureSet + referenceDelta;
                if (deltaPictureOrder is < int.MinValue or > int.MaxValue)
                {
                    throw new InvalidImageContentException("The HEVC short-term reference-picture delta is too large.");
                }

                deltaPictureOrders[pictureCount] = (int)deltaPictureOrder;
                usedByCurrentPicture[pictureCount] = used;
                pictureCount++;
            }

            // HEVC orders negative differences nearest-first, followed by positive differences nearest-first.
            for (int outer = 1; outer < pictureCount; outer++)
            {
                int delta = deltaPictureOrders[outer];
                bool used = usedByCurrentPicture[outer];
                int inner = outer - 1;
                while (inner >= 0 && delta < deltaPictureOrders[inner])
                {
                    deltaPictureOrders[inner + 1] = deltaPictureOrders[inner];
                    usedByCurrentPicture[inner + 1] = usedByCurrentPicture[inner];
                    inner--;
                }

                deltaPictureOrders[inner + 1] = delta;
                usedByCurrentPicture[inner + 1] = used;
            }

            int negativeCount = 0;
            while (negativeCount < pictureCount && deltaPictureOrders[negativeCount] < 0)
            {
                negativeCount++;
            }

            deltaPictureOrders[..negativeCount].Reverse();
            usedByCurrentPicture[..negativeCount].Reverse();
        }
        else
        {
            uint negativePictureCount = reader.ReadUnsignedExpGolomb();
            uint positivePictureCount = reader.ReadUnsignedExpGolomb();
            if (negativePictureCount > 16 || positivePictureCount > 16 - negativePictureCount)
            {
                throw new InvalidImageContentException("The HEVC short-term reference-picture set is too large.");
            }

            int previousDelta = 0;
            for (uint negativeIndex = 0; negativeIndex < negativePictureCount; negativeIndex++)
            {
                uint deltaMinusOne = reader.ReadUnsignedExpGolomb();
                if (deltaMinusOne >= int.MaxValue)
                {
                    throw new InvalidImageContentException("The HEVC short-term reference-picture delta is too large.");
                }

                long deltaPictureOrder = (long)previousDelta - deltaMinusOne - 1;
                if (deltaPictureOrder < int.MinValue)
                {
                    throw new InvalidImageContentException("The HEVC short-term reference-picture delta is too large.");
                }

                previousDelta = (int)deltaPictureOrder;
                deltaPictureOrders[pictureCount] = previousDelta;
                usedByCurrentPicture[pictureCount] = reader.ReadFlag();
                pictureCount++;
            }

            previousDelta = 0;
            for (uint positiveIndex = 0; positiveIndex < positivePictureCount; positiveIndex++)
            {
                uint deltaMinusOne = reader.ReadUnsignedExpGolomb();
                if (deltaMinusOne >= int.MaxValue)
                {
                    throw new InvalidImageContentException("The HEVC short-term reference-picture delta is too large.");
                }

                long deltaPictureOrder = (long)previousDelta + deltaMinusOne + 1;
                if (deltaPictureOrder > int.MaxValue)
                {
                    throw new InvalidImageContentException("The HEVC short-term reference-picture delta is too large.");
                }

                previousDelta = (int)deltaPictureOrder;
                deltaPictureOrders[pictureCount] = previousDelta;
                usedByCurrentPicture[pictureCount] = reader.ReadFlag();
                pictureCount++;
            }
        }

        result.Count = pictureCount;
        return result;
    }
}
