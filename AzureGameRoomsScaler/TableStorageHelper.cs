using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public async Task AddVMDetailsAsync(string VMID, VMState state)
        {
            VMDetailsEntity entity = new VMDetailsEntity(VMID, state);
            CloudTable table = tableClient.GetTableReference(tableName);
            TableOperation insertOperation = TableOperation.Insert(entity);
            await table.ExecuteAsync(insertOperation);
        }

        public async Task<IEnumerable<VMDetailsEntity>> GetAllVMsInStateAsync(VMState state)
        {
            CloudTable table = tableClient.GetTableReference(tableName);
            var query = new TableQuery<VMDetailsEntity>().Where
                (TableQuery.GenerateFilterConditionForInt("VMStateValue", QueryComparisons.Equal, Convert.ToInt32(state)));

            var items = new List<VMDetailsEntity>();
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

        public async Task<VMDetailsUpdateResult> ModifyVMStateAsync(string VMID, VMState newState)
        {
            CloudTable table = tableClient.GetTableReference(tableName);
            TableOperation retrieveOperation = TableOperation.Retrieve<VMDetailsEntity>(VMID, VMID);

            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);
            VMDetailsEntity vmdetails = (VMDetailsEntity)retrievedResult.Result;

            if (vmdetails != null)
            {
                vmdetails.State = newState;
                TableOperation updateOperation = TableOperation.Replace(vmdetails);
                await table.ExecuteAsync(updateOperation);
                return VMDetailsUpdateResult.UpdateOK;
            }
            else
            {
                return VMDetailsUpdateResult.VMNotFound;
            }
        }

    }

    public class VMDetailsEntity : TableEntity
    {
        public VMDetailsEntity(string VMID, VMState VMState)
        {
            this.PartitionKey = this.RowKey = VMID;
            this.State = VMState;
        }

        public VMDetailsEntity() { }

        public string VMID
        {
            get { return this.PartitionKey; }
        }

        public int VMStateValue { get; set; } //for use by the Azure client libraries only
        [IgnoreProperty]
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
