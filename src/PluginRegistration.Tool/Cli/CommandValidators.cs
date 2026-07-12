using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

namespace PluginRegistration.Tool.Cli;

internal static class CommandValidators
{
    public static void AddDeployValidators(
        Command command,
        Option<DirectoryInfo> pathOption,
        Option<string?> profileOption,
        Option<string?> connectionOption)
    {
        command.AddValidator(result =>
        {
            var errors = new List<string>();

            if (!PathValidation.TryValidateConfigFile(result.GetValueForOption(pathOption), out var pathError))
            {
                errors.Add(pathError);
            }

            if (string.IsNullOrWhiteSpace(result.GetValueForOption(profileOption)))
            {
                errors.Add("Profile is required. Provide --profile / -pr (for example: dev, test, prod).");
            }

            if (!ConnectionValidation.TryValidate(result.GetValueForOption(connectionOption), out var connectionError))
            {
                errors.Add(connectionError);
            }

            if (errors.Count > 0)
            {
                CliErrorReporter.ReportValidationErrors(result, errors);
            }
        });
    }

    public static void AddSyncValidators(
        Command command,
        Option<DirectoryInfo> pathOption,
        Option<string?> connectionOption)
    {
        command.AddValidator(result =>
        {
            var errors = new List<string>();

            if (!PathValidation.TryValidateDirectory(result.GetValueForOption(pathOption), out var pathError))
            {
                errors.Add(pathError);
            }

            if (!ConnectionValidation.TryValidate(result.GetValueForOption(connectionOption), out var connectionError))
            {
                errors.Add(connectionError);
            }

            if (errors.Count > 0)
            {
                CliErrorReporter.ReportValidationErrors(result, errors);
            }
        });
    }

    public static void AddWhoAmIValidators(Command command, Option<string?> connectionOption)
    {
        command.AddValidator(result =>
        {
            if (!ConnectionValidation.TryValidate(result.GetValueForOption(connectionOption), out var connectionError))
            {
                CliErrorReporter.ReportValidationErrors(result, [connectionError]);
            }
        });
    }

    public static void AddInitValidators(
        Command command,
        Option<DirectoryInfo> pathOption,
        Option<string> profilesOption)
    {
        command.AddValidator(result =>
        {
            var errors = new List<string>();

            if (!PathValidation.TryValidateDirectory(result.GetValueForOption(pathOption), out var pathError))
            {
                errors.Add(pathError);
            }

            var profiles = result.GetValueForOption(profilesOption)?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(profile => !string.IsNullOrWhiteSpace(profile))
                .ToArray();

            if (profiles is null || profiles.Length == 0)
            {
                errors.Add("At least one profile is required. Provide --profiles (for example: dev,test,prod).");
            }

            if (errors.Count > 0)
            {
                CliErrorReporter.ReportValidationErrors(result, errors);
            }
        });
    }

    public static void AddEarlyBoundValidators(
        Command command,
        Option<DirectoryInfo> pathOption,
        Option<string?> connectionOption,
        Option<bool> initConfigOption)
    {
        command.AddValidator(result =>
        {
            var errors = new List<string>();

            if (!PathValidation.TryValidateDirectory(result.GetValueForOption(pathOption), out var pathError))
            {
                errors.Add(pathError);
            }

            if (!result.GetValueForOption(initConfigOption)
                && !ConnectionValidation.TryValidate(result.GetValueForOption(connectionOption), out var connectionError))
            {
                errors.Add(connectionError);
            }

            if (errors.Count > 0)
            {
                CliErrorReporter.ReportValidationErrors(result, errors);
            }
        });
    }
}