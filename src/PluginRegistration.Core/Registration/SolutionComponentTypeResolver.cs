using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using PluginRegistration.Core.Model.Entities;

namespace PluginRegistration.Core.Registration;

/// <summary>
/// Resolves <see cref="AddSolutionComponent"/> component type codes for solution-aware tables.
/// Custom API uses the table ObjectTypeCode — not the classic picklist values 371/372 (Connector).
/// </summary>
public sealed class SolutionComponentTypeResolver
{
    private readonly IOrganizationService _service;
    private readonly Dictionary<string, int> _objectTypeCodeByLogicalName = new(StringComparer.OrdinalIgnoreCase);

    public SolutionComponentTypeResolver(IOrganizationService service)
    {
        _service = service;
    }

    public int CustomApi => GetObjectTypeCode(CustomAPI.EntityLogicalName);

    public int CustomApiRequestParameter => GetObjectTypeCode(CustomAPIRequestParameter.EntityLogicalName);

    public int CustomApiResponseProperty => GetObjectTypeCode(CustomAPIResponseProperty.EntityLogicalName);

    public int GetObjectTypeCode(string entityLogicalName)
    {
        if (_objectTypeCodeByLogicalName.TryGetValue(entityLogicalName, out int cached))
        {
            return cached;
        }

        var response = (RetrieveEntityResponse)_service.Execute(new RetrieveEntityRequest
        {
            LogicalName = entityLogicalName,
            EntityFilters = EntityFilters.Entity
        });

        int? objectTypeCode = response.EntityMetadata.ObjectTypeCode;
        if (objectTypeCode is null or <= 0)
        {
            throw new PluginRegistrationException(
                $"Could not resolve ObjectTypeCode for entity '{entityLogicalName}' " +
                "(needed for AddSolutionComponent).");
        }

        _objectTypeCodeByLogicalName[entityLogicalName] = objectTypeCode.Value;
        return objectTypeCode.Value;
    }
}
