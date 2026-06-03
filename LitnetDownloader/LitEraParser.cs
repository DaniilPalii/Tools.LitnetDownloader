using System.Net;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace LitnetDownloader;

internal sealed class LitEraParser
{
	private readonly string bookUrl;
	private readonly (string login, string password)? credentials;
	private readonly HtmlParser htmlParser = new();
	private readonly HttpClient httpClient;
	private List<string> chapterIdList = [];
	private string csrfToken = string.Empty;

	private LitEraParser(string bookSlug, (string login, string password)? credentials = null)
	{
		bookUrl = Constants.LITERA_BOOKS_URL.TrimEnd(trimChar: '/') + "/" + bookSlug;
		this.credentials = credentials;

		var handler = new HttpClientHandler
		{
			CookieContainer = new(),
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
		};

		httpClient = new(handler) { Timeout = TimeSpan.FromSeconds(seconds: 100) };
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(input: "Browser 2.1");
		httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(input: "en-US,en;q=0.8");
		httpClient.DefaultRequestHeaders.Add(name: "x-requested-with", value: "XMLHttpRequest");
	}

	public static async Task<LitEraParser> CreateInstanceAsync(
		string bookSlug,
		(string login, string password)? credentials = null)
	{
		var litEraParser = new LitEraParser(bookSlug, credentials);
		await litEraParser.InitializeBookAsync();

		return litEraParser;
	}

	private async Task InitializeBookAsync()
	{
		var html = await httpClient.GetStringAsync(bookUrl);
		var htmlDocument = await htmlParser.ParseDocumentAsync(html);
		Console.WriteLine("Book page loaded");

		var chapterSelectElement = htmlDocument.QuerySelector(selectors: "select[name='chapter']");

		chapterIdList
			= chapterSelectElement
				?.QuerySelectorAll(selectors: "option")
				.Select(selector: o => o.GetAttribute(name: "value") ?? string.Empty)
				.Where(predicate: s => !string.IsNullOrEmpty(s))
				.ToList()
			?? [];
		Console.WriteLine($"Number of chapters: {chapterIdList.Count}");

		var csrfTokenMeta = htmlDocument.QuerySelector(selectors: "meta[name='csrf-token']");
		csrfToken = csrfTokenMeta?.GetAttribute(name: "content") ?? string.Empty;

		httpClient.DefaultRequestHeaders.Remove(name: "origin");
		httpClient.DefaultRequestHeaders.Add(name: "origin", Constants.LITERA_ORIGIN_URL);
		httpClient.DefaultRequestHeaders.Remove(name: "referer");
		httpClient.DefaultRequestHeaders.Add(name: "referer", bookUrl);
		httpClient.DefaultRequestHeaders.Remove(name: "x-csrf-token");

		if (!string.IsNullOrEmpty(csrfToken))
			httpClient.DefaultRequestHeaders.Add(name: "x-csrf-token", csrfToken);

		if (credentials.HasValue)
			await AuthenticateAsync();
	}

	private async Task AuthenticateAsync()
	{
		var content = new FormUrlEncodedContent(
			nameValueCollection:
			[
				new(key: "LoginForm[login]", credentials!.Value.login),
				new(key: "LoginForm[password]", credentials!.Value.password),
				new(key: "ajax", value: "w0"),
			]);

		var response = await httpClient.PostAsync(Constants.LITERA_LOGIN_URL, content);

		if (!response.IsSuccessStatusCode)
			throw new BadAuthorizationException();
	}

	public async Task ParseToFileAsync(string fileName)
	{
		await using var writer = new StreamWriter(
			fileName,
			append: false,
			encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

		Console.Write(value: "Progress: ");

		for (var i = 0; i < chapterIdList!.Count; i++)
		{
			var chapterId = chapterIdList[i];
			var progress = (int)(i * 100.0 / chapterIdList.Count);
			Console.Write(progress + "..");
			await GetChapterAsync(chapterId, writer);
		}

		Console.WriteLine(value: "100..OK");
	}

	private async Task GetChapterAsync(string chapterId, StreamWriter writer)
	{
		httpClient.DefaultRequestHeaders.Remove(name: "referer");
		httpClient.DefaultRequestHeaders.Add(name: "referer", value: $"{bookUrl}?c={chapterId}");

		try
		{
			for (var page = 1; page <= Constants.MAX_PAGES_PER_CHAPTER; page++)
			{
				var isLast = await GetPageAsync(chapterId, page, writer);

				if (isLast)
					break;

				await Task.Delay(Constants.WAIT_BETWEEN);
			}
		}
		catch (NoDataException ex)
		{
			Console.WriteLine(value: "Error! " + ex.Message);
		}
	}

	private async Task<bool> GetPageAsync(string chapterId, int page, StreamWriter writer)
	{
		var postParams = new FormUrlEncodedContent(
			nameValueCollection:
			[
				new(key: "chapterId", chapterId),
				new(key: "page", value: page.ToString()),
				new(key: "_csrf", csrfToken),
			]);

		var response = await httpClient.PostAsync(Constants.LITERA_GET_PAGE_URL, postParams);

		var respText = await response.Content.ReadAsStringAsync();
		using var jsonDocument = JsonDocument.Parse(respText);
		var root = jsonDocument.RootElement;
		var status = root.GetProperty(propertyName: "status").GetInt32();

		if (status is not 1)
			throw new NoDataException(message: root.GetProperty(propertyName: "data").GetString());

		var dataHtml = root.GetProperty(propertyName: "data").GetString() ?? string.Empty;
		var pageDoc = htmlParser.ParseDocument(dataHtml);

		foreach (var node in pageDoc.QuerySelectorAll(selectors: "p"))
		{
			await writer.WriteAsync(node.TextContent);
			await writer.WriteAsync(value: "\n\n");
		}

		var isLast = root.TryGetProperty(propertyName: "isLastPage", value: out var isLastString) 
			&& isLastString.GetBoolean();

		return isLast;
	}
}