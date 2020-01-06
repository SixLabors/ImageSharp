// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Dds.Emums
{
    /// <summary>
    /// Identifies other, less common options for resources.
    /// </summary>
    /// <remarks>
    /// <see cref="Shared" /> and <see cref="SharedKeyedMutex" />
    /// are mutually exclusive flags: either one may be set in the resource creation calls but not both simultaneously.
    /// </remarks>
    [Flags]
    internal enum D3d10ResourceMiscFlags : uint
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Enables an application to call ID3D10Device::GenerateMips on a texture resource.
        /// The resource must be created with the bind flags that specify that the resource is a render target and a
        /// shader resource.
        /// </summary>
        GenerateMips = 0x1,

        /// <summary>
        /// Enables the sharing of resource data between two or more Direct3D devices.
        /// The only resources that can be shared are 2D non-mipmapped textures.
        /// WARP and REF devices do not support shared resources. Attempting to create a resource with this flag on
        /// either a WARP or REF device will cause the create method to return an E_OUTOFMEMORY error code.
        /// </summary>
        Shared = 0x2,

        /// <summary>
        /// Enables an application to create a cube texture from a Texture2DArray that contains 6 textures.
        /// </summary>
        TextureCube = 0x4,

        /// <summary>
        /// Enables the resource created to be synchronized using the IDXGIKeyedMutex::AcquireSync and ReleaseSync APIs.
        /// The following resource creation D3D10 APIs, that all take a D3D10_RESOURCE_MISC_FLAG parameter, have been extended to support the new flag.
        /// ID3D10Device1::CreateTexture1D, ID3D10Device1::CreateTexture2D, ID3D10Device1::CreateTexture3D, ID3D10Device1::CreateBuffer
        /// If any of the listed functions are called with the D3D10_RESOURCE_MISC_SHARED_KEYEDMUTEX flag set, the interface returned can be
        /// queried for an IDXGIKeyedMutex interface, which implements AcquireSync and ReleaseSync APIs to synchronize access to the surface.
        /// The device creating the surface, and any other device opening the surface (using OpenSharedResource) is required to
        /// call IDXGIKeyedMutex::AcquireSync before any rendering commands to the surface, and IDXGIKeyedMutex::ReleaseSync when it is done rendering.
        /// WARP and REF devices do not support shared resources. Attempting to create a resource with this flag on either a WARP or REF device will cause the
        /// create method to return an E_OUTOFMEMORY error code.
        /// </summary>
        SharedKeyedMutex = 0x10,

        /// <summary>
        /// Enables a surface to be used for GDI interoperability. Setting this flag enables rendering on
        /// the surface via IDXGISurface1::GetDC.
        /// </summary>
        GdiCompatible = 0x20
    }
}
