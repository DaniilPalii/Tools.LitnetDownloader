using LitnetDownloader;

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
await bookDownloader.DownloadAsEpubAsync(
	"nasledie-rozy-tanec-dlya-demona-epizod-2-b418009",
	cancellationTokenSource.Token,
	chapterRange: 8..11);

Console.WriteLine("Done");