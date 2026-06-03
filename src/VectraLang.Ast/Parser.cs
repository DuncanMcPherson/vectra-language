using System.Text;
using VectraLang.Ast.AstNodes;
using VectraLang.Ast.Tokens;
using VectraLang.Core;
using VectraLang.Core.Diagnostics;

// ReSharper disable ConvertToPrimaryConstructor

namespace VectraLang.Ast;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly IVectraLogger _logger;
    private int _current;

    public Parser(IReadOnlyList<Token> tokens, IVectraLogger logger)
    {
        _tokens = tokens;
        _logger = logger;
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
        } while (Match(TokenType.Dot));

        Consume(TokenType.Semicolon, "Expected ';' after space declaration.");
        _logger.Debug("Parse", "Parsed space declaration.");

        while (!IsAtEnd())
        {
            var decl = ParseDeclaration();
            space.AddDeclaration(decl);
            decl.ParentSpace = space;
        }

        return space;
    }

    private List<EnterDecl> ParseEnterDeclarations()
    {
        var location = CurrentLocation();
        if (!Match(TokenType.Enter))
            return [];
        _logger.Debug("Parse", "Parsing enter declarations.");
        List<EnterDecl> enterDeclarations = [];
        while (!Check(TokenType.Space))
        {
            var name = ParseQualifiedName();
            enterDeclarations.Add(new EnterDecl(name,
                location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine }));
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
        _logger.Debug("Parse", $"Parsing {type.Lexeme} declaration with modifiers: {string.Join(", ", modifiers.Select(m => m.Lexeme))}");
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
        var cls = new ClassDecl(name, [], [], [], [], modifiers,
            location with { EndColumn = name.Location.EndColumn, EndLine = name.Location.EndLine });
        _logger.Debug("Parse", $"Parsing class declaration. Class name: {name.Lexeme}");
        ParseClassMembers(cls);
        return cls;
    }

    private void ParseClassMembers(ClassDecl cls)
    {
        // We haven't consumed the opening brace yet
        Consume(TokenType.LeftBrace, "Expected '{' after class name.");
        _logger.Debug("Parse", "Parsing class members.");
        while (!Check(TokenType.RightBrace))
        {
            var memberModifiers = ParseModifiers();
            var type = ParseTypeNode();
            switch (type)
            {
                case InferredTypeNode:
                    _logger.Error("Parse", "Cannot infer type for class member.", Previous().Location);
                    // TODO: recover rather than throw
                    throw new ParseException("Cannot infer type for class member.", Previous().Location);
                case PrimitiveTypeNode primitive:
                {
                    if (primitive.TypeToken.Lexeme == cls.Name.Lexeme && Peek().Type == TokenType.LeftParen)
                    {
                        _logger.Debug("Parse", $"Parsing constructor for class '{cls.Name.Lexeme}'.");
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
                _logger.Debug("Parse", $"Parsed field for class member '{name.Lexeme}'");
                cls.Fields.Add(new FieldDecl(type, name, initializer, memberModifiers,
                    type.Location with
                    {
                        EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine
                    }));
                continue;
            }

            if (Peek().Type == TokenType.Semicolon)
            {
                // Consume the semicolon
                Advance();
                _logger.Debug("Parse", $"Parsed field for class member '{name.Lexeme}'");
                cls.Fields.Add(new FieldDecl(type, name, null, memberModifiers,
                    type.Location with
                    {
                        EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine
                    }));
                continue;
            }

            if (Match(TokenType.LeftParen))
            {
                _logger.Debug("Parse", $"Parsing method for class member '{name.Lexeme}'");
                ParseMethod(memberModifiers, cls, type, name);
                continue;
            }

            _logger.Debug("Parse", $"Parsing property for class member '{name.Lexeme}'");
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
        _logger.Debug("Parse", $"Parsed constructor parameters: {string.Join(", ", parameters.Select(p => p.Name.Lexeme))}");
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
        _logger.Debug("Parse", $"Parsed constructor for class '{cls.Name.Lexeme}'.");
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

        _logger.Debug("Parse", $"Parsed method parameters: {string.Join(", ", parameters.Select(p => p.Name.Lexeme))}");
        Consume(TokenType.RightParen, "Expected ')' after parameters.");

        _logger.Debug("Parse", $"Parsing method body for method '{nameToken.Lexeme}'.");
        var body = ParseBlock();
        cls.Methods.Add(new MethodDecl(nameToken, type, parameters, body, memberModifiers,
            type.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine }));
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
                {
                    _logger.Error("Parse", "Cannot have multiple getters in a property.", Previous().Location);
                    // TODO: recover rather than throw
                    throw new ParseException("Cannot have multiple getters in a property.", Previous().Location);
                }
                _logger.Debug("Parse", $"Parsing getter for property '{nameToken.Lexeme}'.");
                getter = ParseBlock();
            }
            else if (Check(TokenType.Identifier) && Peek().Lexeme == "set")
            {
                // Consume the "set"
                Advance();
                if (setter != null)
                {
                    _logger.Error("Parse", "Cannot have multiple setters in a property.", Previous().Location);
                    // TODO: recover rather than throw
                    throw new ParseException("Cannot have multiple setters in a property.", Previous().Location);
                }

                _logger.Debug("Parse", $"Parsing setter for property '{nameToken.Lexeme}'.");
                setter = ParseBlock();
            }
        }

        Consume(TokenType.RightBrace, "Expected '}' for property definition.");
        _logger.Debug("Parse", $"Parsed property '{nameToken.Lexeme}'.");
        var prop = new PropertyDecl(type, nameToken, getter, setter, memberModifiers,
            location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
        cls.Properties.Add(prop);
    }

    private InterfaceDecl ParseInterfaceDeclaration(List<Token> modifiers)
    {
        // Interface keyword already consumed
        var name = Consume(TokenType.Identifier, "Expected interface name.");
        _logger.Debug("Parse", $"Parsing interface '{name.Lexeme}'.");
        Consume(TokenType.LeftBrace, "Expected '{' after interface name.");

        List<MethodSignatureDecl> methods = [];
        while (!Check(TokenType.RightBrace) && !IsAtEnd())
        {
            var returnType = ParseTypeNode();
            var methodName = Consume(TokenType.Identifier, "Expected method name.");
            // TODO: Type parameters when we support generics
            Consume(TokenType.LeftParen, "Expected '(' after method name.");
            List<ParameterNode> parameters = [];
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    parameters.Add(ParseParameter());
                } while (Match(TokenType.Comma));
            }

            Consume(TokenType.RightParen, "Expected ')' after parameter list.");
            Consume(TokenType.Semicolon, "Expected ';' after method declaration.");
            
            _logger.Debug("Parse", $"Parsed method signature '{methodName.Lexeme}' with {parameters.Count} parameters.");
            methods.Add(new MethodSignatureDecl(methodName, returnType, parameters,
                methodName.Location with
                {
                    EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine
                }));
        }

        Consume(TokenType.RightBrace, "Expected '}' after interface members.");
        
        _logger.Debug("Parse", $"Parsed interface '{name.Lexeme}' with {methods.Count} methods.");
        return new InterfaceDecl(name, methods, modifiers,
            name.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
    }

    private EnumDecl ParseEnumDeclaration(List<Token> modifiers)
    {
        // enum keyword already consumed
        var name = Consume(TokenType.Identifier, "Expected enum name.");
        Consume(TokenType.LeftBrace, "Expected '{' after enum name.");
        
        _logger.Debug("Parse", $"Parsing enum '{name.Lexeme}'.");
 
        List<EnumVariantNode> variants = [];
        List<ParameterNode> parameters = [];
        List<MethodDecl> methods = [];
        List<FieldDecl> fields = [];

        // variants come first
        // format is name '(' parameters ')' '{' methods '}' ','
        while (Check(TokenType.Identifier)
               && PeekNext().Type == TokenType.LeftParen
               && Peek().Lexeme != name.Lexeme
               && !IsAtEnd())
        {
            variants.Add(ParseEnumVariant());
        }
        
        _logger.Debug("Parse", $"Parsed {variants.Count} variants for enum '{name.Lexeme}'.");
        
        // enum ctor
        if (Check(TokenType.Identifier) && Peek().Lexeme == name.Lexeme)
        {
            Advance();
            Consume(TokenType.LeftParen, "Expected '(' after enum name.");
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    parameters.Add(ParseParameter());
                } while (Match(TokenType.Comma));
            }

            Consume(TokenType.RightParen, "Expected ')' after enum parameters.");
            Consume(TokenType.LeftBrace, "Expected '{' after enum constructor.");
            Consume(TokenType.RightBrace, "Expected '}' to close enum constructor.");
        }

        fields.AddRange(parameters.Select(param => new FieldDecl(param.Type, param.Name, null, [], param.Location)));

        while (!Check(TokenType.RightBrace) && !IsAtEnd())
        {
            var memberModifiers = ParseModifiers();
            var returnType = ParseTypeNode();
            var methodName = Consume(TokenType.Identifier, "Expected method name.");
            methods.Add(ParseEnumMethod(memberModifiers, returnType, methodName));
        }

        Consume(TokenType.RightBrace, "Expected '}' to close enum definition.");
        _logger.Debug("Parse", $"Parsed enum '{name.Lexeme}' with {methods.Count} methods.");
        return new EnumDecl(name, parameters, fields, variants, methods, modifiers,
            name.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
    }

    private EnumVariantNode ParseEnumVariant()
    {
        _logger.Debug("Parse", "Parsing enum variant.");
        var name = Consume(TokenType.Identifier, "Expected enum variant name.");
        Consume(TokenType.LeftParen, "Expected '(' after enum variant name.");
        List<Expr> arguments = [];
        if (!Check(TokenType.RightParen))
        {
            do
            {
                arguments.Add(ParseExpression());
            } while (Match(TokenType.Comma));
        }

        Consume(TokenType.RightParen, "Expected ')' after enum variant arguments.");
        List<MethodDecl> overrides = [];
        if (Match(TokenType.LeftBrace))
        {
            while (!Check(TokenType.RightBrace) && !IsAtEnd())
            {
                var overrideMods = ParseModifiers();
                var returnType = ParseTypeNode();
                var methodName = Consume(TokenType.Identifier, "Expected method name for enum variant override.");
                overrides.Add(ParseEnumMethod(overrideMods, returnType, methodName));
            }

            Consume(TokenType.RightBrace, "Expected '}' to close enum variant overrides.");
        }
        else
        {
            Consume(TokenType.Semicolon, "Expected ';' to close enum variant.");
        }

        return new EnumVariantNode(name, arguments, overrides,
            name.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
    }

    private MethodDecl ParseEnumMethod(List<Token> modifiers, TypeNode returnType, Token methodName)
    {
        Consume(TokenType.LeftParen, "Expected '(' after method name.");
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
        return new MethodDecl(methodName, returnType, parameters, body, modifiers,
            methodName.Location with
            {
                EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine
            });
    }

    private TypeNode ParseTypeNode()
    {
        _logger.Debug("Parse", "Parsing type node.");
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
            return new GenericTypeNode(typeToken, typeArgs,
                typeToken.Location with
                {
                    EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine
                });
        }

        return new PrimitiveTypeNode(typeToken, typeToken.Location);
    }

    private ParameterNode ParseParameter()
    {
        var type = ParseTypeNode();
        var name = Consume(TokenType.Identifier, "Expected parameter name.");
        _logger.Debug("Parse", $"Parsing parameter '{name.Lexeme}'.");
        return new ParameterNode(type, name,
            type.Location with { EndColumn = name.Location.EndColumn, EndLine = name.Location.EndLine });
    }

    #region Statements

    private BlockStmt ParseBlock()
    {
        var location = CurrentLocation();
        Consume(TokenType.LeftBrace, "Expected '{'.");
        _logger.Debug("Parse", "Parsing block.");
        List<Stmt> statements = [];

        while (!Check(TokenType.RightBrace) && !IsAtEnd())
            statements.Add(ParseStatement());
        Consume(TokenType.RightBrace, "Expected '}'.");
        _logger.Debug("Parse", "Parsed block.");
        return new BlockStmt(statements,
            location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
    }

    private Stmt ParseStatement()
    {
        var location = CurrentLocation();
        _logger.Debug("Parse", "Parsing statement.");

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

        if (Match(TokenType.If)) return ParseIfStmt();
        if (Match(TokenType.While)) return ParseWhileStmt();
        if (Match(TokenType.For)) return ParseForStmt();
        if (Match(TokenType.Return)) return ParseReturnStmt();
        if (Match(TokenType.Break)) return new BreakStmt(location);
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
        return new ExprStmt(expr,
            location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
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
        return new IfStmt(condition, thenBranch, elseBranch,
            location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
    }

    private WhileStmt ParseWhileStmt()
    {
        var location = Previous().Location;
        Consume(TokenType.LeftParen, "Expected '(' after 'while'.");
        var condition = ParseExpression();
        Consume(TokenType.RightParen, "Expected ')' after while condition.");
        var body = ParseStatement();
        return new WhileStmt(condition, body,
            location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
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
            }
            else if (Match(TokenType.Let))
            {
                var type = new InferredTypeNode(Previous().Location);
                initializer = ParseVarDecl(type);
            }
            else
            {
                initializer = ParseStatement();
            }
        }
        else
        {
            Consume(TokenType.Semicolon, "Expected ';' after initializer.");
        }

        Expr? condition = null;
        if (!Check(TokenType.Semicolon))
            condition = ParseExpression();
        Consume(TokenType.Semicolon, "Expected ';' after condition.");

        Expr? incement = null;
        if (!Check(TokenType.RightParen))
            incement = ParseExpression();
        Consume(TokenType.RightParen, "Expected ')' after for clauses.");
        var body = ParseStatement();

        return new ForStmt(initializer, condition, incement, body,
            location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
    }

    private ReturnStmt ParseReturnStmt()
    {
        var location = Previous().Location;
        Expr? value = null;
        if (!Check(TokenType.Semicolon))
            value = ParseExpression();
        Consume(TokenType.Semicolon, "Expected ';' after return value.");
        return new ReturnStmt(value,
            location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
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
            return new GroupingExpr(inner,
                location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
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
            return new DestructureExpr(names, value,
                location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
        }

        if (Match(TokenType.New))
        {
            var typeName = Consume(TokenType.Identifier, "Expected class name after 'new'");
            Consume(TokenType.LeftParen, "Expected '(' after class name.");

            List<Expr> arguments = [];
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    arguments.Add(ParseExpression());
                } while (Match(TokenType.Comma));
            }

            Consume(TokenType.RightParen, "Expected ')' after constructor arguments.");
            return new NewExpr(typeName, arguments,
                location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
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
                return new AssignExpr(expr, value,
                    expr.Location with { EndColumn = value.Location.EndColumn, EndLine = value.Location.EndLine });
            _logger.Debug("Parse", "Assignment target is not a variable.", Previous().Location);
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
            left = new BinaryExpr(left, op, right,
                left.Location with { EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine });
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
            left = new BinaryExpr(left, op, right,
                left.Location with { EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine });
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
            left = new BinaryExpr(left, op, right,
                left.Location with { EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine });
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
            left = new BinaryExpr(left, op, right,
                left.Location with { EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine });
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
            left = new BinaryExpr(left, op, right,
                left.Location with { EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine });
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
            left = new BinaryExpr(left, op, right,
                left.Location with { EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine });
        }

        return left;
    }

    private Expr ParseUnary()
    {
        if (Match(TokenType.Bang, TokenType.Minus))
        {
            var op = Previous();
            var right = ParseUnary();
            return new UnaryExpr(op, right,
                op.Location with { EndColumn = right.Location.EndColumn, EndLine = right.Location.EndLine });
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
            }
            else if (Match(TokenType.Dot))
            {
                var name = Consume(TokenType.Identifier, "Expected property name after '.'.");
                expr = new GetExpr(expr, name,
                    expr.Location with { EndColumn = name.Location.EndColumn, EndLine = name.Location.EndLine });
            }
            else if (Match(TokenType.QuestionDot))
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
        return new CallExpr(callee, args,
            callee.Location with { EndColumn = Previous().Location.EndColumn, EndLine = Previous().Location.EndLine });
    }

    #endregion

    #region Helpers

    // look at current token without consuming
    private Token Peek() => _tokens[_current];

    // Look at next token without consuming
    private Token PeekNext() => _current + 1 >= _tokens.Count ? _tokens[_current] : _tokens[_current + 1];

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
        _logger.Error("Parse", message, Peek().Location);
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