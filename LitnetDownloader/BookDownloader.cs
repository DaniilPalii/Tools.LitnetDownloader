using System.Text;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Helpers;
using LitnetDownloader.Values;

namespace LitnetDownloader;

internal sealed class BookDownloader(
	LitnetHttpClient litnetHttpClient)
{
	public async Task DownloadAsEpubAsync(
		string bookSlug,
		CancellationToken cancellationToken,
		Range? chapterRange = null,
		string? fileName = null)
	{
		(var title, var author, var chapters) = await litnetHttpClient.GetBookReaderWebPageAsync(bookSlug, cancellationToken);
		var bookInfoWebPage = await litnetHttpClient.GetBookInfoWebPageAsync(bookSlug, cancellationToken);
		
		var epubDocument = new EpubDocument(title)
		{
			Identifier = bookSlug,
			Author = author,
			Cover = bookInfoWebPage.Cover,
		};
		   
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
		
		fileName ??= $"{author} - {title}.epub";
		fileName = FileName.Sanitize(fileName);
		fileName = FileName.TruncatePreservingExtension(fileName, maxLength: 150);

		epubDocument.WriteToFile(fileName);
		Console.WriteLine($"Book saved to file:\n\t\"{Path.GetFullPath(fileName)}\"");
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