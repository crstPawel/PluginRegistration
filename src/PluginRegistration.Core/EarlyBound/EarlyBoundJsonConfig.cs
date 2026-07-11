using Newtonsoft.Json;

namespace PluginRegistration.Core.EarlyBound;

public sealed class EarlyBoundJsonConfig
{
    public const string DefaultFileName = "earlybound.json";

    public string? Output { get; set; }

    public string? Namespace { get; set; }

    public string? ServiceContext { get; set; }

    [JsonProperty("entities")]
    [JsonConverter(typeof(FlexibleStringListJsonConverter))]
    public List<string>? Entities { get; set; }

    public string? EntitiesWhitelist { get; set; }

    public string? EntitiesToSkip { get; set; }

    public string? EntityPrefixesWhitelist { get; set; }

    public string? EntityPrefixesToSkip { get; set; }

    public bool? GenerateMessages { get; set; }

    public bool? GenerateGlobalOptionSets { get; set; }

    public string? ActionsWhitelist { get; set; }

    public string? ActionsToSkip { get; set; }

    public string? EntityTypesFolder { get; set; }

    public string? MessageTypesFolder { get; set; }

    public string? OptionSetsTypesFolder { get; set; }

    public bool? EmitEntityETC { get; set; }

    public bool? EmitVirtualAttributes { get; set; }

    public bool? UpdateBuilderSettingsJson { get; set; }

    public bool? Overwrite { get; set; }

    public EarlyBoundJsonCamelCaseConfig? CamelCase { get; set; }

    public EarlyBoundJsonExtensionConfig? Extension { get; set; }

    [JsonIgnore]
    public string FilePath { get; set; } = string.Empty;

    public static EarlyBoundJsonConfig Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new PluginRegistrationException($"Early bound JSON config not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        var config = JsonConvert.DeserializeObject<EarlyBoundJsonConfig>(json)
            ?? throw new PluginRegistrationException($"Invalid early bound JSON config: {filePath}");

        config.FilePath = filePath;
        return config;
    }

    public void Save(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(filePath, json);
        FilePath = filePath;
    }

    public string? ResolveEntitiesWhitelist()
    {
        if (!string.IsNullOrWhiteSpace(EntitiesWhitelist))
        {
            return EntitiesWhitelist;
        }

        if (Entities is null || Entities.Count == 0)
        {
            return null;
        }

        return string.Join("|", Entities.Where(e => !string.IsNullOrWhiteSpace(e)));
    }

    public void ApplyToRequest(EarlyBoundGenerationRequest request, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(request.OutputDirectory) && !string.IsNullOrWhiteSpace(Output))
        {
            request.OutputDirectory = Path.IsPathRooted(Output)
                ? Output
                : Path.Combine(workingDirectory, Output);
        }

        if (string.IsNullOrWhiteSpace(request.Namespace) && !string.IsNullOrWhiteSpace(Namespace))
        {
            request.Namespace = Namespace;
        }

        if (string.IsNullOrWhiteSpace(request.ServiceContextName) && !string.IsNullOrWhiteSpace(ServiceContext))
        {
            request.ServiceContextName = ServiceContext;
        }

        if (string.IsNullOrWhiteSpace(request.EntitiesWhitelist))
        {
            request.EntitiesWhitelist = ResolveEntitiesWhitelist();
        }

        if (!request.GenerateMessages.HasValue && GenerateMessages.HasValue)
        {
            request.GenerateMessages = GenerateMessages.Value;
        }

        if (!request.GenerateGlobalOptionSets.HasValue && GenerateGlobalOptionSets.HasValue)
        {
            request.GenerateGlobalOptionSets = GenerateGlobalOptionSets.Value;
        }

        if (!request.OverwriteExistingFiles.HasValue && Overwrite.HasValue)
        {
            request.OverwriteExistingFiles = Overwrite.Value;
        }
    }
}

public sealed class EarlyBoundJsonCamelCaseConfig
{
    public bool? ClassNames { get; set; }

    public bool? MemberNames { get; set; }

    [JsonProperty("customWords")]
    [JsonConverter(typeof(FlexibleStringListJsonConverter))]
    public List<string>? CustomWords { get; set; }

    public string? CustomWordsDelimiter { get; set; }

    public string? DictionaryPath { get; set; }

    public string? ResolveCustomWords()
    {
        if (!string.IsNullOrWhiteSpace(CustomWordsDelimiter))
        {
            return CustomWordsDelimiter;
        }

        if (CustomWords is null || CustomWords.Count == 0)
        {
            return null;
        }

        return string.Join("|", CustomWords.Where(word => !string.IsNullOrWhiteSpace(word)));
    }
}

public sealed class EarlyBoundJsonExtensionConfig
{
    public bool? CamelCaseClassNames { get; set; }

    public bool? CamelCaseMemberNames { get; set; }

    [JsonProperty("camelCaseCustomWords")]
    [JsonConverter(typeof(FlexibleStringListJsonConverter))]
    public List<string>? CamelCaseCustomWords { get; set; }

    public string? CamelCaseCustomWordsDelimiter { get; set; }

    public string? CamelCaseDictionaryPath { get; set; }

    public string? ResolveCamelCaseCustomWords()
    {
        if (!string.IsNullOrWhiteSpace(CamelCaseCustomWordsDelimiter))
        {
            return CamelCaseCustomWordsDelimiter;
        }

        if (CamelCaseCustomWords is null || CamelCaseCustomWords.Count == 0)
        {
            return null;
        }

        return string.Join("|", CamelCaseCustomWords.Where(word => !string.IsNullOrWhiteSpace(word)));
    }
    public bool? CreateOneFilePerEntity { get; set; }

    public bool? CreateOneFilePerAction { get; set; }

    public bool? CreateOneFilePerOptionSet { get; set; }

    public bool? GenerateAttributeNameConsts { get; set; }

    public bool? GenerateActionAttributeNameConsts { get; set; }

    public bool? GenerateEntityRelationships { get; set; }

    public bool? GenerateEnumProperties { get; set; }

    public bool? ReplaceOptionSetPropertiesWithEnum { get; set; }

    public bool? MakeReferenceTypesNullable { get; set; }

    public bool? MakeAllFieldsEditable { get; set; }

    public bool? DeleteFilesFromOutputFolders { get; set; }

    public string? OptionSetNames { get; set; }

    public string? FilePrefixText { get; set; }
}