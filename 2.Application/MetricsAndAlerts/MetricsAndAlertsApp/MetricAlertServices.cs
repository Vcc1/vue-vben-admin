using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scriban;
using Scriban.Runtime;

namespace MetricsAndAlertsApp;

public sealed class TemplateRenderer : ITemplateRenderer
{
    public string Render(string template, MetricTemplateContext context)
    {
        var rawTemplate = template ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawTemplate))
        {
            return string.Empty;
        }

        var result = rawTemplate;
        if (rawTemplate.Contains("{{", StringComparison.Ordinal))
        {
            var parsed = Template.Parse(rawTemplate);
            if (parsed.HasErrors)
            {
                var message = string.Join("; ", parsed.Messages.Select(x => x.Message));
                throw new InvalidOperationException($"Template parse failed. {message}");
            }

            var globals = new ScriptObject();
            foreach (var item in context.Model)
            {
                globals[item.Key] = ToScriptValue(item.Value);
            }

            var templateContext = new TemplateContext();
            templateContext.PushGlobal(globals);
            result = parsed.Render(templateContext);
        }

        foreach (var item in context.FlatValues)
        {
            result = result.Replace(
                "{" + item.Key + "}",
                item.Value ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    static object? ToScriptValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IReadOnlyDictionary<string, object?> objectDictionary)
        {
            var script = new ScriptObject();
            foreach (var item in objectDictionary)
            {
                script[item.Key] = ToScriptValue(item.Value);
            }

            return script;
        }

        if (value is IReadOnlyDictionary<string, double> doubleDictionary)
        {
            var script = new ScriptObject();
            foreach (var item in doubleDictionary)
            {
                script[item.Key] = item.Value;
            }

            return script;
        }

        if (value is IReadOnlyDictionary<string, string?> stringDictionary)
        {
            var script = new ScriptObject();
            foreach (var item in stringDictionary)
            {
                script[item.Key] = item.Value;
            }

            return script;
        }

        if (value is IEnumerable<object?> objectList && value is not string)
        {
            return objectList.Select(ToScriptValue).ToArray();
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(ToScriptValue(item));
            }

            return list;
        }

        return value;
    }
}

