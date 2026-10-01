// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

/// <summary>
/// The film grain parameters of time ranges, read from the text format that starts with "filmgrn1". Each entry starts
/// with "E", its time range, the apply flag, the random seed and the update flag. An entry that updates its
/// parameters continues with the "p" line of shifts and chroma multipliers, the "sY", "sCb" and "sCr" scaling points,
/// and the "cY", "cCb" and "cCr" autoregressive coefficients. Reference: aom_film_grain_table_t.
/// </summary>
internal sealed class Av1FilmGrainTable
{
    /// <summary>
    /// The text that starts every table. Reference: kFileMagic.
    /// </summary>
    private const string Header = "filmgrn1";

    /// <summary>
    /// The entries in the order of the text. Reference: the head to tail list of aom_film_grain_table_t.
    /// </summary>
    private readonly List<Entry> entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FilmGrainTable"/> class.
    /// </summary>
    /// <param name="entries">The entries in the order of the text.</param>
    private Av1FilmGrainTable(List<Entry> entries) => this.entries = entries;

    /// <summary>
    /// Gets a parameter set with every field zero, as the lookup clears the running parameters to. The coefficients
    /// hold their offset of 128, which is a zero coefficient. Reference: the memset() of
    /// aom_film_grain_table_lookup().
    /// </summary>
    private static ObuFilmGrainParameters Empty { get; } = CreateEmpty();

