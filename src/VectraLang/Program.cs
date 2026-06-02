using Spectre.Console.Cli;
using VectraLang.Commands;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("vectra");
    config.AddCommand<RunCommand>("run")
        .WithDescription("Run a Vectra file, module, or package.");
    config.AddCommand<BuildCommand>("build")
        .WithDescription("Build a Vectra file, module, or package.");   
});

return app.Run(args);