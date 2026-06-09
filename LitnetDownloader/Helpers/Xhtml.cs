using AngleSharp.Html.Parser;
using AngleSharp.Xhtml;

namespace LitnetDownloader.Helpers;

internal class Xhtml
{
	public static async Task<string> FromHtmlAsync(string html, IHtmlParser htmlParser)
	{
		var htmlDocument = await htmlParser.ParseDocumentAsync(html);
		await using var writer = new StringWriter();
		htmlDocument.ToHtml(writer, XhtmlMarkupFormatter.Instance);
		
		return writer.ToString();
	}
}