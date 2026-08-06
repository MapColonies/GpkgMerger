using MergerLogic.Batching;
using MergerLogic.ImageProcessing;
using System.IO.Abstractions;
using MergerLogic.Utils;

namespace MergerLogic.Clients;

public class FileClient : DataUtils, IFileClient
{
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
        // Probe the known extensions directly instead of globbing the directory: File.Exists returns
        // false for a missing tile or missing directory, so no DirectoryNotFound handling is needed.
        foreach (TileFormat format in new[] { TileFormat.Jpeg, TileFormat.Png })
        {
            string candidate = this._fileSystem.Path.Combine(
                this.path, z.ToString(), x.ToString(), $"{y}.{format.ToString().ToLower()}");
            if (this._fileSystem.File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