public sealed class MetricQueryRunner : IMetricQueryRunner
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly IPrometheusQueryClient _prometheus;

    public MetricQueryRunner(IPrometheusQueryClient prometheus)
    {
        _prometheus = prometheus;
    }

    public async Task<MetricQueryRunResult> RunAsync(
        IReadOnlyList<MetricQueryItem> queryItems,
        string? previousValuesJson,
        CancellationToken cancellationToken = default)
    {
        var normalizedItems = MetricRuleHelpers.NormalizeQueryItems(queryItems);
        var previousValues = DeserializeValues(previousValuesJson);
        var currentValues = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in normalizedItems)
        {
            var samples = await _prometheus.QueryInstantAsync(item.Promql, cancellationToken);
            foreach (var sample in samples)
            {
                var instance = string.IsNullOrWhiteSpace(sample.InstanceKey)
                    ? MetricRuleHelpers.GlobalInstanceKey
                    : sample.InstanceKey.Trim();
                if (!currentValues.TryGetValue(instance, out var values))
                {
                    values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    currentValues[instance] = values;
                }

                values[item.Name] = sample.Value;
            }
        }

        var instances = currentValues
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new MetricInstanceQueryValues
            {
                Instance = x.Key,
                Values = x.Value,
                PreviousValues = previousValues.TryGetValue(x.Key, out var previous)
                    ? previous
                    : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            })
            .ToList();

        return new MetricQueryRunResult
        {
            Instances = instances,
            LastValuesJson = JsonSerializer.Serialize(currentValues, JsonOptions)
        };
    }

    static Dictionary<string, Dictionary<string, double>> DeserializeValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(json, JsonOptions);
            return values is null
                ? new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, Dictionary<string, double>>(values, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

public sealed class MetricFormulaEvaluator : IMetricFormulaEvaluator
{
    public MetricFormulaEvaluationResult Evaluate(string formula, MetricFormulaContext context)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            throw new ArgumentException("Formula is required.", nameof(formula));
        }

        var parser = new FormulaParser(formula, context);
        var value = parser.Parse();
        if (!value.TryGetBoolean(out var result))
        {
            throw new InvalidOperationException("Formula result must be boolean.");
        }

        return new MetricFormulaEvaluationResult { IsMatch = result };
    }

    public void Validate(string formula, IReadOnlyCollection<string> variableNames)
    {
        var values = variableNames.ToDictionary(x => x, _ => 1d, StringComparer.OrdinalIgnoreCase);
        var previous = variableNames.ToDictionary(x => x, _ => 0d, StringComparer.OrdinalIgnoreCase);
        Evaluate(formula, new MetricFormulaContext
        {
            Values = values,
            PreviousValues = previous
        });
    }

    enum TokenKind
    {
        End,
        Number,
        Identifier,
        Operator,
        OpenParen,
        CloseParen,
        Comma
    }

    readonly record struct Token(TokenKind Kind, string Text, double Number);

    readonly struct FormulaValue
    {
        readonly bool _isBoolean;
        readonly double _number;
        readonly bool _boolean;

        FormulaValue(double number)
        {
            _number = number;
            _boolean = false;
            _isBoolean = false;
        }

        FormulaValue(bool boolean)
        {
            _number = 0;
            _boolean = boolean;
            _isBoolean = true;
        }

        public static FormulaValue Number(double value) => new(value);
        public static FormulaValue Boolean(bool value) => new(value);

        public double AsNumber()
        {
            if (_isBoolean)
            {
                throw new InvalidOperationException("Boolean value cannot be used as a number.");
            }

            return _number;
        }

        public bool AsBoolean()
        {
            if (!_isBoolean)
            {
                throw new InvalidOperationException("Numeric value cannot be used as a boolean.");
            }

            return _boolean;
        }

        public bool TryGetBoolean(out bool value)
        {
            value = _boolean;
            return _isBoolean;
        }

        public bool IsBoolean => _isBoolean;
    }

    sealed class FormulaParser
    {
        readonly string _formula;
        readonly MetricFormulaContext _context;
        int _position;
        Token _current;

        public FormulaParser(string formula, MetricFormulaContext context)
        {
            _formula = formula;
            _context = context;
            _current = ReadNextToken();
        }

        public FormulaValue Parse()
        {
            var value = ParseOr();
            Expect(TokenKind.End);
            return value;
        }

        FormulaValue ParseOr()
        {
            var left = ParseAnd();
            while (IsOperator("||"))
            {
                Next();
                var right = ParseAnd();
                left = FormulaValue.Boolean(left.AsBoolean() || right.AsBoolean());
            }

            return left;
        }

        FormulaValue ParseAnd()
        {
            var left = ParseEquality();
            while (IsOperator("&&"))
            {
                Next();
                var right = ParseEquality();
                left = FormulaValue.Boolean(left.AsBoolean() && right.AsBoolean());
            }

            return left;
        }

        FormulaValue ParseEquality()
        {
            var left = ParseComparison();
            while (IsOperator("==") || IsOperator("!="))
            {
                var op = _current.Text;
                Next();
                var right = ParseComparison();
                bool equal;
                if (left.IsBoolean || right.IsBoolean)
                {
                    equal = left.AsBoolean() == right.AsBoolean();
                }
                else
                {
                    equal = Math.Abs(left.AsNumber() - right.AsNumber()) < 0.0000001d;
                }

                left = FormulaValue.Boolean(op == "==" ? equal : !equal);
            }

            return left;
        }

        FormulaValue ParseComparison()
        {
            var left = ParseAdditive();
            while (IsOperator(">") || IsOperator(">=") || IsOperator("<") || IsOperator("<="))
            {
                var op = _current.Text;
                Next();
                var right = ParseAdditive();
                var leftNumber = left.AsNumber();
                var rightNumber = right.AsNumber();
                left = FormulaValue.Boolean(op switch
                {
                    ">" => leftNumber > rightNumber,
                    ">=" => leftNumber >= rightNumber,
                    "<" => leftNumber < rightNumber,
                    "<=" => leftNumber <= rightNumber,
                    _ => throw new InvalidOperationException($"Unsupported operator {op}.")
                });
            }

            return left;
        }

        FormulaValue ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (IsOperator("+") || IsOperator("-"))
            {
                var op = _current.Text;
                Next();
                var right = ParseMultiplicative();
                left = FormulaValue.Number(op == "+"
                    ? left.AsNumber() + right.AsNumber()
                    : left.AsNumber() - right.AsNumber());
            }

            return left;
        }

        FormulaValue ParseMultiplicative()
        {
            var left = ParseUnary();
            while (IsOperator("*") || IsOperator("/") || IsOperator("%"))
            {
                var op = _current.Text;
                Next();
                var right = ParseUnary();
                var rightNumber = right.AsNumber();
                if ((op == "/" || op == "%") && Math.Abs(rightNumber) < double.Epsilon)
                {
                    throw new DivideByZeroException("Formula divides by zero.");
                }

                left = FormulaValue.Number(op switch
                {
                    "*" => left.AsNumber() * rightNumber,
                    "/" => left.AsNumber() / rightNumber,
                    "%" => left.AsNumber() % rightNumber,
                    _ => throw new InvalidOperationException($"Unsupported operator {op}.")
                });
            }

            return left;
        }

        FormulaValue ParseUnary()
        {
            if (IsOperator("!"))
            {
                Next();
                return FormulaValue.Boolean(!ParseUnary().AsBoolean());
            }

            if (IsOperator("-"))
            {
                Next();
                return FormulaValue.Number(-ParseUnary().AsNumber());
            }

            if (IsOperator("+"))
            {
                Next();
                return FormulaValue.Number(ParseUnary().AsNumber());
            }

            return ParsePrimary();
        }

        FormulaValue ParsePrimary()
        {
            if (_current.Kind == TokenKind.Number)
            {
                var number = _current.Number;
                Next();
                return FormulaValue.Number(number);
            }

            if (_current.Kind == TokenKind.Identifier)
            {
                var name = _current.Text;
                Next();
                if (_current.Kind == TokenKind.OpenParen)
                {
                    return ParseFunction(name);
                }

                if (string.Equals(name, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return FormulaValue.Boolean(true);
                }

                if (string.Equals(name, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return FormulaValue.Boolean(false);
                }

                return FormulaValue.Number(GetCurrentValue(name));
            }

            if (_current.Kind == TokenKind.OpenParen)
            {
                Next();
                var value = ParseOr();
                Expect(TokenKind.CloseParen);
                return value;
            }

            throw new InvalidOperationException($"Unexpected token '{_current.Text}'.");
        }

        FormulaValue ParseFunction(string name)
        {
            if (string.Equals(name, "changed", StringComparison.OrdinalIgnoreCase))
            {
                Next();
                var variable = ReadVariableArgument(name);
                Expect(TokenKind.CloseParen);
                return FormulaValue.Boolean(HasPreviousValue(variable) &&
                    Math.Abs(GetCurrentValue(variable) - GetPreviousValue(variable)) > 0.0000001d);
            }

            if (string.Equals(name, "delta", StringComparison.OrdinalIgnoreCase))
            {
                Next();
                var variable = ReadVariableArgument(name);
                Expect(TokenKind.CloseParen);
                var delta = HasPreviousValue(variable)
                    ? GetCurrentValue(variable) - GetPreviousValue(variable)
                    : 0d;
                return FormulaValue.Number(delta);
            }

            if (string.Equals(name, "abs", StringComparison.OrdinalIgnoreCase))
            {
                Next();
                var value = ParseOr().AsNumber();
                Expect(TokenKind.CloseParen);
                return FormulaValue.Number(Math.Abs(value));
            }

            if (string.Equals(name, "between", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "outside", StringComparison.OrdinalIgnoreCase))
            {
                Next();
                var value = ParseOr().AsNumber();
                Expect(TokenKind.Comma);
                var min = ParseOr().AsNumber();
                Expect(TokenKind.Comma);
                var max = ParseOr().AsNumber();
                Expect(TokenKind.CloseParen);

                var isBetween = value >= min && value <= max;
                return FormulaValue.Boolean(string.Equals(name, "between", StringComparison.OrdinalIgnoreCase)
                    ? isBetween
                    : !isBetween);
            }

            throw new InvalidOperationException($"Unsupported function '{name}'.");
        }

        string ReadVariableArgument(string functionName)
        {
            if (_current.Kind != TokenKind.Identifier)
            {
                throw new InvalidOperationException($"{functionName}() requires a variable name.");
            }

            var variable = _current.Text;
            Next();
            return variable;
        }

        double GetCurrentValue(string name)
        {
            if (!_context.Values.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException($"Unknown variable '{name}'.");
            }

            return value;
        }

        bool HasPreviousValue(string name)
        {
            return _context.PreviousValues.ContainsKey(name);
        }

        double GetPreviousValue(string name)
        {
            return _context.PreviousValues.TryGetValue(name, out var value) ? value : 0d;
        }

        void Expect(TokenKind kind)
        {
            if (_current.Kind != kind)
            {
                throw new InvalidOperationException($"Expected {kind}, got '{_current.Text}'.");
            }

            Next();
        }

        bool IsOperator(string op)
        {
            return _current.Kind == TokenKind.Operator && _current.Text == op;
        }

        void Next()
        {
            _current = ReadNextToken();
        }

        Token ReadNextToken()
        {
            while (_position < _formula.Length && char.IsWhiteSpace(_formula[_position]))
            {
                _position++;
            }

            if (_position >= _formula.Length)
            {
                return new Token(TokenKind.End, "", 0);
            }

            var ch = _formula[_position];
            if (char.IsDigit(ch) || ch == '.')
            {
                return ReadNumber();
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                return ReadIdentifier();
            }

            _position++;
            return ch switch
            {
                '(' => new Token(TokenKind.OpenParen, "(", 0),
                ')' => new Token(TokenKind.CloseParen, ")", 0),
                ',' => new Token(TokenKind.Comma, ",", 0),
                '>' or '<' or '=' or '!' when TryReadTwoCharOperator(ch, '=', out var op) => new Token(TokenKind.Operator, op, 0),
                '&' when TryReadRequiredSecond('&') => new Token(TokenKind.Operator, "&&", 0),
                '|' when TryReadRequiredSecond('|') => new Token(TokenKind.Operator, "||", 0),
                '+' or '-' or '*' or '/' or '%' or '>' or '<' or '!' => new Token(TokenKind.Operator, ch.ToString(), 0),
                _ => throw new InvalidOperationException($"Unsupported character '{ch}'.")
            };
        }

        Token ReadNumber()
        {
            var start = _position;
            var hasExponent = false;
            while (_position < _formula.Length)
            {
                var ch = _formula[_position];
                if (char.IsDigit(ch) || ch == '.')
                {
                    _position++;
                    continue;
                }

                if ((ch == 'e' || ch == 'E') && !hasExponent)
                {
                    hasExponent = true;
                    _position++;
                    if (_position < _formula.Length && (_formula[_position] == '+' || _formula[_position] == '-'))
                    {
                        _position++;
                    }

                    continue;
                }

                break;
            }

            var raw = _formula[start.._position];
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidOperationException($"Invalid number '{raw}'.");
            }

            return new Token(TokenKind.Number, raw, value);
        }

        Token ReadIdentifier()
        {
            var start = _position;
            while (_position < _formula.Length && (char.IsLetterOrDigit(_formula[_position]) || _formula[_position] == '_'))
            {
                _position++;
            }

            var value = _formula[start.._position];
            return new Token(TokenKind.Identifier, value, 0);
        }

        bool TryReadTwoCharOperator(char first, char second, out string op)
        {
            if (_position < _formula.Length && _formula[_position] == second)
            {
                _position++;
                op = first.ToString() + second;
                return true;
            }

            op = first.ToString();
            return false;
        }

        bool TryReadRequiredSecond(char expected)
        {
            if (_position >= _formula.Length || _formula[_position] != expected)
            {
                throw new InvalidOperationException($"Expected '{expected}{expected}'.");
            }

            _position++;
            return true;
        }
    }
}

