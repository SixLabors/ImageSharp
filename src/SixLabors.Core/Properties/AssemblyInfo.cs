// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("SixLabors.Core")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Six Labors")]
[assembly: AssemblyProduct("SixLabors.Core")]
[assembly: AssemblyCopyright("Copyright (c) Six Labors and contributors.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: NeutralResourcesLanguage("en")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.1.0")]
[assembly: AssemblyFileVersion("0.1.0")]
[assembly: AssemblyInformationalVersion("0.1.0-alpha02")]

// Ensure the internals can be tested.
[assembly: InternalsVisibleTo("SixLabors.Core.Tests")]

// Ensure the internals are visible to the other projects.
[assembly: InternalsVisibleTo("SixLabors.Exif")]
[assembly: InternalsVisibleTo("SixLabors.Fonts")]
[assembly: InternalsVisibleTo("SixLabors.ImageSharp")]
[assembly: InternalsVisibleTo("SixLabors.ImageSharp.Drawing")]
[assembly: InternalsVisibleTo("SixLabors.Shapes")]
