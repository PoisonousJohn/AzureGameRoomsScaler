using System.Configuration;

namespace AzureGameRoomsScaler
{
    public class AzureMgmtCredentials
    {
        public const string ManagementURI = "https://management.core.windows.net/";
        public const string BaseURL = "https://management.azure.com/";
        public const string AuthURL = "https://login.windows.net/";
        public const string GraphURL = "https://graph.windows.net/";
        public string SubscriptionId { get; private set; }
        public string ClientId { get; private set; }
        public string ClientKey { get; private set; }
        public string Tenant { get; private set; }

        public AzureMgmtCredentials()
        {
            SubscriptionId = ConfigurationManager.AppSettings["SubscriptionId"].ToString();
            ClientId = ConfigurationManager.AppSettings["ClientId"].ToString();
            ClientKey = ConfigurationManager.AppSettings["ClientKey"].ToString();
            Tenant = ConfigurationManager.AppSettings["Tenant"].ToString();
        }

        public static readonly AzureMgmtCredentials instance = new AzureMgmtCredentials();
    }
}
