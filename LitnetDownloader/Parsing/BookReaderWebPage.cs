using AngleSharp.Html.Parser;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Parsing.Values;

namespace LitnetDownloader.Parsing;

internal static class BookReaderWebPage
{
	public static async Task<ChapterInfo[]> GetChaptersInfoAsync(string webPageHtml, IHtmlParser htmlParser)
	{
		var htmlDocument = await htmlParser.ParseDocumentAsync(webPageHtml);

		var chapterIndex = 1;
		var chapters = htmlDocument
			.QuerySelector(selectors: "select[name='chapter']")
			?.QuerySelectorAll(selectors: "option")
			.Select(
				selector: option =>
					new ChapterInfo(
						Index: chapterIndex++,
						Id: option.GetAttribute("value") 
						?? throw new NoDataException("Chapter option without value"),
						Title: option.TextContent))
			.ToArray()
			?? throw new NoDataException(message: "No chapter list found");

		return chapters;
	}
}