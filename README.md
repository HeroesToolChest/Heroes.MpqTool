# Heroes Mpq Tool
[![Build and Test](https://github.com/HeroesToolChest/Heroes.MpqTool/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/HeroesToolChest/Heroes.MpqTool/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/release/HeroesToolChest/Heroes.MpqTool.svg)](https://github.com/HeroesToolChest/Heroes.MpqTool/releases/latest) 
[![NuGet](https://img.shields.io/nuget/v/Heroes.MpqTool.svg)](https://www.nuget.org/packages/Heroes.MpqTool/)

Heroes Mpq Tool is a .NET library that is specifically designed for parsing Heroes of the Storm MPQ files, such as `.StormReplay` replay files and `.s2ma` map files. It opens an archive, finds the files inside it, and decompresses them.

The library is read-only. It does not create or modify archives.

## Usage
An archive is opened with `MpqHeroesFile.Open`, which returns a `MpqHeroesArchive`. The archive gives access to the entries (files) inside of it. Dispose the archive when it is no longer needed.

### Opening from a file path
Use `MpqHeroesFile.Open(string path)` by providing the path to the file.

```C#
using Heroes.MpqTool;

using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open("path/to/file.StormReplay");

// get an entry, such as replay.initData for a replay file
if (mpqHeroesArchive.TryGetEntry("replay.initData", out MpqHeroesArchiveEntry? mpqHeroesArchiveEntry))
{
    // decompress the entry and do something with it
    using Stream stream = mpqHeroesArchive.DecompressEntry(mpqHeroesArchiveEntry.Value);
}
```

The file is opened as read-only and is closed when the archive is disposed.

### Opening from a stream
Use `MpqHeroesFile.Open(Stream stream)` to control how the file is opened, such as choosing its `FileShare` mode or other `FileStream` options.

- The stream must support seeking.
- The archive is read from the stream's **current position**, not from the beginning of the stream.
- The archive takes ownership of the stream and disposes it when the archive is disposed.

```C#
// FileShare.ReadWrite lets the file be opened even if another program has it open for writing
FileStream fileStream = new("path/to/file.StormReplay", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(fileStream); // also disposes fileStream

MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry("replay.details");
using Stream detailsStream = mpqHeroesArchive.DecompressEntry(entry);
```

Since the archive is read from the current position, an archive that starts partway through a file can also be opened:

```C#
FileStream fileStream = File.OpenRead("path/to/container.bin");
fileStream.Position = archiveOffset; // where the MPQ data starts

using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(fileStream);
```

### Finding entries
Entry names are case-insensitive. Paths inside an MPQ use backslashes (`\`). Pass `normalizeSlashes: true` to look up a path written with forward slashes (`/`).

```C#
// throws a FileNotFoundException if the entry does not exist
MpqHeroesArchiveEntry detailsEntry = mpqHeroesArchive.GetEntry("replay.details");

// returns false if the entry does not exist
if (mpqHeroesArchive.TryGetEntry("replay.initData", out MpqHeroesArchiveEntry? initDataEntry))
{
    // ...
}

// only checks if the entry exists
bool exists = mpqHeroesArchive.FileEntryExists("replay.tracker.events");

// in a map file, forward slashes are converted to backslashes before the lookup
MpqHeroesArchiveEntry waterDataEntry = mpqHeroesArchive.GetEntry("Base.StormData/GameData/WaterData.xml", normalizeSlashes: true);
```

Each lookup method also has an overload that takes a `ReadOnlySpan<char>`.

### Decompressing entries
`DecompressEntry` returns a read-only `MemoryStream` of the entry's contents.

```C#
using Stream stream = mpqHeroesArchive.DecompressEntry(entry);
```

To avoid allocating a new array for every entry, decompress into an existing buffer instead. The buffer must be at least `FileSize` bytes.

```C#
byte[] buffer = ArrayPool<byte>.Shared.Rent((int)entry.FileSize);

try
{
    Span<byte> data = buffer.AsSpan(0, (int)entry.FileSize);
    mpqHeroesArchive.DecompressEntry(entry, data);

    // use data
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### Listing entries
`MpqArchiveEntries` contains every entry in the archive. MPQ archives store hashes of the names rather than the names themselves, so `FileName` is only set for names found in the archive's `(listfile)`. Otherwise it is `null`.

```C#
foreach (MpqHeroesArchiveEntry entry in mpqHeroesArchive.MpqArchiveEntries)
{
    if (!entry.Exists)
        continue; // a deleted file

    Console.WriteLine($"{entry.FileName ?? "(unknown)"}: {entry.CompressedSize} -> {entry.FileSize} bytes");
}
```

## BZip2 decompression
The BZip2 decompression was originally from [DotNetZip](https://github.com/DinoChiesa/DotNetZip-2025), but has been heavily modified for MPQ files only and optimized for performance.

## Developing
To build and compile the code, it is recommended to use the latest version of [Visual Studio 2026 or Visual Studio Code](https://visualstudio.microsoft.com/downloads/).

Another option is to use the dotnet CLI tools from the latest [.NET SDK](https://dotnet.microsoft.com/download).

## License
[MIT license](/LICENSE)