    /// <summary>
    /// Reads a table from its text. Tokens may be separated by any white space. Reference:
    /// aom_film_grain_table_read() and grain_table_entry_read().
    /// </summary>
    /// <param name="text">The table text.</param>
    /// <returns>The table.</returns>
    /// <exception cref="ArgumentException">The text is not a valid film grain table.</exception>
    public static Av1FilmGrainTable Parse(string text)
    {
        if (!text.StartsWith(Header, StringComparison.Ordinal))
        {
            throw new ArgumentException("The film grain table does not start with the filmgrn1 header.", nameof(text));
        }

        // The reference reads with fscanf(), so every run of white space separates two tokens.
        string[] tokens = text[Header.Length..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int position = 0;
        List<Entry> entries = [];
        while (position < tokens.Length)
        {
            // The entry line: "E start end apply seed update".
            Expect(tokens, ref position, "E");
            Entry entry = new()
            {
                StartTime = ReadLong(tokens, ref position),
                EndTime = ReadLong(tokens, ref position)
            };

            ObuFilmGrainParameters parameters = entry.Parameters;
            parameters.ApplyGrain = ReadInt(tokens, ref position) != 0;

            // The seed is read as a 16-bit signed value and kept as the unsigned bitstream field.
            parameters.GrainSeed = (ushort)(short)ReadInt(tokens, ref position);
            parameters.UpdateGrain = ReadInt(tokens, ref position) != 0;
            if (parameters.UpdateGrain)
            {
                // The "p" line: lag, coefficient shift, grain scale shift, scaling shift, chroma from luma, overlap,
                // and the three blue-difference and three red-difference multipliers.
                Expect(tokens, ref position, "p");
                int lag = ReadInt(tokens, ref position);
                parameters.ArCoeffLag = (uint)lag;
                parameters.ArCoeffShiftMinus6 = (uint)(ReadInt(tokens, ref position) - 6);
                parameters.GrainScaleShift = (uint)ReadInt(tokens, ref position);
                parameters.GrainScalingMinus8 = (uint)(ReadInt(tokens, ref position) - 8);
                parameters.ChromaScalingFromLuma = ReadInt(tokens, ref position) != 0;
                parameters.OverlapFlag = ReadInt(tokens, ref position) != 0;
                parameters.CbMult = (uint)ReadInt(tokens, ref position);
                parameters.CbLumaMult = (uint)ReadInt(tokens, ref position);
                parameters.CbOffset = (uint)ReadInt(tokens, ref position);
                parameters.CrMult = (uint)ReadInt(tokens, ref position);
                parameters.CrLumaMult = (uint)ReadInt(tokens, ref position);
                parameters.CrOffset = (uint)ReadInt(tokens, ref position);
                parameters.NumYPoints = ReadPoints(tokens, ref position, "sY", parameters.PointYValue, parameters.PointYScaling);
                parameters.NumCbPoints = ReadPoints(tokens, ref position, "sCb", parameters.PointCbValue, parameters.PointCbScaling);
                parameters.NumCrPoints = ReadPoints(tokens, ref position, "sCr", parameters.PointCrValue, parameters.PointCrScaling);

                // A lag of L has 2L(L+1) luma coefficients, and each chroma plane has one more for the luma term.
                int count = 2 * lag * (lag + 1);
                ReadCoefficients(tokens, ref position, "cY", count, parameters.ArCoeffsYPlus128);
                ReadCoefficients(tokens, ref position, "cCb", count + 1, parameters.ArCoeffsCbPlus128);
                ReadCoefficients(tokens, ref position, "cCr", count + 1, parameters.ArCoeffsCrPlus128);
            }

            entries.Add(entry);
        }

        return new Av1FilmGrainTable(entries);
    }

    /// <summary>
    /// Copies the parameters of the first entry whose time range holds a time stamp. A time stamp other than zero
    /// keeps the running random seed. Without an entry the parameters are cleared. Reference:
    /// aom_film_grain_table_lookup() without erasing.
    /// </summary>
    /// <param name="timeStamp">The frame's start time in ticks.</param>
    /// <param name="parameters">The running parameters, replaced by the entry's.</param>
    /// <returns>Whether an entry holds the time stamp.</returns>
    public bool Lookup(long timeStamp, ObuFilmGrainParameters parameters)
    {
        uint randomSeed = parameters.GrainSeed;
        parameters.CopyFrom(Empty);
        foreach (Entry entry in this.entries)
        {
            // A range holds its start time and ends before its end time.
            if (timeStamp >= entry.StartTime && timeStamp < entry.EndTime)
            {
                parameters.CopyFrom(entry.Parameters);
                if (timeStamp != 0)
                {
                    parameters.GrainSeed = randomSeed;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a parameter set with every field zero and the coefficients at their offset of 128.
    /// </summary>
    /// <returns>The parameter set.</returns>
    private static ObuFilmGrainParameters CreateEmpty()
    {
        ObuFilmGrainParameters empty = new();
        empty.ArCoeffsYPlus128.Fill(128);
        empty.ArCoeffsCbPlus128.Fill(128);
        empty.ArCoeffsCrPlus128.Fill(128);
        return empty;
    }

    /// <summary>
    /// Moves past a token that must have a given value.
    /// </summary>
    /// <param name="tokens">The tokens of the table text.</param>
    /// <param name="position">The index of the next token, advanced past the expected token.</param>
    /// <param name="expected">The value the token must have.</param>
    /// <exception cref="ArgumentException">The token is missing or has another value.</exception>
    private static void Expect(string[] tokens, ref int position, string expected)
    {
        if (position >= tokens.Length || tokens[position] != expected)
        {
            throw new ArgumentException($"The film grain table is missing '{expected}'.");
        }

        position++;
    }

    /// <summary>
    /// Returns the next token and moves past it.
    /// </summary>
    /// <param name="tokens">The tokens of the table text.</param>
    /// <param name="position">The index of the next token, advanced past it.</param>
    /// <returns>The token.</returns>
    /// <exception cref="ArgumentException">The text ends before the token.</exception>
    private static string Next(string[] tokens, ref int position)
    {
        if (position >= tokens.Length)
        {
            throw new ArgumentException("The film grain table ends inside an entry.");
        }

        return tokens[position++];
    }

    /// <summary>
    /// Reads the next token as a 32-bit integer with an optional sign.
    /// </summary>
    /// <param name="tokens">The tokens of the table text.</param>
    /// <param name="position">The index of the next token, advanced past it.</param>
    /// <returns>The integer.</returns>
    /// <exception cref="ArgumentException">The text ends before the token, or the token is not an integer.</exception>
    private static int ReadInt(string[] tokens, ref int position)
    {
        string token = Next(tokens, ref position);
        if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException($"The film grain table has '{token}' where it needs an integer.");
        }

        return value;
    }

    /// <summary>
    /// Reads the next token as a 64-bit integer with an optional sign, as the times of an entry are.
    /// </summary>
    /// <param name="tokens">The tokens of the table text.</param>
    /// <param name="position">The index of the next token, advanced past it.</param>
    /// <returns>The integer.</returns>
    /// <exception cref="ArgumentException">The text ends before the token, or the token is not an integer.</exception>
    private static long ReadLong(string[] tokens, ref int position)
    {
        string token = Next(tokens, ref position);
        if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
        {
            throw new ArgumentException($"The film grain table has '{token}' where it needs an integer.");
        }

        return value;
    }

    /// <summary>
    /// Reads a scaling function: its header, its point count and the value and scaling of each point. The points
    /// past the count are cleared.
    /// </summary>
    /// <param name="tokens">The tokens of the table text.</param>
    /// <param name="position">The index of the next token, advanced past the scaling function.</param>
    /// <param name="header">The header of the scaling function: "sY", "sCb" or "sCr".</param>
    /// <param name="values">The point values that receive the function.</param>
    /// <param name="scalings">The point scalings that receive the function.</param>
    /// <returns>The point count.</returns>
    /// <exception cref="ArgumentException">The function is missing, ends early or has too many points.</exception>
    private static uint ReadPoints(string[] tokens, ref int position, string header, Span<byte> values, Span<byte> scalings)
    {
        Expect(tokens, ref position, header);
        int count = ReadInt(tokens, ref position);

        // A negative count fails the unsigned comparison as well.
        if ((uint)count > (uint)values.Length)
        {
            throw new ArgumentException($"The film grain table has too many {header} points.");
        }

        values.Clear();
        scalings.Clear();
        for (int i = 0; i < count; i++)
        {
            values[i] = (byte)ReadInt(tokens, ref position);
            scalings[i] = (byte)ReadInt(tokens, ref position);
        }

        return (uint)count;
    }

    /// <summary>
    /// Reads the autoregressive coefficients of one plane: their header and a count of coefficients, each stored
    /// with its offset of 128. The coefficients past the count are zero.
    /// </summary>
    /// <param name="tokens">The tokens of the table text.</param>
    /// <param name="position">The index of the next token, advanced past the coefficients.</param>
    /// <param name="header">The header of the coefficients: "cY", "cCb" or "cCr".</param>
    /// <param name="count">The number of coefficients, which the lag sets.</param>
    /// <param name="plus128">The coefficients that receive the values.</param>
    /// <exception cref="ArgumentException">The coefficients are missing, end early or need a lag above 3.</exception>
    private static void ReadCoefficients(string[] tokens, ref int position, string header, int count, Span<byte> plus128)
    {
        Expect(tokens, ref position, header);
        if (count > plus128.Length)
        {
            throw new ArgumentException("The film grain table has an autoregressive lag above 3.");
        }

        plus128.Fill(128);
        for (int i = 0; i < count; i++)
        {
            plus128[i] = (byte)(ReadInt(tokens, ref position) + 128);
        }
    }

    /// <summary>
    /// One time range of the table and its parameters. Reference: aom_film_grain_table_entry_t.
    /// </summary>
    private sealed class Entry
    {
        /// <summary>
        /// Gets the first tick of the range. Reference: start_time.
        /// </summary>
        public long StartTime { get; init; }

        /// <summary>
        /// Gets the tick after the range. Reference: end_time.
        /// </summary>
        public long EndTime { get; init; }

        /// <summary>
        /// Gets the film grain parameters of the range. Reference: params.
        /// </summary>
        public ObuFilmGrainParameters Parameters { get; } = CreateEmpty();
    }
}
