using Microsoft.Playwright;

namespace LitnetDownloader;

public class LitnetBrowserClient
{
	private const string LoginUrl = "https://litnet.com/auth/login?classic=1&link=https%3A%2F%2Flitnet.com%2F";
	
	public static async Task<List<System.Net.Cookie>> AuthenticateAsync()
	{
		Console.WriteLine("Opening browser for interactive login");
		
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await playwright.Firefox.LaunchAsync(options: new() { Headless = false });
		var page = await browser.NewPageAsync();
		await page.GotoAsync(
			LoginUrl,
			options: new()
			{
				Timeout = TimeSpan.FromMinutes(15).Milliseconds,
			});
		
		await page.GetByText("Обо мне").WaitForAsync(new() { Timeout = TimeSpan.FromMinutes(15).Milliseconds });
		Console.WriteLine("Log in confirmed");

		var playwrightCookies = await page.Context.CookiesAsync();
		Console.WriteLine($"Got {playwrightCookies.Count} cookies");
		
		await browser.CloseAsync();
		
		return playwrightCookies
			.Where(cookie => cookie.Domain is ".litnet.com" or "litnet.com")
			.Select(
				playwrightCookie => new System.Net.Cookie(playwrightCookie.Name, playwrightCookie.Value)
				{
					Domain = playwrightCookie.Domain,
					Path = playwrightCookie.Path,
					Secure = playwrightCookie.Secure,
				})
			.ToList();
	}
}