using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergerCli.Utils
{
    internal class BatchStatusManager
    {
        internal class LayerStatus
        {
            public string? BatchIdentifier { get; set; }
            public bool IsDone { get; set; }
            
            public ConcurrentDictionary<string, bool> Batches { get; set; }

            public LayerStatus()
            {
                this.BatchIdentifier = null;
                this.IsDone = false;
                this.Batches = new ConcurrentDictionary<string, bool>();
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

        public void SetCurrentBatch(string layer, string batchIdentifier)
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
        public void AssignBatch(string layer, string batchIdentifier)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer))
                {
                    this.States[layer].Batches.TryAdd(batchIdentifier, false);
                }
            }
        }

        public KeyValuePair<string, bool>? GetFirstInCompletedBatch(string layer)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer))
                {
                    var inCompletedBatch = this.States[layer].Batches.First(kvp => kvp.Value == false);
                    this.States[layer].Batches.TryUpdate(inCompletedBatch.Key, true, false);
                    return inCompletedBatch;
                }
            }
            return null;
        }

        public void CompleteBatch(string layer, string batchIdentifier)
        {
            lock (_locker)
            {
                if (this.States.ContainsKey(layer))
                {
                    var outValue = true;
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
            this.States[layer].IsDone = true;
            this.BaseLayer.IsNew = false;
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

        public void ResetBatchStatus()
        {
            foreach(string layerKey in this.States.Keys)
            {
                foreach (string batchKey in this.States[layerKey].Batches.Keys)
                {
                    this.States[layerKey].Batches.TryUpdate(batchKey, false, true);
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
