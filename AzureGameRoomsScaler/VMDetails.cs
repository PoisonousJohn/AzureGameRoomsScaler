using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AzureGameRoomsScaler
{
    [JsonObject(MemberSerialization.OptIn)]
    public class VMDetails : TableEntity
    {
        public VMDetails(string VMID, string resourceGroup, VMState VMState, string size, string region)
        {
            PartitionKey = resourceGroup;
            RowKey = VMID;
            Region = region;
            Size = Size;
            State = VMState;
        }

        public VMDetails(string VMID, string resourceGroup, VMState VMState, string IP) :
            this(VMID, resourceGroup, VMState, null, null)
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

        [JsonProperty("region")]
        public string Region { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }


        public int VMStateValue { get; set; } //for use by the Azure client libraries only

		[JsonProperty("roomsNumber")]
		public int RoomsNumber { get; set; }

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

     public enum VMState
    {
        Creating = 0,
        Running = 1,
        MarkedForDeallocation = 2,
        Deallocating = 3,
        Deallocated = 4,
        Failed = 5,
    }
}