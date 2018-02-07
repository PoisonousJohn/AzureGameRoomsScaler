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

        private static async Task<IEnumerable<T>> GetAllFromTableAsync<T>(CloudTable table, TableQuery<T> query)
            where T: ITableEntity, new()
        {
            var result = new List<T>();

            //modified from response here: https://stackoverflow.com/a/24270388/1205817
            TableContinuationToken token = null;
            do
            {
                var seg = await table.ExecuteQuerySegmentedAsync(query, token);
                token = seg.ContinuationToken;
                result.AddRange(seg);
            } while (token != null);

            return result;
        }

        /// <summary>
        /// Used to filter out matching VMs by Size, Region and Resource Group
        /// </summary>
        /// <param name="resourceGroup"></param>
        /// <param name="region"></param>
        /// <param name="size"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public async Task<IEnumerable<VMDetails>> GetMatchingVMsInStateAsync(string resourceGroup, string region, string size, VMState state)
        {
            CloudTable table = tableClient.GetTableReference(tableName);
            var query = new TableQuery<VMDetails>()
                .Where(TableQuery.GenerateFilterConditionForInt(nameof(VMDetails.VMStateValue), QueryComparisons.Equal, (int)state))
                .Where(TableQuery.GenerateFilterCondition(nameof(VMDetails.ResourceGroup), QueryComparisons.Equal, resourceGroup))
                .Where(TableQuery.GenerateFilterCondition(nameof(VMDetails.Region), QueryComparisons.Equal, region))
                .Where(TableQuery.GenerateFilterCondition(nameof(VMDetails.Size), QueryComparisons.Equal, size));

            return await GetAllFromTableAsync(table, query);
        }

        public async Task<IEnumerable<VMDetails>> GetAllVMsInStateAsync(VMState state)
        {
            CloudTable table = tableClient.GetTableReference(tableName);
            var query = new TableQuery<VMDetails>().Where
                (TableQuery.GenerateFilterConditionForInt(nameof(VMDetails.VMStateValue), QueryComparisons.Equal, Convert.ToInt32(state)));

            return await GetAllFromTableAsync(table, query);
        }

        public async Task ModifyVMDetailsAsync(VMDetails updatedVM)
        {
            ValidateVMDetails(updatedVM);
            updatedVM.ETag = "*"; //required for the Merge operation

            CloudTable table = tableClient.GetTableReference(tableName);

            TableOperation updateOperation = TableOperation.Merge(updatedVM);
            var x = await table.ExecuteAsync(updateOperation);
        }

        public async Task ModifyVMStateByIdAsync(string VMID, VMState state)
        {
            if (string.IsNullOrEmpty(VMID))
                throw new Exception($"{nameof(VMID)} should have a value");

            CloudTable table = tableClient.GetTableReference(tableName);

            VMDetails vm = await GetVMByID(VMID);
            vm.State = state;
            TableOperation updateOperation = TableOperation.Merge(vm);
            await table.ExecuteAsync(updateOperation);
        }

        public async Task<VMDetails> GetVMByID(string VMID)
        {
            if (string.IsNullOrEmpty(VMID))
                throw new Exception($"{nameof(VMID)} should have a value");

            CloudTable table = tableClient.GetTableReference(tableName);
            var query = new TableQuery<VMDetails>().Where
                (TableQuery.GenerateFilterCondition(nameof(VMDetails.RowKey), QueryComparisons.Equal, VMID));

            var items = new List<VMDetails>();
            //modified from response here: https://stackoverflow.com/a/24270388/1205817
            TableContinuationToken token = null;
            do
            {
                var seg = await table.ExecuteQuerySegmentedAsync(query, token);
                token = seg.ContinuationToken;
                items.AddRange(seg);
            } while (token != null);

            if (items.Count == 0)
            {
                return null;
            }

            if (items.Count != 1)
            {
                throw new Exception($"More than 1 Virtual Machines were found with the name {VMID}. Database is probably corrupt");
            }
            else
            {
                return items.Single();
            }
        }

        public void ValidateVMDetails(VMDetails entity)
        {
            if (string.IsNullOrEmpty(entity.VMID))
                throw new ArgumentException($"{nameof(entity.VMID)} should not be null");

            if (string.IsNullOrEmpty(entity.ResourceGroup))
                throw new ArgumentException($"{nameof(entity.ResourceGroup)} should not be null");
        }

    }

}
