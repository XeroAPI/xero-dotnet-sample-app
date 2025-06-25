using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XeroDotnetSampleApp.Models
{
    // Model of what values SignUpWithXeroUser object will hold and record into
    // These values are used to create a new user in the database as a new user signs up with Xero
    public class SignUpWithXeroUser
    {
        // "sub" Object Key in ID Token
        [Key]
        [Column("XeroUserId")]
        public string XeroUserId { get; set; }

        // "email" Object Key in ID Token
        [Required]
        public string Email { get; set; }

        // "given_name" Object Key in ID Token
        [Required]
        public string GivenName { get; set; }

        // "family_name" Object Key in ID Token
        [Required]
        public string FamilyName { get; set; }

        // Tenant Information
        [Required]
        public string TenantId { get; set; }

        // Tenant's (aka. Xero organisation's) name
        [Required]
        public string TenantName { get; set; }
        
        // The authentication event ID received of when the connection was established
        [Required]
        public string AuthEventId { get; set; }

        // The date and time of when the connection was establihed
        [Required]
        public string ConnectionCreatedDateUtc { get; set; }
        
        // Unique tenant short code to be used when building Xero App Store Subscription URL
        [Required]
        public string TenantShortCode { get; set; }

        // Country code to be used when deciding whether to send the subscriber to XASS flow
        // or to use app owner's own customer billing flow
        [Required]
        public string TenantCountryCode { get; set; }

        [Required]
        // When the account was created in UTC format
        public DateTime AccountCreatedDateTime { get; set; }

        // Retrieved from Subscription webhook
        public string SubscriptionId { get; set; }

        // Retrieved from Get Subscription API Call
        public string SubscriptionPlan { get; set; }

        // Method to set a Guid tenantId as a string
        public void SetTenantId(Guid tenantId)
        {
            TenantId = tenantId.ToString();
        }

        // Method to set a Guid authEventId as a string
        public void SetAuthEventId(Guid authEventId)
        {
            AuthEventId = authEventId.ToString();
        }   
    }
}