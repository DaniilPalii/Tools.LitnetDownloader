using LitnetDownloader.Helpers;
using VieappsEpubDocument = net.vieapps.Components.Utility.Epub.Document;

namespace LitnetDownloader;

internal class EpubDocument(string title)
{
	public string Title { get; set; } = title;

	public string? Author { get; set; }
	
	public string? Identifier { get; set; }

	public string? Description { get; set; }
	
	public string? Publisher { get; set; }
	
	public string? Language { get; set; }
	
	public string? Annotation { get; set; }

	public string? Series { get; set; }

	public byte[]? Cover { get; set; }

	public List<Chapter> Chapters { get; } = [];

	public string AddIllustration(byte[] illustration, string source)
	{
		if (illustrations.FirstOrDefault(i => i.Source == source) is { } existingIllustration)
			return existingIllustration.FilePath;

		var fileName = $"{ImageDirectoryPath}/illustration{illustration.Length}.xhtml";
		illustrations.Add(new (fileName, illustration, source));
		
		return fileName;
	}

	public string WriteToFile(string? fileName = null)
	{
		var internalEpubDocument = new VieappsEpubDocument();
		
		if (Identifier is not null)
			internalEpubDocument.AddBookIdentifier(Identifier);
		
		if (Author is not null)
			internalEpubDocument.AddAuthor(Author);
		
		if (Description is not null)
			internalEpubDocument.AddDescription(Description);
		
		if (Publisher is not null)
			internalEpubDocument.AddPublisher(Publisher);

		if (Language is not null)
			internalEpubDocument.AddLanguage(Language);
		
		if (Series is not null)
			internalEpubDocument.AddMetaItem("calibre:series", Series);

		if (Cover is not null)
		{
			var coverFileId = internalEpubDocument.AddImageData(CoverFilePath, Cover);
			internalEpubDocument.AddMetaItem("cover", coverFileId);
		}

		foreach (var illustration in illustrations)
		{
			internalEpubDocument.AddImageData(illustration.FilePath, illustration.Bytes);
		}
		
		fileName ??= $"{Author} - {Title}.epub";
		fileName = FileName.Sanitize(fileName);
		fileName = FileName.TruncatePreservingExtension(fileName, maxLength: 150);

		var chapterIndex = 0;
		
		AddTitlePage(internalEpubDocument, chapterIndex++);

		foreach (var chapter in Chapters)
			AddChapterFile(internalEpubDocument, chapterIndex++, chapter.Title, chapter.Content);
		
		internalEpubDocument.Generate(fileName);
		return Path.GetFullPath(fileName);
	}

	private void AddTitlePage(VieappsEpubDocument internalEpubDocument, int chapterIndex)
	{
		var coverImg = Cover is not null
			? /* lang=xhtml */ $"""<img src="{CoverFilePath}" alt="Cover" />"""
			: string.Empty;
		
		var annotation = Annotation is not null
			? /* lang=xhtml */ $"<p>{Annotation}</p>"
			: string.Empty;

		var content = coverImg + annotation;
		AddChapterFile(internalEpubDocument, chapterIndex, Title, content);
	}

	private static void AddChapterFile(VieappsEpubDocument internalEpubDocument, int index, string title, string content)
	{
		var chapterFileName = $"chapter{index}.xhtml";
		var xhtmlContent = 
			/* lang=xhtml */ $"""
				<!DOCTYPE html>
				<html xmlns="http://www.w3.org/1999/xhtml">
					<head>
						<title>{title}</title>
						<meta http-equiv="content-type" content="text/html; charset=utf-8"/>
					</head>
					<body>
						<h2>{title}</h2>
						{content}
					</body>
				</html>
			""";

		internalEpubDocument.AddXhtmlData(chapterFileName, xhtmlContent);
		internalEpubDocument.AddNavPoint(title, chapterFileName, index);
	}
	
	private readonly List<Illustration> illustrations = [];

	private const string CoverFilePath = $"{ImageDirectoryPath}/cover.jpg";
	private const string ImageDirectoryPath = "images";

	public class Chapter(string title, string content)
	{
		public string Title { get; set; } = title;
		
		public string Content { get; set; } = content;
	}
	
	private record Illustration(
		string FilePath,
		byte[] Bytes,
		string Source);
}