public sealed class MetricAlertRuleService
{
    readonly IMetricRulesRepository _metricRepository;
    readonly IMetricsAndAlertsRepository _targetRepository;
    readonly IMetricQueryRunner _queryRunner;
    readonly IMetricFormulaEvaluator _formulaEvaluator;
    readonly ITemplateRenderer _templateRenderer;
    readonly IAlertNotifier _notifier;

    public MetricAlertRuleService(
        IMetricRulesRepository metricRepository,
        IMetricsAndAlertsRepository targetRepository,
        IMetricQueryRunner queryRunner,
        IMetricFormulaEvaluator formulaEvaluator,
        ITemplateRenderer templateRenderer,
        IAlertNotifier notifier)
    {
        _metricRepository = metricRepository;
        _targetRepository = targetRepository;
        _queryRunner = queryRunner;
        _formulaEvaluator = formulaEvaluator;
        _templateRenderer = templateRenderer;
        _notifier = notifier;
    }

    public async Task<MetricAlertRuleDto> UpsertAsync(UpsertMetricAlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTargetExistsAsync(request.TargetKey, request.TargetType, cancellationToken);
        var items = MetricRuleHelpers.NormalizeQueryItems(request.QueryItems);
        _formulaEvaluator.Validate(request.Formula, items.Select(x => x.Name).ToArray());

        var now = DateTime.UtcNow;
        var rule = await _metricRepository.UpsertMetricAlertRuleAsync(new MetricAlertRule
        {
            Id = request.Id,
            TargetKey = request.TargetKey.Trim(),
            TargetType = request.TargetType,
            RuleName = string.IsNullOrWhiteSpace(request.RuleName) ? request.Formula.Trim() : request.RuleName.Trim(),
            QueryItemsJson = MetricRuleHelpers.SerializeQueryItems(items),
            Formula = request.Formula.Trim(),
            DurationMinutes = Math.Max(0, request.DurationMinutes),
            NotifyMinuteQueue = MetricsAndAlertsUtilities.FormatNotifyMinuteQueue(request.NotifyMinuteQueue),
            NotifyUsers = MetricsAndAlertsUtilities.FormatNotifyUsers(request.NotifyUsers),
            MessageTemplate = MetricRuleHelpers.TrimToNull(request.MessageTemplate),
            RecoverTemplate = MetricRuleHelpers.TrimToNull(request.RecoverTemplate),
            AggregateInstances = request.AggregateInstances,
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        return ToDto(rule);
    }

    public async Task<IReadOnlyList<MetricAlertRuleDto>> ListAsync(MetricAlertRuleListQuery query, CancellationToken cancellationToken = default)
    {
        var rules = await _metricRepository.ListMetricAlertRulesAsync(query, cancellationToken);
        return rules.Select(ToDto).ToArray();
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _metricRepository.DisableMetricAlertRuleAsync(id, cancellationToken);
    }

    public async Task<MetricAlertPreviewDto> PreviewAsync(MetricAlertRulePreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTargetExistsAsync(request.TargetKey, request.TargetType, cancellationToken);
        var items = MetricRuleHelpers.NormalizeQueryItems(request.QueryItems);
        _formulaEvaluator.Validate(request.Formula, items.Select(x => x.Name).ToArray());

        var run = await _queryRunner.RunAsync(items, null, cancellationToken);
        var target = await _targetRepository.GetMonitorTargetAsync(request.TargetKey.Trim(), request.TargetType, cancellationToken);
        var evaluationInputs = request.AggregateInstances
            ? new[] { MetricRuleHelpers.AggregateInstances(run.Instances, items) }
            : run.Instances.ToArray();
        var evaluations = MetricRuleHelpers.EvaluateFormula(request.Formula, evaluationInputs, _formulaEvaluator);
        var abnormal = evaluations.Where(x => x.IsAbnormal).ToArray();
        var primaryInstance = request.AggregateInstances
            ? MetricRuleHelpers.AggregateInstances(run.Instances, items)
            : abnormal.Length == 1
                ? run.Instances.FirstOrDefault(x => string.Equals(x.Instance, abnormal[0].Instance, StringComparison.OrdinalIgnoreCase))
                : null;
        var context = MetricRuleHelpers.BuildMetricTemplateContext(
            request.TargetKey.Trim(),
            MetricsAndAlertsUtilities.GetDisplayName(request.TargetKey.Trim(), target?.TargetName),
            request.TargetType,
            string.IsNullOrWhiteSpace(request.RuleName) ? request.Formula : request.RuleName,
            request.Formula,
            request.AggregateInstances,
            primaryInstance,
            run.Instances,
            abnormal,
            null,
            null,
            null,
            null,
            null);

        return new MetricAlertPreviewDto
        {
            AggregateInstances = request.AggregateInstances,
            IsAbnormal = abnormal.Length > 0,
            Formula = request.Formula,
            Instances = evaluations,
            CurrentValues = MetricRuleHelpers.FormatCurrentValues(run.Instances),
            AbnormalInstances = MetricRuleHelpers.FormatAbnormalInstances(abnormal.Select(x => x.Instance)),
            MessageContent = _templateRenderer.Render(
                string.IsNullOrWhiteSpace(request.MessageTemplate)
                    ? MetricRuleHelpers.DefaultMetricAlertTemplate
                    : request.MessageTemplate,
                context),
            RecoverContent = _templateRenderer.Render(
                string.IsNullOrWhiteSpace(request.RecoverTemplate)
                    ? MetricRuleHelpers.DefaultMetricRecoverTemplate
                    : request.RecoverTemplate,
                context)
        };
    }

    public async Task<TestSendMessageDto> TestSendAsync(MetricAlertRuleTestSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = await PreviewAsync(new MetricAlertRulePreviewRequest
        {
            TargetKey = request.TargetKey,
            TargetType = request.TargetType,
            RuleName = request.RuleName,
            QueryItems = request.QueryItems,
            Formula = request.Formula,
            MessageTemplate = request.MessageTemplate,
            RecoverTemplate = request.RecoverTemplate,
            AggregateInstances = request.AggregateInstances
        }, cancellationToken);

        var targetKey = request.TargetKey.Trim();
        var target = await _targetRepository.GetMonitorTargetAsync(targetKey, request.TargetType, cancellationToken);
        var targetName = MetricsAndAlertsUtilities.GetDisplayName(targetKey, target?.TargetName);
        var toUsers = request.NotifyUsers.Count > 0
            ? request.NotifyUsers
            : MetricsAndAlertsUtilities.ParseNotifyUsers(target?.NotifyUsers);
        var title = $"【测试】指标告警：{(string.IsNullOrWhiteSpace(request.RuleName) ? request.Formula : request.RuleName)}";
        var notifyResult = await _notifier.SendMessageAsync(new AlertMessage
        {
            AlertType = AlertType.MetricFormulaAlert,
            NotifyType = NotifyType.Alert,
            TargetKey = targetKey,
            TargetName = targetName,
            TargetType = request.TargetType,
            ToUsers = toUsers,
            Title = title,
            Content = preview.MessageContent,
            CurrentValue = preview.CurrentValues,
            ThresholdValue = request.Formula
        }, cancellationToken);
        await AddTestNotifyLogAsync(
            AlertType.MetricFormulaAlert,
            request.TargetType,
            targetKey,
            title,
            preview.MessageContent,
            notifyResult,
            cancellationToken);

        return new TestSendMessageDto
        {
            IsSuccess = notifyResult.IsSuccess,
            ErrorMessage = notifyResult.ErrorMessage,
            Title = title,
            Content = preview.MessageContent
        };
    }

    async Task AddTestNotifyLogAsync(
        AlertType alertType,
        MonitorTargetType targetType,
        string targetKey,
        string title,
        string content,
        NotifyResult notifyResult,
        CancellationToken cancellationToken)
    {
        await _targetRepository.AddAlertNotifyLogAsync(new AlertNotifyLog
        {
            AlertStateId = 0,
            AlertType = alertType,
            NotifyType = NotifyType.Alert,
            NotifyMinute = null,
            TargetKey = targetKey,
            TargetType = targetType,
            Title = title,
            Content = content,
            IsSuccess = notifyResult.IsSuccess,
            ErrorMessage = notifyResult.ErrorMessage,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    async Task EnsureTargetExistsAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            throw new ArgumentException("TargetKey is required.", nameof(targetKey));
        }

        var target = await _targetRepository.GetMonitorTargetAsync(targetKey.Trim(), targetType, cancellationToken);
        if (target is null)
        {
            throw new ArgumentException($"Monitor target {targetKey}/{targetType} does not exist.", nameof(targetKey));
        }
    }

    static MetricAlertRuleDto ToDto(MetricAlertRule rule)
    {
        return new MetricAlertRuleDto
        {
            Id = rule.Id,
            TargetKey = rule.TargetKey,
            TargetType = rule.TargetType,
            RuleName = rule.RuleName,
            QueryItems = MetricRuleHelpers.DeserializeQueryItems(rule.QueryItemsJson),
            Formula = rule.Formula,
            DurationMinutes = rule.DurationMinutes,
            NotifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue),
            NotifyUsers = MetricsAndAlertsUtilities.ParseNotifyUsers(rule.NotifyUsers),
            MessageTemplate = rule.MessageTemplate,
            RecoverTemplate = rule.RecoverTemplate,
            AggregateInstances = rule.AggregateInstances,
            IsEnabled = rule.IsEnabled,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
    }
}

public sealed class ScheduledMetricMessageRuleService
{
    readonly IMetricRulesRepository _metricRepository;
    readonly IMetricsAndAlertsRepository _targetRepository;
    readonly IMetricQueryRunner _queryRunner;
    readonly ITemplateRenderer _templateRenderer;
    readonly IAlertNotifier _notifier;

    public ScheduledMetricMessageRuleService(
        IMetricRulesRepository metricRepository,
        IMetricsAndAlertsRepository targetRepository,
        IMetricQueryRunner queryRunner,
        ITemplateRenderer templateRenderer,
        IAlertNotifier notifier)
    {
        _metricRepository = metricRepository;
        _targetRepository = targetRepository;
        _queryRunner = queryRunner;
        _templateRenderer = templateRenderer;
        _notifier = notifier;
    }

    public async Task<ScheduledMetricMessageRuleDto> UpsertAsync(UpsertScheduledMetricMessageRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTargetExistsAsync(request.TargetKey, request.TargetType, cancellationToken);
        var items = MetricRuleHelpers.NormalizeQueryItems(request.QueryItems);
        var timeZone = MetricRuleHelpers.ResolveTimeZone(request.TimeZoneId);
        var nextRunAt = MetricRuleHelpers.GetNextOccurrenceUtc(request.Cron, timeZone, DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var rule = await _metricRepository.UpsertScheduledMetricMessageRuleAsync(new ScheduledMetricMessageRule
        {
            Id = request.Id,
            TargetKey = request.TargetKey.Trim(),
            TargetType = request.TargetType,
            RuleName = string.IsNullOrWhiteSpace(request.RuleName) ? request.Cron.Trim() : request.RuleName.Trim(),
            QueryItemsJson = MetricRuleHelpers.SerializeQueryItems(items),
            MessageTemplate = string.IsNullOrWhiteSpace(request.MessageTemplate)
                ? MetricRuleHelpers.DefaultScheduledMessageTemplate
                : request.MessageTemplate.Trim(),
            Cron = request.Cron.Trim(),
            TimeZoneId = timeZone.Id,
            NextRunAt = nextRunAt,
            NotifyUsers = MetricsAndAlertsUtilities.FormatNotifyUsers(request.NotifyUsers),
            AggregateInstances = request.AggregateInstances,
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        return ToDto(rule);
    }

    public async Task<IReadOnlyList<ScheduledMetricMessageRuleDto>> ListAsync(ScheduledMetricMessageRuleListQuery query, CancellationToken cancellationToken = default)
    {
        var rules = await _metricRepository.ListScheduledMetricMessageRulesAsync(query, cancellationToken);
        return rules.Select(ToDto).ToArray();
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _metricRepository.DisableScheduledMetricMessageRuleAsync(id, cancellationToken);
    }

    public async Task<ScheduledMetricMessagePreviewDto> PreviewAsync(ScheduledMetricMessagePreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTargetExistsAsync(request.TargetKey, request.TargetType, cancellationToken);
        var items = MetricRuleHelpers.NormalizeQueryItems(request.QueryItems);
        var run = await _queryRunner.RunAsync(items, null, cancellationToken);
        var target = await _targetRepository.GetMonitorTargetAsync(request.TargetKey.Trim(), request.TargetType, cancellationToken);
        var targetName = MetricsAndAlertsUtilities.GetDisplayName(request.TargetKey.Trim(), target?.TargetName);
        var contents = MetricRuleHelpers.BuildRenderScopes(run.Instances, items, request.AggregateInstances)
            .Select(scope => _templateRenderer.Render(
                string.IsNullOrWhiteSpace(request.MessageTemplate)
                    ? MetricRuleHelpers.DefaultScheduledMessageTemplate
                    : request.MessageTemplate,
                MetricRuleHelpers.BuildMetricTemplateContext(
                    request.TargetKey.Trim(),
                    targetName,
                    request.TargetType,
                    request.RuleName,
                    null,
                    request.AggregateInstances,
                    scope,
                    run.Instances,
                    Array.Empty<MetricInstanceFormulaResult>(),
                    null,
                    null,
                    null,
                    null,
                    null)))
            .ToArray();

        return new ScheduledMetricMessagePreviewDto
        {
            AggregateInstances = request.AggregateInstances,
            Instances = run.Instances,
            CurrentValues = MetricRuleHelpers.FormatCurrentValues(run.Instances),
            MessageContent = MetricRuleHelpers.CombineMessages(contents)
        };
    }

    public async Task<TestSendMessageDto> TestSendAsync(ScheduledMetricMessageRuleTestSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = await PreviewAsync(new ScheduledMetricMessagePreviewRequest
        {
            TargetKey = request.TargetKey,
            TargetType = request.TargetType,
            RuleName = request.RuleName,
            QueryItems = request.QueryItems,
            MessageTemplate = request.MessageTemplate,
            AggregateInstances = request.AggregateInstances
        }, cancellationToken);

        var targetKey = request.TargetKey.Trim();
        var target = await _targetRepository.GetMonitorTargetAsync(targetKey, request.TargetType, cancellationToken);
        var targetName = MetricsAndAlertsUtilities.GetDisplayName(targetKey, target?.TargetName);
        var toUsers = request.NotifyUsers.Count > 0
            ? request.NotifyUsers
            : MetricsAndAlertsUtilities.ParseNotifyUsers(target?.NotifyUsers);
        var title = $"【测试】定时报表：{(string.IsNullOrWhiteSpace(request.RuleName) ? targetName : request.RuleName)}";
        var notifyResult = await _notifier.SendMessageAsync(new AlertMessage
        {
            AlertType = AlertType.MetricFormulaAlert,
            NotifyType = NotifyType.Alert,
            TargetKey = targetKey,
            TargetName = targetName,
            TargetType = request.TargetType,
            ToUsers = toUsers,
            Title = title,
            Content = preview.MessageContent,
            CurrentValue = preview.CurrentValues
        }, cancellationToken);
        await _metricRepository.AddScheduledMessageSendLogAsync(new ScheduledMessageSendLog
        {
            RuleId = request.Id,
            TargetKey = targetKey,
            TargetType = request.TargetType,
            Title = title,
            Content = preview.MessageContent,
            ScheduledAt = null,
            NextRunAt = null,
            IsSuccess = notifyResult.IsSuccess,
            ErrorMessage = notifyResult.ErrorMessage,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return new TestSendMessageDto
        {
            IsSuccess = notifyResult.IsSuccess,
            ErrorMessage = notifyResult.ErrorMessage,
            Title = title,
            Content = preview.MessageContent
        };
    }

    async Task EnsureTargetExistsAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            throw new ArgumentException("TargetKey is required.", nameof(targetKey));
        }

        var target = await _targetRepository.GetMonitorTargetAsync(targetKey.Trim(), targetType, cancellationToken);
        if (target is null)
        {
            throw new ArgumentException($"Monitor target {targetKey}/{targetType} does not exist.", nameof(targetKey));
        }
    }

    static ScheduledMetricMessageRuleDto ToDto(ScheduledMetricMessageRule rule)
    {
        return new ScheduledMetricMessageRuleDto
        {
            Id = rule.Id,
            TargetKey = rule.TargetKey,
            TargetType = rule.TargetType,
            RuleName = rule.RuleName,
            QueryItems = MetricRuleHelpers.DeserializeQueryItems(rule.QueryItemsJson),
            MessageTemplate = rule.MessageTemplate,
            Cron = rule.Cron,
            TimeZoneId = rule.TimeZoneId,
            NextRunAt = rule.NextRunAt,
            LastRunAt = rule.LastRunAt,
            NotifyUsers = MetricsAndAlertsUtilities.ParseNotifyUsers(rule.NotifyUsers),
            AggregateInstances = rule.AggregateInstances,
            IsEnabled = rule.IsEnabled,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
    }
}

public sealed class MetricAlertWorker : BackgroundService
{
    static readonly TimeSpan MissingInstanceStateRetention = TimeSpan.FromDays(7);

    readonly IServiceScopeFactory _scopeFactory;
    readonly IOptionsMonitor<AlertOptions> _options;
    readonly ILogger<MetricAlertWorker> _logger;

    public MetricAlertWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AlertOptions> options,
        ILogger<MetricAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.MetricAlertWorkerFailed(ex);
            }

            var seconds = Math.Max(1, _options.CurrentValue.MetricAlertWorkerIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }

    async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var metricRepository = scope.ServiceProvider.GetRequiredService<IMetricRulesRepository>();
        var stateRepository = scope.ServiceProvider.GetRequiredService<IMetricsAndAlertsRepository>();
        var queryRunner = scope.ServiceProvider.GetRequiredService<IMetricQueryRunner>();
        var formulaEvaluator = scope.ServiceProvider.GetRequiredService<IMetricFormulaEvaluator>();
        var alertState = scope.ServiceProvider.GetRequiredService<AlertStateService>();

        var rules = await metricRepository.ListEnabledMetricAlertRulesAsync(cancellationToken);
        foreach (var rule in rules)
        {
            try
            {
                var queryItems = MetricRuleHelpers.DeserializeQueryItems(rule.QueryItemsJson);
                var run = await queryRunner.RunAsync(queryItems, rule.LastValuesJson, cancellationToken);
                await metricRepository.UpdateMetricAlertRuleLastValuesAsync(rule.Id, run.LastValuesJson, cancellationToken);

                var results = BuildMetricAlertResults(rule, queryItems, run, formulaEvaluator);
                foreach (var result in results)
                {
                    await alertState.HandleCheckResultAsync(result, cancellationToken);
                }

                await RecoverRuleErrorStateAsync(rule, queryItems, run, stateRepository, alertState, cancellationToken);
                if (!rule.AggregateInstances)
                {
                    var currentAlertKeys = results
                        .Select(x => x.AlertKey)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!)
                        .ToArray();
                    await CleanupExpiredInstanceStatesAsync(rule, currentAlertKeys, stateRepository, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await alertState.HandleCheckResultAsync(BuildMetricQueryErrorResult(rule, ex.Message), cancellationToken);
            }
        }
    }

    static List<AlertCheckResult> BuildMetricAlertResults(
        MetricAlertRule rule,
        IReadOnlyList<MetricQueryItem> queryItems,
        MetricQueryRunResult run,
        IMetricFormulaEvaluator formulaEvaluator)
    {
        var evaluationInputs = rule.AggregateInstances
            ? new[] { MetricRuleHelpers.AggregateInstances(run.Instances, queryItems) }
            : run.Instances.ToArray();
        var evaluations = MetricRuleHelpers.EvaluateFormula(rule.Formula, evaluationInputs, formulaEvaluator);
        var evaluationError = evaluations.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ErrorMessage));
        if (evaluationError is not null)
        {
            throw new InvalidOperationException(evaluationError.ErrorMessage);
        }

        var notifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue);
        var notifyUsers = MetricsAndAlertsUtilities.ParseNotifyUsers(rule.NotifyUsers);
        if (rule.AggregateInstances)
        {
            var aggregateInstance = evaluationInputs[0];
            var aggregateEvaluation = evaluations[0];
            return
            [
                new AlertCheckResult
                {
                    AlertType = AlertType.MetricFormulaAlert,
                    RuleId = rule.Id,
                    AlertKey = MetricsAndAlertsUtilities.BuildAlertKey(AlertType.MetricFormulaAlert, rule.Id),
                    TargetKey = rule.TargetKey,
                    TargetType = rule.TargetType,
                    IsNormal = !aggregateEvaluation.IsAbnormal,
                    DurationMinutes = rule.DurationMinutes,
                    NotifyMinuteQueue = notifyMinuteQueue,
                    CurrentValue = MetricRuleHelpers.FormatSingleInstanceValues(aggregateInstance),
                    ThresholdValue = rule.Formula,
                    RuleName = rule.RuleName,
                    Formula = rule.Formula,
                    Instance = aggregateInstance.Instance,
                    CurrentValues = MetricRuleHelpers.FormatCurrentValues([aggregateInstance]),
                    AbnormalInstances = aggregateEvaluation.IsAbnormal
                        ? MetricRuleHelpers.FormatAbnormalInstances([aggregateEvaluation.Instance])
                        : "-",
                    MessageTemplate = rule.MessageTemplate,
                    RecoverTemplate = rule.RecoverTemplate,
                    AggregateInstances = true,
                    MetricCurrentInstance = aggregateInstance,
                    MetricInstances = [aggregateInstance],
                    MetricAbnormalInstances = aggregateEvaluation.IsAbnormal ? [aggregateEvaluation] : [],
                    NotifyUsers = notifyUsers
                }
            ];
        }

        var results = new List<AlertCheckResult>(run.Instances.Count);
        for (var index = 0; index < run.Instances.Count; index++)
        {
            var instance = run.Instances[index];
            var evaluation = evaluations[index];
            results.Add(new AlertCheckResult
            {
                AlertType = AlertType.MetricFormulaAlert,
                RuleId = rule.Id,
                AlertKey = MetricsAndAlertsUtilities.BuildAlertKey(AlertType.MetricFormulaAlert, rule.Id, instance.Instance),
                TargetKey = rule.TargetKey,
                TargetType = rule.TargetType,
                IsNormal = !evaluation.IsAbnormal,
                DurationMinutes = rule.DurationMinutes,
                NotifyMinuteQueue = notifyMinuteQueue,
                CurrentValue = MetricRuleHelpers.FormatSingleInstanceValues(instance),
                ThresholdValue = rule.Formula,
                RuleName = rule.RuleName,
                Formula = rule.Formula,
                Instance = instance.Instance,
                CurrentValues = MetricRuleHelpers.FormatCurrentValues([instance]),
                AbnormalInstances = evaluation.IsAbnormal
                    ? MetricRuleHelpers.FormatAbnormalInstances([instance.Instance])
                    : "-",
                MessageTemplate = rule.MessageTemplate,
                RecoverTemplate = rule.RecoverTemplate,
                AggregateInstances = false,
                MetricCurrentInstance = instance,
                MetricInstances = [instance],
                MetricAbnormalInstances = evaluation.IsAbnormal ? [evaluation] : [],
                NotifyUsers = notifyUsers
            });
        }

        return results;
    }

    static AlertCheckResult BuildMetricQueryErrorResult(MetricAlertRule rule, string errorMessage)
    {
        return new AlertCheckResult
        {
            AlertType = AlertType.MetricFormulaAlert,
            RuleId = rule.Id,
            AlertKey = MetricsAndAlertsUtilities.BuildAlertKey(AlertType.MetricFormulaAlert, rule.Id, MetricRuleHelpers.RuleScopeInstanceKey),
            TargetKey = rule.TargetKey,
            TargetType = rule.TargetType,
            IsNormal = false,
            DurationMinutes = rule.DurationMinutes,
            NotifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue),
            CurrentValue = "query_error",
            ThresholdValue = rule.Formula,
            ErrorMessage = errorMessage,
            RuleName = rule.RuleName,
            Formula = rule.Formula,
            Instance = MetricRuleHelpers.RuleScopeInstanceKey,
            CurrentValues = "query_error",
            AbnormalInstances = MetricRuleHelpers.RuleScopeInstanceKey,
            MessageTemplate = rule.MessageTemplate,
            RecoverTemplate = rule.RecoverTemplate,
            AggregateInstances = rule.AggregateInstances,
            NotifyUsers = MetricsAndAlertsUtilities.ParseNotifyUsers(rule.NotifyUsers)
        };
    }

    static async Task RecoverRuleErrorStateAsync(
        MetricAlertRule rule,
        IReadOnlyList<MetricQueryItem> queryItems,
        MetricQueryRunResult run,
        IMetricsAndAlertsRepository stateRepository,
        AlertStateService alertState,
        CancellationToken cancellationToken)
    {
        var ruleErrorKey = MetricsAndAlertsUtilities.BuildAlertKey(
            AlertType.MetricFormulaAlert,
            rule.Id,
            MetricRuleHelpers.RuleScopeInstanceKey);
        var existingState = await stateRepository.GetAlertStateByKeyAsync(ruleErrorKey, cancellationToken);
        if (existingState is null)
        {
            return;
        }

        var aggregateInstance = rule.AggregateInstances
            ? MetricRuleHelpers.AggregateInstances(run.Instances, queryItems)
            : null;
        var metricInstances = rule.AggregateInstances && aggregateInstance is not null
            ? new List<MetricInstanceQueryValues> { aggregateInstance }
            : run.Instances.ToList();
        var currentValue = rule.AggregateInstances && aggregateInstance is not null
            ? MetricRuleHelpers.FormatSingleInstanceValues(aggregateInstance)
            : MetricRuleHelpers.FormatCurrentValues(run.Instances);

        await alertState.HandleCheckResultAsync(new AlertCheckResult
        {
            AlertType = AlertType.MetricFormulaAlert,
            RuleId = rule.Id,
            AlertKey = ruleErrorKey,
            TargetKey = rule.TargetKey,
            TargetType = rule.TargetType,
            IsNormal = true,
            DurationMinutes = rule.DurationMinutes,
            NotifyMinuteQueue = MetricsAndAlertsUtilities.ParseNotifyMinuteQueue(rule.NotifyMinuteQueue),
            CurrentValue = currentValue,
            ThresholdValue = rule.Formula,
            RuleName = rule.RuleName,
            Formula = rule.Formula,
            CurrentValues = currentValue,
            AbnormalInstances = "-",
            MessageTemplate = rule.MessageTemplate,
            RecoverTemplate = rule.RecoverTemplate,
            AggregateInstances = rule.AggregateInstances,
            MetricCurrentInstance = aggregateInstance,
            MetricInstances = metricInstances,
            NotifyUsers = MetricsAndAlertsUtilities.ParseNotifyUsers(rule.NotifyUsers)
        }, cancellationToken);
    }

    static async Task CleanupExpiredInstanceStatesAsync(
        MetricAlertRule rule,
        IReadOnlyCollection<string> currentAlertKeys,
        IMetricsAndAlertsRepository stateRepository,
        CancellationToken cancellationToken)
    {
        var existingStates = await stateRepository.ListAlertStatesByRuleAsync(AlertType.MetricFormulaAlert, rule.Id, cancellationToken);
        if (existingStates.Count == 0)
        {
            return;
        }

        var currentLookup = new HashSet<string>(currentAlertKeys, StringComparer.OrdinalIgnoreCase);
        var ruleErrorKey = MetricsAndAlertsUtilities.BuildAlertKey(
            AlertType.MetricFormulaAlert,
            rule.Id,
            MetricRuleHelpers.RuleScopeInstanceKey);
        var legacyRuleKey = MetricsAndAlertsUtilities.BuildAlertKey(AlertType.MetricFormulaAlert, rule.Id);
        var cutoff = DateTime.UtcNow - MissingInstanceStateRetention;
        var staleStateIds = existingStates
            .Where(x => !string.Equals(x.AlertKey, ruleErrorKey, StringComparison.OrdinalIgnoreCase))
            .Where(x =>
                string.Equals(x.AlertKey, legacyRuleKey, StringComparison.OrdinalIgnoreCase)
                || (!currentLookup.Contains(x.AlertKey) && x.UpdatedAt <= cutoff))
            .Select(x => x.Id)
            .ToArray();
        if (staleStateIds.Length == 0)
        {
            return;
        }

        await stateRepository.DeleteAlertStatesAsync(staleStateIds, cancellationToken);
    }
}

public sealed class ScheduledMetricMessageWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IOptionsMonitor<AlertOptions> _options;
    readonly ILogger<ScheduledMetricMessageWorker> _logger;

    public ScheduledMetricMessageWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AlertOptions> options,
        ILogger<ScheduledMetricMessageWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.ScheduledMetricMessageWorkerFailed(ex);
            }

