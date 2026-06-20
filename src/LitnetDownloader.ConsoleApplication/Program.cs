using System.Text;
using LitnetDownloader.ConsoleApplication.Commands;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

await new CommandApp<DownloadBookCommand>()
	.RunAsync(args);