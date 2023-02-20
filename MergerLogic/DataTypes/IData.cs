﻿using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public interface IData
    {
        public DataType Type { get; }
        public string Path { get; }
        public bool IsNew { get; }

        bool Exists();
        Tile? GetCorrespondingTile(Coord coords, bool upscale);
        List<Tile> GetNextBatch(out string batchIdentifier, out string? nextBatchIdentifier, long? totalTilesCount);
        void Reset();
        void setBatchIdentifier(string batchIdentifier);
        void markAsNew();
        void markAsNotNew();
        long TileCount();
        bool TileExists(Coord coord);
        bool TileExists(Tile tile);
        void UpdateTiles(IEnumerable<Tile> tiles);
        void Wrapup();
    }
}
