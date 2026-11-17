using CommunityToolkit.Maui;
using IndividualWork.App.Services;
using IndividualWork.Core.Storage;
using IndividualWork.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace IndividualWork.App;

public static class MauiProgram
{
	private const string ClientSecretsFileName = "client_secret.json";

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			.ConfigureLifecycleEvents(events =>
			{
#if WINDOWS
				events.AddWindows(windows => windows.OnWindowCreated(ConfirmExitOnClose));
#endif
			});

		builder.Services.AddSingleton<IUserDialogs, MauiDialogs>();
		builder.Services.AddSingleton<ILocalFiles, MauiLocalFiles>();
		builder.Services.AddSingleton<IAppLifetime, AppLifetime>();
		builder.Services.AddSingleton<ICloudFileStorage>(_ => new GoogleDriveStorage(
			_ => FileSystem.OpenAppPackageFileAsync(ClientSecretsFileName),
			Path.Combine(FileSystem.AppDataDirectory, "GoogleDrive")));
		builder.Services.AddSingleton<MainViewModel>();
		builder.Services.AddSingleton<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

#if WINDOWS
	/// <summary>Закриття вікна «хрестиком» теж проходить через підтвердження виходу.</summary>
	private static void ConfirmExitOnClose(Microsoft.UI.Xaml.Window window)
	{
		bool confirmed = false;
		bool asking = false;
		window.AppWindow.Closing += async (_, args) =>
		{
			if (confirmed)
				return;

			args.Cancel = true;
			if (asking || Application.Current?.Windows.FirstOrDefault()?.Page?.BindingContext is not MainViewModel viewModel)
				return;

			asking = true;
			try
			{
				if (await viewModel.ConfirmExitAsync())
				{
					confirmed = true;
					await new AppLifetime().QuitAsync();
				}
			}
			finally
			{
				asking = false;
			}
		};
	}
#endif
}
