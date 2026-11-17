using System.Text;

namespace IndividualWork.Core.Logging;

/// <summary>Рівень важливості (вид) події.</summary>
public enum EventLevel
{
	Filtering,
	Transformation,
	Saving
}

/// <summary>
/// Шаблон «Одинак»: єдиний на всю програму протокол подій, що пишеться на локальний диск.
/// Рядок протоколу: «21.09.2023 13:02:22 Фільтрація. Текст повідомлення».
/// </summary>
public sealed class EventLogger
{
	private static readonly Lazy<EventLogger> LazyInstance = new(() => new EventLogger());

	private readonly object sync = new();

	private EventLogger()
	{
		LogFilePath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"IndividualWorkXml",
			"events.log");
	}

	public static EventLogger Instance => LazyInstance.Value;

	/// <summary>Шлях до файлу протоколу на локальному диску.</summary>
	public string LogFilePath { get; set; }

	public void Log(EventLevel level, string message)
	{
		string line = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} {TitleOf(level)}. {message}{Environment.NewLine}";
		lock (sync)
		{
			string? directory = Path.GetDirectoryName(LogFilePath);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);
			File.AppendAllText(LogFilePath, line, Encoding.UTF8);
		}
	}

	public string ReadAll()
	{
		lock (sync)
		{
			return File.Exists(LogFilePath) ? File.ReadAllText(LogFilePath, Encoding.UTF8) : string.Empty;
		}
	}

	public static string TitleOf(EventLevel level) => level switch
	{
		EventLevel.Filtering => "Фільтрація",
		EventLevel.Transformation => "Трансформація",
		EventLevel.Saving => "Збереження",
		_ => throw new ArgumentOutOfRangeException(nameof(level))
	};
}
