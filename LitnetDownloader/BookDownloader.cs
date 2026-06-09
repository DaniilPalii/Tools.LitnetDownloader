using System.Text;
using AngleSharp.Html.Parser;
using AngleSharp.Xhtml;
using EpubCore;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Helpers;
using LitnetDownloader.Parsing;
using LitnetDownloader.Values;

namespace LitnetDownloader;

internal sealed class BookDownloader(
	LitnetHttpClient litnetHttpClient)
{
	private readonly HtmlParser htmlParser = new();

	public async Task DownloadAsEpubAsync(
		string bookSlug,
		CancellationToken cancellationToken,
		string? fileName = null)
	{
		var epubWriter = new EpubWriter();
		
		(var title, var author, var chapters) = await litnetHttpClient.GetBookReaderWebPageAsync(bookSlug, cancellationToken);
		epubWriter.SetTitle(title);
		epubWriter.AddAuthor(author);

		// writer.SetCover();
		
		epubWriter.SetUniqueIdentifier(bookSlug);
		
		Console.WriteLine($"Number of chapters: {chapters.Length}");

		for (var i = 0; i < chapters.Length && i < 3 && !cancellationToken.IsCancellationRequested; i++)
		{
			var chapter = chapters[i];
			var chapterContent = await GetChapterContentAsync(bookSlug, chapter, cancellationToken);
			
			epubWriter.AddChapter(chapter.Title, chapterContent);
			Console.WriteLine($"Got chapter {i + 1} out of {chapters.Length}");
		}
		
		fileName ??= $"{author} - {title}.epub";
		fileName = FileName.Sanitize(fileName);
		fileName = FileName.TruncatePreservingExtension(fileName, maxLength: 150);

		epubWriter.Write(fileName);
		Console.WriteLine($"Book saved to file:\n\t\"{Path.GetFullPath(fileName)}\"");
	}

	private async Task<string> GetChapterContentAsync(
		string bookSlug,
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
		catch (NoDataException ex)
		{
			Console.WriteLine(value: "Error! " + ex.Message);
		}
		
		var chapterContent = chapterContentBuilder.ToString();
		return await ToXhtmlAsync(chapterContent);
	}

	private async Task<string> ToXhtmlAsync(string chapterContent)
	{
		var document = await htmlParser.ParseDocumentAsync(chapterContent);
		await using var writer = new StringWriter();
		document.ToHtml(writer, XhtmlMarkupFormatter.Instance);
		
		return writer.ToString();
	}
}