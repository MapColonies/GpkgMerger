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

            public LayerStatus()
            {
                this.BatchIdentifier = null;
                this.IsDone = false;
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

        public BatchStatusManager(string[] command)
        {
            this.BaseLayer = new BaseLayerStatus();
            this.States = new Dictionary<string, LayerStatus>();
            this.Command = command;
        }

        public void SetCurrentBatch(string layer, string batchIdentifier)
        {
            this.States[layer].BatchIdentifier = batchIdentifier;
        }

        public void InitializeLayer(string layer)
        {
            if (!this.States.ContainsKey(layer))
            {
                this.States.Add(layer, new LayerStatus());
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
            if (this.States.ContainsKey(layer))
            {
                return this.States[layer].BatchIdentifier;
            }
            return null;
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
