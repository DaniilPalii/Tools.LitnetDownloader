namespace LitnetDownloader;

internal class NoDataException : Exception
{
	public NoDataException(string? message) : base(message)
	{ }
}

internal class BadAuthorizationException : Exception
{
	public BadAuthorizationException() : base(message: "Bad authorization")
	{ }
}