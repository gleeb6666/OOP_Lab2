namespace IndividualWork.Core.ViewModels;

/// <summary>Діалоги з користувачем. Реалізацію надає застосунок MAUI.</summary>
public interface IUserDialogs
{
	Task ShowMessageAsync(string title, string message);

	Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

	/// <summary>Просить ввести рядок; null — користувач скасував.</summary>
	Task<string?> PromptAsync(string title, string message, string initialValue);

	/// <summary>Пропонує обрати один варіант; повертає його індекс або null, якщо скасовано.</summary>
	Task<int?> ChooseAsync(string title, IReadOnlyList<string> options);
}

public sealed record TextFile(string Name, string Content);

/// <summary>Вибір і збереження файлів на локальному диску.</summary>
public interface ILocalFiles
{
	/// <summary>Відкриває файл із заданим розширенням; null — користувач скасував.</summary>
	Task<TextFile?> PickAsync(string title, string extension);

	/// <summary>Зберігає текст у файл; повертає шлях або null, якщо скасовано.</summary>
	Task<string?> SaveAsync(string suggestedName, string content);
}

public interface IAppLifetime
{
	Task QuitAsync();
}
