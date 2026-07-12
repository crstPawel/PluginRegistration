using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PluginRegistration.Attributes.Analyzers
{
    /// <summary>
    /// Roslyn analyzer that reports a compile-time error when a CustomApiRegistration
    /// attribute is used with an invalid UniqueName (must contain only letters, digits and underscore).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UniqueNameAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PRA001";

        private static readonly DiagnosticDescriptor Rule = new(
            id: DiagnosticId,
            title: "Invalid Custom API UniqueName",
            messageFormat: "The UniqueName '{0}' is invalid. It may only contain letters (a-z, A-Z), digits (0-9) and the underscore (_) character.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Custom API unique names are restricted to alphanumeric characters and underscore for compatibility with Dataverse.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        }

        private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            var attributeSyntax = (AttributeSyntax)context.Node;

            // Fast path: check the attribute name by syntax (before expensive semantic model)
            var name = attributeSyntax.Name.ToString();
            if (!name.EndsWith("CustomApiRegistration") && !name.EndsWith("CustomApiRegistrationAttribute"))
                return;

            // Get semantic symbol for more accurate matching
            var semanticModel = context.SemanticModel;
            var symbolInfo = semanticModel.GetSymbolInfo(attributeSyntax);
            var attributeSymbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.FirstOrDefault() as IMethodSymbol;

            if (attributeSymbol?.ContainingType == null)
                return;

            // Match by containing type name to avoid hard dependency on full qualified name in all cases
            var containingType = attributeSymbol.ContainingType;
            if (containingType.Name != "CustomApiRegistration" &&
                containingType.Name != "CustomApiRegistrationAttribute")
                return;

            // Check the namespace
            if (containingType.ContainingNamespace?.ToDisplayString() != "PluginRegistration.Attributes")
                return;

            // The first argument in all current constructors is the uniqueName
            if (attributeSyntax.ArgumentList == null || attributeSyntax.ArgumentList.Arguments.Count == 0)
                return;

            var firstArg = attributeSyntax.ArgumentList.Arguments[0];

            // We only care about positional arguments that are the first one (uniqueName)
            // Named arguments for UniqueName are not supported (property is read-only)
            if (firstArg.NameColon != null || firstArg.NameEquals != null)
                return;

            // Get the constant value of the argument (string literal)
            var constantValue = semanticModel.GetConstantValue(firstArg.Expression);

            if (!constantValue.HasValue || constantValue.Value is not string uniqueNameValue)
            {
                // Non-constant (e.g. nameof or const from elsewhere) - we can't validate at this point,
                // or we can decide to skip. For safety we skip.
                return;
            }

            if (!IsValidUniqueName(uniqueNameValue))
            {
                var location = firstArg.Expression.GetLocation();
                var diagnostic = Diagnostic.Create(Rule, location, uniqueNameValue);
                context.ReportDiagnostic(diagnostic);
            }
        }

        /// <summary>
        /// Same rule as defined in PluginRegistration.Attributes.CustomApiValidation / CustomApiRegistration.
        /// Kept here so the analyzer assembly remains self-contained (no runtime dependency on the attributes dll).
        /// </summary>
        private static bool IsValidUniqueName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string name = value!;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }
    }
}