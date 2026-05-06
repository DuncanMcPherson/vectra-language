using VectraLang.Ast;
using VectraLang.Formatters;

namespace VectraLang;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            return;
        }

        if (!File.Exists(args[0]))
        {
            return;
        }
        
        var source = File.ReadAllText(args[0]);
        
        var lexer = new Lexer(source, args[0]);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var printer = new AstPrinter();
        printer.Print(program);
        // var interpreter = new Interpreter.Interpreter();
        // interpreter.Interpret(program);
    }
}