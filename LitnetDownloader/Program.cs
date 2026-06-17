using System.Text;
using LitnetDownloader;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// var slug = args.Length > 0 ? args[0] : "nasledie-rozy-tanec-dlya-demona-epizod-2-b418009";
// var outFile = args.Length > 1 ? args[1] : null;

var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, _) =>
{
	Console.WriteLine("Cancellation requested");
	cancellationTokenSource.Cancel();
};

var litnetHttpClient = new LitnetHttpClient();

await litnetHttpClient.AuthenticateAsync(cancellationTokenSource.Token);

var bookDownloader = new BookDownloader(litnetHttpClient);
var epubDocument = await bookDownloader.DownloadAsEpubAsync(
	"nasledie-rozy-tanec-dlya-demona-epizod-2-b418009",
	cancellationTokenSource.Token,
	chapterRange: 8..9);

epubDocument.Series = "Хеллиана Валанди";

var filePath = epubDocument.WriteToFile();
Console.WriteLine($"Book saved to file:\n\t\"{filePath}\"");

Console.WriteLine("Done");