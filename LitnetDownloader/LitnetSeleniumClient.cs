using System.Net;
using OpenQA.Selenium.Firefox;

namespace LitnetDownloader;

public class LitnetSeleniumClient
{
	public static List<Cookie> Authenticate(string loginUrl, CancellationToken cancellationToken)
	{
		Console.WriteLine("Launching Firefox for interactive login. Please complete CAPTCHA in the opened browser.");
		using var webDriver = CreateWebDriver();

		try
		{
			Console.WriteLine("Opening browser for interactive login. Press enter after completing login and CAPTCHA.");
			webDriver.Navigate().GoToUrl(loginUrl);

			Console.ReadLine();

			return webDriver.Manage().Cookies.AllCookies
				.Select(
					seleniumCookie => new System.Net.Cookie(seleniumCookie.Name, seleniumCookie.Value)
					{
						Domain = seleniumCookie.Domain,
						Path = seleniumCookie.Path,
						Secure = seleniumCookie.Secure,
					})
				.ToList();
		}
		finally
		{
			webDriver.Quit();
		}
	}

	private static FirefoxDriver CreateWebDriver()
	{
		var firefoxDriverService = FirefoxDriverService.CreateDefaultService();
		firefoxDriverService.HideCommandPromptWindow = true;
		
		var firefoxOptions = new FirefoxOptions();
		firefoxOptions.AddArgument("--width=1200");
		firefoxOptions.AddArgument("--height=800");

		return new FirefoxDriver(firefoxDriverService, firefoxOptions);
	}
}