using IndividualWork.Core.ViewModels;

namespace IndividualWork.App.Services;

/// <summary>Діалоги MAUI для моделі подання.</summary>
public sealed class MauiDialogs : IUserDialogs
{
	private const string CancelText = "Скасувати";

	private static Page CurrentPage =>
		Application.Current?.Windows.FirstOrDefault()?.Page
		?? throw new InvalidOperationException("Головне вікно ще не створено.");

	public Task ShowMessageAsync(string title, string message) =>
		CurrentPage.DisplayAlert(title, message, "OK");

	public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
		CurrentPage.DisplayAlert(title, message, accept, cancel);

	public Task<string?> PromptAsync(string title, string message, string initialValue) =>
		CurrentPage.DisplayPromptAsync(title, message, "Зберегти", CancelText, initialValue: initialValue)!;

	public async Task<int?> ChooseAsync(string title, IReadOnlyList<string> options)
	{
		// Нумерація робить пункти унікальними, навіть якщо назви файлів збігаються.
		string[] buttons = options.Select((option, index) => $"{index + 1}. {option}").ToArray();
		string? choice = await CurrentPage.DisplayActionSheet(title, CancelText, null, buttons);

		int found = Array.IndexOf(buttons, choice);
		return found >= 0 ? found : null;
	}
}
