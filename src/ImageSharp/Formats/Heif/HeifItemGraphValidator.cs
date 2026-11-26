// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Validates the item graph of a HEIF still image and creates its decode plan.
/// </summary>
/// <remarks>
/// The validator runs one time, after the parser reads the metadata boxes and before the decoder allocates the image. The validator owns the rules for the item
/// references, the alpha items and the clean aperture. It also validates that the tile types and the AV1 codec configurations agree. A structural error of the
/// color image throws. The alpha plane is optional image data. Thus the image data mode controls an error of the alpha plane. When the mode ignores that error,
/// the decoder decodes the image without alpha.
/// </remarks>
internal static class HeifItemGraphValidator
{
    /// <summary>
    /// Validates the item graph of the primary image and creates its decode plan.
    /// </summary>
    /// <param name="items">The declared items, indexed by item identifier.</param>
    /// <param name="links">The item references.</param>
    /// <param name="primaryItemId">The identifier of the primary item.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns>The decode plan of the primary image.</returns>
    /// <exception cref="ImageFormatException">The file has no primary item, or its coding format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The item graph of the color image is not valid.</exception>
    public static HeifDecodePlan Validate(Dictionary<uint, HeifItem> items, List<HeifItemLink> links, uint primaryItemId, SegmentIntegrityHandling handling)
    {
        HeifItem primary = items.GetValueOrDefault(primaryItemId) ?? throw new ImageFormatException("No primary HEIF item found.");

        // HEIF requires an image spatial extents property for every image item. The property parser rejects zero dimensions. Thus an empty extent means that
        // the item has no such property.
        if (primary.Extent == default)
        {
            throw new InvalidImageContentException($"Image item {primary.Id} has no image spatial extents property.");
        }

        // ToRectangle throws when the clean-aperture fractions do not give an integer rectangle inside the extents.
        Rectangle sourceRectangle = primary.CleanAperture?.ToRectangle(primary.Extent) ?? new Rectangle(Point.Empty, primary.Extent);
        HeifItem[] tiles = [];
        bool[] tilePremultiplied = [];
        HeifItem codedItem = primary;
        if (primary.Type == Heif4CharCode.Grid)
        {
            string? tileError = GetDerivedTiles(items, links, primary, out tiles);
            if (tileError is not null)
            {
                throw new InvalidImageContentException(tileError);
            }

            codedItem = tiles[0];
            tilePremultiplied = new bool[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                tilePremultiplied[i] = IsPremultipliedByAlpha(items, links, tiles[i]);
            }
        }

        if (codedItem.Type != Heif4CharCode.Av01)
        {
            throw new ImageFormatException($"The primary HEIF item uses unsupported item type '{codedItem.Type}'.");
        }

        string? error = primary.Type == Heif4CharCode.Grid ? ValidateCodedItems(tiles) : ValidateCodedItems([primary]);
        if (error is not null)
        {
            throw new InvalidImageContentException(error);
        }

        // The alpha plane is optional image data. Thus each alpha validation returns its error, and only DropAlpha reports it.
        HeifItem[] alphaTiles = [];
        bool premultiplied = false;
        error = FindAlphaItem(items, links, primary, handling, out HeifItem? alphaItem);
        if (error is null && alphaItem is not null)
        {
            error = ValidateAlphaItem(items, links, primary, alphaItem, out alphaTiles);
            premultiplied = HasPremultipliedLink(links, primary.Id, alphaItem.Id);
        }
        else if (error is null && primary.Type == Heif4CharCode.Grid)
        {
            // A grid can link one alpha item to each coded tile instead of to the derived image. Then the alpha tiles use the row and column layout of the
            // color grid descriptor.
            error = FindGridAlphaTiles(items, links, tiles, handling, out alphaTiles);
            if (error is null && alphaTiles.Length > 0)
            {
                alphaItem = primary;
                error = ValidateAlphaCodedItems(alphaTiles, true);
            }
        }

        HeifDecodePlan plan = new(primary, codedItem, tiles, tilePremultiplied, sourceRectangle, alphaItem, alphaTiles, premultiplied);
        return error is null ? plan : DropAlpha(plan, error, handling);
    }

