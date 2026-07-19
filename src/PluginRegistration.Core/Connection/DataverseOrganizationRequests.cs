using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

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

    public static Guid CreateWithSolution(
        IOrganizationService service,
        Entity entity,
        string? solutionUniqueName)
    {
        CreateRequest request = new CreateRequest { Target = entity };
        if (!String.IsNullOrWhiteSpace(solutionUniqueName))
        {
            request.Parameters["SolutionUniqueName"] = solutionUniqueName;
        }

        return ((CreateResponse)service.Execute(request)).id;
    }

    public static void UpdateWithSolution(
        IOrganizationService service,
        Entity entity,
        string? solutionUniqueName)
    {
        UpdateRequest request = new UpdateRequest { Target = entity };
        if (!String.IsNullOrWhiteSpace(solutionUniqueName))
        {
            request.Parameters["SolutionUniqueName"] = solutionUniqueName;
        }

        service.Execute(request);
    }
}
