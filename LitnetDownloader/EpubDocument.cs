using LitnetDownloader.Helpers;
using VieappsEpubDocument = net.vieapps.Components.Utility.Epub.Document;

namespace LitnetDownloader;

internal class EpubDocument
{
	private readonly VieappsEpubDocument internalEpubDocument = new();

	public EpubDocument(string title, string author, string annotation)
	{
		Title = title;
		Author = author;
		Annotation = annotation;
		
		AddChapter(
			index: 0,
			title: title,
			content: IntroChapterContentTemplate);
	}
	
	public string Identifier
	{
		set => internalEpubDocument.AddBookIdentifier(value);
	}

	public string Author
	{
		get => field;

		private set
		{  
			field = value;
			internalEpubDocument.AddAuthor(value);
		}
	}

	public string Description
	{
		set => internalEpubDocument.AddDescription(value);
	}
	
	public string Publisher
	{
		set => internalEpubDocument.AddPublisher(value);
	}
	
	public string Language
	{
		set => internalEpubDocument.AddLanguage(value);
	}
	
	public string Title
	{
		get => field;

		private set
		{
			field = value;
			internalEpubDocument.AddTitle(value);
		}
	}
	
	public string Annotation { get; private set; }

	public string? Series
	{
		get => field;

		set
		{
			field = value;
			
			if (field is not null)
				internalEpubDocument.AddMetaItem("calibre:series", value);
		}
	}

	public byte[] Cover
	{
		set 
		{
			var coverImageId = internalEpubDocument.AddImageData(CoverFileName, value);
			internalEpubDocument.AddMetaItem("cover", coverImageId);
		}
	}

	public void AddChapter(string title, int index, string content)
	{
		var chapterFileName = $"page{index}.xhtml";

		internalEpubDocument.AddXhtmlData(
			epubPath: chapterFileName,
			content: PageTemplate
				.Replace("{0}", title)
				.Replace("{1}", content));
				
		internalEpubDocument.AddNavPoint(
			label: title,
			content: chapterFileName,
			playOrder: index);
	}

	public string WriteToFile(string? fileName = null)
	{
		fileName ??= $"{Author} - {Title}.epub";
		fileName = FileName.Sanitize(fileName);
		fileName = FileName.TruncatePreservingExtension(fileName, maxLength: 150);
		
		internalEpubDocument.Generate(fileName);

		return Path.GetFullPath(fileName);
	}
	
	private const string CoverFileName = "cover.jpg";
	
	private const string PageTemplate = 
	"""
		<!DOCTYPE html>
		<html xmlns="http://www.w3.org/1999/xhtml">
			<head>
				<title>{0}</title>
				<meta http-equiv="Content-Type" content="text/html; charset=utf-8"/>
			</head>
			<body>
				<h2>{0}</h2>
				{1}
			</body>
		</html>
	""";

	public string IntroChapterContentTemplate =>
	$"""
		<img src="{CoverFileName}" alt="{Title}" />
		<p>{Annotation}</p>
	""";
}