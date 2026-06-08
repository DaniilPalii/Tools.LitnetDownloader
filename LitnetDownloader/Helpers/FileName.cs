namespace LitnetDownloader.Helpers;

public class FileName
{
	public static string Sanitize(string fileName)
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
		safeFileName = safeFileName.TrimStart(' ');

		if (string.IsNullOrWhiteSpace(safeFileName))
			return Guid.NewGuid().ToString();

		return safeFileName;
	}

	public static string TruncatePreservingExtension(string fileName, int maxLength)
	{
		if (fileName.Length <= maxLength)
			return fileName;

		var lastDotIndex = fileName.LastIndexOf('.');

		if (lastDotIndex is -1 or 0)
			return fileName[..maxLength].TrimEnd('.', ' ');

		var extension = fileName[lastDotIndex..];
		var maxNameLength = maxLength - extension.Length;

		if (maxNameLength <= 0)
			return fileName;
		
		return fileName[..maxNameLength].TrimEnd('.', ' ') + extension;
	}
}