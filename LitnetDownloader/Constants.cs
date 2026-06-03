namespace LitnetDownloader;

internal static class Constants
{
	public const int MAX_PAGES_PER_CHAPTER = 10000;

	public const string LITERA_ORIGIN_URL = "https://litnet.com";
	public const string LITERA_BOOKS_URL = "https://litnet.com/reader";
	public const string LITERA_GET_PAGE_URL = "https://litnet.com/reader/get-page";
	public const string LITERA_LOGIN_URL = "https://litnet.com/auth/login?classic=1&link=https://litnet.com/";
	public static readonly TimeSpan WAIT_BETWEEN = TimeSpan.FromSeconds(seconds: 10);
}