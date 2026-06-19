using System.Net;
using System.Text.Json;

namespace LitnetDownloader.Core;

internal static class CookieStorage
{
	private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, ".cookies");
	
	public static async Task SaveCookiesAsync(List<Cookie> cookies)
	{
		await using var fileStream = new StreamWriter(FilePath, append: false);
		await JsonSerializer.SerializeAsync(fileStream.BaseStream, cookies);
	}

	public static async Task<List<Cookie>> LoadCookiesAsync()
	{
		if (!File.Exists(FilePath))
			return [];

		using var fileStream = new StreamReader(FilePath);
		
		return await JsonSerializer.DeserializeAsync<List<Cookie>>(fileStream.BaseStream)
			?? [];	
	}
}