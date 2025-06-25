using System;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Model.Appstore;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Token;

using XeroDotnetSampleApp.IO;
using XeroDotnetSampleApp.Models;


namespace XeroDotnetSampleApp.Services
{
    public class AppStoreService
    {
        private readonly IOptions<XeroConfiguration> _xeroConfig;
        private readonly DatabaseService _databaseService;
        private readonly ITokenIO _tokenIO;
        protected readonly INonTenantedTokenIO _nonTenantedTokenIO;


        public AppStoreService(IOptions<XeroConfiguration> xeroConfig,
                                DatabaseService databaseService)
        {
            _xeroConfig = xeroConfig;
            _databaseService = databaseService;
            _tokenIO = LocalStorageTokenIO.Instance;
            _nonTenantedTokenIO = LocalStorageNonTenantedTokenIO.Instance;
        }


        // Retrieves subscription details from Xero.
        // Note that non tenanted token is called here as it expires in 30 minutes
        // You must set the "After Subscribe URL" from the developer portal to this URL
        // https://YOUR_NGROK_FORWARDING_URL/Views/AppStore/GetSubscriptions.cshtml
        public async Task<Subscription> GetSubscriptionAsync()
        {
            // Get the tenanted Xero Token
            var _tenantedXeroToken = _tokenIO.GetToken();

            // Create a new set of XeroConfig to request a new "XeroOAuth2Token"
            XeroConfiguration XeroConfig = new XeroConfiguration
            {
                ClientId = _xeroConfig.Value.ClientId,
                ClientSecret = _xeroConfig.Value.ClientSecret
            };

            // Retrieve stored Non-Tenanted Spcecial Xero Token
            // which is just an access token and holds no info on refresh token
            var client = new XeroClient(XeroConfig);
            var xeroNonTenantedToken = (XeroOAuth2Token) await client.RequestClientCredentialsTokenAsync(false);
            
            // Save the non tenanted Xero token
            _nonTenantedTokenIO.StoreToken(xeroNonTenantedToken);

            // Find the current user with the tenant subscribing
            SignUpWithXeroUser user = await _databaseService.FindUserByTenantId(_tenantedXeroToken.Tenants[0].TenantId.ToString());

            // Get subscription information
            Guid subscriptionId = Guid.Parse(user.SubscriptionId);


            // Call API (Directly returns a Subscription object)
            var appStoreApiInstance = new AppStoreApi();
            var subscription = await appStoreApiInstance.GetSubscriptionAsync(_nonTenantedTokenIO.GetToken().AccessToken, subscriptionId);

            if (subscription == null)
            {
                Console.WriteLine("Xero API returned null subscription.");
                return null;
            }

            Console.WriteLine($"Xero API Subscription Response: {JsonConvert.SerializeObject(subscription, Formatting.Indented)}");

            return subscription;
        }

        public async Task<UsageRecord> PostUsageRecordsAsync(Guid subscriptionId, Guid subscriptionItemId, int quantity)
        {
            // Create AppStoreAPI instance to call the API
            var appStoreApiInstance = new AppStoreApi();

            // Create usage record object
            var usageRecord = new CreateUsageRecord
            {
                Quantity = quantity,
                Timestamp = DateTime.UtcNow
            };

            var response = await appStoreApiInstance.PostUsageRecordsAsync(_nonTenantedTokenIO.GetToken().AccessToken, subscriptionId, subscriptionItemId, usageRecord);

            return response;
        }

        public async Task<UsageRecordsList> GetUsageRecordsAsync(Guid subscriptionId)
        {
            // Create AppStoreAPI instance to call the API
            var appStoreApiInstance = new AppStoreApi();

            var response = await appStoreApiInstance.GetUsageRecordsAsync(_nonTenantedTokenIO.GetToken().AccessToken, subscriptionId);

            return response;
        }
    }
}