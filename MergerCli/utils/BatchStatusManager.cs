using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergerCli.Utils
{
    internal class BatchStatusManager
    {
        internal class LayerStatus
        {
            public string? BathIdentifier { get; set; }
            public bool IsDone { get; set; }
            public LayerStatus()
            {
                this.BathIdentifier = null;
                this.IsDone = false;
            }
        }

        [JsonInclude]
        public Dictionary<string, LayerStatus> States { get; private set; }

        [JsonInclude]
        public string[] Command { get; private set; }

        public BatchStatusManager(string[] command)
        {
            this.States = new Dictionary<string, LayerStatus>();
            this.Command = command;
        }

        public void SetCurrentBatch(string layer, string batchIdentifier)
        {
            this.States[layer].BathIdentifier = batchIdentifier;
        }

        public void InitilaizeLayer(string layer)
        {
            if (!this.States.ContainsKey(layer))
            {
                this.States.Add(layer, new LayerStatus());
            }
        }

        public void ComplateLayer(string layer)
        {
            this.States[layer].IsDone = true;
        }

        public bool IsLayerComplated(string layer)
        {
            return this.States.ContainsKey(layer) && this.States[layer].IsDone;
        }

        public string? GetLayerBatchIdentifier(string layer)
        {
            if (this.States.ContainsKey(layer))
            {
                return this.States[layer].BathIdentifier;
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
                throw new Exception("invalid bach status manager json");
            }
            return batchStatusManager;
        }
    }
}
