using System.IO;
using System.Linq;
using System.Text.Json;
using Xero.NetStandard.OAuth2.Token;

namespace XeroDotnetSampleApp.IO
{
    /// <summary>
    /// Manager for locally storing and loading token data
    /// Note that this token is specifically for non-tenanted / client credential
    /// special access token.
    /// </summary>
    public sealed class LocalStorageNonTenantedTokenIO : INonTenantedTokenIO
    {
        // Singleton
        private static LocalStorageNonTenantedTokenIO _instance;
        private static readonly object Lock = new object();

        /// <summary>
        /// Thread safe instance retrieval
        /// </summary>
        public static LocalStorageNonTenantedTokenIO Instance
        {
            get
            {
                lock (Lock) return _instance ??= new LocalStorageNonTenantedTokenIO();
            }
        }

        // Prevent object being instantiated outside of class
        private LocalStorageNonTenantedTokenIO(){}

        private const string TokenFilePath = "./non_tenanted_xerotoken.json";

        /// <summary>
        /// Check if a stored token exists if so, return saved token otherwise, instantiate a new token
        /// </summary>
        /// <returns>Returns a Xero OAuth2 Token however only access token fields will be used</returns>
        public XeroOAuth2Token GetToken()
        {
            if (File.Exists(TokenFilePath))
            {
                var serializedToken = File.ReadAllText(TokenFilePath);
                return JsonSerializer.Deserialize<XeroOAuth2Token>(serializedToken);
            }

            return new XeroOAuth2Token();
        }

        /// <summary>
        /// Save token contents to file
        /// </summary>
        /// <param name="non_tenanted_xerotoken">Xero OAuth2 token to save - only access token field will be used</param>
        public void StoreToken(XeroOAuth2Token non_tenanted_xerotoken)
        {

            if (!File.Exists(TokenFilePath))
            {
                File.Create(TokenFilePath).Dispose();
            }

            var serializedToken = JsonSerializer.Serialize(non_tenanted_xerotoken);
            File.WriteAllText(TokenFilePath, serializedToken);
        }

        /// <summary>
        /// Destroy json file holding Xero OAuth2 token data. Also destroy associated tenant id
        /// </summary>
        public void DestroyToken()
        {
            if (File.Exists(TokenFilePath))
                File.Delete(TokenFilePath);
        }

        /// <summary>
        /// Check if stored token file exists
        /// </summary>
        /// <returns>Returns boolean specify if stored token file exists</returns>
        public bool TokenExists()
        {
            return File.Exists(TokenFilePath);
        }
    }
}

