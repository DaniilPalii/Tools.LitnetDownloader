using System.Text;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Values;

namespace LitnetDownloader;

internal sealed class BookDownloader(
	LitnetHttpClient litnetHttpClient)
{
	public async Task<EpubDocument> DownloadAsEpubAsync(
		string bookSlug,
		CancellationToken cancellationToken,
		Range? chapterRange = null)
	{
		(var title, var author, var annotation, var series, var cover) 
			= await litnetHttpClient.GetBookInfoWebPageAsync(bookSlug, cancellationToken);
		
		var epubDocument = new EpubDocument(title, author, annotation)
		{
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
				var chapterContent = await GetChapterContentAsync(bookSlug, chapter, cancellationToken);
				epubDocument.AddChapter(chapter.Title, chapter.Index, chapterContent);

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
		ChapterInfo chapter,
		CancellationToken cancellationToken)
	{
		var chapterContentBuilder = new StringBuilder();

		try
		{
			var isPageLast = false;
			var pageIndex = 1;
			while (!isPageLast && !cancellationToken.IsCancellationRequested)
			{
				(var pageContent, isPageLast) = await litnetHttpClient.GetBookPageContentAsync(bookSlug, chapter.Id, pageIndex, cancellationToken);
				chapterContentBuilder.Append(pageContent);
				pageIndex++;
			}
		}
		catch (OperationCanceledException) { }
		
		return chapterContentBuilder.ToString();;
	}
}