using LitnetDownloader.Helpers;

namespace LitnetDownloader.Tests.Unit.HelpersTests;

[TestFixture]
public class FileNameTests
{
#region Sanitize tests

	[TestCase("Valid file name.epub", "Valid file name.epub")]
	[TestCase("File name with diacritićś.txt", "File name with diacritićś.txt")]
	[TestCase("Ім'я файлу кирилицею.docx", "Ім'я файлу кирилицею.docx")]
	[TestCase("Invalid/ file name?.epub", "Invalid file name.epub")]
	[TestCase("   Trailing spaces   .epub   ", "Trailing spaces   .epub")]
	[TestCase("Invalid chars<>:\"/\\|?*.epub", "Invalid chars.epub")]
	[TestCase("Control chars\u0001\u0002\u0003.epub", "Control chars.epub")]
	public void Sanitize_ShouldRemoveInvalidCharacters(string input, string expected)
	{
		var result = FileName.Sanitize(input);
		Assert.That(result, Is.EqualTo(expected));
	}
	
	[TestCase("")]
	[TestCase("<>:\"/\\|?*.")]
	public void Sanitize_EmptyString_ShouldCreateSomeFileName(string input)
	{
		var result = FileName.Sanitize(input);
		
		Assert.That(result, Has.Length.GreaterThan(0));
	}
	
	[Test]
	public void Sanitize_EmptyString_ShouldCreateRandomFileName()
	{
		var result1 = FileName.Sanitize(string.Empty);
		var result2 = FileName.Sanitize(string.Empty);
		
		Assert.That(result1, Is.Not.EqualTo(result2));
	}
	
#endregion

#region TruncatePreservingTests

	[TestCase("Short name.epub", 20, "Short name.epub")]
	[TestCase("This is a very long file name that should be truncated.epub", 30, "This is a very long file.epub")]
	[TestCase("No extension file name that is too long", 20, "No extension file na")]
	[TestCase(".hiddenfilewithoutextension", 10, ".hiddenfil")]
	public void TruncatePreservingExtension_ShouldTruncateLongFileName(string input, int maxLength, string expected)
	{
		var result = FileName.TruncatePreservingExtension(input, maxLength);
		Assert.That(result, Is.EqualTo(expected));
	}

#endregion
}