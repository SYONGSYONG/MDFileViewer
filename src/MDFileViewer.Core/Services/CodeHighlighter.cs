using System.Collections.Concurrent;
using System.Text;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 코드 블록 한 개에 구문 색상을 입힌다 (01 F-22, 02 §17.13). TextMate 문법으로 토큰을 나누고 토큰마다 `&lt;span class="tk-…"&gt;`를 만든다.
/// 토큰화가 느려(코드 200줄에 약 0.15초) 문서 표시 경로에서는 <see cref="TryGetCached"/>만 쓰고, <see cref="Highlight"/>는 표시 뒤에 UI 스레드 밖에서 부른다.
/// </summary>
public static class CodeHighlighter
{
    private const char LineSeparator = '\n';
    private const string SpanClose = "</span>";

    // 문법의 id·별칭·확장자에 없지만 흔히 쓰는 이름
    private static readonly Dictionary<string, string> ExtraLanguageNames = new(StringComparer.Ordinal)
    {
        ["shell"] = "shellscript",
    };

    // 문자열 안 보간(`$"{x}"`, `${x}`)을 감싸는 scope. 여기서 바깥으로 더 나가면 안의 식이 문자열 색을 물려받는다.
    private static readonly string[] EmbeddedCodeScopes = ["meta.interpolation", "meta.embedded", "meta.template.expression"];

    // 위에서부터 본다 — 좁은 규칙(`support.type.property-name`)이 넓은 규칙(`support.type`)보다 앞에 있어야 한다. 표는 02 §17.13.
    private static readonly (string Scope, string CssClass)[] ScopeClasses =
    [
        ("comment", "tk-comment"),
        ("string", "tk-string"),
        ("constant", "tk-number"),
        ("support.type.property-name", "tk-attribute"),
        ("entity.other.attribute-name", "tk-attribute"),
        ("entity.name.tag", "tk-tag"),
        ("entity.name.function", "tk-function"),
        ("support.function", "tk-function"),
        ("entity.name.type", "tk-type"),
        ("entity.name.class", "tk-type"),
        ("entity.name.namespace", "tk-type"),
        ("entity.name.section", "tk-type"),
        ("entity.other.inherited-class", "tk-type"),
        ("support.type", "tk-type"),
        ("support.class", "tk-type"),
        ("keyword", "tk-keyword"),
        ("storage", "tk-keyword"),
        ("variable.language", "tk-keyword"),
        ("markup.inserted", "tk-inserted"),
        ("markup.deleted", "tk-deleted"),
    ];

    private const string OperatorScope = "keyword.operator";

    // TextMateSharp가 여러 스레드에서 같이 써도 되는지 확인하지 못해 토큰화와 문법 로딩을 한 줄로 세운다
    private static readonly Lock TokenizerGate = new();

    // 아래 셋은 TokenizerGate 안에서만 쓴다. 처음 색을 입힐 때 만든다 — 준비에 약 1초가 걸려 앱 시작이나 문서 표시를 늦추면 안 된다
    private static RegistryOptions? _registryOptions;
    private static Registry? _registry;
    private static Dictionary<string, string>? _scopeByLanguageName;
    private static readonly Dictionary<string, IGrammar?> Grammars = new(StringComparer.Ordinal);

    // 색 입힌 결과. 문서 표시 경로가 토큰화를 기다리지 않고 읽어야 해서 TokenizerGate와 따로 둔다
    private static readonly ConcurrentDictionary<(string Language, string Code), string> Cache = new();
    private static int _cachedLength;

