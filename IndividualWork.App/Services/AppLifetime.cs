using IndividualWork.Core.ViewModels;

namespace IndividualWork.App.Services;

public sealed class AppLifetime : IAppLifetime
{
	private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(150);

	public async Task QuitAsync()
	{
		HideWindows();
		await Task.Delay(HideDelay);
		Application.Current?.Quit();
	}

	private static void HideWindows()
	{
#if WINDOWS
		foreach (var window in Application.Current?.Windows ?? [])
			(window.Handler?.PlatformView as Microsoft.UI.Xaml.Window)?.AppWindow.Hide();
#endif
	}
}
