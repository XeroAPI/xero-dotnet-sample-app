using System.Runtime.Serialization;

namespace XeroWebhooks.DTO
{
    public enum TenantType{
        [EnumMember(Value = "ORGANISATION")]
        ORGANISATION = 1,
        [EnumMember(Value = "APPLICATION")]
        APPLICATION = 2
    }
}
