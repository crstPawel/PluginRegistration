using Microsoft.Xrm.Sdk;

namespace PluginRegistration.Core.Connection;

public static class DataverseOrganizationRequests
{
    public static (Guid OrganizationId, Guid BusinessUnitId, Guid UserId) WhoAmI(IOrganizationService service)
    {
        var response = service.Execute(new OrganizationRequest("WhoAmI"));
        return (
            (Guid)response["OrganizationId"],
            (Guid)response["BusinessUnitId"],
            (Guid)response["UserId"]);
    }

    public static void AddSolutionComponent(
        IOrganizationService service,
        string solutionUniqueName,
        int componentType,
        Guid componentId,
        bool addRequiredComponents = false)
    {
        var request = new OrganizationRequest("AddSolutionComponent")
        {
            ["SolutionUniqueName"] = solutionUniqueName,
            ["ComponentType"] = componentType,
            ["ComponentId"] = componentId,
            ["AddRequiredComponents"] = addRequiredComponents
        };

        service.Execute(request);
    }
}