using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Parsing;

namespace LitnetDownloader;

internal class LitnetHttpClient
{
	public TimeSpan BetweenRequestsTimeout { get; set; } = TimeSpan.FromSeconds(seconds: 3);
	
	private readonly HttpClient httpClient;
	private readonly HttpClientHandler httpClientHandler;
	private readonly HtmlParser htmlParser = new();
	private string csrfToken = string.Empty;
	private readonly string cookieFilePath = Path.Combine(AppContext.BaseDirectory, ".litnet_cookies");

	private const string BaseUrl = "https://litnet.com";
	private const string BookInfoUrlPrefix = "https://litnet.com/book/";
	private const string BookReaderUrlPrefix = "https://litnet.com/reader/";
	private const string GetPageUrl = "https://litnet.com/reader/get-page";
	private const string LoginUrl = "https://litnet.com/auth/login?classic=1&link=https%3A%2F%2Flitnet.com%2F";
	private const string Domain = "litnet.com";

	public LitnetHttpClient()
	{
		(httpClient, httpClientHandler) = CreateHttpClient();
	}

	public async Task AuthenticateAsync(CancellationToken cancellationToken)
	{
		var cookies = await LitnetBrowserClient.AuthenticateAsync(LoginUrl);
		
		foreach (var cookie in cookies)
		{
			try 
			{ 
				httpClientHandler.CookieContainer.Add(new Uri(BaseUrl), cookie);
			}
			catch (CookieException)
			{
				Console.WriteLine($"Failed to add cookie: {cookie.Name}={cookie.Value}, Domain={cookie.Domain}, Path={cookie.Path}");
			}
		}

		var verificationHtml = await httpClient.GetStringAsync(BaseUrl, cancellationToken);
		var parsedVerificationHtml = await htmlParser.ParseDocumentAsync(verificationHtml);
		var csrfTokenMeta = parsedVerificationHtml.QuerySelector("meta[name='csrf-token']");
		csrfToken = csrfTokenMeta?.GetAttribute("content") 
			?? throw new NoDataException("CSRF token not found after login");

		if (verificationHtml.Contains("Авторизация") || verificationHtml.Contains("LoginForm"))
			throw new BadAuthorizationException();

		Console.WriteLine("Authentication successful");
	}

	public async Task<BookInfoWebPage> GetBookInfoWebPageAsync(string bookSlug, CancellationToken cancellationToken)
	{
		await Task.Delay(BetweenRequestsTimeout, cancellationToken);
		var bookInfoUrl = BookInfoUrlPrefix + bookSlug;

		var webPageHtml = await httpClient.GetStringAsync(bookInfoUrl, cancellationToken);
		Console.WriteLine($"Book info page loaded: {bookInfoUrl}");

		return await BookInfoWebPage.ParseAsync(webPageHtml, htmlParser, httpClient);
	}

	public async Task<BookReaderWebPage> GetBookReaderWebPageAsync(string bookSlug, CancellationToken cancellationToken)
	{
		await Task.Delay(BetweenRequestsTimeout, cancellationToken);
		var bookReaderUrl = BookReaderUrlPrefix + bookSlug;

		var webPageHtml = await httpClient.GetStringAsync(bookReaderUrl, cancellationToken);
		Console.WriteLine($"Book reader page loaded: {bookReaderUrl}");

		return await BookReaderWebPage.ParseAsync(webPageHtml, htmlParser);
	}
	
	public async Task<(string content, bool isPageLast)> GetBookPageContentAsync(
		string bookSlug,
		string chapterId,
		int pageIndex,
		CancellationToken cancellationToken)
	{
		var chapterUrl = $"{BookReaderUrlPrefix}{bookSlug}?c={chapterId}";
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
		{
			var errorData = root.TryGetProperty("data", out var dataProperty) ? dataProperty.GetString() : "Unknown error";
			throw new NoDataException(message: $"Page status is not 1 but {status}. Response: {errorData}");
		}

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
	
	private static (HttpClient, HttpClientHandler) CreateHttpClient()
	{
		var handler = new HttpClientHandler
		{
			CookieContainer = new(),
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
		};

		var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(seconds: 100) };
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(input: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
		httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(input: "en-US,en;q=0.8");
		httpClient.DefaultRequestHeaders.Add(name: "x-requested-with", value: "XMLHttpRequest");

		return (httpClient, handler);
	}
}