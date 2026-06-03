using System.Net;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;
using AngleSharp.Xhtml;
using EpubCore;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Values;

namespace LitnetDownloader;

internal sealed class BookDownloader(
	int maxPagesPerChapter = 10000,
	string baseUrl = "https://litnet.com",
	string booksUrl = "https://litnet.com/reader",
	string getPageUrl = "https://litnet.com/reader/get-page",
	string loginUrl = "https://litnet.com/auth/login?classic=1&link=https://litnet.com/",
	TimeSpan? betweenRequestsTimeout = null)
{
	private readonly HtmlParser htmlParser = new();
	private readonly HttpClient httpClient = CreateHttpClient();
	private string csrfToken = string.Empty;

	public TimeSpan BetweenRequestsTimeout { get; } = betweenRequestsTimeout ?? TimeSpan.FromSeconds(seconds: 10);

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

	public async Task AuthenticateAsync(Credentials credentials, CancellationToken cancellationToken)
	{
		var html = await httpClient.GetStringAsync(baseUrl, cancellationToken);
		var htmlDocument = await htmlParser.ParseDocumentAsync(html);

		var csrfTokenMeta = htmlDocument.QuerySelector(selectors: "meta[name='csrf-token']");
		csrfToken = csrfTokenMeta?.GetAttribute(name: "content") ?? string.Empty;

		var response = await PostHttpRequestAsync(
			loginUrl,
			contentParameters:
			[
				new(key: "LoginForm[login]", value: credentials.Login),
				new(key: "LoginForm[password]", value: credentials.Password),
				new(key: "ajax", value: "w0"),
			],
			referer: baseUrl,
			cancellationToken);

		if (!response.IsSuccessStatusCode)
			throw new BadAuthorizationException();
	}

	public async Task DownloadAsEpubAsync(
		string bookSlug,
		CancellationToken cancellationToken,
		string? fileName = null)
	{
		await Task.Delay(BetweenRequestsTimeout, cancellationToken);
		var bookUrl = booksUrl.TrimEnd(trimChar: '/') + "/" + bookSlug;
		var bookWebPageHtml = await httpClient.GetStringAsync(bookUrl, cancellationToken);
		var bookWebPage = await htmlParser.ParseDocumentAsync(bookWebPageHtml);
		Console.WriteLine($"Book page loaded: {bookUrl}");

		var epubWriter = new EpubWriter();
		
		var title = bookWebPage.QuerySelector(".book-heading")?.TextContent.Trim() ?? throw new NoDataException("Book title not found");
		epubWriter.SetTitle(title);
		
		var author = bookWebPage.QuerySelector(".sa-name")?.TextContent.Trim() ?? throw new NoDataException("Author not found");
		epubWriter.AddAuthor(author);

		// writer.SetCover();
		
		epubWriter.SetUniqueIdentifier(bookSlug);
		
		var chapters =
			bookWebPage 
				.QuerySelector(selectors: "select[name='chapter']")
				?.QuerySelectorAll(selectors: "option")
				.Select(
					selector: option =>
						new ChapterInfo(
							Id: option.GetAttribute(name: "value")
							?? throw new NoDataException(message: "Chapter option without value"),
							option.TextContent))
				.ToArray()
			?? throw new NoDataException(message: "No chapter list found on book page");

		Console.WriteLine($"Number of chapters: {chapters.Length}");

		for (var i = 0; i < chapters.Length && i < 3 && !cancellationToken.IsCancellationRequested; i++)
		{
			var chapter = chapters[i];
			var chapterContent = await GetChapterContentAsync(bookUrl, chapter, cancellationToken);
			
			epubWriter.AddChapter(chapter.Title, chapterContent);
			Console.WriteLine($"Got chapter {i + 1} out of {chapters.Length}");
		}
		
		fileName ??= $"{author} - {title}.epub";
		fileName = SanitizeFileName(fileName);

		epubWriter.Write(fileName);
		Console.WriteLine($"Book saved to file:\n\t\"{Path.GetFullPath(fileName)}\"");
	}

	private async Task<string> GetChapterContentAsync(
		string bookUrl,
		ChapterInfo chapter,
		CancellationToken cancellationToken)
	{
		var chapterContentBuilder = new StringBuilder();
		chapterContentBuilder
			.Append("<h2>")
			.Append(chapter.Title)
			.Append("</h2>");

		try
		{
			for (var pageIndex = 1;
				pageIndex <= maxPagesPerChapter && !cancellationToken.IsCancellationRequested;
				pageIndex++)
			{
				(var content, var isPageLast) = await GetPageContentAsync(bookUrl, chapter.Id, pageIndex, cancellationToken);
				chapterContentBuilder.Append(content);

				if (isPageLast)
					break;
			}
		}
		catch (OperationCanceledException) { }
		catch (NoDataException ex)
		{
			Console.WriteLine(value: "Error! " + ex.Message);
		}
		
		var chapterContent = chapterContentBuilder.ToString();
		chapterContent = await ToXhtmlAsync(chapterContent);

		return chapterContent;
	}

	private async Task<string> ToXhtmlAsync(string chapterContent)
	{
		var document = await htmlParser.ParseDocumentAsync(chapterContent);
		await using var writer = new StringWriter();
		document.ToHtml(writer, XhtmlMarkupFormatter.Instance);
		
		return writer.ToString();
	}

	private async Task<(string content, bool isPageLast)> GetPageContentAsync(
		string bookUrl,
		string chapterId,
		int pageIndex,
		CancellationToken cancellationToken)
	{
		var response = await PostHttpRequestAsync(
			getPageUrl,
			contentParameters:
			[
				new(key: "chapterId", value: chapterId),
				new(key: "page", value: pageIndex.ToString()),
				new(key: "_csrf", value: csrfToken),
			],
			referer: $"{bookUrl}?c={chapterId}",
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
	
	private async Task<HttpResponseMessage> PostHttpRequestAsync(
		string url,
		IEnumerable<KeyValuePair<string, string>> contentParameters,
		string referer,
		CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, url);
		using var requestContent = new FormUrlEncodedContent(contentParameters);

		request.Content = requestContent;
		request.Headers.Add(name: "Origin", value: baseUrl);
		request.Headers.Referrer = new Uri(referer);

		if (!string.IsNullOrEmpty(csrfToken))
			request.Headers.Add(name: "x-csrf-token", value: csrfToken);

		await Task.Delay(BetweenRequestsTimeout, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		
		return await httpClient.SendAsync(request, cancellationToken);
	}
	
	private static string SanitizeFileName(string fileName)
	{
		// Windows invalid characters
		var invalidChars = new HashSet<char> 
		{ 
			'<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0', 
		};

		// Control characters (ASCII 0 through 31)
		for (var i = 0; i < 32; i++)
		{
			invalidChars.Add((char)i);
		}

		var safeChars = fileName.Where(c => !invalidChars.Contains(c)).ToArray();
		var safeFileName = new string(safeChars);

		safeFileName = safeFileName.TrimEnd('.', ' ');

		if (string.IsNullOrWhiteSpace(safeFileName))
		{
			return Guid.NewGuid().ToString();
		}

		// Truncate to avoid File System limits (typically 255 characters)
		// Preserve extension and truncate filename part
		if (safeFileName.Length > 245)
		{
			var lastDotIndex = safeFileName.LastIndexOf('.');
			if (lastDotIndex > 0)
			{
				var extension = safeFileName[lastDotIndex..];
				var maxNameLength = 245 - extension.Length;
				if (maxNameLength > 0)
				{
					safeFileName = safeFileName[..maxNameLength] + extension;
				}
			}
			else
			{
				safeFileName = safeFileName[..245];
			}
		}

		return safeFileName;
	}
}