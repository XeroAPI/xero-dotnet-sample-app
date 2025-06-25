using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using Newtonsoft.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Model.Appstore;

using XeroWebhooks.DTO;
using XeroDotnetSampleApp.IO;
using XeroDotnetSampleApp.Services;
// using XeroDotnetSampleApp.Models;


namespace XeroDotnetSampleApp.Controllers
{
    [Route("webhooks")]
    public class WebhookController : Controller
    {
        protected readonly IOptions<XeroConfiguration> _xeroConfig;
        private readonly IOptions<WebhookSettings> webhookSettings;
        private readonly XeroClient _client;
        private static readonly ConcurrentQueue<Payload> payloadQueue = new ConcurrentQueue<Payload>();
        private readonly AppStoreService _appStoreService;
        private readonly DatabaseService _databaseService;
        private readonly ITokenIO _tokenIO;
        private readonly INonTenantedTokenIO _nonTenantedTokenIO;
        private readonly IServiceScopeFactory _scopeFactory;


        public WebhookController(IOptions<XeroConfiguration> xeroConfig,
                                    IOptions<WebhookSettings> webhookSettings,
                                    AppStoreService appStoreService,
                                    DatabaseService databaseService,
                                    IServiceScopeFactory scopeFactory)
        {
            _xeroConfig = xeroConfig;
            this.webhookSettings = webhookSettings;
            _databaseService = databaseService;
            _appStoreService = appStoreService;
            _client = new XeroClient(xeroConfig.Value);
            _tokenIO = LocalStorageTokenIO.Instance;
            _nonTenantedTokenIO = LocalStorageNonTenantedTokenIO.Instance;
            _scopeFactory = scopeFactory;
        }


        // Method called when webhook notification sent
        // You must have the webhook delivery URL from developer portal to
        // https://YOUR_NGROK_FORWARDING_URL/webhooks
        [HttpPost]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Read request body asynchronously
                var payloadString = await GetRequestBodyAsync();
                var signature = Request.Headers[webhookSettings.Value.XeroSignature].FirstOrDefault();

                // Validate webhook signature
                if (!VerifySignature(payloadString, signature))
                {
                    return Unauthorized();
                }

                // Store latest payload (overwrite instead of appending)
                webhookSettings.Value.LastWebhookPayload = payloadString;

                // Deserialize payload and enqueue for background processing
                var payload = JsonConvert.DeserializeObject<Payload>(payloadString);
                payloadQueue.Enqueue(payload);

                // Process the queue asynchronously without blocking the response
                await ProcessPayloadQueue();

