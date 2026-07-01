using System.Text;
using LitnetDownloader.ConsoleApplication.Commands;
using LitnetDownloader.ConsoleApplication.DependencyInjection;
using LitnetDownloader.Core;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var services = new ServiceCollection();
services.AddSingleton<BookDownloader>();
services.AddSingleton<LitnetHttpClient>();

var app = new CommandApp<DownloadBookCommand>(
	registrar: new TypeRegistrar(services));

var returnCode = await app.RunAsync(args);

return returnCode;