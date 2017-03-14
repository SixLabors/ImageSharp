#!/bin/bash

# Build in release mode
dotnet build -c Release -f netcoreapp1.1

# Run benchmarks
dotnet bin/Release/netcoreapp1.1/ImageSharp.Benchmarks.dll