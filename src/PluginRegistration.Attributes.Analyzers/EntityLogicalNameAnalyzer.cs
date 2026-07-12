using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PluginRegistration.Attributes.Analyzers
{
    /// <summary>
    /// Roslyn analyzer that validates EntityLogicalName on Custom API request/response attributes.
    /// Entity-related parameter types require EntityLogicalName; all other types must not set it.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EntityLogicalNameAnalyzer : DiagnosticAnalyzer
    {
        public const string RequiredDiagnosticId = "PRA002";
        public const string ForbiddenDiagnosticId = "PRA003";

        private static readonly DiagnosticDescriptor EntityLogicalNameRequiredRule = new(
            id: RequiredDiagnosticId,
            title: "EntityLogicalName is required for entity parameter types",
            messageFormat: "EntityLogicalName must be set when Type is '{0}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Custom API parameters and response properties of type Entity, EntityCollection, or EntityReference must specify EntityLogicalName.");

        private static readonly DiagnosticDescriptor EntityLogicalNameForbiddenRule = new(
            id: ForbiddenDiagnosticId,
            title: "EntityLogicalName is not allowed for non-entity parameter types",
            messageFormat: "EntityLogicalName must not be set when Type is '{0}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "EntityLogicalName is only valid for Custom API parameters and response properties of type Entity, EntityCollection, or EntityReference.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(EntityLogicalNameRequiredRule, EntityLogicalNameForbiddenRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        }

        private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            var attributeSyntax = (AttributeSyntax)context.Node;

            var name = attributeSyntax.Name.ToString();
            if (!name.EndsWith("CustomApiRequestParameter") &&
                !name.EndsWith("CustomApiRequestParameterAttribute") &&
                !name.EndsWith("CustomApiResponseProperty") &&
                !name.EndsWith("CustomApiResponsePropertyAttribute"))
            {
                return;
            }

            var semanticModel = context.SemanticModel;
            var symbolInfo = semanticModel.GetSymbolInfo(attributeSyntax);
            var attributeSymbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.FirstOrDefault() as IMethodSymbol;

            if (attributeSymbol?.ContainingType == null)
                return;

            var containingType = attributeSymbol.ContainingType;
            if (containingType.Name is not ("CustomApiRequestParameter" or "CustomApiRequestParameterAttribute" or
                "CustomApiResponseProperty" or "CustomApiResponsePropertyAttribute"))
            {
                return;
            }

            if (containingType.ContainingNamespace?.ToDisplayString() != "PluginRegistration.Attributes")
                return;

            if (attributeSyntax.ArgumentList == null)
                return;

            if (!TryGetParameterType(attributeSyntax, semanticModel, out var parameterType, out var typeLocation))
                return;

            var entityLogicalName = TryGetEntityLogicalName(attributeSyntax, semanticModel, out var entityLogicalNameLocation);
            var requiresEntityLogicalName = IsEntityRelatedType(parameterType);
            var typeDisplayName = parameterType.ToString();

            if (requiresEntityLogicalName && string.IsNullOrWhiteSpace(entityLogicalName))
            {
                var location = typeLocation ?? attributeSyntax.GetLocation();
                context.ReportDiagnostic(Diagnostic.Create(EntityLogicalNameRequiredRule, location, typeDisplayName));
                return;
            }

            if (!requiresEntityLogicalName && !string.IsNullOrWhiteSpace(entityLogicalName))
            {
                var location = entityLogicalNameLocation ?? attributeSyntax.GetLocation();
                context.ReportDiagnostic(Diagnostic.Create(EntityLogicalNameForbiddenRule, location, typeDisplayName));
            }
        }

        private static bool TryGetParameterType(
            AttributeSyntax attributeSyntax,
            SemanticModel semanticModel,
            out CustomApiParameterType parameterType,
            out Location? typeLocation)
        {
            parameterType = default;
            typeLocation = null;

            foreach (var argument in attributeSyntax.ArgumentList!.Arguments)
            {
                if (GetNamedArgumentName(argument) == "Type")
                {
                    typeLocation = argument.Expression.GetLocation();
                    return TryGetConstantEnumValue(semanticModel, argument.Expression, out parameterType);
                }
            }

            var positionalTypeArgument = attributeSyntax.ArgumentList.Arguments
                .FirstOrDefault(argument => argument.NameColon == null && argument.NameEquals == null);

            if (positionalTypeArgument == null)
                return false;

            var positionalArguments = attributeSyntax.ArgumentList.Arguments
                .Where(argument => argument.NameColon == null && argument.NameEquals == null)
                .ToList();

            if (positionalArguments.Count < 2)
                return false;

            var typeArgument = positionalArguments[1];
            typeLocation = typeArgument.Expression.GetLocation();
            return TryGetConstantEnumValue(semanticModel, typeArgument.Expression, out parameterType);
        }

        private static string? TryGetEntityLogicalName(
            AttributeSyntax attributeSyntax,
            SemanticModel semanticModel,
            out Location? entityLogicalNameLocation)
        {
            entityLogicalNameLocation = null;

            foreach (var argument in attributeSyntax.ArgumentList!.Arguments)
            {
                if (GetNamedArgumentName(argument) != "EntityLogicalName")
                    continue;

                entityLogicalNameLocation = argument.Expression.GetLocation();
                var constantValue = semanticModel.GetConstantValue(argument.Expression);
                return constantValue.HasValue ? constantValue.Value as string : null;
            }

            return null;
        }

        private static bool TryGetConstantEnumValue(
            SemanticModel semanticModel,
            ExpressionSyntax expression,
            out CustomApiParameterType parameterType)
        {
            parameterType = default;

            var constantValue = semanticModel.GetConstantValue(expression);
            if (!constantValue.HasValue)
                return false;

            if (constantValue.Value is int intValue)
            {
                parameterType = (CustomApiParameterType)intValue;
                return true;
            }

            if (constantValue.Value is short shortValue)
            {
                parameterType = (CustomApiParameterType)shortValue;
                return true;
            }

            return false;
        }

        private static bool IsEntityRelatedType(CustomApiParameterType parameterType) =>
            parameterType is CustomApiParameterType.Entity
                or CustomApiParameterType.EntityCollection
                or CustomApiParameterType.EntityReference;

        private static string? GetNamedArgumentName(AttributeArgumentSyntax argument) =>
            argument.NameColon?.Name.Identifier.Text ?? argument.NameEquals?.Name.Identifier.Text;

        /// <summary>
        /// Mirrors <c>PluginRegistration.Attributes.CustomApiParameterTypeEnum</c> without a runtime dependency.
        /// </summary>
        private enum CustomApiParameterType
        {
            Boolean = 0,
            DateTime = 1,
            Decimal = 2,
            Entity = 3,
            EntityCollection = 4,
            EntityReference = 5,
            Float = 6,
            Integer = 7,
            Money = 8,
            Picklist = 9,
            String = 10,
            Guid = 12,
            StringArray = 13
        }
    }
}