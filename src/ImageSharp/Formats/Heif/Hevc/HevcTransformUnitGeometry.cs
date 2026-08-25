// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Maps one luma transform-tree node to its primary and subsampled component rectangles.
/// </summary>
internal readonly struct HevcTransformUnitGeometry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcTransformUnitGeometry"/> struct.
    /// </summary>
    /// <param name="log2LumaSize">The base-two logarithm of the luma transform-node side.</param>
    /// <param name="primaryPlane">The primary plane coded with luma syntax.</param>
    /// <param name="primary">The primary component rectangle.</param>
    /// <param name="chromaBlue">The blue-difference chroma rectangle.</param>
    /// <param name="chromaRed">The red-difference chroma rectangle.</param>
    /// <param name="hasCombinedChroma">Whether chroma syntax accompanies the primary luma syntax.</param>
    private HevcTransformUnitGeometry(
        int log2LumaSize,
        HevcPlane primaryPlane,
        HevcTransformComponentGeometry primary,
        HevcTransformComponentGeometry chromaBlue,
        HevcTransformComponentGeometry chromaRed,
        bool hasCombinedChroma)
    {
        this.Log2LumaSize = log2LumaSize;
        this.PrimaryPlane = primaryPlane;
        this.Primary = primary;
        this.ChromaBlue = chromaBlue;
        this.ChromaRed = chromaRed;
        this.HasCombinedChroma = hasCombinedChroma;
    }

    /// <summary>
    /// Gets the base-two logarithm of the luma transform-node side.
    /// </summary>
    public int Log2LumaSize { get; }

    /// <summary>
    /// Gets the plane coded with luma transform syntax.
    /// </summary>
    public HevcPlane PrimaryPlane { get; }

    /// <summary>
    /// Gets the primary component rectangle.
    /// </summary>
    public HevcTransformComponentGeometry Primary { get; }

    /// <summary>
    /// Gets the blue-difference chroma rectangle.
    /// </summary>
    public HevcTransformComponentGeometry ChromaBlue { get; }

    /// <summary>
    /// Gets the red-difference chroma rectangle.
    /// </summary>
    public HevcTransformComponentGeometry ChromaRed { get; }

    /// <summary>
    /// Gets a value indicating whether combined chroma syntax accompanies the primary luma syntax.
    /// </summary>
    public bool HasCombinedChroma { get; }

    /// <summary>
    /// Creates the root component geometry for one coding unit.
    /// </summary>
    /// <param name="x">The coding-unit left luma coordinate.</param>
    /// <param name="y">The coding-unit top luma coordinate.</param>
    /// <param name="log2Size">The base-two logarithm of the coding-unit side.</param>
    /// <param name="chromaFormat">The sequence chroma-format identifier.</param>
    /// <param name="separateColorPlane">Whether each 4:4:4 component is coded as an independent color plane.</param>
    /// <param name="colorPlaneIndex">The selected separate-color plane, or zero for combined coding.</param>
    /// <returns>The root transform-unit geometry.</returns>
    public static HevcTransformUnitGeometry CreateRoot(
        int x,
        int y,
        int log2Size,
        byte chromaFormat,
        bool separateColorPlane,
        int colorPlaneIndex)
    {
        int size = 1 << log2Size;
        HevcPlane primaryPlane = separateColorPlane ? (HevcPlane)colorPlaneIndex : HevcPlane.Y;
        HevcTransformComponentGeometry primary = new(x, y, size, size, true, true);
        if (chromaFormat == 0 || separateColorPlane)
        {
            return new HevcTransformUnitGeometry(log2Size, primaryPlane, primary, default, default, false);
        }

        int subsamplingX = chromaFormat is 1 or 2 ? 1 : 0;
        int subsamplingY = chromaFormat == 1 ? 1 : 0;
        HevcTransformComponentGeometry chroma = new(
            x >> subsamplingX,
            y >> subsamplingY,
            size >> subsamplingX,
            size >> subsamplingY,
            true,
            true);

        return new HevcTransformUnitGeometry(log2Size, primaryPlane, primary, chroma, chroma, true);
    }

    /// <summary>
    /// Creates one of the four Z-ordered child transform nodes.
    /// </summary>
    /// <param name="section">The child section from zero through three.</param>
    /// <returns>The selected child geometry.</returns>
    public HevcTransformUnitGeometry CreateChild(int section)
        => new(
            this.Log2LumaSize - 1,
            this.PrimaryPlane,
            SplitComponent(this.Primary, section),
            SplitComponent(this.ChromaBlue, section),
            SplitComponent(this.ChromaRed, section),
            this.HasCombinedChroma);

    /// <summary>
    /// Splits one component rectangle while retaining sub-minimum chroma at the owning parent level.
    /// </summary>
    /// <param name="parent">The parent component rectangle.</param>
    /// <param name="section">The luma child section from zero through three.</param>
    /// <returns>The component rectangle visible from the selected child.</returns>
    private static HevcTransformComponentGeometry SplitComponent(HevcTransformComponentGeometry parent, int section)
    {
        if (!parent.Process || parent.Width == 0)
        {
            return default;
        }

        int width = parent.Width >> 1;
        int height = parent.Height >> 1;
        int sampleCount = width * height;
        if ((width < 4 || height < 4) && sampleCount < 16)
        {
            // A component transform cannot be smaller than four by four. Its parent rectangle is associated with
            // the final luma quadrant so CBF and coefficient syntax are consumed exactly once.
            return new HevcTransformComponentGeometry(parent.X, parent.Y, parent.Width, parent.Height, section == 3, false);
        }

        if (width < 4)
        {
            width = 4;
            height = sampleCount / width;
        }
        else if (height < 4)
        {
            height = 4;
            width = sampleCount / height;
        }

        int columns = parent.Width / width;
        int x = parent.X + ((section % columns) * width);
        int y = parent.Y + ((section / columns) * height);
        return new HevcTransformComponentGeometry(x, y, width, height, true, true);
    }
}