            var seconds = Math.Max(1, _options.CurrentValue.ScheduledMessageWorkerScanIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }

    async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var metricRepository = scope.ServiceProvider.GetRequiredService<IMetricRulesRepository>();
        var targetRepository = scope.ServiceProvider.GetRequiredService<IMetricsAndAlertsRepository>();
        var queryRunner = scope.ServiceProvider.GetRequiredService<IMetricQueryRunner>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<ITemplateRenderer>();
        var notifier = scope.ServiceProvider.GetRequiredService<IAlertNotifier>();

        var now = DateTime.UtcNow;
        var rules = await metricRepository.ListDueScheduledMetricMessageRulesAsync(now, cancellationToken);
        foreach (var rule in rules)
        {
            var timeZone = MetricRuleHelpers.ResolveTimeZone(rule.TimeZoneId);
            var nextRunAt = MetricRuleHelpers.GetNextOccurrenceUtc(rule.Cron, timeZone, now.AddSeconds(1));
            try
            {
                var queryItems = MetricRuleHelpers.DeserializeQueryItems(rule.QueryItemsJson);
                var run = await queryRunner.RunAsync(queryItems, null, cancellationToken);
                var target = await targetRepository.GetMonitorTargetAsync(rule.TargetKey, rule.TargetType, cancellationToken);
                var targetName = MetricsAndAlertsUtilities.GetDisplayName(rule.TargetKey, target?.TargetName);
                var explicitUsers = MetricsAndAlertsUtilities.ParseNotifyUsers(rule.NotifyUsers);
                var toUsers = explicitUsers.Count > 0
                    ? explicitUsers
                    : MetricsAndAlertsUtilities.ParseNotifyUsers(target?.NotifyUsers);

                foreach (var scopeInstance in MetricRuleHelpers.BuildRenderScopes(run.Instances, queryItems, rule.AggregateInstances))
                {
                    var content = templateRenderer.Render(
                        rule.MessageTemplate,
                        MetricRuleHelpers.BuildMetricTemplateContext(
                            rule.TargetKey,
                            targetName,
                            rule.TargetType,
                            rule.RuleName,
                            null,
                            rule.AggregateInstances,
                            scopeInstance,
                            run.Instances,
                            Array.Empty<MetricInstanceFormulaResult>(),
                            null,
                            null,
                            null,
                            nextRunAt,
                            null));
                    var title = MetricRuleHelpers.BuildScheduledMessageTitle(
                        rule.RuleName,
                        rule.AggregateInstances ? null : scopeInstance.Instance);

                    NotifyResult notifyResult;
                    try
                    {
                        notifyResult = await notifier.SendMessageAsync(new AlertMessage
                        {
                            AlertType = AlertType.MetricFormulaAlert,
                            NotifyType = NotifyType.Alert,
                            ToUsers = toUsers,
                            TargetKey = rule.TargetKey,
                            TargetName = targetName,
                            TargetType = rule.TargetType,
                            Title = title,
                            Content = content
                        }, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        notifyResult = NotifyResult.Failure(ex.Message);
                    }

                    await metricRepository.AddScheduledMessageSendLogAsync(new ScheduledMessageSendLog
                    {
                        RuleId = rule.Id,
                        TargetKey = rule.TargetKey,
                        TargetType = rule.TargetType,
                        Title = title,
                        Content = content,
                        ScheduledAt = rule.NextRunAt,
                        NextRunAt = nextRunAt,
                        IsSuccess = notifyResult.IsSuccess,
                        ErrorMessage = notifyResult.ErrorMessage,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await metricRepository.AddScheduledMessageSendLogAsync(new ScheduledMessageSendLog
                {
                    RuleId = rule.Id,
                    TargetKey = rule.TargetKey,
                    TargetType = rule.TargetType,
                    Title = MetricRuleHelpers.BuildScheduledMessageTitle(rule.RuleName, null),
                    Content = "",
                    ScheduledAt = rule.NextRunAt,
                    NextRunAt = nextRunAt,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }

            await metricRepository.UpdateScheduledMetricMessageRuleRunAsync(rule.Id, DateTime.UtcNow, nextRunAt, cancellationToken);
        }
    }
}

public static class MetricRuleHelpers
{
    public const string GlobalInstanceKey = "_global";
    public const string AggregateInstanceKey = "_aggregate";
    public const string RuleScopeInstanceKey = "_rule";

    public const string DefaultMetricAlertTemplate =
        "目标：{targetName}\n规则：{ruleName}\n公式：{formula}\n模式：{instanceMode}\n异常实例：{abnormalInstances}\n当前值：\n{currentValues}\n首次异常：{firstAbnormalAt}\n触发时间：{firingAt}";

    public const string DefaultMetricRecoverTemplate =
        "目标：{targetName}\n规则：{ruleName}\n公式：{formula}\n模式：{instanceMode}\n恢复时间：{recoveredAt}\n当前值：\n{currentValues}";

    public const string DefaultScheduledMessageTemplate =
        "目标：{targetName}\n规则：{ruleName}\n模式：{instanceMode}\n当前值：\n{currentValues}";

    static readonly Regex VariableNameRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static List<MetricQueryItem> NormalizeQueryItems(IReadOnlyList<MetricQueryItem>? items)
    {
        if (items is null || items.Count == 0)
        {
            throw new ArgumentException("At least one query item is required.", nameof(items));
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<MetricQueryItem>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || !VariableNameRegex.IsMatch(item.Name.Trim()))
            {
                throw new ArgumentException($"Invalid query item name '{item.Name}'.");
            }

            if (!names.Add(item.Name.Trim()))
            {
                throw new ArgumentException($"Duplicate query item name '{item.Name}'.");
            }

            if (string.IsNullOrWhiteSpace(item.Promql))
            {
                throw new ArgumentException($"Promql is required for query item '{item.Name}'.");
            }

            var valueMode = string.IsNullOrWhiteSpace(item.ValueMode)
                ? MetricValueModes.Instant
                : item.ValueMode.Trim().ToLowerInvariant();
            if (valueMode is not MetricValueModes.Instant and not MetricValueModes.Changed)
            {
                throw new ArgumentException($"Unsupported valueMode '{item.ValueMode}' for query item '{item.Name}'.");
            }

            var aggregateMode = string.IsNullOrWhiteSpace(item.AggregateMode)
                ? MetricAggregateModes.Average
                : item.AggregateMode.Trim().ToLowerInvariant();
            if (aggregateMode is not MetricAggregateModes.Average
                and not MetricAggregateModes.Sum
                and not MetricAggregateModes.Max
                and not MetricAggregateModes.Min
                and not MetricAggregateModes.Count)
            {
                throw new ArgumentException($"Unsupported aggregateMode '{item.AggregateMode}' for query item '{item.Name}'.");
            }

            normalized.Add(new MetricQueryItem
            {
                Name = item.Name.Trim(),
                Promql = item.Promql.Trim(),
                ValueMode = valueMode,
                AggregateMode = aggregateMode
            });
        }

        return normalized;
    }

    public static string SerializeQueryItems(IReadOnlyList<MetricQueryItem> items)
    {
        return JsonSerializer.Serialize(items, JsonOptions);
    }

    public static List<MetricQueryItem> DeserializeQueryItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<MetricQueryItem>();
        }

        return JsonSerializer.Deserialize<List<MetricQueryItem>>(json, JsonOptions) ?? new List<MetricQueryItem>();
    }

    public static List<MetricInstanceFormulaResult> EvaluateFormula(
        string formula,
        IReadOnlyList<MetricInstanceQueryValues> instances,
        IMetricFormulaEvaluator formulaEvaluator)
    {
        return instances.Select(instance =>
        {
            try
            {
                var evaluation = formulaEvaluator.Evaluate(formula, new MetricFormulaContext
                {
                    Values = instance.Values,
                    PreviousValues = instance.PreviousValues
                });

                return new MetricInstanceFormulaResult
                {
                    Instance = instance.Instance,
                    IsAbnormal = evaluation.IsMatch,
                    Values = instance.Values,
                    PreviousValues = instance.PreviousValues
                };
            }
            catch (Exception ex)
            {
                return new MetricInstanceFormulaResult
                {
                    Instance = instance.Instance,
                    Values = instance.Values,
                    PreviousValues = instance.PreviousValues,
                    ErrorMessage = ex.Message
                };
            }
        }).ToList();
    }

    public static MetricInstanceQueryValues AggregateInstances(
        IReadOnlyList<MetricInstanceQueryValues> instances,
        IReadOnlyList<MetricQueryItem> queryItems)
    {
        var normalizedItems = NormalizeQueryItems(queryItems);
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var previousValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in normalizedItems)
        {
            var currentSamples = instances
                .Where(x => x.Values.TryGetValue(item.Name, out _))
                .Select(x => x.Values[item.Name])
                .ToArray();
            if (currentSamples.Length > 0)
            {
                values[item.Name] = AggregateValues(currentSamples, item.AggregateMode);
            }

            var previousSamples = instances
                .Where(x => x.PreviousValues.TryGetValue(item.Name, out _))
                .Select(x => x.PreviousValues[item.Name])
                .ToArray();
            if (previousSamples.Length > 0)
            {
                previousValues[item.Name] = AggregateValues(previousSamples, item.AggregateMode);
            }
        }

        return new MetricInstanceQueryValues
        {
            Instance = AggregateInstanceKey,
            Values = values,
            PreviousValues = previousValues
        };
    }

    public static IReadOnlyList<MetricInstanceQueryValues> BuildRenderScopes(
        IReadOnlyList<MetricInstanceQueryValues> instances,
        IReadOnlyList<MetricQueryItem> queryItems,
        bool aggregateInstances)
    {
        if (aggregateInstances)
        {
            return new[] { AggregateInstances(instances, queryItems) };
        }

        if (instances.Count == 0)
        {
            return new[]
            {
                new MetricInstanceQueryValues
                {
                    Instance = GlobalInstanceKey
                }
            };
        }

        return instances;
    }

    public static string FormatCurrentValues(IReadOnlyList<MetricInstanceQueryValues> instances)
    {
        if (instances.Count == 0)
        {
            return "-";
        }

        return string.Join(Environment.NewLine, instances.Select(instance =>
            $"{instance.Instance}: {FormatSingleInstanceValues(instance)}"));
    }

    public static string FormatSingleInstanceValues(MetricInstanceQueryValues instance)
    {
        return FormatValueMap(instance.Values);
    }

    public static string FormatAbnormalInstances(IEnumerable<string> instances)
    {
        var values = instances
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "-" : string.Join(", ", values);
    }

    public static MetricTemplateContext BuildMetricTemplateContext(
        string targetKey,
        string targetName,
        MonitorTargetType targetType,
        string? ruleName,
        string? formula,
        bool aggregateInstances,
        MetricInstanceQueryValues? currentInstance,
        IReadOnlyList<MetricInstanceQueryValues> instances,
        IReadOnlyList<MetricInstanceFormulaResult> abnormalInstances,
        DateTime? firstAbnormalAt,
        DateTime? firingAt,
        DateTime? recoveredAt,
        DateTime? nextRunAt,
        string? errorMessage)
    {
        var resolvedRuleName = string.IsNullOrWhiteSpace(ruleName) ? (formula ?? targetKey) : ruleName;
        var currentValues = FormatCurrentValues(instances);
        var currentInstanceValues = currentInstance is null ? null : FormatSingleInstanceValues(currentInstance);
        var abnormalInstancesText = FormatAbnormalInstances(abnormalInstances.Select(x => x.Instance));
        var instanceMode = aggregateInstances ? "aggregate" : "instance";
        var abnormalLookup = abnormalInstances.ToDictionary(x => x.Instance, StringComparer.OrdinalIgnoreCase);
        var currentInstanceView = currentInstance is null
            ? null
            : BuildInstanceView(
                currentInstance.Instance,
                currentInstance.Values,
                currentInstance.PreviousValues,
                abnormalLookup.TryGetValue(currentInstance.Instance, out var currentFormula) && currentFormula.IsAbnormal,
                currentFormula?.ErrorMessage);

        var instanceViews = instances
            .Select(instance =>
            {
                abnormalLookup.TryGetValue(instance.Instance, out var formulaResult);
                return BuildInstanceView(
                    instance.Instance,
                    instance.Values,
                    instance.PreviousValues,
                    formulaResult?.IsAbnormal ?? false,
                    formulaResult?.ErrorMessage);
            })
            .Cast<object?>()
            .ToArray();
        var abnormalViews = abnormalInstances
            .Select(instance => BuildInstanceView(
                instance.Instance,
                instance.Values,
                instance.PreviousValues,
                instance.IsAbnormal,
                instance.ErrorMessage))
            .Cast<object?>()
            .ToArray();

        var model = new Dictionary<string, object?>
        {
            ["target"] = new Dictionary<string, object?>
            {
                ["key"] = targetKey,
                ["name"] = targetName,
                ["type"] = targetType.ToString(),
                ["typeName"] = MetricsAndAlertsUtilities.GetTargetTypeName(targetType)
            },
            ["rule"] = new Dictionary<string, object?>
            {
                ["name"] = resolvedRuleName,
                ["formula"] = formula,
                ["aggregateInstances"] = aggregateInstances,
                ["instanceMode"] = instanceMode
            },
            ["state"] = new Dictionary<string, object?>
            {
                ["firstAbnormalAt"] = FormatDateTime(firstAbnormalAt),
                ["firingAt"] = FormatDateTime(firingAt),
                ["recoveredAt"] = FormatDateTime(recoveredAt),
                ["nextRunAt"] = FormatDateTime(nextRunAt),
                ["now"] = FormatDateTime(DateTime.UtcNow),
                ["errorMessage"] = errorMessage
            },
            ["targetKey"] = targetKey,
            ["targetName"] = targetName,
            ["targetType"] = targetType.ToString(),
            ["targetTypeName"] = MetricsAndAlertsUtilities.GetTargetTypeName(targetType),
            ["ruleName"] = resolvedRuleName,
            ["formula"] = formula,
            ["aggregateInstances"] = aggregateInstances,
            ["instanceMode"] = instanceMode,
            ["instance"] = currentInstance?.Instance,
            ["currentInstance"] = currentInstanceView,
            ["instances"] = instanceViews,
            ["abnormalInstances"] = abnormalViews,
            ["metrics"] = currentInstance?.Values ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            ["previousMetrics"] = currentInstance?.PreviousValues ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            ["currentValues"] = currentValues,
            ["currentValuesText"] = currentValues,
            ["currentInstanceValues"] = currentInstanceValues,
            ["currentInstanceValuesText"] = currentInstanceValues,
            ["abnormalInstancesText"] = abnormalInstancesText,
            ["firstAbnormalAt"] = FormatDateTime(firstAbnormalAt),
            ["firingAt"] = FormatDateTime(firingAt),
            ["recoveredAt"] = FormatDateTime(recoveredAt),
            ["nextRunAt"] = FormatDateTime(nextRunAt),
            ["now"] = FormatDateTime(DateTime.UtcNow),
            ["errorMessage"] = errorMessage
        };

        var flatValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["targetKey"] = targetKey,
            ["targetName"] = targetName,
            ["targetType"] = targetType.ToString(),
            ["targetTypeName"] = MetricsAndAlertsUtilities.GetTargetTypeName(targetType),
            ["ruleName"] = resolvedRuleName,
            ["formula"] = formula,
            ["aggregateInstances"] = aggregateInstances ? "true" : "false",
            ["instanceMode"] = instanceMode,
            ["instance"] = currentInstance?.Instance,
            ["currentValues"] = currentValues,
            ["currentInstanceValues"] = currentInstanceValues,
            ["abnormalInstances"] = abnormalInstancesText,
            ["firstAbnormalAt"] = FormatDateTime(firstAbnormalAt),
            ["firingAt"] = FormatDateTime(firingAt),
            ["recoveredAt"] = FormatDateTime(recoveredAt),
            ["nextRunAt"] = FormatDateTime(nextRunAt),
            ["now"] = FormatDateTime(DateTime.UtcNow),
            ["errorMessage"] = errorMessage
        };

        return new MetricTemplateContext
        {
            Model = model,
            FlatValues = flatValues
        };
    }

