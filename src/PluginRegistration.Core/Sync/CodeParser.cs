using System.Text;
using System.Text.RegularExpressions;
using PluginRegistration.Attributes;
using PluginRegistration.Core.Registration;

namespace PluginRegistration.Core.Sync;

public sealed class CodeParser
{
    private const string AttributeRegex = @"([ ]*?)\[CrmPluginRegistration\(([\W\w\s]+?)(\)\])([ ]*?(\r\n|\r|\n))";
    private const string StepImageRegex = @"([ ]*?)\[CrmPluginStepImage\(([\W\w\s]+?)(\)\])([ ]*?(\r\n|\r|\n))";
    private const string RequestParameterRegex = @"([ ]*?)\[CrmCustomApiRequestParameter\(([\W\w\s]+?)(\)\])([ ]*?(\r\n|\r|\n))";
    private const string ResponsePropertyRegex = @"([ ]*?)\[CrmCustomApiResponseProperty\(([\W\w\s]+?)(\)\])([ ]*?(\r\n|\r|\n))";

    private readonly string _filePath;
    private string _code;
    private readonly Encoding _encoding;
    private readonly Dictionary<string, string> _classDeclarations;
    private readonly HashSet<string> _pluginTypes;
    private readonly HashSet<string> _workflowTypes;

    public CodeParser(
        string filePath,
        IReadOnlyCollection<string> pluginTypes,
        IReadOnlyCollection<string> workflowTypes,
        SourceCodeTypeIndex typeIndex)
    {
        _filePath = filePath;
        _code = File.ReadAllText(filePath);

        using var reader = new StreamReader(filePath, Encoding.Default, true);
        if (reader.Peek() >= 0)
        {
            reader.Read();
        }

        _encoding = reader.CurrentEncoding;
        _pluginTypes = pluginTypes.ToHashSet(StringComparer.Ordinal);
        _workflowTypes = workflowTypes.ToHashSet(StringComparer.Ordinal);

        _classDeclarations = pluginTypes
            .Concat(workflowTypes)
            .Select(typeName => (typeName, typeIndex.GetClassDeclaration(typeName)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Item2))
            .ToDictionary(pair => pair.typeName, pair => pair.Item2!, StringComparer.Ordinal);
    }

    /// <summary>
    /// Legacy constructor using regex-only detection (supports --class-regex override).
    /// </summary>
    public CodeParser(string filePath, string? customClassRegex = null)
    {
        _filePath = filePath;
        _code = File.ReadAllText(filePath);

        using var reader = new StreamReader(filePath, Encoding.Default, true);
        if (reader.Peek() >= 0)
        {
            reader.Read();
        }

        _encoding = reader.CurrentEncoding;
        _pluginTypes = new HashSet<string>(StringComparer.Ordinal);
        _workflowTypes = new HashSet<string>(StringComparer.Ordinal);
        _classDeclarations = new Dictionary<string, string>(StringComparer.Ordinal);

        var classRegex = string.IsNullOrWhiteSpace(customClassRegex)
            ? @"((public( sealed)? class (?'class'[\w]*)[\W]*?)((?'plugin':[\W]*?((IPlugin)|(PluginBase)|(Plugin)))|(?'wf':[\W]*?CodeActivity)))"
            : customClassRegex;

        const string namespaceRegex = @"namespace (?'ns'[\w.]*)";
        var classMatches = Regex.Matches(_code, classRegex).Cast<Match>().Where(m => m.Groups.Count > 3).ToArray();
        var namespaces = Regex.Matches(_code, namespaceRegex).Cast<Match>().Reverse().ToDictionary(match => match.Index);
        var classNamespaces = new Dictionary<string, string>();

        foreach (var match in classMatches)
        {
            var className = match.Groups["class"].Value;
            var namespaceMatch = namespaces.Values.FirstOrDefault(n => n.Index <= match.Index)
                ?? throw new PluginRegistrationException($"Cannot find namespace for class {className}");
            classNamespaces[className] = namespaceMatch.Groups["ns"].Value;
            var fullName = $"{namespaceMatch.Groups["ns"].Value}.{className}";

            _classDeclarations[fullName] = match.Value;

            if (match.Groups["plugin"].Length > 0)
            {
                _pluginTypes.Add(fullName);
            }

            if (match.Groups["wf"].Length > 0)
            {
                _workflowTypes.Add(fullName);
            }
        }
    }

