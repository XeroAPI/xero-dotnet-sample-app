using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using IdentityModel.Client;

using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Token;
using XeroDotnetSampleApp.IO;
using XeroDotnetSampleApp.Models;
using XeroDotnetSampleApp.Services;


namespace XeroDotnetSampleApp.Controllers
{
    // <summary>
    // The Sign Up With Xero controller has quite a bit of same code from AuthorizationController.
    // Thus we have added AuthorizationController as a service in Startup.cs.
    // This file was written with quite a bit of duplicated codes from the SDKs so when the developer
    // wishes to use this part of the code it gives them one pager view of how SSU with SQLite works.
    // </summary>
    public class SignUpWithXeroController : Controller
    {
        protected readonly ITokenIO _tokenIO;
        protected readonly IOptions<XeroConfiguration> _xeroConfig;
        private readonly IOptions<SignUpWithXeroSettings> _signUpWithXeroSettings;
        private readonly UserContext _userContext;
        private readonly DatabaseService _databaseService;
        private readonly XeroClient _client;
        private readonly HttpClient _httpClient;
        private const string StateFilePath = "./state.json";
        private readonly RequestUrl _xeroAuthorizeUri;


        // Constructor
        public SignUpWithXeroController(IOptions<XeroConfiguration> xeroConfig,
                                        IOptions<SignUpWithXeroSettings> signUpWithXeroSettings,
                                        UserContext context,
                                        DatabaseService databaseService,
                                        IHttpClientFactory httpClientFactory)
        {
            _xeroConfig = xeroConfig;
            _client = new XeroClient(xeroConfig.Value);
            _signUpWithXeroSettings = signUpWithXeroSettings;
            _userContext = context;
            _databaseService = databaseService;
            _httpClient = httpClientFactory.CreateClient();

            _xeroAuthorizeUri = new RequestUrl("https://login.xero.com/identity/connect/authorize");
            _tokenIO = LocalStorageTokenIO.Instance;
        }


        /// Create the view for the Sign Up with Xero page
        public IActionResult StartSignUpWithXero()
        {
            return View("StartSignUpWithXero");
        }


        // This function creates an URL that will be used for after Sign Up With Xero
        // callback URL. In our case, it's stored in the appsettings.json file as
        // https://localhost:5001/SignUpWithXero/Callback under SignUpWithXeroSettings
        // part of appsettings.json.
        public IActionResult ExecuteSignUpWithXero()
        {
            // Generate random guid for site security
            var clientState = Guid.NewGuid().ToString();
            StoreState(clientState);

            // BuildLoginUri Sign Up With Xero version
            var url = _xeroAuthorizeUri.CreateAuthorizeUrl(
                clientId: _xeroConfig.Value.ClientId,
                responseType: "code",
                redirectUri: _signUpWithXeroSettings.Value.CallbackUri,
                state: clientState,
                scope: _signUpWithXeroSettings.Value.SignUpWithXeroScope
                );

            return Redirect(url);
        }

        // Extract user and tenant information from xeroToken, the function is similar to the
        // callback function in AuthorizationController by its nature. The code below is also
        // written to help developers use the code snippet so it has minimal dependencies to
        // the Xero Dotnet SDK except for token handling.
        public async Task<IActionResult> Callback(string code, string state)
        {
            var clientState = GetCurrentState();
            if (state != clientState)
            {
                return Content("Cross site forgery attack detected!");
            }

            // Requesting for token
            var response = await _httpClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
            {
                Address = $"{_xeroConfig.Value.XeroIdentityBaseUri}/connect/token",
                GrantType = "code",
                Code = code,
                ClientId = _xeroConfig.Value.ClientId,
                ClientSecret = _xeroConfig.Value.ClientSecret,
                RedirectUri = _signUpWithXeroSettings.Value.CallbackUri,
                Parameters =
                    {
                        { "scope", _signUpWithXeroSettings.Value.SignUpWithXeroScope}
                    }
            });

            if (response.IsError)
            {
                // No tokens were received.
                throw new Exception(response.Error);
            }

            // Disect the response tokens into specific tokens
            var xeroToken = new XeroOAuth2Token()
            {
                AccessToken = response.AccessToken,
                IdToken = response.IdentityToken,
                RefreshToken = response.RefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(response.ExpiresIn),
            };
            xeroToken.Tenants = await _client.GetConnectionsAsync(xeroToken);

            if (xeroToken.IdToken != null && !JwtUtils.validateIdToken(xeroToken.IdToken, _xeroConfig.Value.ClientId))
            {
                return Content("ID token is not valid");
            }

            if (xeroToken.AccessToken != null && !JwtUtils.validateAccessToken(xeroToken.AccessToken))
            {
                return Content("Access token is not valid");
            }

