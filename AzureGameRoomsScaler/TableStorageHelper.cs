using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AzureGameRoomsScaler
{

    public sealed class TableStorageHelper
    {
        //singleton implementation stolen from http://csharpindepth.com/Articles/General/Singleton.aspx#lazy
        private static readonly Lazy<TableStorageHelper> lazy = new Lazy<TableStorageHelper>(() => new TableStorageHelper());
        private readonly CloudStorageAccount storageAccount;
        private readonly CloudTableClient tableClient;
        private readonly string tableName = "vmdetails";

        public static TableStorageHelper Instance { get { return lazy.Value; } }

        private TableStorageHelper()
        {
            string storageConnectionString = ConfigurationManager.AppSettings["VMDETAILSSTORAGE"];
            if (string.IsNullOrEmpty(storageConnectionString))
                throw new Exception("Fatal error, cannot find vmdetailsstorage connection string");

            storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            //TODO: how can we get rid of this? probably slows down our execution, it will run only once!
            tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference(tableName);
            table.CreateIfNotExists();
        }

        public async Task AddVMEntityAsync(VMDetails newVM)
        {
            ValidateVMDetails(newVM);

            CloudTable table = tableClient.GetTableReference(tableName);
            TableOperation insertOperation = TableOperation.Insert(newVM);
            await table.ExecuteAsync(insertOperation);
        }

        public async Task<IEnumerable<VMDetails>> GetAllVMsInStateAsync(VMState state)
        {
            CloudTable table = tableClient.GetTableReference(tableName);
            var query = new TableQuery<VMDetails>().Where
                (TableQuery.GenerateFilterConditionForInt(nameof(VMDetails.VMStateValue), QueryComparisons.Equal, Convert.ToInt32(state)));

            var items = new List<VMDetails>();
            //modified from response here: https://stackoverflow.com/a/24270388/1205817
            TableContinuationToken token = null;
            do
            {
                var seg = await table.ExecuteQuerySegmentedAsync(query, token);
                token = seg.ContinuationToken;
                items.AddRange(seg);
            } while (token != null);

            return items;
        }

        public async Task<VMDetailsUpdateResult> ModifyVMDetailsAsync(VMDetails updatedVM)
        {
            ValidateVMDetails(updatedVM);

            CloudTable table = tableClient.GetTableReference(tableName);
            TableOperation retrieveOperation = TableOperation.Retrieve<VMDetails>(updatedVM.VMID, updatedVM.VMID);

            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);
            VMDetails vmdetails = (VMDetails)retrievedResult.Result;

            if (vmdetails != null)
            {
                vmdetails.State = updatedVM.State;
                TableOperation updateOperation = TableOperation.Replace(vmdetails);
                await table.ExecuteAsync(updateOperation);
                return VMDetailsUpdateResult.UpdateOK;
            }
            else
            {
                return VMDetailsUpdateResult.VMNotFound;
            }
        }

        public void ValidateVMDetails(VMDetails entity)
        {
            if (string.IsNullOrEmpty(entity.VMID))
                throw new ArgumentException($"{nameof(entity.VMID)} should not be null");
        }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class VMDetails : TableEntity
    {
        public VMDetails(string VMID, VMState VMState)
        {
            this.PartitionKey = this.RowKey = VMID;
            this.State = VMState;
        }

        public VMDetails() { }

        [JsonProperty("id")]
        public string VMID //ID of the VM
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

    }

    public enum VMState
    {
        Creating,
        Running,
        MarkedForDeallocation,
        Deallocated,
    }

    public enum VMDetailsUpdateResult
    {
        UpdateOK,
        VMNotFound
    }
}
