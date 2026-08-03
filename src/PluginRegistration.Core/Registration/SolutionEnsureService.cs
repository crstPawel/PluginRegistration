using Microsoft.Xrm.Sdk;

namespace PluginRegistration.Core.Registration;

/// <summary>
/// Ensures a Dataverse solution exists before components are added to it.
/// Reuses an existing unmanaged solution; creates one when missing.
/// </summary>
public sealed class SolutionEnsureService
{
    private readonly IOrganizationService _service;
    private readonly DataverseQueries _queries;
    private readonly ITrace _trace;
    private readonly HashSet<string> _ensuredSolutions = new(StringComparer.OrdinalIgnoreCase);

    public SolutionEnsureService(IOrganizationService service, ITrace trace)
    {
        _service = service;
        _queries = new DataverseQueries(service);
        _trace = trace;
    }

    /// <summary>
    /// If <paramref name="solutionUniqueName"/> is set, verifies the solution exists on the environment.
    /// When missing, creates an unmanaged solution with the environment default publisher.
    /// </summary>
    public void EnsureExists(string? solutionUniqueName)
    {
        if (string.IsNullOrWhiteSpace(solutionUniqueName))
        {
            return;
        }

        if (!_ensuredSolutions.Add(solutionUniqueName))
        {
            return;
        }

        var existing = _queries.GetSolutionByUniqueName(solutionUniqueName);
        if (existing is not null)
        {
            if (existing.GetAttributeValue<bool?>("ismanaged") == true)
            {
                throw new PluginRegistrationException(
                    $"Solution '{solutionUniqueName}' exists but is managed. " +
                    "Components can only be added to unmanaged solutions.");
            }

            _trace.WriteLine("Using existing solution '{0}'", solutionUniqueName);
            return;
        }

        _trace.WriteLine("Solution '{0}' not found on environment. Creating new solution...", solutionUniqueName);
        CreateSolution(solutionUniqueName);
        _trace.WriteLine("Created solution '{0}'", solutionUniqueName);
    }

    private void CreateSolution(string uniqueName)
    {
        Guid publisherId = _queries.GetDefaultPublisherId();

        var solution = new Entity("solution")
        {
            ["uniquename"] = uniqueName,
            ["friendlyname"] = uniqueName,
            ["version"] = "1.0.0.0",
            ["publisherid"] = new EntityReference("publisher", publisherId)
        };

        _service.Create(solution);
    }
}
