using System.Text;
using AngleSharp;
using AngleSharp.Html.Parser;
using LitnetDownloader.Core.Exceptions;

namespace LitnetDownloader.Core;

public sealed class BookDownloader(LitnetHttpClient litnetHttpClient)
{
	public Task AuthenticateAsync(CancellationToken cancellationToken, bool forceLogin = false) 
		=> litnetHttpClient.AuthenticateAsync(cancellationToken, forceLogin);

	public async Task<EpubDocument> DownloadAsEpubAsync(
		string bookSlug,
		CancellationToken cancellationToken,
		Range? chapterRange = null)
	{
		(var title, var author, var annotation, var series, var cover) 
			= await litnetHttpClient.GetBookInfoWebPageAsync(bookSlug, cancellationToken);
		
		var epubDocument = new EpubDocument(title)
		{
			Author = author,
			Annotation = annotation,
			Identifier = bookSlug,
			Cover = cover,
			Series = series,
		};

		var chapters = await litnetHttpClient.GetBookChaptersAsync(bookSlug, cancellationToken);
		Console.WriteLine($"Total number of chapters: {chapters.Length}");
		
		if (chapterRange is not null)
			chapters = chapters[chapterRange.Value];

		try
		{
			foreach(var chapter in chapters)
			{
				var chapterContent = await GetChapterContentAsync(bookSlug, chapter.Id, epubDocument, cancellationToken);
				epubDocument.Chapters.Add(new (chapter.Title, chapterContent));

				Console.WriteLine($"Got chapter {chapter.Index}");
			
				if (cancellationToken.IsCancellationRequested)
					break;
			}
		}
		catch (NoDataException ex)
		{
			Console.WriteLine($"Error while getting chapters. Saving available data.\n{ex.Message}");
		}
		
		return epubDocument;
	}

	private async Task<string> GetChapterContentAsync(
		string bookSlug,
		string chapterId,
		EpubDocument epubDocument,
		CancellationToken cancellationToken)
	{
		var chapterContentBuilder = new StringBuilder();

		try
		{
			var isPageLast = false;
			var pageIndex = 1;
			while (!isPageLast && !cancellationToken.IsCancellationRequested)
			{
				(var pageContent, isPageLast) = await litnetHttpClient.GetBookPageContentAsync(bookSlug, chapterId, pageIndex, cancellationToken);

				pageContent = await ReplaceRemoteImagesWithLocalAsync(pageContent, epubDocument, cancellationToken);

				chapterContentBuilder.Append(pageContent);
				pageIndex++;
			}
		}
		catch (OperationCanceledException) { }
		
		return chapterContentBuilder.ToString();;
	}

	private async Task<string> ReplaceRemoteImagesWithLocalAsync(
		string pageContent, 
		EpubDocument epubDocument,
		CancellationToken cancellationToken)
	{
		var htmlParser = new HtmlParser();
		using var htmlDocument = await htmlParser.ParseDocumentAsync(pageContent);
		foreach (var imageElement in htmlDocument.Images)
		{
			var imageSource = imageElement.GetAttribute("src")
				?? throw new NoDataException("Image source not found");
			
			var image = await litnetHttpClient.DownloadImageAsync(imageSource, cancellationToken);
			var localPath = epubDocument.AddIllustration(image, imageSource);
			imageElement.SetAttribute("src", localPath);
		}
		
		return htmlDocument.ToHtml();
	}
}