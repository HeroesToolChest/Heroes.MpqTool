# Heroes Mpq Tool
[![Build and Test](https://github.com/HeroesToolChest/Heroes.MpqTool/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/HeroesToolChest/Heroes.MpqTool/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/release/HeroesToolChest/Heroes.MpqTool.svg)](https://github.com/HeroesToolChest/Heroes.MpqTool/releases/latest) 
[![NuGet](https://img.shields.io/nuget/v/Heroes.MpqTool.svg)](https://www.nuget.org/packages/Heroes.MpqTool/)

Heroes Mpq Tool is a .NET library that is specifically for parsing Heroes of the Storm MPQ files.

## Usage
To parse an mpq file, such as a `.StormReplay` file, use `MpqHeroesFile.Open(string fileName)` by providing the path to the file. It will provide a `MpqHeroesArchive` object to allow access to the files inside of the archive.

Example:
```C#
// parse the mpq file
using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open("path/to/file");

// get an entry, such as replay.initData for a replay file
if (mpqHeroesArchive.TryGetEntry("replay.initData", out MpqHeroesArchiveEntry? mpqHeroesArchiveEntry))
{
    // decompress the entry and do something with it
    using Stream stream = mpqHeroesArchive.DecompressEntry(mpqHeroesArchiveEntry.Value);
}
```
## Developing
To build and compile the code, it is recommended to use the latest version of [Visual Studio 2022 or Visual Studio Code](https://visualstudio.microsoft.com/downloads/).

Another option is to use the dotnet CLI tools from the [.NET Core 8.0 SDK](https://dotnet.microsoft.com/download).

## License
[MIT license](/LICENSE)