using System;
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;
using DataverseModel;

namespace Sample.Plugins
{
    [CrmPluginRegistration(
        "Create",
        Account.EntityLogicalName,
        StageEnum.PreOperation,
        ExecutionModeEnum.Synchronous,
        [Account.Fields.Name],
        1)]
    public sealed class AccountCreatePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Plugin logic
        }
    }

    [CrmPluginRegistration(
        "sample_ProcessAccount",
        FriendlyName = "Process Account",
        Description = "Sample Custom API that processes an account identifier")]
    [CrmCustomApiRequestParameter("AccountId", CustomApiParameterTypeEnum.String, IsRequired = true, Description = "Account identifier")]
    [CrmCustomApiResponseProperty("Success", CustomApiParameterTypeEnum.Boolean, Description = "Whether processing succeeded")]
    public sealed class ProcessAccountCustomApiPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Custom API handler
        }
    }
}