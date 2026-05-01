using VectraLang.Ast.AstNodes;
using VectraLang.Ast.Tokens;

// ReSharper disable ConvertToPrimaryConstructor

namespace VectraLang.Ast;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _current = 0;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public VectraFile Parse()
    {
        var location = CurrentLocation();
        var space = ParseSpaceDecl();
        List<ITopLevelDecl> declarations = [];

        while (!IsAtEnd())
            declarations.Add(ParseDeclaration());
        return new VectraFile(space, declarations, location);
    }

    private List<Token> ParseModifiers()
    {
        List<Token> modifiers = [];
        while (Match(TokenType.Public, TokenType.Private, TokenType.Static))
            modifiers.Add(Previous());
        return modifiers;
    }

    private TypeNode ParseTypeNode()
    {
        if (Match(TokenType.Let))
            return new InferredTypeNode(Previous().Location);
        var typeToken = Consume(TokenType.Identifier, "Expected type name.");
        if (Check(TokenType.Less))
        {
            Advance();
            List<TypeNode> typeArgs = [];
            do
            {
                typeArgs.Add(ParseTypeNode());
            } while (Match(TokenType.Comma));
            Consume(TokenType.Greater, "Expected '>'.");
            return new GenericTypeNode(typeToken, typeArgs, typeToken.Location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
        }
        
        return new PrimitiveTypeNode(typeToken, typeToken.Location);
    }

    private ParameterNode ParseParameter()
    {
        var type = ParseTypeNode();
        var name = Consume(TokenType.Identifier, "Expected parameter name.");
        return new ParameterNode(type, name, type.Location with {EndColumn = name.Location.EndColumn, EndLine = name.Location.EndLine});
    }

    private BlockStmt ParseBlock()
    {
        var location = CurrentLocation();
        Consume(TokenType.LeftBrace, "Expected '{'.");
        List<Stmt> statements = [];
        
        while (!Check(TokenType.RightBrace) && !IsAtEnd()) 
            statements.Add(ParseStatement());
        Consume(TokenType.RightBrace, "Expected '}'.");
        return new BlockStmt(statements, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
    }

    private Expr ParsePrimary()
    {
        var location = CurrentLocation();
        if (Match(TokenType.Int))
            return new IntegerLiteralExpr((int)Previous().Literal!, location);
        if (Match(TokenType.Float))
            return new FloatLiteralExpr((float)Previous().Literal!, location);
        if (Match(TokenType.String))
            return new StringLiteralExpr((string)Previous().Literal!, location);
        if (Match(TokenType.True))
            return new BoolLiteralExpr(true, location);

        if (Match(TokenType.False))
            return new BoolLiteralExpr(false, location);

        if (Match(TokenType.Null))
            return new NullLiteralExpr(location);

        if (Match(TokenType.This))
            return new VariableExpr(Previous(), location);

        if (Match(TokenType.Identifier))
            return new VariableExpr(Previous(), location);

        if (Match(TokenType.LeftParen))
        {
            var inner = ParseExpression();
            Consume(TokenType.RightParen, "Expected ')'");
            return new GroupingExpr(inner, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
        }

        if (Match(TokenType.LeftBrace))
        {
            List<Token> names = [];
            do
            {
                names.Add(Consume(TokenType.Identifier, "Expected identifier in destructuring"));
            } while (Match(TokenType.Comma));
            
            Consume(TokenType.RightBrace, "Expected '}' after destructuring");
            Consume(TokenType.Equal, "Expected '=' after destructure pattern.");
            var value = ParseExpression();
            return new DestructureExpr(names, value, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
        }
        
        throw new ParseException($"Unexpected token '{Peek().Lexeme}'.", location);
    }

    private Expr ParseExpression()
    {
        return ParseAssignment();
    }

    private Expr ParseAssignment()
    {
        var expr = ParseOr();
        if (Match(TokenType.Equal))
        {
            var value = ParseAssignment();
            if (expr is VariableExpr v)
                return new AssignExpr(v.Name, value, expr.Location with {EndColumn = value.Location.EndColumn, EndLine = value.Location.EndLine});
            throw new ParseException("Invalid assignment target.", Previous().Location);
        }
        return expr;
    }

    private Expr ParseOr()
    {
        var left = ParseAnd();
        while (Match(TokenType.PipePipe))
        {
            var op = Previous();
            var right = ParseAnd();
            left = new BinaryExpr(left, op, right, left.Location with {EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine});
        }
        return left;
    }

    private Expr ParseAnd()
    {
        var left = ParseEquality();
        while (Match(TokenType.AmpAmp))
        {
            var op = Previous();
            var right = ParseEquality();
            left = new BinaryExpr(left, op, right, left.Location with {EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine});
        }
        return left;
    }

    private Expr ParseEquality()
    {
        var left = ParseComparison();
        while (Match(TokenType.EqualEqual, TokenType.BangEqual))
        {
            var op = Previous();
            var right = ParseComparison();
            left = new BinaryExpr(left, op, right, left.Location with {EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine});
        }
        return left;
    }

    private Expr ParseComparison()
    {
        var left = ParseTerm();
        while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
        {
            var op = Previous();
            var right = ParseTerm();
            left = new BinaryExpr(left, op, right, left.Location with {EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine});
        }
        return left;
    }

    private Expr ParseTerm()
    {
        var left = ParseFactor();
        while (Match(TokenType.Plus, TokenType.Minus))
        {
            var op = Previous();
            var right = ParseFactor();
            left = new BinaryExpr(left, op, right, left.Location with {EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine});
        }
        return left;
    }

    private Expr ParseFactor()
    {
        var left = ParseUnary();
        while (Match(TokenType.Star, TokenType.Slash, TokenType.Percent))
        {
            var op = Previous();
            var right = ParseUnary();
            left = new BinaryExpr(left, op, right, left.Location with {EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine});
        }
        return left;
    }

    private Expr ParseUnary()
    {
        if (Match(TokenType.Bang, TokenType.Minus))
        {
            var op = Previous();
            var right = ParseUnary();
            return new UnaryExpr(op, right, op.Location with {EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine});
        }

        return ParseCall();
    }

    private Expr ParseCall()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (Match(TokenType.LeftParen))
            {
                expr = FinishCall(expr);
            } else if (Match(TokenType.Dot))
            {
                var name = Consume(TokenType.Identifier, "Expected property name after '.'.");
                expr = new GetExpr(expr, name, expr.Location with {EndColumn = name.Location.EndColumn, EndLine = name.Location.EndLine});
            } else if (Match(TokenType.QuestionDot))
            {
                var name = Consume(TokenType.Identifier, "Expected property name after '?'.");
                expr = new OptionalGetExpr(expr, name,
                    expr.Location with { EndColumn = name.Location.EndColumn, EndLine = name.Location.EndLine });
            }
            else break;
        }

        return expr;
    }

    private CallExpr FinishCall(Expr callee)
    {
        List<Expr> args = new();

        if (!Check(TokenType.RightParen))
        {
            do
            {
                args.Add(ParseExpression());
            } while (Match(TokenType.Comma));
        }
        Consume(TokenType.RightParen, "Expected ')' after arguments.");
        return new CallExpr(callee, args, callee.Location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
    }

    #region Helpers

    // look at current token without consuming
    private Token Peek() => _tokens[_current];

// look at previous token
    private Token Previous() => _tokens[_current - 1];

// are we at the end?
    private bool IsAtEnd() => Peek().Type == TokenType.EndOfFile;

// consume and return current token
    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous();
    }

// check current token type without consuming
    private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;

// consume if current token matches any of the given types
    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }

        return false;
    }

// consume expected token or throw
    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw new ParseException(message, Peek().Location);
    }

// current token location
    private TokenLocation CurrentLocation() => Peek().Location;

    #endregion
}

public sealed class ParseException(string message, TokenLocation location) : Exception(message)
{
    public TokenLocation Location { get; } = location;
}