    public string Code => _code;
    public Encoding Encoding => _encoding;
    public IReadOnlyList<string> ClassNames => _classDeclarations.Keys.ToList();
    public int PluginCount => _classDeclarations.Count;

    public bool IsPlugin(string className) => _pluginTypes.Contains(className);
    public bool IsWorkflowActivity(string className) => _workflowTypes.Contains(className);

    public int RemoveExistingAttributes()
    {
        var count = 0;
        _code = Regex.Replace(_code, AttributeRegex, _ =>
        {
            count++;
            return string.Empty;
        });
        _code = Regex.Replace(_code, StepImageRegex, _ =>
        {
            count++;
            return string.Empty;
        });
        _code = Regex.Replace(_code, RequestParameterRegex, _ =>
        {
            count++;
            return string.Empty;
        });
        _code = Regex.Replace(_code, ResponsePropertyRegex, _ =>
        {
            count++;
            return string.Empty;
        });

        return count;
    }

    public void AddCustomApiAttributes(
        PluginRegistrationAttribute attribute,
        IEnumerable<CustomApiParameterModel> requestParameters,
        IEnumerable<CustomApiParameterModel> responseProperties,
        string className)
    {
        if (!_classDeclarations.TryGetValue(className, out var classDeclaration))
        {
            throw new PluginRegistrationException($"Cannot find class {className}");
        }

        var pos = _code.IndexOf(classDeclaration, StringComparison.Ordinal);
        var lineBreak = _code.LastIndexOf("\r\n", pos - 1, StringComparison.Ordinal);
        if (lineBreak < 0)
        {
            lineBreak = _code.LastIndexOf('\n', pos - 1);
        }

        var indentation = _code.Substring(lineBreak, pos - lineBreak);
        var blocks = CustomApiCodeGenerator.GenerateBlocks(
            attribute,
            requestParameters,
            responseProperties,
            indentation);

        _code = _code.Insert(lineBreak, string.Concat(blocks));
    }

    public void AddAttribute(PluginRegistrationAttribute attribute, string className)
    {
        InsertBeforeClass(AttributeCodeGenerator.Generate(attribute, indentation: GetIndentation(className), className), className);
    }

    public void AddStepImageAttributes(
        StageEnum stage,
        string? message,
        IEnumerable<PluginStepImageModel> images,
        string className)
    {
        var indentation = GetIndentation(className);
        var blocks = images
            .Select(image => PluginStepImageCodeGenerator.Generate(stage, message, image, indentation))
            .ToList();

        if (blocks.Count == 0)
        {
            return;
        }

        InsertBeforeClass(string.Concat(blocks), className);
    }

    private string GetIndentation(string className)
    {
        if (!_classDeclarations.TryGetValue(className, out var classDeclaration))
        {
            throw new PluginRegistrationException($"Cannot find class {className}");
        }

        var pos = _code.IndexOf(classDeclaration, StringComparison.Ordinal);
        var lineBreak = _code.LastIndexOf("\r\n", pos - 1, StringComparison.Ordinal);
        if (lineBreak < 0)
        {
            lineBreak = _code.LastIndexOf('\n', pos - 1);
        }

        return _code.Substring(lineBreak, pos - lineBreak);
    }

    private void InsertBeforeClass(string text, string className)
    {
        if (!_classDeclarations.TryGetValue(className, out var classDeclaration))
        {
            throw new PluginRegistrationException($"Cannot find class {className}");
        }

        var pos = _code.IndexOf(classDeclaration, StringComparison.Ordinal);
        var lineBreak = _code.LastIndexOf("\r\n", pos - 1, StringComparison.Ordinal);
        if (lineBreak < 0)
        {
            lineBreak = _code.LastIndexOf('\n', pos - 1);
        }

        _code = _code.Insert(lineBreak, text);
    }

    public void Save()
    {
        File.WriteAllText(_filePath, _code, _encoding);
    }
}