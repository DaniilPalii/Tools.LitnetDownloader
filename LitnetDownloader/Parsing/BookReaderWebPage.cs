using AngleSharp.Html.Parser;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Values;

namespace LitnetDownloader.Parsing;

internal record BookReaderWebPage(
	string Title,
	string Author,
	ChapterInfo[] Chapters)
{
	public static async Task<BookReaderWebPage> ParseAsync(string webPageHtml, IHtmlParser htmlParser)
	{
		var htmlDocument = await htmlParser.ParseDocumentAsync(webPageHtml);

		var title = htmlDocument.QuerySelector(".book-heading")?.TextContent.Trim()
			?? throw new NoDataException("Book title not found");
	
		var author = htmlDocument.QuerySelector(".sa-name")?.TextContent.Trim()
			?? throw new NoDataException("Author not found");

		var chapters = htmlDocument
				.QuerySelector(selectors: "select[name='chapter']")
				?.QuerySelectorAll(selectors: "option")
				.Select(
					selector: option =>
						new ChapterInfo(
							Id: option.GetAttribute(name: "value")
							?? throw new NoDataException(message: "Chapter option without value"),
							option.TextContent))
				.ToArray()
			?? throw new NoDataException(message: "No chapter list found");
		
		return new(title, author, chapters);
	}
}