    /// <summary>
    /// Validates the grid descriptor of an alpha grid against its alpha tiles.
    /// </summary>
    /// <remarks>
    /// The grid descriptor is item data. Thus the decoder calls this method after it validates the item extents and before it decodes the alpha plane. For
    /// alpha tiles of a color grid, the alpha item is the color grid. Then the descriptor of the color grid gives the alpha layout.
    /// </remarks>
    /// <param name="plan">The decode plan from <see cref="Validate"/>.</param>
    /// <param name="itemDataReader">Reads one item payload whose extents the decoder validated.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns>The decode plan. It has no alpha when the alpha grid is not valid and the mode ignores image data errors.</returns>
    public static HeifDecodePlan ValidateAlphaGrid(HeifDecodePlan plan, Func<HeifItem, IMemoryOwner<byte>> itemDataReader, SegmentIntegrityHandling handling)
    {
        if (plan.AlphaTiles.Length == 0)
        {
            return plan;
        }

        // Validate sets the alpha item when the plan has alpha tiles.
        using IMemoryOwner<byte> descriptorData = itemDataReader(plan.AlphaItem!);
        string? error = HeifGridDescriptor.Parse(descriptorData.GetSpan(), out HeifGridDescriptor descriptor) ?? descriptor.Validate(plan.AlphaTiles);
        return error is null ? plan : DropAlpha(plan, error, handling);
    }

    /// <summary>
    /// Reports an error of the alpha plane and gives the decode plan without alpha.
    /// </summary>
    /// <remarks>
    /// This method is the only report site of an alpha error. The color image can decode without the optional alpha plane. Thus the image data mode controls
    /// the error. The Strict and IgnoreAncillary modes throw.
    /// </remarks>
    /// <param name="plan">The decode plan with alpha.</param>
    /// <param name="error">The message of the alpha error.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <returns>The decode plan without alpha.</returns>
    private static HeifDecodePlan DropAlpha(HeifDecodePlan plan, string error, SegmentIntegrityHandling handling)
    {
        handling.ThrowOrIgnoreImageDataError(error);
        return new HeifDecodePlan(plan.Primary, plan.CodedItem, plan.Tiles, plan.TilePremultiplied, plan.SourceRectangle, null, [], false);
    }

    /// <summary>
    /// Gets the derived-image tiles of a grid item in row-major order.
    /// </summary>
    /// <param name="items">The declared items.</param>
    /// <param name="links">The item references.</param>
    /// <param name="gridItem">The grid item.</param>
    /// <param name="tiles">Receives the tiles. The array is not empty when the method returns no error.</param>
    /// <returns>The error message, or <see langword="null"/> when the grid has declared tiles.</returns>
    private static string? GetDerivedTiles(Dictionary<uint, HeifItem> items, List<HeifItemLink> links, HeifItem gridItem, out HeifItem[] tiles)
    {
        tiles = [];
        List<HeifItem> derivedTiles = [];
        foreach (HeifItemLink link in links)
        {
            if (link.Type != Heif4CharCode.Dimg || link.SourceId != gridItem.Id)
            {
                continue;
            }

            // The order of the dimg references is the normative row-major order of the grid cells.
            foreach (uint id in link.DestinationIds)
            {
                HeifItem? tile = items.GetValueOrDefault(id);
                if (tile is null)
                {
                    return $"HEIF item reference targets undeclared item {id}.";
                }

                derivedTiles.Add(tile);
            }
        }

        if (derivedTiles.Count == 0)
        {
            return $"HEIF image grid {gridItem.Id} has no tiles.";
        }

        tiles = [.. derivedTiles];
        return null;
    }

