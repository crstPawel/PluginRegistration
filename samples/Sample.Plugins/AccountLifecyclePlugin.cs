using System;
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;
using DataverseModel;

namespace Sample.Plugins
{
    /// <summary>
    /// One plugin class can register multiple steps by applying several
    /// [PluginRegistration] attributes. Dataverse invokes Execute once per matching step.
    /// </summary>
    [PluginRegistration(MessageTypeEnum.Create, Account.EntityLogicalName, StageEnum.PreOperation, ExecutionModeEnum.Synchronous, [Account.Fields.Name], 1)]
    [PluginRegistration(MessageTypeEnum.Update, Account.EntityLogicalName, StageEnum.PostOperation, ExecutionModeEnum.Synchronous, [Account.Fields.Name], 1)]
    [PluginStepImage("PostImage", ImageTypeEnum.PostImage, "name,telephone1")]
    public class AccountLifecyclePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Shared handler for Create and Update pre-operation steps.
        }
    }
    
    [CustomApiRegistration("sample_AccountLifecycle", "Account Lifecycle")]
    public class AccountLifecycleCustomApi : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Shared handler for Create and Update pre-operation steps.
        }
    }
}