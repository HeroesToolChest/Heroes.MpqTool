﻿namespace Heroes.MpqTool;

/// <summary>
/// Contains methods to open a (Heroes of the Storm) mpq file.
/// </summary>
public static class MpqHeroesFile
{
    /// <summary>
    /// Opens an mpq file.
    /// </summary>
    /// <param name="fileName">The file name or path to the mpq file.</param>
    /// <returns>An <see cref="MpqHeroesArchive"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> cannot be <see langword="null"/> or empty.</exception>
    public static MpqHeroesArchive Open(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        FileStream fileStream = new(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, false);

        try
        {
            return new MpqHeroesArchive(fileStream);
        }
        catch
        {
            fileStream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens an mpq file.
    /// </summary>
    /// <param name="stream">A <see cref="Stream"/> of a mpq file.</param>
    /// <returns>An <see cref="MpqHeroesArchive"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static MpqHeroesArchive Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new MpqHeroesArchive(stream);
    }
}
