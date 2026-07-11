using System;
using Microsoft.Xrm.Sdk;
using PluginRegistration.Attributes;
using DataverseModel;

namespace Sample.Plugins
{
    /// <summary>
    /// One plugin class can register multiple steps by applying several
    /// [CrmPluginRegistration] attributes. Dataverse invokes Execute once per matching step.
    /// </summary>
    [CrmPluginRegistration(
        "Create",
        Account.EntityLogicalName,
        StageEnum.PreOperation,
        ExecutionModeEnum.Synchronous,
        [Account.Fields.Name],
        1)]
    [CrmPluginRegistration(
        "Update",
        Account.EntityLogicalName,
        StageEnum.PostOperation,
        ExecutionModeEnum.Synchronous,
        [Account.Fields.Name],
        1)]
    [CrmPluginStepImage(
        StageEnum.PostOperation,
        "PostImage",
        ImageTypeEnum.PostImage,
        "name,telephone1",
        Message = "Update")]
    public sealed class AccountLifecyclePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Shared handler for Create and Update pre-operation steps.
        }
    }
}