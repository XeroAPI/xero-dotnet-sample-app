using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable

namespace XeroDotnetSampleApp.Models
{
    // Subscription model to be used when retrieving subscription details from Xero
    // These can be used to store user subscription data in more detail, but for this sample app
    // these are extracted as needed rather than stored to make the database smaller and simpler
    public class SubscriptionModel
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("currentPeriodEnd")]
        public string? CurrentPeriodEnd { get; set; }

        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("organisationId")]
        public Guid OrganisationId { get; set; }

        [JsonPropertyName("plans")]
        public List<Plan>? Plans { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("testMode")]
        public bool? TestMode { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime? EndDate { get; set; }
    }

    public class Plan
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("subscriptionItems")]
        public List<SubscriptionItem>? SubscriptionItems { get; set; }
    }

    public class SubscriptionItem
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime? EndDate { get; set; }  // Nullable to support `null`

        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("price")]
        public Price? Price { get; set; }

        [JsonPropertyName("product")]
        public Product? Product { get; set; }

        [JsonPropertyName("quantity")]
        public int? Quantity { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("testMode")]
        public bool? TestMode { get; set; }
    }

    public class Product
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("seatUnit")]
        public string? SeatUnit { get; set; }

        [JsonPropertyName("usageUnit")]
        public string? UsageUnit { get; set; }
    }

    public class Price
    {
        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }

    public class UsageRecordModel
    {
        public Guid UsageRecordId { get; set; }
        public Guid SubscriptionId { get; set; }
        public Guid SubscriptionItemId { get; set; }
        public Guid ProductId { get; set; }
        public decimal PricePerUnit { get; set; }
        public int Quantity { get; set; }
        public bool TestMode { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
