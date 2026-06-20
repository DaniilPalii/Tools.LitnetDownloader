using System.ComponentModel;
using LitnetDownloader.Core;
using Spectre.Console.Cli;

namespace LitnetDownloader.ConsoleApplication.Commands;

public class DownloadBookCommandSettings : CommandSettings
{
	[CommandArgument(position: 0, template: "<book-url>")]
	[Description("URL of the book to download")]
	public required string BookUrl { get; init; }
}

public class DownloadBookCommand : AsyncCommand<DownloadBookCommandSettings>
{
	protected override async Task<int> ExecuteAsync(
		CommandContext context,
		DownloadBookCommandSettings settings,
		CancellationToken cancellationToken)
	{
		var litnetHttpClient = new LitnetHttpClient();

		await litnetHttpClient.AuthenticateAsync(cancellationToken);

		var bookDownloader = new BookDownloader(litnetHttpClient);
		var epubDocument = await bookDownloader.DownloadAsEpubAsync(
			settings.BookUrl,
			cancellationToken,
			chapterRange: ..1);

		epubDocument.Series = "Хеллиана Валанди";

		var filePath = epubDocument.WriteToFile();
		Console.WriteLine($"Book saved to file:\n\t\"{filePath}\"");
		Console.WriteLine("Done");
		return 0;
	}
}