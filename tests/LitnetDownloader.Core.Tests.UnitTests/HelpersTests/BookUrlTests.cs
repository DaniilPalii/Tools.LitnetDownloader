using LitnetDownloader.Core.Helpers;

namespace LitnetDownloader.Tests.UnitTests.CoreTests.HelpersTests;

[TestFixture]
public class BookUrlTests
{
	[TestCase("testslug")]
	[TestCase("test-slug")]
	[TestCase("testslug1")]
	public void GetSlug_WhenGivenSlug_ReturnsSlug(string input)
	{
		var succeed = BookUrl.TryGetSlug(input, out var result);
		
		using (Assert.EnterMultipleScope())
		{
			Assert.That(succeed, Is.True);
			Assert.That(result, Is.EqualTo(input));
		}
	}
	
	[TestCase("https://litnet.com/book/testslug", "testslug")]
	[TestCase("https://litnet.com/book/testslug1", "testslug1")]
	[TestCase("https://litnet.com/book/test-slug", "test-slug")]
	[TestCase("https://litnet.com/io/book/testslug", "testslug")]
	[TestCase("http://litnet.com/book/testslug", "testslug")]
	[TestCase("litnet.com/book/testslug", "testslug")]
	[TestCase("https://litnet.com/book/testslug/", "testslug")]
	public void GetSlug_WhenGivenUrl_ReturnsSlug(string input, string expected)
	{
		var succeed = BookUrl.TryGetSlug(input, out var result);
		
		using (Assert.EnterMultipleScope())
		{
			Assert.That(succeed, Is.True);
			Assert.That(result, Is.EqualTo(expected));
		}
	}
}