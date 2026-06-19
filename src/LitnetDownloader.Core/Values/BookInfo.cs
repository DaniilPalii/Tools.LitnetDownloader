namespace LitnetDownloader.Core.Values;

public record BookInfo(
	string Title,
	string Author,
	string Annotation,
	string? Series,
	byte[] Cover);