using LitnetDownloader;

var slug = args.Length > 0 ? args[0] : "nasledie-b9397";
var outFile = args.Length > 1 ? args[1] : $"{slug}.txt";

(string login, string password)? credentials
	= args.Length > 3
		? (args[2], args[3])
		: null;

var parser = await LitEraParser.CreateInstanceAsync(slug, credentials);

await parser.ParseToFileAsync(outFile);