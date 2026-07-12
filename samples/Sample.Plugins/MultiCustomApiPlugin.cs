using System;
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;

namespace Sample.Plugins
{
    /// <summary>
    /// One plugin class can implement multiple Custom APIs. Each API is declared with its own
    /// [PluginRegistration] attribute; request/response metadata is scoped with ApiUniqueName.
    /// </summary>
    [CustomApiRegistration("sample_ValidateAccount", FriendlyName = "Validate Account", Description = "Validates account data before processing")]
    [CustomApiRequestParameter("AccountId", CustomApiParameterTypeEnum.String, ApiUniqueName = "sample_ValidateAccount", IsRequired = true, Description = "Account identifier")]
    [CustomApiResponseProperty("IsValid", CustomApiParameterTypeEnum.Boolean, ApiUniqueName = "sample_ValidateAccount", Description = "Whether the account passed validation")]
    [CustomApiRegistration("sample_EnrichAccount", FriendlyName = "Enrich Account", Description = "Enriches account data from an external source")]
    [CustomApiRequestParameter("AccountId", CustomApiParameterTypeEnum.String, ApiUniqueName = "sample_EnrichAccount", IsRequired = true, Description = "Account identifier")]
    [CustomApiResponseProperty("EnrichedName", CustomApiParameterTypeEnum.String, ApiUniqueName = "sample_EnrichAccount", Description = "Enriched account name")] 
    public class MultiCustomApiPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Route to the correct API handler based on context.MessageName.
        }
    }
}