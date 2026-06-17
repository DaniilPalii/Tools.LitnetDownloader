using AngleSharp.Html.Parser;
using LitnetDownloader.Exceptions;
using AngleSharp.Html.Dom;

namespace LitnetDownloader.Parsing;

internal record BookInfoWebPage(
	string Title,
	string Author,
	string Annotation,
	string? Series,
	byte[] Cover)
{
	public static async Task<BookInfoWebPage> ParseAsync(
		string webPageHtml,
		IHtmlParser htmlParser,
		HttpClient httpClient)
	{
		var htmlDocument = await htmlParser.ParseDocumentAsync(webPageHtml);
		
		return new(
			Title: GetTitle(htmlDocument),
			Author: GetAuthor(htmlDocument),
			Annotation: GetAnnotation(htmlDocument),
			Series: GetSeries(htmlDocument),
			Cover: await GetCoverAsync(htmlDocument, httpClient));
	}

	private static string GetTitle(IHtmlDocument htmlDocument)
	{
		return htmlDocument.QuerySelector(".book-view-info h1")?.TextContent.Trim()
			?? throw new NoDataException("Book title not found");
	}

	private static string GetAuthor(IHtmlDocument htmlDocument)
	{
		return htmlDocument.QuerySelector(".book-view-info .author span")?.TextContent.Trim()
			?? throw new NoDataException("Author not found");
	}

	private static string GetAnnotation(IHtmlDocument htmlDocument)
	{
		return htmlDocument.QuerySelector("#annotation div")?.InnerHtml.Trim()
			?? throw new NoDataException("Annotation not found");
	}
	
	private static string? GetSeries(IHtmlDocument htmlDocument)
	{
		return htmlDocument
			.QuerySelector(
				"div.book-view-info-coll:nth-child(1) > div:nth-child(1) > p:nth-child(3) > a:nth-child(2)")?
			.TextContent
			.Trim();
	}

	private static async Task<byte[]> GetCoverAsync(IHtmlDocument htmlDocument, HttpClient httpClient)
	{
		var imgElement = htmlDocument.QuerySelector(".book-view-cover img");
		if (imgElement is null)
			throw new NoDataException("Cover image element not found");

		var imgSrc = imgElement.GetAttribute("src");
		if (string.IsNullOrWhiteSpace(imgSrc))
			throw new NoDataException("Cover image src attribute is empty");

		if (imgSrc.StartsWith("//"))
			imgSrc = "https:" + imgSrc;

		// Expected URL is similar to https://publiccdn.litnet.com/books/covers/0/1668613814_74.jpg
		if (!Uri.TryCreate(imgSrc, UriKind.Absolute, out var imageUri)
			|| (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
		{
			throw new NoDataException("Cover image URL is not an absolute HTTP/HTTPS URL");
		}

		using var imageResponse = await httpClient.GetAsync(imageUri);
		 
		if (!imageResponse.IsSuccessStatusCode)
			throw new NoDataException("Failed to download cover image");
		
		return await imageResponse.Content.ReadAsByteArrayAsync();
	}
}