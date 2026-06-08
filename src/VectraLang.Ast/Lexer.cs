using VectraLang.Ast.Tokens;
using VectraLang.Core;
using VectraLang.Core.Diagnostics;

// ReSharper disable ConvertToPrimaryConstructor

namespace VectraLang.Ast;

public sealed class Lexer
{
    private readonly string _source;
    private readonly string _fileName;
    private readonly List<Token> _tokens = [];
    private readonly IVectraLogger _logger;

    private int _start;
    private int _current;
    private int _startLine = 1;
    private int _startColumn = 1;
    private int _currentLine = 1;
    private int _currentColumn = 1;

    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        // Types
        { "int", TokenType.Int },
        { "float", TokenType.Float },
        { "string", TokenType.String },
        { "bool", TokenType.Bool },
        { "void", TokenType.Void },

        // Declarations
        { "class", TokenType.Class },
        { "enum", TokenType.Enum },
        { "space", TokenType.Space},
        { "enter", TokenType.Enter},
        { "interface", TokenType.Interface },
        { "let", TokenType.Let },

        // Control Flow
        { "if", TokenType.If },
        { "else", TokenType.Else },
        { "for", TokenType.For },
        { "while", TokenType.While },
        { "return", TokenType.Return },
        { "break", TokenType.Break },
        { "continue", TokenType.Continue },

        // OOP
        { "new", TokenType.New },
        { "this", TokenType.This },
        { "static", TokenType.Static },
        { "public", TokenType.Public },
        { "private", TokenType.Private },
        { "protected", TokenType.Protected },

        // Async
        { "async", TokenType.Async },
        { "await", TokenType.Await },

        // Literals
        { "true", TokenType.True },
        { "false", TokenType.False },
        { "null", TokenType.Null },
    };

    public Lexer(string source, string fileName, IVectraLogger logger)
    {
        _source = source;
        _fileName = fileName;
        _logger = logger;
    }

    public IReadOnlyList<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            _start = _current;
            _startLine = _currentLine;
            _startColumn = _currentColumn;
            ScanToken();
        }

        _tokens.Add(MakeToken(TokenType.EndOfFile, "", null));
        return _tokens;
    }

    private void ScanToken()
    {
        var c = Advance();
        switch (c)
        {
            case '(': AddToken(TokenType.LeftParen); break;
            case ')': AddToken(TokenType.RightParen); break;
            case '{': AddToken(TokenType.LeftBrace); break;
            case '}': AddToken(TokenType.RightBrace); break;
            case '[': AddToken(TokenType.LeftBracket); break;
            case ']': AddToken(TokenType.RightBracket); break;
            case ',': AddToken(TokenType.Comma); break;
            case '.': AddToken(TokenType.Dot); break;
            case ';': AddToken(TokenType.Semicolon); break;
            case ':': AddToken(TokenType.Colon); break;
            case '%': AddToken(TokenType.Percent); break;
            case '*': AddToken(TokenType.Star); break;
            case '!': AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang); break;
            case '=': AddToken(Match('=') ? TokenType.EqualEqual : TokenType.Equal); break;
            case '<': AddToken(Match('=') ? TokenType.LessEqual : TokenType.Less); break;
            case '>': AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater); break;
            case '+': AddToken(TokenType.Plus); break;
            case '-': AddToken(TokenType.Minus); break;
            case '?': AddToken(Match('.') ? TokenType.QuestionDot : TokenType.Error); break;
            case '/':
                if (Match('/'))
                    SkipLineComment();
                else if (Match('*'))
                    SkipBlockComment();
                else
                    AddToken(TokenType.Slash);
                break;
            case ' ':
            case '\r':
            case '\t':
            case '\n':
                break;
            case '"': ScanString(); break;

            default:
                if (char.IsDigit(c)) ScanNumber();
                else if (char.IsLetter(c) || c == '_') ScanIdentifierOrKeyword();
                else AddToken(TokenType.Error);
                break;
        }
    }

    private void SkipLineComment()
    {
        while (Peek() != '\n' && !IsAtEnd())
            Advance();
    }

    private void SkipBlockComment()
    {
        while (!IsAtEnd())
        {
            if (Peek() == '*' && PeekNext() == '/')
            {
                Advance();
                Advance();
                return;
            }
            Advance();
        }
        AddToken(TokenType.Error);
    }

    private void ScanString()
    {
        while (Peek() != '"' && !IsAtEnd())
            Advance();
        if (IsAtEnd())
        {
            AddToken(TokenType.Error);
            return;
        }

        Advance();
        var value = _source[(_start + 1)..(_current - 1)];
        AddToken(TokenType.String, value);
    }

    private void ScanNumber()
    {
        while (char.IsDigit(Peek())) Advance();
        
        var isFloat = false;
        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            isFloat = true;
            Advance();
            while (char.IsDigit(Peek())) Advance();
        }
        
        var raw = _source[_start.._current];
        if (isFloat)
            AddToken(TokenType.Float, float.Parse(raw));
        else
            AddToken(TokenType.Int, int.Parse(raw));
    }

    private void ScanIdentifierOrKeyword()
    {
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_')
            Advance();
        
        var text = _source[_start.._current];
        if (!Keywords.TryGetValue(text, out var type))
        {
            type = TokenType.Identifier;
        }
        AddToken(type);
    }

    #region Helpers

    private char Peek() => IsAtEnd() ? '\0' : _source[_current];

    private char PeekNext() => _current + 1 >= _source.Length
        ? '\0'
        : _source[_current + 1];

    private char Advance()
    {
        var c = _source[_current++];
        if (c == '\n')
        {
            _currentLine++;
            _currentColumn = 1;
        }
        else if (c != '\r')
        {
            _currentColumn++;
        }

        return c;
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_current] != expected) return false;
        Advance();
        return true;
    }
    
    private bool IsAtEnd() => _current >= _source.Length;

    private Token MakeToken(TokenType type, string lexeme, object? literal)
    {
        var location = new TokenLocation(
            _fileName,
            _startLine,
            _startColumn,
            _currentLine,
            _currentColumn - 1);
        return new Token(type, lexeme, literal, location);
    }

    private void AddToken(TokenType type, Object? literal = null)
    {
        if (type == TokenType.Error)
        {
            _logger.Error("Lex", $"Unexpected character '{_source[_current]}'.", new TokenLocation(_fileName, _startLine, _startColumn, _currentLine, _currentColumn));
        }
        var lexeme = _source[_start.._current];
        _tokens.Add(MakeToken(type, lexeme, literal));
    }

    #endregion
}