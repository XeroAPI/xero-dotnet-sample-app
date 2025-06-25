using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Token;
using XeroDotnetSampleApp.IO;
using XeroDotnetSampleApp.Models;
using XeroDotnetSampleApp.Services;

#nullable enable

namespace XeroDotnetSampleApp.Controllers
{
    public class AppStoreController : ApiAccessorController<AppStoreApi>
    {
        protected readonly IOptions<XeroConfiguration> _xeroConfig;
        private readonly IOptions<XeroAppStoreSubscriptionSettings> _xeroAppStoreSubscriptionSettings;
        private readonly AppStoreService _appStoreService;
        private readonly DatabaseService _databaseService;
        private readonly ITokenIO _tokenIO;
        protected readonly INonTenantedTokenIO _nonTenantedTokenIO;

        public AppStoreController(IOptions<XeroConfiguration> xeroConfig,
                                    IOptions<XeroAppStoreSubscriptionSettings> xeroAppStoreSubscriptionSettings,
                                    AppStoreService appStoreService,
                                    DatabaseService databaseService):base(xeroConfig)
        {
            _xeroConfig = xeroConfig;
            _xeroAppStoreSubscriptionSettings = xeroAppStoreSubscriptionSettings;
            _appStoreService = appStoreService;
            _databaseService = databaseService;
            _tokenIO = LocalStorageTokenIO.Instance;
            _nonTenantedTokenIO = LocalStorageNonTenantedTokenIO.Instance;
        }

        
        /*===========================================|\
        ||              Subscribe Section            ||
        \*===========================================*/
        // Populates either Subscribe.chtml or CannotSubscribe.cshtml based on the
        // country the Xero organisation is in.
        public async Task<IActionResult> Subscribe()
        {
            // Get the tenanted Xero Token stored in xerotoken.json
            var _tenantedXeroToken = _tokenIO.GetToken();

            // Get organisation details
            var apiInstance = new AccountingApi();
            var response = await apiInstance.GetOrganisationsAsync(_tenantedXeroToken.AccessToken, _tenantedXeroToken.Tenants[0].TenantId.ToString());

            // Compare the country codes eligible for XASS flow
            String CountryCode = response._Organisations[0].CountryCode.ToString();

            // Only allow XASS for AU, NZ and UK
            if(CountryCode.Equals("AU") ||
                CountryCode.Equals("NZ") ||
                CountryCode.Equals("UK"))
            {
                // Create a link for subscription in the format required
                // https://apps.xero.com/{ShortCode}/subscribe/{AppID}
                String tenantShortCode = response._Organisations[0].ShortCode;
                String appId = _xeroAppStoreSubscriptionSettings.Value.AppId;

                String xassLink = "https://apps.xero.com/"+tenantShortCode+"/subscribe/"+appId;

                ViewBag.subscriptionLink = xassLink;

                // Redirect the user to Subscribe.cshtml page
                return View("Subscribe");
            }
            else
            {
                // The region does not support XASS flow yet
                ViewBag.Org = response._Organisations[0].Name;
                ViewBag.CountryCode = response._Organisations[0].CountryCode;
                
                // Redirect the user to CannotSubscribe.cshtml page
                return View("CannotSubscribe");
            }
        }


        /*==============================================|\
        ||          Get Subscription Section            ||
        \*==============================================*/
        // Create the page for Get Subscription Info
        // Note that non tenanted token is called here as it expires in 30 minutes
        // You must set the "After Subscribe URL" from the developer portal to this URL
        // If you are using this sample app with ngrok, use
        // https://YOUR_NGROK_FORWARDING_URL/Views/AppStore/GetSubscriptions.cshtml
        public async Task<IActionResult> GetSubscription()
        {
            // Get the subscription information from Xero and add the raw JSON to the ViewBag
            var response = await _appStoreService.GetSubscriptionAsync();
            ViewBag.SubscriptionJsonResponse = response.ToJson();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Deserialize as a single SubscriptionModel (NOT a list)
            SubscriptionModel? subscription = JsonSerializer.Deserialize<SubscriptionModel>(response.ToJson(), options);

            if (subscription != null &&
                subscription.Status?.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) == true &&
                subscription.Plans != null)
            {
                // Filter active plans and select the one that is active and ignore the rest as they will be under CANCELED status
                var activePlans = subscription.Plans
                    .Where(p => p.Status?.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) == true &&
                                p.SubscriptionItems?.Any(i => i.Status?.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) == true) == true)
                    .Select(p =>
                    {
                        // Filter active subscription items
                        p.SubscriptionItems = p.SubscriptionItems?
                            .Where(i => i.Status?.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) == true)
                            .ToList();
                        return p;
                    })
                    .ToList();

                // There should only be one plan in this list, as one tenant can only have one active subscription
                if (activePlans.Count != 0)
                {
                    subscription.Plans = activePlans;
                    ViewBag.SubscriptionModel = subscription;

                    if(subscription.Plans.Any(plan => plan.SubscriptionItems?.Any(item => item.Product?.Type == "METERED") == true))
                    {
                        // Let's get the usage records for the subscription items
                        var usageRecords = await _appStoreService.GetUsageRecordsAsync(subscription.Id);
                        ViewBag.UsageRecords = usageRecords.UsageRecords;
                    }
                    else
                    {
                        ViewBag.UsageRecords = null; // No metered items
                    }
                }
            }
            else
            {
                ViewBag.SubscriptionModel = null; // Subscription is not active or null
            }

            return View();
        }


        // Post usage to Xero App Store Metered Billing API
        [HttpPost]
        public async Task<IActionResult> PostUsage(string subscriptionId, string subscriptionItemId, int quantity)
        {
            var usageRecord = await _appStoreService.PostUsageRecordsAsync(
            Guid.Parse(subscriptionId),
            Guid.Parse(subscriptionItemId),
            quantity);

            if (usageRecord != null)
            {
                TempData["UsagePostSuccessMessage"] = $"Usage posted successfully: {usageRecord.Quantity} x {usageRecord.PricePerUnit:C} = {usageRecord.Quantity * usageRecord.PricePerUnit:C} for item {usageRecord.SubscriptionItemId}";
            }

            return RedirectToAction("GetSubscription");
        }
    }
}