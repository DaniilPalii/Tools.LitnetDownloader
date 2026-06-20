using System.Text.RegularExpressions;

namespace LitnetDownloader.Core.Helpers;

public static partial class BookUrl
{
	public static bool TryGetSlug(string bookUrl, out string slug)
	{
		var match = GetRegex().Match(bookUrl);

		if (!match.Success)
		{
			slug = string.Empty;
			return false;
		}

		slug = match.Groups[1].Value;
		return true;
	}

	[GeneratedRegex("([^/]+)/?$")]
	private static partial Regex GetRegex();
}