    public static string BuildScheduledMessageTitle(string ruleName, string? instance)
    {
        var resolvedRuleName = string.IsNullOrWhiteSpace(ruleName) ? "未命名规则" : ruleName.Trim();
        return string.IsNullOrWhiteSpace(instance) || string.Equals(instance, AggregateInstanceKey, StringComparison.OrdinalIgnoreCase)
            ? $"定时报表：{resolvedRuleName}"
            : $"定时报表：{resolvedRuleName} [{instance}]";
    }

    public static string CombineMessages(IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
        {
            return string.Empty;
        }

        if (messages.Count == 1)
        {
            return messages[0];
        }

        return string.Join(
            $"{Environment.NewLine}{Environment.NewLine}-----{Environment.NewLine}{Environment.NewLine}",
            messages);
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Shanghai" : timeZoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (string.Equals(id, "Asia/Shanghai", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
    }

    public static DateTime? GetNextOccurrenceUtc(string cron, TimeZoneInfo timeZone, DateTime fromUtc)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            throw new ArgumentException("Cron is required.", nameof(cron));
        }

        var normalizedCron = cron.Trim();
        var partCount = normalizedCron
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Length;
        var cronFormat = partCount switch
        {
            5 => CronFormat.Standard,
            6 => CronFormat.IncludeSeconds,
            _ => throw new ArgumentException(
                "Cron format is invalid. Use 5 fields (minute hour day month day-of-week) or 6 fields with seconds.",
                nameof(cron))
        };

        var expression = CronExpression.Parse(normalizedCron, cronFormat);
        var next = expression.GetNextOccurrence(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), timeZone);
        return next.HasValue
            ? DateTime.SpecifyKind(next.Value, DateTimeKind.Utc)
            : null;
    }

    public static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    static Dictionary<string, object?> BuildInstanceView(
        string instance,
        IReadOnlyDictionary<string, double> values,
        IReadOnlyDictionary<string, double> previousValues,
        bool isAbnormal,
        string? errorMessage)
    {
        return new Dictionary<string, object?>
        {
            ["instance"] = instance,
            ["values"] = values,
            ["previousValues"] = previousValues,
            ["valuesText"] = FormatValueMap(values),
            ["previousValuesText"] = FormatValueMap(previousValues),
            ["isAbnormal"] = isAbnormal,
            ["errorMessage"] = errorMessage
        };
    }

    static string FormatValueMap(IReadOnlyDictionary<string, double> values)
    {
        if (values.Count == 0)
        {
            return "-";
        }

        return string.Join(
            ", ",
            values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Key}={x.Value.ToString("G", CultureInfo.InvariantCulture)}"));
    }

    static double AggregateValues(IReadOnlyList<double> values, string aggregateMode)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        return aggregateMode switch
        {
            MetricAggregateModes.Sum => values.Sum(),
            MetricAggregateModes.Max => values.Max(),
            MetricAggregateModes.Min => values.Min(),
            MetricAggregateModes.Count => values.Count,
            _ => values.Average()
        };
    }

    static string? FormatDateTime(DateTime? utc)
    {
        if (!utc.HasValue)
        {
            return null;
        }

        var timeZone = ResolveTimeZone("Asia/Shanghai");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), timeZone);
        return local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
