using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace MergerCli.Utils
{
    internal class BatchStatusManager
    {
        internal class LayerStatus
        {
            public string? BatchIdentifier { get; set; }
            public bool IsDone { get; set; }
            public long TotalCompletedTiles { get; set; }
            
            public ConcurrentDictionary<string, bool> Batches { get; set; }

            public LayerStatus()
            {
                this.BatchIdentifier = null;
                this.IsDone = false;
                this.Batches = new ConcurrentDictionary<string, bool>();
                this.TotalCompletedTiles = 0;
            }
        }

        internal class BaseLayerStatus
        {
            public bool IsNew { get; set; }

            public BaseLayerStatus()
            {
                this.IsNew = false;
            }
        }

        [JsonInclude]
        public BaseLayerStatus BaseLayer { get; private set; }

        [JsonInclude]
        public Dictionary<string, LayerStatus> States { get; private set; }

        [JsonInclude]
        public string[] Command { get; private set; }

        static readonly object _locker = new object();

        public BatchStatusManager(string[] command)
        {
            this.BaseLayer = new BaseLayerStatus();
            this.States = new Dictionary<string, LayerStatus>();
            this.Command = command;
        }

        public void SetCurrentBatch(string layer, string? batchIdentifier)
        {
            lock (_locker)
            {
                this.States[layer].BatchIdentifier = batchIdentifier;
            }
        }
        
        public ConcurrentDictionary<string, bool>? GetBatches(string layer)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer))
                {
                    return this.States[layer].Batches;
                }

                return null;
            }
        }
        public void AssignBatch(string layer, string? batchIdentifier)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer) && batchIdentifier != null)
                {
                    this.States[layer].Batches.TryAdd(batchIdentifier, true);
                }
            }
        }

        public KeyValuePair<string, bool>? GetFirstInCompletedBatch(string layer)
        {
            lock (_locker)
            {
                try
                {
                    if (this.States.ContainsKey(layer))
                    {
                        if (!this.States[layer].Batches.IsEmpty)
                        {
                            KeyValuePair<string, bool>? inCompletedBatch = null;
                            inCompletedBatch = this.States[layer].Batches.FirstOrDefault(kvp => kvp.Value == false);
                            if (inCompletedBatch.Value.Key is not null)
                            {
                                this.States[layer].Batches.TryUpdate(inCompletedBatch.Value.Key, true, false);
                                return inCompletedBatch;
                            }
                            return null;
                        }
                    }
                    return null;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception: {e}");
                    return null;
                }
            }
        }

        public void CompleteBatch(string layer, string batchIdentifier, long totalCompletedTiles)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer) && batchIdentifier != null)
                {
                    var outValue = true;
                    this.States[layer].TotalCompletedTiles = totalCompletedTiles;
                    this.States[layer].Batches.Remove(batchIdentifier, out outValue );
                }
            }
        }

        public void InitializeLayer(string layer)
        {
            lock (_locker)
            {
                if (!this.States.ContainsKey(layer))
                {
                    this.States.Add(layer, new LayerStatus());
                }
            }
        }

        public void CompleteLayer(string layer)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer))
                {
                    this.States[layer].IsDone = true;
                    this.BaseLayer.IsNew = false;
                }
            }
        }

        public bool IsLayerCompleted(string layer)
        {
            return this.States.ContainsKey(layer) && this.States[layer].IsDone;
        }

        public bool IsBaseLayerNew()
        {
            return this.BaseLayer.IsNew;
        }

        public string? GetLayerBatchIdentifier(string layer)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer))
                {
                    return this.States[layer].BatchIdentifier;
                }
                return null;
            }
        }

        public long? GetTotalCompletedTiles(string layer)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer))
                {
                    return this.States[layer].TotalCompletedTiles;
                }
                return null;
            }
        }

        public void ResetBatchStatus()
        {
            foreach(string layer in this.States.Keys)
            {
                foreach (string batch in this.States[layer].Batches.Keys)
                {
                    if (batch is not null)
                    {
                        this.States[layer].Batches.TryUpdate(batch, false, true);
                    }
                }
            }
        }
        
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }

        public static BatchStatusManager FromJson(string json)
        {
            BatchStatusManager? batchStatusManager = JsonSerializer.Deserialize<BatchStatusManager>(json);
            if (batchStatusManager == null)
            {
                throw new Exception("invalid batch status manager json");
            }
            return batchStatusManager;
        }
    }
}
