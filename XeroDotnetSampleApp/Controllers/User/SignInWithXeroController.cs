using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;


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
    public class SignInWithXeroController : Controller
    {
        protected readonly ITokenIO _tokenIO;
        protected readonly IOptions<XeroConfiguration> _xeroConfig;
        private readonly IOptions<SignInWithXeroSettings> _signInWithXeroSettings;
        private readonly DatabaseService _databaseService;
        private readonly XeroClient _client;
        private readonly HttpClient _httpClient;
        private const string StateFilePath = "./state.json";
        private readonly RequestUrl _xeroAuthorizeUri;


        // Constructor
        public SignInWithXeroController(IOptions<XeroConfiguration> xeroConfig,
                                        IOptions<SignInWithXeroSettings> signInWithXeroSettings,
                                        DatabaseService databaseService,
                                        IHttpClientFactory httpClientFactory)
        {
            _xeroConfig = xeroConfig;
            _client = new XeroClient(xeroConfig.Value);
            _signInWithXeroSettings = signInWithXeroSettings;
            _databaseService = databaseService;
            _httpClient = httpClientFactory.CreateClient();

            _xeroAuthorizeUri = new RequestUrl("https://login.xero.com/identity/connect/authorize");
            _tokenIO = LocalStorageTokenIO.Instance;
        }


        // This function creates an URL that will be used for after Sign Ip With Xero
        // callback URL. In our case, it's stored in the appsettings.json file as
        // https://localhost:5001/SignInWithXero/Callback under SignInWithXeroSettings
        // part of appsettings.json.
        public IActionResult StartSignInWithXero()
        {
            // Generate random guid for site security
            var clientState = Guid.NewGuid().ToString();
            StoreState(clientState);

            // BuildLoginUri Sign Up With Xero version
            var url = _xeroAuthorizeUri.CreateAuthorizeUrl(
                clientId: _xeroConfig.Value.ClientId,
                responseType: "code",
                redirectUri: _signInWithXeroSettings.Value.CallbackUri,
                state: clientState,
                scope: _signInWithXeroSettings.Value.SignInWithXeroScope
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
            if (clientState == null || !string.Equals(clientState, state, StringComparison.Ordinal))
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
                RedirectUri = _signInWithXeroSettings.Value.CallbackUri,
                Parameters =
                    {
                        { "scope", _signInWithXeroSettings.Value.SignInWithXeroScope}
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

            // Fetch connections (tenants) — this requires the token to be issued with accounting.settings.
            // If the user skipped or closed it, we may get 0 connections.
            if (xeroToken.Tenants == null || xeroToken.Tenants.Count == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // Store the token (one that contains both access and refresh tokens <-- Received because we had "offline_access" scope)
            _tokenIO.StoreToken(xeroToken);

            // Variables to use to check on database
            var decodedIDToken = JwtUtils.decode(xeroToken.IdToken);
            var xeroUserID = decodedIDToken.Claims.First(claim => claim.Type == "sub").Value;

            // Checking if the user exists in the database using email as the unique identifier
            var existing = await _databaseService.GetByXeroUserID(xeroUserID);
            if (existing == null)
            {
                // Extract this user's information and prepare to prefill the sign up form in NoUserFoundSignUp.cshtml
                var thisNewUser = new SignUpWithXeroUser
                {
                    XeroUserId = xeroUserID,
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
                thisNewUser.SetTenantId(xeroToken.Tenants[0].TenantId);
                thisNewUser.SetAuthEventId(xeroToken.Tenants[0].authEventId.Value);

                // Retrieve this user's tenant information and update the user info
                thisNewUser = await GetTenantInfo(xeroToken, thisNewUser);

                return View("NoUserFoundSignUp", thisNewUser);
            }


            // "Log in" (session-based). Keep it simple – header can read these
            HttpContext.Session.SetString("UserEmail", existing.Email);
            HttpContext.Session.SetString("UserName", $"{existing.GivenName} {existing.FamilyName}");
            HttpContext.Session.SetString("XeroUserId", existing.XeroUserId);


            // land the user on a dashboard/home page
            return RedirectToAction("Index", "OrganisationInfo");
        }


        [HttpPost]
        // This function completes the Sign Up With Xero - Modified flow by saving the user
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteSignUpWithXero(SignUpWithXeroUser model)
        {
            // if (!ModelState.IsValid) return View("NoUserFoundSignUp", model);

            await _databaseService.RegisterUserToDb(model);

            return RedirectToAction("Index", "OrganisationInfo");
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
            var serializedState = JsonSerializer.Serialize(new State { state = state });
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

        /// <summary>
        /// Get tenant information via GetOrganisation API call
        /// </summary>
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
    }
}