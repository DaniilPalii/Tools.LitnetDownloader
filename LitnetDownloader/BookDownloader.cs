using System.Text;
using AngleSharp.Html.Parser;
using LitnetDownloader.Exceptions;
using LitnetDownloader.Helpers;
using LitnetDownloader.Values;
using EpubDocument = net.vieapps.Components.Utility.Epub.Document;

namespace LitnetDownloader;

internal sealed class BookDownloader(
	LitnetHttpClient litnetHttpClient)
{
	private readonly HtmlParser htmlParser = new();

	public async Task DownloadAsEpubAsync(
		string bookSlug,
		CancellationToken cancellationToken,
		Range? chapterRange = null,
		string? fileName = null)
	{
		var epubDocument = new EpubDocument();
		epubDocument.AddBookIdentifier(bookSlug);
		
		(var title, var author, var chapters) = await litnetHttpClient.GetBookReaderWebPageAsync(bookSlug, cancellationToken);
		epubDocument.AddTitle(title);
		epubDocument.AddAuthor(author);

		var bookInfoWebPage = await litnetHttpClient.GetBookInfoWebPageAsync(bookSlug, cancellationToken);
		var coverImageId = epubDocument.AddImageData("cover.jpg", bookInfoWebPage.Cover);
		epubDocument.AddMetaItem("cover", coverImageId);
		   
		Console.WriteLine($"Total number of chapters: {chapters.Length}");

		if (chapterRange is not null)
			chapters = chapters[chapterRange.Value];

		epubDocument.AddXhtmlData(
			"page0.xhtml",
			PageTemplate
				.Replace("{0}", title)
				.Replace("{1}", $"""<img src="cover.jpg" alt="{title}" />"""));

		try
		{
			foreach(var chapter in chapters)
			{
				var chapterContent = await GetChapterContentAsync(bookSlug, chapter, cancellationToken);
				var chapterFileName = $"page{chapter.Index}.xhtml";

				epubDocument.AddXhtmlData(
					epubPath: chapterFileName,
					content: PageTemplate
						.Replace("{0}", chapter.Title)
						.Replace("{1}", chapterContent));
				
				epubDocument.AddNavPoint(
					label: chapter.Title, 
					content: chapterFileName,
					playOrder: chapter.Index);
				
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

		epubDocument.Generate(fileName);
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
		
		var chapterContent = chapterContentBuilder.ToString();
		return await Xhtml.FromHtmlAsync(chapterContent, htmlParser);
	}
	
	private const string PageTemplate = """
		<!DOCTYPE html>
		<html xmlns="http://www.w3.org/1999/xhtml">
			<head>
				<title>{0}</title>
				<meta http-equiv="Content-Type" content="text/html; charset=utf-8"/>
			</head>
			<body>
				{1}
			</body>
		</html>
		""";
}