    /// <summary>
    /// Validates the AV1 codec configuration of one coded item or of the tiles of one grid.
    /// </summary>
    /// <param name="codedItems">The coded items. The caller validated that the first item is an AV1 item.</param>
    /// <returns>The error message, or <see langword="null"/> when the items are valid.</returns>
    private static string? ValidateCodedItems(ReadOnlySpan<HeifItem> codedItems)
    {
        Av1CodecConfiguration? firstConfiguration = null;
        foreach (HeifItem item in codedItems)
        {
            if (item.Type != Heif4CharCode.Av01)
            {
                return "All HEIF image grid tiles must use the same coding format.";
            }

            Av1CodecConfiguration? configuration = item.Av1CodecConfiguration;
            if (configuration is null)
            {
                return $"AV1 image item {item.Id} has no codec configuration property.";
            }

            // All grid cells use one output sample layout. Thus the channel precision and the chroma geometry cannot change between cells.
            if (firstConfiguration is null)
            {
                firstConfiguration = configuration;
            }
            else if (!firstConfiguration.HasMatchingImageConfiguration(configuration))
            {
                return "All AV1 image grid tiles must use matching codec configurations.";
            }

            if (item.ChannelBitDepths is not null)
            {
                foreach (byte channelBitDepth in item.ChannelBitDepths)
                {
                    if (channelBitDepth != (byte)configuration.BitDepth)
                    {
                        return $"AV1 image item {item.Id} has mismatched pixel-information and codec-configuration bit depths.";
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Validates an alpha item that is linked to the primary item.
    /// </summary>
    /// <param name="items">The declared items.</param>
    /// <param name="links">The item references.</param>
    /// <param name="colorItem">The primary color item.</param>
    /// <param name="alphaItem">The alpha item.</param>
    /// <param name="alphaTiles">Receives the tiles of an alpha grid, or an empty array.</param>
    /// <returns>The error message, or <see langword="null"/> when the alpha item is valid.</returns>
    private static string? ValidateAlphaItem(
        Dictionary<uint, HeifItem> items,
        List<HeifItemLink> links,
        HeifItem colorItem,
        HeifItem alphaItem,
        out HeifItem[] alphaTiles)
    {
        alphaTiles = [];

        // Some older encoders did not associate the transform properties with the alpha item. For compatibility, the validator accepts an alpha item without
        // transforms. If the alpha item has a transform, all of its transforms must match the transforms of the color item.
        bool alphaHasTransforms = alphaItem.CleanAperture is not null || alphaItem.RotationAngle is not null || alphaItem.MirrorAxis is not null;
        if (alphaHasTransforms
            && (!Nullable.Equals(alphaItem.CleanAperture, colorItem.CleanAperture)
                || alphaItem.RotationAngle != colorItem.RotationAngle
                || alphaItem.MirrorAxis != colorItem.MirrorAxis))
        {
            return "The alpha auxiliary image and color image use different presentation transforms.";
        }

        if (alphaItem.Type == Heif4CharCode.Grid)
        {
            return GetDerivedTiles(items, links, alphaItem, out alphaTiles) ?? ValidateAlphaCodedItems(alphaTiles, true);
        }

        return ValidateAlphaCodedItems([alphaItem], false);
    }

    /// <summary>
    /// Validates the coded items of an alpha plane.
    /// </summary>
    /// <param name="codedItems">The coded alpha item or the alpha tiles.</param>
    /// <param name="isGrid"><see langword="true"/> if the items are the tiles of an alpha grid.</param>
    /// <returns>The error message, or <see langword="null"/> when the items are valid.</returns>
    private static string? ValidateAlphaCodedItems(ReadOnlySpan<HeifItem> codedItems, bool isGrid)
    {
        HeifItem first = codedItems[0];
        if (first.Type != Heif4CharCode.Av01)
        {
            return $"The alpha auxiliary item uses unsupported item type '{first.Type}'.";
        }

        string? error = ValidateCodedItems(codedItems);
        if (error is not null)
        {
            return error;
        }

        // The decoder reads the alpha plane from the luma plane of a monochrome AV1 sequence. ValidateCodedItems validated that every item has a codec
        // configuration and that the configurations match.
        if (!first.Av1CodecConfiguration!.IsMonochrome)
        {
            return $"AV1 alpha image item {first.Id} is not monochrome.";
        }

        // The grid compositor sizes each alpha tile from its spatial extents.
        if (isGrid)
        {
            foreach (HeifItem item in codedItems)
            {
                if (item.Extent == default)
                {
                    return $"HEIF alpha grid tile {item.Id} has no spatial extent.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the alpha auxiliary item that is linked to a color item.
    /// </summary>
    /// <param name="items">The declared items.</param>
    /// <param name="links">The item references.</param>
    /// <param name="colorItem">The color item.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <param name="alphaItem">Receives the alpha item, or <see langword="null"/> when no alpha relationship exists.</param>
    /// <returns>The error message when the color item has more than one alpha item, or <see langword="null"/>.</returns>
    private static string? FindAlphaItem(
        Dictionary<uint, HeifItem> items,
        List<HeifItemLink> links,
        HeifItem colorItem,
        SegmentIntegrityHandling handling,
        out HeifItem? alphaItem)
    {
        alphaItem = null;
        foreach (HeifItemLink link in links)
        {
            if (link.Type != Heif4CharCode.Auxl || !link.DestinationIds.Contains(colorItem.Id))
            {
                continue;
            }

            HeifItem? candidate = items.GetValueOrDefault(link.SourceId);
            if (candidate is null)
            {
                handling.ThrowOrIgnoreImageDataError($"The auxiliary image reference names undeclared item {link.SourceId}.");
                continue;
            }

            if (!HeifConstants.IsAlphaAuxiliaryType(candidate.AuxiliaryType))
            {
                continue;
            }

            if (alphaItem is not null && alphaItem.Id != candidate.Id)
            {
                return $"Item {colorItem.Id} has more than one alpha auxiliary image.";
            }

            alphaItem = candidate;
        }

        return null;
    }

    /// <summary>
    /// Finds one alpha auxiliary item for each tile of a color grid.
    /// </summary>
    /// <param name="items">The declared items.</param>
    /// <param name="links">The item references.</param>
    /// <param name="tiles">The row-major color tiles.</param>
    /// <param name="handling">The segment integrity mode.</param>
    /// <param name="alphaTiles">Receives the row-major alpha tiles, or an empty array when a color tile has no alpha item.</param>
    /// <returns>The error message, or <see langword="null"/> when the alpha tiles are valid.</returns>
    private static string? FindGridAlphaTiles(
        Dictionary<uint, HeifItem> items,
        List<HeifItemLink> links,
        HeifItem[] tiles,
        SegmentIntegrityHandling handling,
        out HeifItem[] alphaTiles)
    {
        alphaTiles = [];
        HeifItem[] foundTiles = new HeifItem[tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            string? error = FindAlphaItem(items, links, tiles[i], handling, out HeifItem? alphaTile);
            if (error is not null)
            {
                return error;
            }

            if (alphaTile is null)
            {
                // A partial set of alpha tiles cannot describe an alpha plane for the complete grid. Thus the validator treats the image as opaque and does not
                // mix opaque cells with auxiliary alpha cells.
                return null;
            }

            foreach (HeifItemLink link in links)
            {
                if (link.Type == Heif4CharCode.Dimg && link.DestinationIds.Contains(alphaTile.Id))
                {
                    return $"Alpha auxiliary item {alphaTile.Id} is already a derived-image tile.";
                }
            }

            foundTiles[i] = alphaTile;
        }

        alphaTiles = foundTiles;
        return null;
    }

    /// <summary>
    /// Determines whether a premultiplication reference links a color item to an alpha item.
    /// </summary>
    /// <param name="links">The item references.</param>
    /// <param name="colorItemId">The color item identifier.</param>
    /// <param name="alphaItemId">The alpha item identifier.</param>
    /// <returns><see langword="true"/> when the alpha item premultiplies the color values of the color item.</returns>
    private static bool HasPremultipliedLink(List<HeifItemLink> links, uint colorItemId, uint alphaItemId)
    {
        foreach (HeifItemLink link in links)
        {
            if (link.Type == Heif4CharCode.Prem && link.SourceId == colorItemId && link.DestinationIds.Contains(alphaItemId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a premultiplication reference links a color tile to an alpha item.
    /// </summary>
    /// <param name="items">The declared items.</param>
    /// <param name="links">The item references.</param>
    /// <param name="tile">The color tile.</param>
    /// <returns><see langword="true"/> when an alpha item premultiplies the color values of the tile.</returns>
    private static bool IsPremultipliedByAlpha(Dictionary<uint, HeifItem> items, List<HeifItemLink> links, HeifItem tile)
    {
        foreach (HeifItemLink link in links)
        {
            if (link.Type != Heif4CharCode.Prem || link.SourceId != tile.Id)
            {
                continue;
            }

            foreach (uint id in link.DestinationIds)
            {
                if (HeifConstants.IsAlphaAuxiliaryType(items.GetValueOrDefault(id)?.AuxiliaryType))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
