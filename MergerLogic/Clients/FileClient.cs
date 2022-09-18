using MergerLogic.Batching;
using MergerLogic.ImageProcessing;
using System.IO.Abstractions;
using MergerLogic.Utils;

namespace MergerLogic.Clients;

public class FileClient : DataUtils, IFileClient
{
    private readonly IPathUtils _pathUtils;
    private readonly IFileSystem _fileSystem;

    public FileClient(string path, IPathUtils pathUtils, IGeoUtils geoUtils, IFileSystem fileSystem, IImageFormatter formatter) 
        : base(path, geoUtils, formatter)
    {
        this._pathUtils = pathUtils;
        this._fileSystem = fileSystem;
    }

    public override Tile GetTile(int z, int x, int y)
    {
        string tilePath = this._pathUtils.GetTilePath(this.path, z, x, y, format!.Value);
        this._fileSystem.Directory
            .EnumerateFiles(this.path, $".*", SearchOption.AllDirectories)

        if (this._fileSystem.File.Exists(tilePath))
        {
            byte[] fileBytes = this._fileSystem.File.ReadAllBytes(tilePath);
            return new Tile(z, x, y, fileBytes);
        }
        else
        {
            return null;
        }
    }

    public override bool TileExists(int z, int x, int y)
    {
        string fullPath = this._pathUtils.GetTilePath(this.path, z, x, y, format!.Value);
        return this._fileSystem.File.Exists(fullPath);
    }
}
