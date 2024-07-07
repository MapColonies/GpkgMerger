using MergerLogic.Batching;
using System.IO.Abstractions;
using MergerLogic.Utils;

namespace MergerLogic.Clients;

public class FileClient : DataUtils, IFileClient
{
    private readonly IFileSystem _fileSystem;

    public FileClient(string path, IGeoUtils geoUtils, IFileSystem fileSystem, IConfigurationManager configuration)
        : base(path, geoUtils, configuration)
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
        return this.GetTilePath(z, x, y) != null;
    }

    private string? GetTilePath(int z, int x, int y)
    {
        var tilePath = this._fileSystem.Path.Join(z.ToString(), x.ToString(), y.ToString());
        try
        {
            //this may or may not be faster then checking specific files of every supported type depending on the used file system
            return this._fileSystem.Directory
                .EnumerateFiles(this.path, $"{tilePath}.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
