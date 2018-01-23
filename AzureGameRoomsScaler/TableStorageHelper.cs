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

        public async Task AddVMDetailsAsync(string VM_ID, VMState state)
        {
            VMDetailsEntity entity = new VMDetailsEntity(VM_ID, state);
            CloudTable table = tableClient.GetTableReference(tableName);
            TableOperation insertOperation = TableOperation.Insert(entity);
            await table.ExecuteAsync(insertOperation);
        }

        public async Task ModifyVMStateAsync(string VM_ID, VMState newState)
        {
            CloudTable table = tableClient.GetTableReference(tableName);
            var query = new TableQuery<VMDetailsEntity>().Where
                (TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, VM_ID));

            var result = table.ExecuteQuery(query);

            VMDetailsEntity vmdetails = result.Single(); //will throw if more than one
            vmdetails.VM_State = newState;

            TableOperation updateOperation = TableOperation.Replace(vmdetails);
            await table.ExecuteAsync(updateOperation);
        }
    }

    public class VMDetailsEntity : TableEntity
    {
        public VMDetailsEntity(string VM_ID, VMState VM_state)
        {
            this.PartitionKey = VM_ID;
            this.RowKey = VM_state.ToString();
        }

        public VMDetailsEntity() {}

        public string VM_ID
        {
            get { return this.PartitionKey; }
        }

        public VMState VM_State
        {
            get { return (VMState)Enum.Parse(typeof(VMState), this.RowKey); }
            set { this.RowKey = value.ToString(); }
        }

    }

    public enum VMState
    {
        Creating,
        Running,
        MarkedForDeallocation,
        Deallocated,
    }
}
