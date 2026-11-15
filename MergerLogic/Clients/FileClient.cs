using MergerLogic.Batching;
using MergerLogic.ImageProcessing;
using System;
using System.IO.Abstractions;
using MergerLogic.Utils;

namespace MergerLogic.Clients;

public class FileClient : DataUtils, IFileClient
{
    // "jpg" is not a TileFormat (it encodes as Jpeg), but folder sources hold .jpg tiles too.
    private static readonly string[] TileExtensions = Enum.GetValues<TileFormat>()
        .Select(format => format.ToString().ToLower())
        .Append("jpg")
        .ToArray();

    private readonly IFileSystem _fileSystem;

    public FileClient(string path, IGeoUtils geoUtils, IFileSystem fileSystem) 
        : base(path, geoUtils)
    {
        this._fileSystem = fileSystem;
    }

    public override Tile? GetTile(int z, int x, int y)
    {
        var tilePath = this.GetTilePath(z, x, y);

        if (tilePath != null)
        {
            byte[] fileBytes = this._fileSystem.File.ReadAllBytes(tilePath);
            return this.CreateTile(z, x, y, fileBytes);
        }
        else
        {
            return null;
        }
    }

    public override bool TileExists(int z, int x, int y)
    {
        return this.GetTilePath(z,x,y) != null;
    }

    private string? GetTilePath(int z, int x, int y)
    {
        // Probe each supported extension via File.Exists (false for a missing dir too, so no glob/catch).
        foreach (string extension in TileExtensions)
        {
            string candidate = this._fileSystem.Path.Combine(
                this.path, z.ToString(), x.ToString(), $"{y}.{extension}");
            if (this._fileSystem.File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
