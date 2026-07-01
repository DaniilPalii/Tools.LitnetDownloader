using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace LitnetDownloader.ConsoleApplication.DependencyInjection;

public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
	public ITypeResolver Build()
	{
		return new TypeResolver(services.BuildServiceProvider());
	}

	public void Register(Type service, Type implementation)
	{
		services.AddSingleton(service, implementation);
	}

	public void RegisterInstance(Type service, object implementation)
	{
		services.AddSingleton(service, implementation);
	}

	public void RegisterLazy(Type service, Func<object> factory)
	{
		services.AddSingleton(service, _ => factory());
	}
	
	private sealed class TypeResolver(IServiceProvider provider) : ITypeResolver
	{
		public object? Resolve(Type? type)
		{
			return type == null
				? null
				: provider.GetService(type);
		}
	}
}