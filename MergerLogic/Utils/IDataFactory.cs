﻿using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IDataFactory
    {
        IData CreateDataSource(string type, string path, int batchSize, bool? isOneXOne = null, GridOrigin? origin = null, Extent? extent = null, bool isBase = false);
        IData CreateDataSource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, bool? isOneXone = null, GridOrigin? origin = null);
    }
}
