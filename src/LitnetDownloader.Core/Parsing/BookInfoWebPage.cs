using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using LitnetDownloader.Core.Exceptions;

namespace LitnetDownloader.Core.Parsing;

internal record BookInfoWebPage(
	string Title,
	string Author,
	string Annotation,
	string? Series,
	string CoverSource)
{
	public static async Task<BookInfoWebPage> ParseAsync(
		string webPageHtml,
		IHtmlParser htmlParser)
	{
		using var htmlDocument = await htmlParser.ParseDocumentAsync(webPageHtml);
		
		return new(
			Title: GetTitle(htmlDocument),
			Author: GetAuthor(htmlDocument),
			Annotation: GetAnnotation(htmlDocument),
			Series: GetSeries(htmlDocument),
			CoverSource: GetCoverSource(htmlDocument));
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

	private static string GetCoverSource(IHtmlDocument htmlDocument)
	{
		var imageElement = htmlDocument.QuerySelector(".book-view-cover img")
			?? throw new NoDataException("Cover image element not found");

		var imageSource = imageElement.GetAttribute("src");
		if (string.IsNullOrWhiteSpace(imageSource))
			throw new NoDataException("Cover image src attribute is empty");

		return imageSource;
	}
}