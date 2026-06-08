using LitnetDownloader;
using LitnetDownloader.Values;

var slug = args.Length > 0 ? args[0] : "nasledie-rozy-tanec-dlya-demona-epizod-2-b418009";
var outFile = args.Length > 1 ? args[1] : null;

var credentials
	= args.Length > 3
		? new Credentials(args[2], args[3])
		: (Credentials?)null;

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) =>
{
	Console.WriteLine("Cancellation requested");
	cancellationTokenSource.Cancel();
};

var litnetHttpClient = new LitnetHttpClient();

if (credentials is not null)
	await litnetHttpClient.AuthenticateAsync(credentials.Value, cancellationTokenSource.Token);

var bookDownloader = new BookDownloader(litnetHttpClient);
await bookDownloader.DownloadAsEpubAsync(slug, cancellationTokenSource.Token, outFile);

Console.WriteLine("Done");