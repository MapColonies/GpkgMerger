﻿using MergerLogic.Batching;
using MergerLogic.Monitoring.Metrics;

namespace MergerLogic.DataTypes
{
    public class WMTS : HttpDataSource
    {
        public WMTS(IServiceProvider container, IMetricsProvider metricsProvider,
            string path, int batchSize, Extent extent, Grid? grid, GridOrigin? tileGridOrigin, int maxZoom, int minZoom = 0)
            : base(container, metricsProvider, DataType.WMTS, path, batchSize, extent, tileGridOrigin, grid, maxZoom, minZoom)
        {
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.UPPER_LEFT;
        }
    }
}