                // Return 200 OK immediately
                return Ok();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing webhook: {ex.Message}");
                return StatusCode(500, "Internal Server Error");
            }
        }
    
        // Gets request body from HTML POST
        private async Task<string> GetRequestBodyAsync()
        {
            using (var reader = new StreamReader(Request.Body))
            {
                return await reader.ReadToEndAsync();
            }
        }
        
        // Validate webhook signature, signature must match hash of json payload using webhook key as the hash key
        private bool VerifySignature(string payload, string signature)
        {
            if (string.IsNullOrEmpty(signature))
                return false;

            using (var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(webhookSettings.Value.WebhookKey)))
            {
                var computedHash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
                return computedHash == signature;
            }
        }

        // Use method to process payloads
        private async Task ProcessPayloadQueue()
        {
            while(payloadQueue.Count > 0)
            {
                payloadQueue.TryDequeue(out Payload payload);

                foreach(PayloadEvent payloadEvent in payload.Events)
                {                    
                    if(payloadEvent.EventCategory.ToString() == "INVOICE")
                    {
                        // Process invoice webhook events
                        if(payloadEvent.EventType.ToString() == "CREATE")
                        {
                            // Process invoice created event
                            Debug.WriteLine("Invoice Created: " + payloadEvent.ResourceId.ToString());
                        }
                        else if(payloadEvent.EventType.ToString() == "UPDATE")
                        {
                            // Process invoice updated event
                            Debug.WriteLine("Invoice Updated: " + payloadEvent.ResourceId.ToString());
                        }
                        else
                        {
                            // Process other invoice events - for future expansion
                            Debug.WriteLine("Invoice Event: " + payloadEvent.EventType.ToString());
                        }
                    }
                    else if(payloadEvent.EventCategory.ToString() == "CONTACT")
                    {
                        // Process contact webhook events
                        if(payloadEvent.EventType.ToString() == "CREATE")
                        {
                            // Process contact created event
                            Debug.WriteLine("Contact Created: " + payloadEvent.ResourceId.ToString());
                        }
                        else if(payloadEvent.EventType.ToString() == "UPDATE")
                        {
                            // Process contact updated event
                            Debug.WriteLine("Contact Updated: " + payloadEvent.ResourceId.ToString());
                        }
                        else
                        {
                            // Process other contact events - for future expansion
                            Debug.WriteLine("Contact Event: " + payloadEvent.EventType.ToString());
                        }
                    }
                    else if(payloadEvent.EventCategory.ToString() == "SUBSCRIPTION")
                    {
                        await HandleSubscriptionEvent(payloadEvent);
                    }
                    else
                    {
                        // Process other webhook events - for future expansion
                        Debug.WriteLine("This is a new type of webhook: " + payloadEvent.EventType.ToString());
                    }                    
                }
            }
        }


        // Display last webhook payload received
        public IActionResult LastWebhook()
        {
            if (string.IsNullOrEmpty(webhookSettings.Value.LastWebhookPayload))
            {
                ViewBag.WebhookJsonResponse = "No webhook received yet.";
            }
            else
            {
                // Deserialize first, then pretty print
                var formattedJson = JsonConvert.DeserializeObject(webhookSettings.Value.LastWebhookPayload);
                ViewBag.WebhookJsonResponse = JsonConvert.SerializeObject(formattedJson, Formatting.Indented);
            }

            return View();
        }
        

        /*==========================================|\
        ||              Database Section            ||
        \*==========================================*/
        // Calls GetSubscriptionAsync() to get subscription details.
        // Finds the associated user in the database.
        // Updates the subscription plan for the user.
        // Note that non tenanted token is called here as it expires in 30 minutes
        private async Task HandleSubscriptionEvent(PayloadEvent payloadEvent)
        {
            try
            {
                // Create a new scope
                using (var scope = _scopeFactory.CreateScope())
                {
                    // Set the services accessible from this code with Scope Factory
                    var _appStoreService = scope.ServiceProvider.GetRequiredService<AppStoreService>();
                    var _databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                    // Retrieve stored tenanted Xero token
                    var xeroToken = _tokenIO.GetToken();
                    
                    // Find the current user with the tenant subscribing
                    var user = await _databaseService.FindUserByTenantId(xeroToken.Tenants[0].TenantId.ToString());

                    if (user == null)
                    {
                        // This would happen if there is an error that a tenant is being directed to XASS flow
                        // when the tenant is not a Xero referral
                        Console.WriteLine($"No user found for Tenant ID: {xeroToken.Tenants[0].TenantId}");
                        return;
                    }

                    // Extract Subscription ID
                    Guid subscriptionId = payloadEvent.ResourceId;
                    user.SubscriptionId = subscriptionId.ToString();

                    // Fetch Subscription Details from the App Store API using method defined in AppStoreServices.cs
                    var subscription = await _appStoreService.GetSubscriptionAsync();

                    if (subscription == null)
                    {
                        Console.WriteLine($"Subscription {payloadEvent.ResourceId} not found.");
                        return;
                    }

                    // Extract Plan Name of the active subscription
                    var activePlan = subscription.Plans?.FirstOrDefault(p => p.Status == Plan.StatusEnum.ACTIVE);

                    // Check whether there is a plan that is under pending activation
                    var pendingActivationPlan = subscription.Plans?.FirstOrDefault(p => p.Status == Plan.StatusEnum.PENDINGACTIVATION);

                    // There is a pending activation plan, so show both plans
                    // Otherwise, show only the active plan
                    if(pendingActivationPlan != null)
                    {
                        user.SubscriptionPlan = "Active Plan = " + activePlan.Name + "<br /><br />Pending Activation Plan = " + pendingActivationPlan.Name;
                    }
                    else
                    {
                        user.SubscriptionPlan = activePlan.Name;
                    }

                    // Update User with Subscription Plan
                    await _databaseService.RegisterUserToDb(user);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling subscription event: {ex.Message}");
            }
        }
    }
}