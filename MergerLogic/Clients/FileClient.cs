using MergerLogic.Batching;
using System.IO.Abstractions;
using MergerLogic.Utils;
using MergerLogic.ImageProcessing;

namespace MergerLogic.Clients;

public class FileClient : DataUtils, IFileClient
{
    private readonly IFileSystem _fileSystem;

    public FileClient(string path, IGeoUtils geoUtils, IFileSystem fileSystem)
        : base(path, geoUtils)
    {
        this._fileSystem = fileSystem;
    }

    public override Tile? GetTile(int z, int x, int y, TileFormat? format)
    {
        if (format is null)
        {
            throw new ArgumentNullException(nameof(format));
        }

        var tilePath = this.GetTilePath(z, x, y, format.Value);

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

    public override bool TileExists(int z, int x, int y, TileFormat? format)
    {
        if (format is null)
        {
            throw new ArgumentNullException(nameof(format));
        }
        return this.GetTilePath(z, x, y, format.Value) != null;
    }

    private string? GetTilePath(int z, int x, int y, TileFormat format)
    {
        var tilePath = this._fileSystem.Path.Join(z.ToString(), x.ToString(), y.ToString());
        string fullPath = this._fileSystem.Path.Join(this.path, tilePath, ".", format.ToString().ToLower());
        if (this._fileSystem.File.Exists(fullPath))
        {
            return fullPath;
        }
        return null;
    }
}
