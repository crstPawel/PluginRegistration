using System;
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;
using DataverseModel;

namespace Sample.Plugins
{
    [PluginRegistration(
        MessageTypeEnum.Create,
        Account.EntityLogicalName,
        StageEnum.PostOperation,
        ExecutionModeEnum.Synchronous,
        [Account.Fields.Name],
        1)]
    public sealed class AccountCreatePlugin : PluginBase
    {
        public AccountCreatePlugin(Type pluginClassName) : base(pluginClassName)
        {
            
        }
    }

    [CustomApiRegistration(
        "sample_ProcessAccount",
        FriendlyName = "Process Account",
        Description = "Sample Custom API that processes an account identifier")]
    [CustomApiRequestParameter("AccountId", CustomApiParameterTypeEnum.String, IsRequired = true, Description = "Account identifier")]
    [CustomApiResponseProperty("Success", CustomApiParameterTypeEnum.Boolean, Description = "Whether processing succeeded")]
    public sealed class ProcessAccountCustomApiPlugin : PluginBase
    {
        public ProcessAccountCustomApiPlugin(Type pluginClassName) : base(pluginClassName)
        {
            
        }
    }
}