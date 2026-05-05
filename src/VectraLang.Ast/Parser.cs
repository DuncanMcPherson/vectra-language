using System.Text;
using VectraLang.Ast.AstNodes;
using VectraLang.Ast.Tokens;

// ReSharper disable ConvertToPrimaryConstructor

namespace VectraLang.Ast;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _current;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public VectraFile Parse()
    {
        var location = CurrentLocation();
        var enterDeclarations = ParseEnterDeclarations();
        var space = ParseSpaceDecl();

        while (space.Parent != null)
            space = space.Parent;

        return new VectraFile(space, enterDeclarations, location);
    }

    private SpaceDecl ParseSpaceDecl()
    {
        if (!Match(TokenType.Space))
            throw new ParseException("Expected space declaration.", CurrentLocation());

        var location = Previous().Location;
        SpaceDecl? space = null;
        do
        {
            var name = Consume(TokenType.Identifier, "Expected space name.");
            var newSpace = new SpaceDecl(name, space, [], [],
                location with { EndColumn = name.Location.EndColumn, EndLine = name.Location.EndLine });
            space?.AddChild(newSpace);
            space = newSpace;
        } while (!Check(TokenType.Semicolon));
        Consume(TokenType.Semicolon, "Expected ';' after space declaration.");

        while (!IsAtEnd())
        {
            var decl = ParseDeclaration();
            space.AddDeclaration(decl);
        }
        return space;
    }

    private List<EnterDecl> ParseEnterDeclarations()
    {
        var location = CurrentLocation();
        if (!Match(TokenType.Enter))
            return [];

        List<EnterDecl> enterDeclarations = [];
        while (!Check(TokenType.Space))
        {
            var name = ParseQualifiedName();
            enterDeclarations.Add(new EnterDecl(name, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine}));
            Consume(TokenType.Semicolon, "Expected ';' after enter declaration.");
        }

        return enterDeclarations;
    }

    private string ParseQualifiedName()
    {
        var sb = new StringBuilder();
        do
        {
            sb.Append(Consume(TokenType.Identifier, "Expected identifier in qualified name").Lexeme);
            if (Match(TokenType.Dot))
                sb.Append('.');
        } while (!Check(TokenType.Semicolon));
        return sb.ToString();
    }

    private List<Token> ParseModifiers()
    {
        List<Token> modifiers = [];
        while (Match(TokenType.Public, TokenType.Private, TokenType.Static))
            modifiers.Add(Previous());
        return modifiers;
    }

    private ITopLevelDecl ParseDeclaration()
    {
        var modifiers = ParseModifiers();
        if (!Match(TokenType.Class, TokenType.Interface, TokenType.Enum))
            throw new ParseException("Expected class, interface or enum declaration.", CurrentLocation());
        var type = Previous();
        return type.Type switch
        {
            TokenType.Class => ParseClassDeclaration(modifiers),
            TokenType.Interface => ParseInterfaceDeclaration(modifiers),
            TokenType.Enum => ParseEnumDeclaration(modifiers),
            _ => throw new Exception()
        };
    }

    private ClassDecl ParseClassDeclaration(List<Token> modifiers)
    {
        var location = Previous().Location;
        var name = Consume(TokenType.Identifier, "Expected class name.");
        var cls = new ClassDecl(name, [], [], [], [], modifiers, location with {EndColumn = name.Location.EndColumn, EndLine = name.Location.EndLine});
        ParseClassMembers(cls);
        return cls;
    }

    private void ParseClassMembers(ClassDecl cls)
    {
        // We haven't consumed the opening brace yet
        Consume(TokenType.LeftBrace, "Expected '{' after class name.");
        while (!Check(TokenType.RightBrace))
        {
            var memberModifiers = ParseModifiers();
            var type = ParseTypeNode();
            switch (type)
            {
                case InferredTypeNode:
                    throw new ParseException("Cannot infer type for class member.", Previous().Location);
                case PrimitiveTypeNode primitive:
                {
                    if (primitive.TypeToken.Lexeme == cls.Name.Lexeme && Peek().Type == TokenType.LeftParen)
                    {
                        ParseConstructor(memberModifiers, cls, primitive);
                        continue;
                    }

                    break;
                }
            }
            var name = Consume(TokenType.Identifier, "Expected class member name.");
            if (Peek().Type == TokenType.Equal)
            {
                // Consume the '='
                Advance();
                
                var initializer = ParseExpression();
                // End the instruction with a semicolon
                Consume(TokenType.Semicolon, "Expected ';' after field initializer.");
                cls.Fields.Add(new FieldDecl(type, name, initializer, memberModifiers, type.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine }));
                continue;
            } 
            if (Peek().Type == TokenType.Semicolon)
            {
                // Consume the semicolon
                Advance();
                cls.Fields.Add(new FieldDecl(type, name, null, memberModifiers, type.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine }));
                continue;
            }

            if (Match(TokenType.LeftParen))
            {
                ParseMethod(memberModifiers, cls, type, name);
                continue;
            }
            
            ParseProperty(memberModifiers, cls, type, name);
        }
        Consume(TokenType.RightBrace, "Expected '}' after class members.");
    }

    private void ParseConstructor(List<Token> memberModifiers, ClassDecl cls, PrimitiveTypeNode primitive)
    {
        // We have not consumed the opening parenthesis yet
        Consume(TokenType.LeftParen, "Expected '(' after class name.");
        var parameters = new List<ParameterNode>();
        while (!Check(TokenType.RightParen))
        {
            parameters.Add(ParseParameter());
            if (!Check(TokenType.Comma))
                break;
            Consume(TokenType.Comma, "Expected ',' after parameter.");
        }
        Consume(TokenType.RightParen, "Expected ')' after parameters.");
        if (Peek().Lexeme == ":")
        {
            // consume the colon
            Advance();
            // TODO: BaseType constructor calls
        }
        
        var body = ParseBlock();
        
        cls.Constructors.Add(new ConstructorDecl(
            cls.Name,
            parameters,
            body,
            memberModifiers,
            primitive.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine }
        ));
    }

    private void ParseMethod(List<Token> memberModifiers, ClassDecl cls, TypeNode type, Token nameToken)
    {
        // We have already consumed the opening parenthesis
        var parameters = new List<ParameterNode>();
        while (!Check(TokenType.RightParen))
        {
            parameters.Add(ParseParameter());
            if (!Check(TokenType.Comma))
                break;
            Consume(TokenType.Comma, "Expected ',' after parameter.");
        }
        Consume(TokenType.RightParen, "Expected ')' after parameters.");
        
        var body = ParseBlock();
        cls.Methods.Add(new MethodDecl(nameToken, type, parameters, body, memberModifiers, type.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine }));
    }
    
    private void ParseProperty(List<Token> memberModifiers, ClassDecl cls, TypeNode type, Token nameToken)
    {
        // We have not consumed the opening brace yet
        var location = Consume(TokenType.LeftBrace, "Expected '{' for property definition.").Location;
        
        BlockStmt? getter = null;
        BlockStmt? setter = null;

        while (!Check(TokenType.RightBrace))
        {
            if (Check(TokenType.Identifier) && Peek().Lexeme == "get")
            {
                // Consume the "get"
                Advance();
                if (getter != null)
                    throw new ParseException("Cannot have multiple getters in a property.", Previous().Location);
                getter = ParseBlock();
            } else if (Check(TokenType.Identifier) && Peek().Lexeme == "set")
            {
                // Consume the "set"
                Advance();
                if (setter != null)
                    throw new ParseException("Cannot have multiple setters in a property.", Previous().Location);
                setter = ParseBlock();
            }
        }

        Consume(TokenType.RightBrace, "Expected '}' for property definition.");
        var prop = new PropertyDecl(type, nameToken, getter, setter, memberModifiers, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
        cls.Properties.Add(prop);
    }

    private InterfaceDecl ParseInterfaceDeclaration(List<Token> _)
    {
        throw new NotImplementedException();
    }
    
    private EnumDecl ParseEnumDeclaration(List<Token> _)
    {
        throw new NotImplementedException();
    }

    private TypeNode ParseTypeNode()
    {
        if (Match(TokenType.Let))
            return new InferredTypeNode(Previous().Location);
        if (Match(TokenType.Void, TokenType.Bool, TokenType.Int, TokenType.Float, TokenType.String))
            return new PrimitiveTypeNode(Previous(), Previous().Location);
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

    #region Statements
    
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

    private Stmt ParseStatement()
    {
        var location = CurrentLocation();

        if (Match(TokenType.Int, TokenType.Float, TokenType.String, TokenType.Bool, TokenType.Void))
        {
            var type = new PrimitiveTypeNode(Previous(), location);
            return ParseVarDecl(type);
        }

        if (Match(TokenType.Let))
        {
            var type = new InferredTypeNode(location);
            return ParseVarDecl(type);
        }
        
        if (Match(TokenType.If))     return ParseIfStmt();
        if (Match(TokenType.While))  return ParseWhileStmt();
        if (Match(TokenType.For))    return ParseForStmt();
        if (Match(TokenType.Return)) return ParseReturnStmt();
        if (Match(TokenType.Break))  return new BreakStmt(location);
        if (Match(TokenType.Continue)) return new ContinueStmt(location);
        if (Check(TokenType.LeftBrace)) return ParseBlock();

        return ParseExprStmt();
    }

    private VarDeclStmt ParseVarDecl(TypeNode type)
    {
        var name = Consume(TokenType.Identifier, "Expected a variable name.");
        Expr? initializer = null;
        if (Match(TokenType.Equal))
        {
            initializer = ParseExpression();
        }
        Consume(TokenType.Semicolon, "Expected ';' after variable declaration.");
        return new VarDeclStmt(type, name, initializer, type.Location);
    }

    private ExprStmt ParseExprStmt()
    {
        var location = CurrentLocation();
        var expr = ParseExpression();
        Consume(TokenType.Semicolon, "Expected ';' after expression.");
        return new ExprStmt(expr, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
    }

    private IfStmt ParseIfStmt()
    {
        var location = Previous().Location;
        Consume(TokenType.LeftParen, "Expected '(' after 'if'.");
        var condition = ParseExpression();
        Consume(TokenType.RightParen, "Expected ')' after if condition.");

        var thenBranch = ParseStatement();
        Stmt? elseBranch = null;
        if (Match(TokenType.Else))
            elseBranch = ParseStatement();
        return new IfStmt(condition, thenBranch, elseBranch, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
    }

    private WhileStmt ParseWhileStmt()
    {
        var location = Previous().Location;
        Consume(TokenType.LeftParen, "Expected '(' after 'while'.");
        var condition = ParseExpression();
        Consume(TokenType.RightParen, "Expected ')' after while condition.");
        var body = ParseStatement();
        return new WhileStmt(condition, body, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
    }

    private ForStmt ParseForStmt()
    {
        var location = Previous().Location;
        Consume(TokenType.LeftParen, "Expected '(' after 'for'.");
        Stmt? initializer = null;
        if (!Check(TokenType.Semicolon))
        {
            if (Match(TokenType.Int, TokenType.Float, TokenType.String, TokenType.Bool))
            {
                var type = new PrimitiveTypeNode(Previous(), Previous().Location);
                initializer = ParseVarDecl(type);
            } else if (Match(TokenType.Let))
            {
                var type = new InferredTypeNode(Previous().Location);
                initializer = ParseVarDecl(type);
            }
            else
            {
                initializer = ParseStatement();
            }
        } else {Consume(TokenType.Semicolon, "Expected ';' after initializer.");}
        
        Expr? condition = null;
        if (!Check(TokenType.Semicolon)) 
            condition = ParseExpression();
        Consume(TokenType.Semicolon, "Expected ';' after condition.");

        Expr? incement = null;
        if (!Check(TokenType.RightParen))
            incement = ParseExpression();
        Consume(TokenType.RightParen, "Expected ')' after for clauses.");
        var body = ParseStatement();
        
        return new ForStmt(initializer, condition, incement, body, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
    }

    private ReturnStmt ParseReturnStmt()
    {
        var location = Previous().Location;
        Expr? value = null;
        if (!Check(TokenType.Semicolon))
            value = ParseExpression();
        Consume(TokenType.Semicolon, "Expected ';' after return value.");
        return new ReturnStmt(value, location with {EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine});
    }
    
    #endregion

    #region Expressions

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
            if (expr is VariableExpr or GetExpr or OptionalGetExpr)
                return new AssignExpr(expr, value, expr.Location with {EndColumn = value.Location.EndColumn, EndLine = value.Location.EndLine});
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

    #endregion
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