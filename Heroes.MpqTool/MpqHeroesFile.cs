namespace Heroes.MpqTool;

/// <summary>
/// Contains methods to open a (Heroes of the Storm) mpq file.
/// </summary>
public static class MpqHeroesFile
{
    /// <summary>
    /// Opens an MPQ file.
    /// </summary>
    /// <param name="path">The path to the MPQ file.</param>
    /// <returns>An <see cref="MpqHeroesArchive"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> cannot be <see langword="null"/> or empty.</exception>
    public static MpqHeroesArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FileStream? fileStream = null;

        try
        {
            fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, false);
            return new MpqHeroesArchive(fileStream);
        }
        catch
        {
            fileStream?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens an MPQ file.
    /// </summary>
    /// <param name="stream">A <see cref="Stream"/> of a MPQ file.</param>
    /// <returns>An <see cref="MpqHeroesArchive"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public static MpqHeroesArchive Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return new MpqHeroesArchive(stream);
    }
}
