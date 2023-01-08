using Microsoft.AspNetCore.Components.WebView.Maui;
using AI_C4T.Data;
using C4T_Core;
using TwitchLib.EventSub.Websockets.Extensions;
using Microsoft.Extensions.Hosting;
using TwitchLib.EventSub.Websockets.Example;

namespace AI_C4T;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif
		
		builder.Services.AddSingleton<WeatherForecastService>();
		builder.Services.AddSingleton<C4T>();
		builder.Services.AddTwitchLibEventSubWebsockets();
		builder.Services.AddHostedService<TwitchWebSocketService>();

		return builder.Build();
	}
}
