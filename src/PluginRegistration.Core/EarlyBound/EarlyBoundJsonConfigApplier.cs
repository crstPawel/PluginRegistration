using DLaB.EarlyBoundGeneratorV2.Settings;

namespace PluginRegistration.Core.EarlyBound;

internal static class EarlyBoundJsonConfigApplier
{
    public static void Apply(EarlyBoundJsonConfig jsonConfig, EarlyBoundGeneratorConfig config)
    {
        if (!string.IsNullOrWhiteSpace(jsonConfig.Namespace))
        {
            config.Namespace = jsonConfig.Namespace;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.ServiceContext))
        {
            config.ServiceContextName = jsonConfig.ServiceContext;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.EntityTypesFolder))
        {
            config.EntityTypesFolder = jsonConfig.EntityTypesFolder;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.MessageTypesFolder))
        {
            config.MessageTypesFolder = jsonConfig.MessageTypesFolder;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.OptionSetsTypesFolder))
        {
            config.OptionSetsTypesFolder = jsonConfig.OptionSetsTypesFolder;
        }

        if (jsonConfig.EmitEntityETC.HasValue)
        {
            config.EmitEntityETC = jsonConfig.EmitEntityETC.Value;
        }

        if (jsonConfig.EmitVirtualAttributes.HasValue)
        {
            config.EmitVirtualAttributes = jsonConfig.EmitVirtualAttributes.Value;
        }

        if (jsonConfig.GenerateMessages.HasValue)
        {
            config.GenerateMessages = jsonConfig.GenerateMessages.Value;
        }

        if (jsonConfig.UpdateBuilderSettingsJson.HasValue)
        {
            config.UpdateBuilderSettingsJson = jsonConfig.UpdateBuilderSettingsJson.Value;
        }

        var entitiesWhitelist = jsonConfig.ResolveEntitiesWhitelist();
        if (!string.IsNullOrWhiteSpace(entitiesWhitelist))
        {
            config.ExtensionConfig.EntitiesWhitelist = entitiesWhitelist;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.EntitiesToSkip))
        {
            config.ExtensionConfig.EntitiesToSkip = jsonConfig.EntitiesToSkip;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.EntityPrefixesWhitelist))
        {
            config.ExtensionConfig.EntityPrefixesWhitelist = jsonConfig.EntityPrefixesWhitelist;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.EntityPrefixesToSkip))
        {
            config.ExtensionConfig.EntityPrefixesToSkip = jsonConfig.EntityPrefixesToSkip;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.ActionsWhitelist))
        {
            config.ExtensionConfig.ActionsWhitelist = jsonConfig.ActionsWhitelist;
        }

        if (!string.IsNullOrWhiteSpace(jsonConfig.ActionsToSkip))
        {
            config.ExtensionConfig.ActionsToSkip = jsonConfig.ActionsToSkip;
        }

        if (jsonConfig.GenerateGlobalOptionSets.HasValue)
        {
            config.ExtensionConfig.GenerateGlobalOptionSets = jsonConfig.GenerateGlobalOptionSets.Value;
        }

        if (jsonConfig.Overwrite.HasValue)
        {
            config.ExtensionConfig.DeleteFilesFromOutputFolders = jsonConfig.Overwrite.Value;
        }

        if (jsonConfig.CamelCase is not null)
        {
            ApplyCamelCase(jsonConfig.CamelCase, config.ExtensionConfig);
        }

        if (jsonConfig.Extension is not null)
        {
            ApplyExtension(jsonConfig.Extension, config.ExtensionConfig);
        }
    }

    public static EarlyBoundJsonConfig FromGeneratorConfig(
        EarlyBoundGeneratorConfig config,
        string outputDirectory,
        string workingDirectory)
    {
        var output = Path.GetFullPath(outputDirectory);
        var working = Path.GetFullPath(workingDirectory);
        var relativeOutput = output.StartsWith(working + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? output[working.Length..].TrimStart(Path.DirectorySeparatorChar)
            : output;

        return new EarlyBoundJsonConfig
        {
            Output = relativeOutput,
            Namespace = config.Namespace,
            ServiceContext = config.ServiceContextName,
            EntitiesWhitelist = config.ExtensionConfig.EntitiesWhitelist,
            EntitiesToSkip = config.ExtensionConfig.EntitiesToSkip,
            EntityPrefixesWhitelist = config.ExtensionConfig.EntityPrefixesWhitelist,
            EntityPrefixesToSkip = config.ExtensionConfig.EntityPrefixesToSkip,
            GenerateMessages = config.GenerateMessages,
            GenerateGlobalOptionSets = config.ExtensionConfig.GenerateGlobalOptionSets,
            ActionsWhitelist = config.ExtensionConfig.ActionsWhitelist,
            ActionsToSkip = config.ExtensionConfig.ActionsToSkip,
            EntityTypesFolder = config.EntityTypesFolder,
            MessageTypesFolder = config.MessageTypesFolder,
            OptionSetsTypesFolder = config.OptionSetsTypesFolder,
            EmitEntityETC = config.EmitEntityETC,
            EmitVirtualAttributes = config.EmitVirtualAttributes,
            UpdateBuilderSettingsJson = config.UpdateBuilderSettingsJson,
            Overwrite = config.ExtensionConfig.DeleteFilesFromOutputFolders,
            CamelCase = new EarlyBoundJsonCamelCaseConfig
            {
                ClassNames = config.ExtensionConfig.CamelCaseClassNames,
                MemberNames = config.ExtensionConfig.CamelCaseMemberNames,
                CustomWordsDelimiter = config.ExtensionConfig.CamelCaseCustomWords,
                DictionaryPath = config.ExtensionConfig.CamelCaseNamesDictionaryRelativePath
            },
            Extension = new EarlyBoundJsonExtensionConfig
            {
                CreateOneFilePerEntity = config.ExtensionConfig.CreateOneFilePerEntity,
                CreateOneFilePerAction = config.ExtensionConfig.CreateOneFilePerAction,
                CreateOneFilePerOptionSet = config.ExtensionConfig.CreateOneFilePerOptionSet,
                GenerateAttributeNameConsts = config.ExtensionConfig.GenerateAttributeNameConsts,
                GenerateActionAttributeNameConsts = config.ExtensionConfig.GenerateActionAttributeNameConsts,
                GenerateEntityRelationships = config.ExtensionConfig.GenerateEntityRelationships,
                GenerateEnumProperties = config.ExtensionConfig.GenerateEnumProperties,
                ReplaceOptionSetPropertiesWithEnum = config.ExtensionConfig.ReplaceOptionSetPropertiesWithEnum,
                MakeReferenceTypesNullable = config.ExtensionConfig.MakeReferenceTypesNullable,
                MakeAllFieldsEditable = config.ExtensionConfig.MakeAllFieldsEditable,
                DeleteFilesFromOutputFolders = config.ExtensionConfig.DeleteFilesFromOutputFolders,
                OptionSetNames = config.ExtensionConfig.OptionSetNames,
                FilePrefixText = config.ExtensionConfig.FilePrefixText
            }
        };
    }

    private static void ApplyCamelCase(EarlyBoundJsonCamelCaseConfig camelCase, ExtensionConfig target)
    {
        if (camelCase.ClassNames.HasValue)
        {
            target.CamelCaseClassNames = camelCase.ClassNames.Value;
        }

        if (camelCase.MemberNames.HasValue)
        {
            target.CamelCaseMemberNames = camelCase.MemberNames.Value;
        }

        var customWords = camelCase.ResolveCustomWords();
        if (!string.IsNullOrWhiteSpace(customWords))
        {
            target.CamelCaseCustomWords = customWords;
        }

        if (!string.IsNullOrWhiteSpace(camelCase.DictionaryPath))
        {
            target.CamelCaseNamesDictionaryRelativePath = camelCase.DictionaryPath;
        }
    }

    private static void ApplyExtension(EarlyBoundJsonExtensionConfig extension, ExtensionConfig target)
    {
        if (extension.CamelCaseClassNames.HasValue)
        {
            target.CamelCaseClassNames = extension.CamelCaseClassNames.Value;
        }

        if (extension.CamelCaseMemberNames.HasValue)
        {
            target.CamelCaseMemberNames = extension.CamelCaseMemberNames.Value;
        }

        var camelCaseCustomWords = extension.ResolveCamelCaseCustomWords();
        if (!string.IsNullOrWhiteSpace(camelCaseCustomWords))
        {
            target.CamelCaseCustomWords = camelCaseCustomWords;
        }

        if (!string.IsNullOrWhiteSpace(extension.CamelCaseDictionaryPath))
        {
            target.CamelCaseNamesDictionaryRelativePath = extension.CamelCaseDictionaryPath;
        }

        if (extension.CreateOneFilePerEntity.HasValue)
        {
            target.CreateOneFilePerEntity = extension.CreateOneFilePerEntity.Value;
        }

        if (extension.CreateOneFilePerAction.HasValue)
        {
            target.CreateOneFilePerAction = extension.CreateOneFilePerAction.Value;
        }

        if (extension.CreateOneFilePerOptionSet.HasValue)
        {
            target.CreateOneFilePerOptionSet = extension.CreateOneFilePerOptionSet.Value;
        }

        if (extension.GenerateAttributeNameConsts.HasValue)
        {
            target.GenerateAttributeNameConsts = extension.GenerateAttributeNameConsts.Value;
        }

        if (extension.GenerateActionAttributeNameConsts.HasValue)
        {
            target.GenerateActionAttributeNameConsts = extension.GenerateActionAttributeNameConsts.Value;
        }

        if (extension.GenerateEntityRelationships.HasValue)
        {
            target.GenerateEntityRelationships = extension.GenerateEntityRelationships.Value;
        }

        if (extension.GenerateEnumProperties.HasValue)
        {
            target.GenerateEnumProperties = extension.GenerateEnumProperties.Value;
        }

        if (extension.ReplaceOptionSetPropertiesWithEnum.HasValue)
        {
            target.ReplaceOptionSetPropertiesWithEnum = extension.ReplaceOptionSetPropertiesWithEnum.Value;
        }

        if (extension.MakeReferenceTypesNullable.HasValue)
        {
            target.MakeReferenceTypesNullable = extension.MakeReferenceTypesNullable.Value;
        }

        if (extension.MakeAllFieldsEditable.HasValue)
        {
            target.MakeAllFieldsEditable = extension.MakeAllFieldsEditable.Value;
        }

        if (extension.DeleteFilesFromOutputFolders.HasValue)
        {
            target.DeleteFilesFromOutputFolders = extension.DeleteFilesFromOutputFolders.Value;
        }

        if (!string.IsNullOrWhiteSpace(extension.OptionSetNames))
        {
            target.OptionSetNames = extension.OptionSetNames;
        }

        if (!string.IsNullOrWhiteSpace(extension.FilePrefixText))
        {
            target.FilePrefixText = extension.FilePrefixText;
        }
    }
}