            // Store the token (one that contains both access and refresh tokens <-- Received because we had "offline_access" scope)
            _tokenIO.StoreToken(xeroToken);

            // Prepare ID token for user information extraction
            var decodedIDToken = JwtUtils.decode(xeroToken.IdToken);
       
            // Extract and save the user information from token
            var thisUser = new SignUpWithXeroUser
            {
                XeroUserId = decodedIDToken.Claims.First(claim => claim.Type == "sub").Value,
                Email = decodedIDToken.Claims.First(claim => claim.Type == "email").Value,
                GivenName = decodedIDToken.Claims.First(claim => claim.Type == "given_name").Value,
                FamilyName = decodedIDToken.Claims.First(claim => claim.Type == "family_name").Value,
                TenantId = "",
                TenantName = xeroToken.Tenants[0].TenantName,
                AuthEventId = "",
                ConnectionCreatedDateUtc = xeroToken.Tenants[0].CreatedDateUtc.ToString(),
                TenantShortCode = "",
                TenantCountryCode = "",
                AccountCreatedDateTime = DateTime.UtcNow,
                SubscriptionId = "",
                SubscriptionPlan = "",
            };

            // Extract and save the tenant information from token
            thisUser.SetTenantId(xeroToken.Tenants[0].TenantId);
            thisUser.SetAuthEventId(xeroToken.Tenants[0].authEventId.Value);

            // Retrieve this user's tenant information and update the user info
            thisUser = await GetTenantInfo(xeroToken, thisUser);

            // Now, register or update the user information to the database
            await RegisterUserToDb(thisUser);
            
            // Read data from the database to present
            var usersFromDb = await ReadAllData();

            // The user who went through Sign Up With Xero flow will be directed to the page you decide.
            // For this sample app, the user will be directed to the page ReferralUserInfo which shows
            // list of users who went through the Sign Up with Xero - Recommended flow.
            // However, if you wish to build the sign up with Xero - Modified flow, you would lead the
            // customer to your user registration / contact us form and use the values in usersFromDb
            // to pre-populate the form.
            return View("ReferralUserInfo", usersFromDb);
        }


        // Get tenant information via GetOrganisation API call
        [HttpGet]
        private async Task<SignUpWithXeroUser> GetTenantInfo(XeroOAuth2Token xeroToken, SignUpWithXeroUser user)
        {
            var apiInstance = new AccountingApi();
            var response = await apiInstance.GetOrganisationsAsync(xeroToken.AccessToken, user.TenantId);

            // Save the shortcode for the organisation/tenant
            user.TenantShortCode = response._Organisations[0].ShortCode;


            // As the country code is a type of CountryCode and since we want to use minimal dependencies to Xero SDK,
            // we will extract the country code from the JSON response.
            string tenantInfo = response.ToJson();

            using (JsonDocument doc = JsonDocument.Parse(tenantInfo))
            {
                JsonElement root = doc.RootElement;

                // Access the first organisation's CountryCode
                if (root.TryGetProperty("Organisations", out JsonElement organisations) && organisations.GetArrayLength() > 0)
                {
                    string countryCode = organisations[0].GetProperty("CountryCode").GetString();
                    user.TenantCountryCode = countryCode;
                }
            }

            return user;
        }


         // Show the referral users stored in the local SQLite database
        public async Task<IActionResult> ShowReferralUsers()
        {
            // Read data from the database
            var usersFromDb = await _databaseService.ReadAllData();

            return View("ReferralUserInfo", usersFromDb);
        }


        /*==========================================|\
        ||              Database Section            ||
        \*==========================================*/
        // Creates a user in the local SQLite database
        private async Task RegisterUserToDb(SignUpWithXeroUser user)
        {
            await _databaseService.RegisterUserToDb(user);
        }
 
        // Reads data from the local SQLite database using Entity Framework Core
        private async Task<List<SignUpWithXeroUser>> ReadAllData()
        {
            return await _databaseService.ReadAllData();
        }


        /*=======================================|\
        ||              Misc. Section            ||
        \*=======================================*/
        /// <summary>
        /// Save state value to disk
        /// </summary>
        /// <param name="state">State data to save</param>
        private void StoreState(string state)
        {
            var serializedState = JsonSerializer.Serialize(new State{state = state});
            System.IO.File.WriteAllText(StateFilePath, serializedState);
        }

        /// <summary>
        /// Get current state from disk
        /// </summary>
        /// <returns>Returns state from disk if exists, otherwise returns null</returns>
        private string GetCurrentState()
        {
            if (System.IO.File.Exists(StateFilePath))
            {
                var serializeState = System.IO.File.ReadAllText(StateFilePath);
                return JsonSerializer.Deserialize<State>(serializeState)?.state;
            }

            return null;
        }
    }
}