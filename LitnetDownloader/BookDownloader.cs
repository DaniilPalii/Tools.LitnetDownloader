using System.Net;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;
using AngleSharp.Xhtml;
using EpubCore;
using LitnetDownloader.Exceptions;
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
		var bookWebPageHtml = await litnetHttpClient.GetBookWebPageAsync(bookSlug, cancellationToken);
		var bookWebPage = await htmlParser.ParseDocumentAsync(bookWebPageHtml);

		var epubWriter = new EpubWriter();
		
		var title = bookWebPage.QuerySelector(".book-heading")?.TextContent.Trim() ?? throw new NoDataException("Book title not found");
		epubWriter.SetTitle(title);
		
		var author = bookWebPage.QuerySelector(".sa-name")?.TextContent.Trim() ?? throw new NoDataException("Author not found");
		epubWriter.AddAuthor(author);

		// writer.SetCover();
		
		epubWriter.SetUniqueIdentifier(bookSlug);
		
		var chapters =
			bookWebPage 
				.QuerySelector(selectors: "select[name='chapter']")
				?.QuerySelectorAll(selectors: "option")
				.Select(
					selector: option =>
						new ChapterInfo(
							Id: option.GetAttribute(name: "value")
								?? throw new NoDataException(message: "Chapter option without value"),
							option.TextContent))
				.ToArray()
			?? throw new NoDataException(message: "No chapter list found on book page");

		Console.WriteLine($"Number of chapters: {chapters.Length}");

		for (var i = 0; i < chapters.Length && i < 3 && !cancellationToken.IsCancellationRequested; i++)
		{
			var chapter = chapters[i];
			var chapterContent = await GetChapterContentAsync(bookSlug, chapter, cancellationToken);
			
			epubWriter.AddChapter(chapter.Title, chapterContent);
			Console.WriteLine($"Got chapter {i + 1} out of {chapters.Length}");
		}
		
		fileName ??= $"{author} - {title}.epub";
		fileName = SanitizeFileName(fileName);

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
				(var pageContent, isPageLast) = await litnetHttpClient.GetPageContentAsync(bookSlug, chapter.Id, pageIndex, cancellationToken);
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
	
	private static string SanitizeFileName(string fileName)
	{
		// Windows invalid characters
		var invalidChars = new HashSet<char> 
		{ 
			'<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0', 
		};

		// Control characters (ASCII 0 through 31)
		for (var i = 0; i < 32; i++)
		{
			invalidChars.Add((char)i);
		}

		var safeChars = fileName.Where(c => !invalidChars.Contains(c)).ToArray();
		var safeFileName = new string(safeChars);

		safeFileName = safeFileName.TrimEnd('.', ' ');

		if (string.IsNullOrWhiteSpace(safeFileName))
		{
			return Guid.NewGuid().ToString();
		}

		// Truncate to avoid File System limits (typically 255 characters)
		// Preserve extension and truncate filename part
		if (safeFileName.Length > 245)
		{
			var lastDotIndex = safeFileName.LastIndexOf('.');
			if (lastDotIndex > 0)
			{
				var extension = safeFileName[lastDotIndex..];
				var maxNameLength = 245 - extension.Length;
				if (maxNameLength > 0)
				{
					safeFileName = safeFileName[..maxNameLength] + extension;
				}
			}
			else
			{
				safeFileName = safeFileName[..245];
			}
		}

		return safeFileName;
	}
}