using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Values;

namespace LitnetDownloader;

internal class LitnetHttpClient
{
	public TimeSpan BetweenRequestsTimeout { get; set; } = TimeSpan.FromSeconds(seconds: 10);
	
	private readonly HttpClient httpClient = CreateHttpClient();
	private readonly HtmlParser htmlParser = new();
	private string csrfToken = string.Empty;

	private const string BaseUrl = "https://litnet.com";
	private const string BooksUrl = "https://litnet.com/reader";
	private const string GetPageUrl = "https://litnet.com/reader/get-page";
	private const string LoginUrl = "https://litnet.com/auth/login?classic=1&link=https://litnet.com/";

	public async Task AuthenticateAsync(Credentials credentials, CancellationToken cancellationToken)
	{
		var html = await httpClient.GetStringAsync(BaseUrl, cancellationToken);
		var htmlDocument = await htmlParser.ParseDocumentAsync(html);

		var csrfTokenMeta = htmlDocument.QuerySelector(selectors: "meta[name='csrf-token']");
		csrfToken = csrfTokenMeta?.GetAttribute(name: "content") ?? string.Empty;

		var response = await PostAsync(
			LoginUrl,
			contentParameters:
			[
				new(key: "LoginForm[login]", value: credentials.Login),
				new(key: "LoginForm[password]", value: credentials.Password),
				new(key: "ajax", value: "w0"),
			],
			referer: BaseUrl,
			cancellationToken);

		if (!response.IsSuccessStatusCode)
			throw new BadAuthorizationException();
	}

	public async Task<string> GetBookWebPageAsync(string bookSlug, CancellationToken cancellationToken)
	{
		await Task.Delay(BetweenRequestsTimeout, cancellationToken);
		var bookUrl = $"{BooksUrl}/{bookSlug}";

		var bookPageHtml = await httpClient.GetStringAsync(bookUrl, cancellationToken);
		Console.WriteLine($"Book page loaded: {bookUrl}");

		return bookPageHtml;
	}
	
	public async Task<(string content, bool isPageLast)> GetPageContentAsync(
		string bookSlug,
		string chapterId,
		int pageIndex,
		CancellationToken cancellationToken)
	{
		var chapterUrl = $"{BooksUrl}/{bookSlug}?c={chapterId}";
		var response = await PostAsync(
			GetPageUrl,
			contentParameters:
			[
				new(key: "chapterId", value: chapterId),
				new(key: "page", value: pageIndex.ToString()),
				new(key: "_csrf", value: csrfToken),
			],
			referer: chapterUrl,
			cancellationToken: cancellationToken);

		var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
		using var jsonDocument = JsonDocument.Parse(responseText);
		var root = jsonDocument.RootElement;

		var status = root.GetProperty("status").GetInt32();

		if (status is not 1)
			throw new NoDataException(message: $"Page status is not 1 but {status}");

		var content = root.GetProperty("data").GetString()
			?? throw new NoDataException(message: $"No data found for page {pageIndex}");

		var isLast = root.TryGetProperty("isLastPage", out var isLastString)
			&& isLastString.GetBoolean();

		return (content, isLast);
	}
	
	private async Task<HttpResponseMessage> PostAsync(
		string url,
		IEnumerable<KeyValuePair<string, string>> contentParameters,
		string referer,
		CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, url);
		using var requestContent = new FormUrlEncodedContent(contentParameters);

		request.Content = requestContent;
		request.Headers.Add(name: "Origin", value: BaseUrl);
		request.Headers.Referrer = new Uri(referer);

		if (!string.IsNullOrEmpty(csrfToken))
			request.Headers.Add(name: "x-csrf-token", value: csrfToken);

		await Task.Delay(BetweenRequestsTimeout, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		
		return await httpClient.SendAsync(request, cancellationToken);
	}
	
	private static HttpClient CreateHttpClient()
	{
		var handler = new HttpClientHandler
		{
			CookieContainer = new(),
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
		};

		var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(seconds: 100) };
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(input: "Browser 2.1");
		httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(input: "en-US,en;q=0.8");
		httpClient.DefaultRequestHeaders.Add(name: "x-requested-with", value: "XMLHttpRequest");

		return httpClient;
	}
}