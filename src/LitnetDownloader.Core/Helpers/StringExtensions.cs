namespace LitnetDownloader.Core.Helpers;

public static class StringExtensions
{
	extension(string @string)
	{
		public string SubstringAfterLast(char character)
		{
			var lastIndexOfCharacter = @string.LastIndexOf(character);
			
			return lastIndexOfCharacter is -1
				? @string
				: @string[(lastIndexOfCharacter + 1)..];
		}
	}
}