    /// <summary>이미 색을 입힌 적이 있으면 그 HTML을, 없으면 <c>null</c>을 돌려준다. 토큰화하지 않아 바로 끝난다.</summary>
    public static string? TryGetCached(string language, string code)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(code);
        return Cache.TryGetValue(KeyOf(language, code), out var html) ? html : null;
    }

    /// <summary>
    /// 색을 입힌 HTML(`&lt;code&gt;` 안에 넣을 조각, 줄마다 줄 끝)을 만들어 기억한다. 모르는 언어이거나 블록이 너무 크면 <c>null</c>.
    /// </summary>
    /// <param name="language">코드 블록에 적은 언어 이름. 대소문자를 가리지 않고 id·별칭·확장자(`cs`, `yml`)로 찾는다.</param>
    /// <param name="code">줄을 `\n`으로 이은 코드.</param>
    /// <exception cref="OperationCanceledException">줄 사이에서 취소를 알아챘다. 기억하지 않는다.</exception>
    public static string? Highlight(string language, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(code);
        cancellationToken.ThrowIfCancellationRequested();
        if (language.Length == 0 || code.Length > AppConstants.MaxHighlightedBlockLength)
        {
            return null;
        }

        var key = KeyOf(language, code);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        string html;
        lock (TokenizerGate)
        {
            if (FindGrammar(key.Language) is not { } grammar)
            {
                return null;
            }

            html = Tokenize(grammar, code, cancellationToken);
        }

        Remember(key, html);
        return html;
    }

    private static (string Language, string Code) KeyOf(string language, string code) => (language.ToLowerInvariant(), code);

    private static void Remember((string Language, string Code) key, string html)
    {
        if (!Cache.TryAdd(key, html))
        {
            return;
        }

        // 오래된 것부터 지우는 대신 전부 비운다 — 상한에 닿는 일이 드물고, 비워도 다시 열 때 다시 입히면 된다
        if (Interlocked.Add(ref _cachedLength, key.Code.Length) > AppConstants.HighlightCacheMaxLength)
        {
            Cache.Clear();
            Interlocked.Exchange(ref _cachedLength, 0);
        }
    }

    private static IGrammar? FindGrammar(string languageName)
    {
        if (Grammars.TryGetValue(languageName, out var known))
        {
            return known;
        }

        _registryOptions ??= new RegistryOptions(ThemeName.Light);
        _registry ??= new Registry(_registryOptions);
        _scopeByLanguageName ??= BuildLanguageNames(_registryOptions);

        // 로딩이 실패한 문법은 없는 것으로 기억한다 — 문서를 열 때마다 다시 시도해 실패 로그가 쌓이지 않게. 첫 실패는 그대로 던져 로그에 남긴다
        Grammars[languageName] = null;
        var grammar = _scopeByLanguageName.TryGetValue(languageName, out var scope) ? _registry.LoadGrammar(scope) : null;
        Grammars[languageName] = grammar;
        return grammar;
    }

    // id가 별칭·확장자보다 먼저다 — 한 언어의 확장자가 다른 언어의 id를 가리지 않게 세 번에 나눠 넣는다
    private static Dictionary<string, string> BuildLanguageNames(RegistryOptions options)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var languages = options.GetAvailableLanguages()
            .Select(language => (Language: language, Scope: options.GetScopeByLanguageId(language.Id)))
            .Where(entry => !string.IsNullOrEmpty(entry.Scope))
            .ToList();

        foreach (var (language, scope) in languages)
        {
            names.TryAdd(language.Id.ToLowerInvariant(), scope);
        }

        foreach (var (language, scope) in languages)
        {
            foreach (var alias in language.Aliases ?? [])
            {
                names.TryAdd(alias.ToLowerInvariant(), scope);
            }
        }

        foreach (var (language, scope) in languages)
        {
            foreach (var extension in language.Extensions ?? [])
            {
                names.TryAdd(extension.TrimStart('.').ToLowerInvariant(), scope);
            }
        }

        foreach (var (name, languageId) in ExtraLanguageNames)
        {
            if (names.TryGetValue(languageId, out var scope))
            {
                names.TryAdd(name, scope);
            }
        }

        return names;
    }

    private static string Tokenize(IGrammar grammar, string code, CancellationToken cancellationToken)
    {
        var html = new StringBuilder(code.Length * 2);
        IStateStack? state = null;
        foreach (var line in code.Split(LineSeparator))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 여러 줄 주석·문자열이 이어지도록 앞 줄의 상태를 넘긴다
            var result = grammar.TokenizeLine(line, state, AppConstants.HighlightLineTimeout);
            state = result.RuleStack;
            AppendLine(html, line, result.Tokens);
            html.Append(LineSeparator);
        }

        return html.ToString();
    }

    private static void AppendLine(StringBuilder html, string line, IToken[] tokens)
    {
        string? openClass = null;
        var position = 0;
        foreach (var token in tokens)
        {
            // 토큰 끝은 줄 끝(줄 바꿈 자리)을 넘을 수 있고, 시간 제한에 걸리면 줄을 다 덮지 않을 수 있다 — 글자를 빠뜨리거나 두 번 쓰지 않게 맞춘다
            var start = Math.Clamp(token.StartIndex, position, line.Length);
            var end = Math.Clamp(token.EndIndex, start, line.Length);
            if (start > position)
            {
                openClass = AppendText(html, openClass, cssClass: null, line.AsSpan(position, start - position));
            }

            if (end > start)
            {
                openClass = AppendText(html, openClass, ClassOf(token.Scopes, line[start]), line.AsSpan(start, end - start));
            }

            position = end;
        }

        if (position < line.Length)
        {
            openClass = AppendText(html, openClass, cssClass: null, line.AsSpan(position));
        }

        if (openClass is not null)
        {
            html.Append(SpanClose);
        }
    }

    // 같은 색이 이어지는 토큰(문자열의 따옴표와 내용 등)은 한 span으로 합친다. 지금 열려 있는 span의 클래스를 돌려준다.
    private static string? AppendText(StringBuilder html, string? openClass, string? cssClass, ReadOnlySpan<char> text)
    {
        if (!string.Equals(openClass, cssClass, StringComparison.Ordinal))
        {
            if (openClass is not null)
            {
                html.Append(SpanClose);
            }

            if (cssClass is not null)
            {
                html.Append("<span class=\"").Append(cssClass).Append("\">");
            }
        }

        AppendEscaped(html, text);
        return cssClass;
    }

    // Markdig가 코드 블록에 쓰는 것과 같은 네 글자만 바꾼다 — 색을 입힌 블록과 입히지 않은 블록의 글자가 같게
    private static void AppendEscaped(StringBuilder html, ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            switch (character)
            {
                case '&':
                    html.Append("&amp;");
                    break;
                case '<':
                    html.Append("&lt;");
                    break;
                case '>':
                    html.Append("&gt;");
                    break;
                case '"':
                    html.Append("&quot;");
                    break;
                default:
                    html.Append(character);
                    break;
            }
        }
    }

    // 가장 안쪽 scope부터 바깥으로 보며 처음 맞는 규칙을 쓴다. 맞는 것이 없으면 색을 입히지 않는다(변수·구두점).
    private static string? ClassOf(IReadOnlyList<string> scopes, char firstCharacter)
    {
        for (var index = scopes.Count - 1; index >= 0; index--)
        {
            var scope = scopes[index];
            if (EmbeddedCodeScopes.Any(embedded => IsScope(scope, embedded)))
            {
                return null;
            }

            // 기호 연산자(=, +)는 본문색으로 두고, 글자로 된 연산자(new, and, typeof)만 키워드 색을 입힌다
            if (IsScope(scope, OperatorScope) && !char.IsLetter(firstCharacter))
            {
                return null;
            }

            foreach (var (prefix, cssClass) in ScopeClasses)
            {
                if (IsScope(scope, prefix))
                {
                    return cssClass;
                }
            }
        }

        return null;
    }

    // `comment`는 `comment`와 `comment.line…`에 맞고 `commentary`에는 맞지 않는다
    private static bool IsScope(string scope, string prefix) =>
        scope.StartsWith(prefix, StringComparison.Ordinal) && (scope.Length == prefix.Length || scope[prefix.Length] == '.');
}
