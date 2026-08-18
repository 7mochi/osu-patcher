using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Osu.Stubs.Tests;

[PublicAPI]
public static class OsuApi
{
    [PublicAPI]
    public enum ReleaseStream
    {
        CuttingEdge,
        Stable40,
        Beta40,
    }

    private static readonly HttpClient Http = new();

    /// <summary>
    ///     Gets the latest release files for a specific release stream.
    /// </summary>
    public static async Task<List<OsuUpdateFile>> GetReleaseFiles(ReleaseStream stream)
    {
        Console.WriteLine("Fetching latest osu! update info");

        var url = $"https://osu.ppy.sh/web/check-updates.php" +
                  $"?action=check" +
                  $"&stream={stream.ToString().ToLower()}" +
                  $"&time={DateTime.Now.Ticks}";

        using var response = await Http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var bodyText = await response.Content.ReadAsStringAsync();
        if (bodyText == null) throw new Exception("Response returned no body");

        var releaseFiles = JsonConvert.DeserializeObject<List<OsuUpdateFile>>(bodyText);
        if (releaseFiles == null) throw new Exception("Failed to deserialize update files");

        return releaseFiles;
    }

    /// <summary>
    ///     Downloads the full osu! update file list to a specific directory.
    /// </summary>
    /// <param name="dir">An empty directory.</param>
    /// <param name="stream">The release stream to download.</param>
    public static async Task DownloadOsu(string dir, ReleaseStream stream = ReleaseStream.Stable40)
    {
        var updateFiles = await GetReleaseFiles(stream);

        Parallel.ForEach(updateFiles, updateFile =>
        {
            Console.WriteLine($"Downloading {updateFile.FileName}");
            DownloadFile(updateFile.DownloadUrl, Path.Combine(dir, updateFile.FileName)).Wait();
        });

        Console.WriteLine("Finished downloading osu!");
    }

    /// <summary>
    ///     Ensures the osu! directory matches the latest release files of the given stream,
    ///     only re-downloading files whose hash differs from the local copy (or that are missing).
    /// </summary>
    /// <param name="dir">The directory to hold the osu! files.</param>
    /// <param name="stream">The release stream to keep up-to-date.</param>
    public static async Task RefreshOsu(string dir, ReleaseStream stream = ReleaseStream.Stable40)
    {
        var updateFiles = await GetReleaseFiles(stream);

        Directory.CreateDirectory(dir);

        Parallel.ForEach(updateFiles, updateFile =>
        {
            var path = Path.Combine(dir, updateFile.FileName);

            if (File.Exists(path) && ComputeMd5(path).Equals(updateFile.FileHash, StringComparison.OrdinalIgnoreCase))
                return;

            Console.WriteLine($"Downloading {updateFile.FileName}");
            DownloadFile(updateFile.DownloadUrl, path).Wait();
        });

        Console.WriteLine("Finished refreshing osu!");
    }

    private static string ComputeMd5(string path)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        using var stream = File.OpenRead(path);
        return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
    }

    private static async Task DownloadFile(string url, string path)
    {
        using var response = await Http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var bodyStream = await response.Content.ReadAsStreamAsync();
        if (bodyStream == null) throw new Exception("Response returned no body");

        using var file = File.Create(path);
        await bodyStream.CopyToAsync(file);
    }
}