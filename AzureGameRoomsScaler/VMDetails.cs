using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AzureGameRoomsScaler
{
    [JsonObject(MemberSerialization.OptIn)]
    public class VMDetails : TableEntity
    {
        public VMDetails(string VMID, string resourceGroup, VMState VMState)
        {
            this.PartitionKey = resourceGroup;
            this.RowKey = VMID;

            this.State = VMState;

        }

        public VMDetails(string VMID, string resourceGroup, VMState VMState, string IP) :
            this(VMID, resourceGroup, VMState)
        {
            this.IP = IP;
        }

        public VMDetails() { }

        [JsonProperty("id")]
        public string VMID //ID of the VM
        {
            get { return this.RowKey; }
        }

        [JsonProperty("RG")]
        public string ResourceGroup
        {
            get { return this.PartitionKey; }
        }

        public int VMStateValue { get; set; } //for use by the Azure client libraries only

        [IgnoreProperty]
        [JsonProperty("state")]
        [JsonConverter(typeof(StringEnumConverter))]
        public VMState State
        {
            get { return (VMState)VMStateValue; }
            set { VMStateValue = (int)value; }
        }

        [JsonProperty("ip")]
        public string IP { get; set; }

    }
}