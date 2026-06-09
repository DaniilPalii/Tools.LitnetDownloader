using AngleSharp.Html.Parser;
using LitnetDownloader.Exceptions;
using AngleSharp.Html.Dom;

namespace LitnetDownloader.Parsing;

internal record BookInfoWebPage(
	byte[] Cover)
{
	public static async Task<BookInfoWebPage> ParseAsync(
		string webPageHtml,
		IHtmlParser htmlParser,
		HttpClient httpClient)
	{
		var htmlDocument = await htmlParser.ParseDocumentAsync(webPageHtml);
		
		return new(
			Cover: await GetCoverAsync(htmlDocument, httpClient));
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