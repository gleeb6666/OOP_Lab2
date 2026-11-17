using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndividualWork.Core.Export;
using IndividualWork.Core.Logging;
using IndividualWork.Core.Model;
using IndividualWork.Core.Search;
using IndividualWork.Core.Storage;
using IndividualWork.Core.Transform;

namespace IndividualWork.Core.ViewModels;

/// <summary>
/// Модель подання головного вікна (MVVM). Уся поведінка програми — тут і в класах ядра;
/// у вікні залишаються лише прив'язки, тож антишаблону «магічна кнопка» немає.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
	public const string AllOption = "— усі —";
	public const string ExitQuestion = "Чи дійсно ви хочете завершити роботу з програмою?";
	private const string NoFile = "не обрано";

	private readonly IUserDialogs dialogs;
	private readonly ILocalFiles localFiles;
	private readonly ICloudFileStorage cloud;
	private readonly IAppLifetime lifetime;
	private readonly EventLogger logger = EventLogger.Instance;
	private readonly XslTransformer transformer = new();

	private readonly IXmlSearchStrategy sax = new SaxSearchStrategy();
	private readonly IXmlSearchStrategy dom = new DomSearchStrategy();
	private readonly IXmlSearchStrategy linq = new LinqSearchStrategy();
	private readonly XmlAnalyzer analyzer;

	private string? xmlContent;
	private string? xslContent;
	private SearchResult? currentResult;

	public MainViewModel(IUserDialogs dialogs, ILocalFiles localFiles, ICloudFileStorage cloud, IAppLifetime lifetime)
	{
		this.dialogs = dialogs;
		this.localFiles = localFiles;
		this.cloud = cloud;
		this.lifetime = lifetime;
		analyzer = new XmlAnalyzer(dom);
		ResetAttributeLists();
	}

	public ObservableCollection<string> Students { get; } = [];
	public ObservableCollection<string> Groups { get; } = [];

	[ObservableProperty]
	private string xmlFileName = NoFile;

	[ObservableProperty]
	private string xslFileName = NoFile;

	[ObservableProperty]
	private string selectedStudent = AllOption;

	[ObservableProperty]
	private string selectedGroup = AllOption;

	[ObservableProperty]
	private string keyword = string.Empty;

	[ObservableProperty]
	private string outputText = string.Empty;

	[ObservableProperty]
	private string htmlPreview = string.Empty;

	[ObservableProperty]
	private string statusText = "Оберіть XML-файл, щоб почати.";

	public string StrategyName => analyzer.Strategy.Name;

	public bool IsSaxSelected
	{
		get => analyzer.Strategy == sax;
		set => SelectStrategy(sax, value);
	}

	public bool IsDomSelected
	{
		get => analyzer.Strategy == dom;
		set => SelectStrategy(dom, value);
	}

	public bool IsLinqSelected
	{
		get => analyzer.Strategy == linq;
		set => SelectStrategy(linq, value);
	}

	public string LogFilePath => logger.LogFilePath;

	// ---------- Вибір файлів ----------

	[RelayCommand]
	private Task OpenXmlAsync() => GuardAsync(async () =>
	{
		TextFile? file = await localFiles.PickAsync("Відкрити XML-файл", ".xml");
		if (file is not null)
			LoadXml(file);
	});

	[RelayCommand]
	private Task OpenXslAsync() => GuardAsync(async () =>
	{
		TextFile? file = await localFiles.PickAsync("Відкрити XSL-файл", ".xsl");
		if (file is not null)
			LoadXsl(file);
	});

	[RelayCommand]
	private Task OpenXmlFromDriveAsync() => GuardAsync(async () =>
	{
		TextFile? file = await PickFromDriveAsync(".xml");
		if (file is not null)
			LoadXml(file);
	});

	[RelayCommand]
	private Task OpenXslFromDriveAsync() => GuardAsync(async () =>
	{
		TextFile? file = await PickFromDriveAsync(".xsl");
		if (file is not null)
			LoadXsl(file);
	});

	/// <summary>Зберігає на Google Drive відкриті вхідні файли XML та XSL.</summary>
	[RelayCommand]
	private Task UploadSourcesAsync() => GuardAsync(async () =>
	{
		if (xmlContent is null && xslContent is null)
		{
			await dialogs.ShowMessageAsync("Google Drive", "Спочатку відкрийте XML- або XSL-файл.");
			return;
		}

		var saved = new List<string>();
		if (xmlContent is not null)
		{
			await UploadTextAsync(XmlFileName, "application/xml", xmlContent);
			saved.Add(XmlFileName);
		}
		if (xslContent is not null)
		{
			await UploadTextAsync(XslFileName, "application/xslt+xml", xslContent);
			saved.Add(XslFileName);
		}

		string names = string.Join(", ", saved);
		logger.Log(EventLevel.Saving, $"Збережено вхідні файли на Google Drive: {names}");
		StatusText = $"На Google Drive збережено: {names}";
	});

	// ---------- Пошук ----------

	[RelayCommand]
	private Task SearchAsync() => GuardAsync(async () =>
	{
		if (xmlContent is null)
		{
			await dialogs.ShowMessageAsync("Пошук", "Спочатку оберіть XML-файл.");
			return;
		}

		SearchCriteria criteria = BuildCriteria();
		currentResult = analyzer.Search(xmlContent, criteria);

		OutputText =
			$"Спосіб аналізу: {analyzer.Strategy.Name}{Environment.NewLine}" +
			$"Запит: {analyzer.DescribeQuery(criteria)}{Environment.NewLine}" +
			ResultTextFormatter.Format(currentResult);
		HtmlPreview = string.Empty;
		StatusText = $"Знайдено записів: {currentResult.Works.Count} ({analyzer.Strategy.Name}).";

		logger.Log(EventLevel.Filtering,
			$"Параметри: {criteria.Describe()}. Спосіб: {analyzer.Strategy.Name}. Знайдено: {currentResult.Works.Count}");
	});

	/// <summary>Очищує поле виводу та задані параметри пошуку.</summary>
	[RelayCommand]
	private void Clear()
	{
		SelectedStudent = AllOption;
		SelectedGroup = AllOption;
		Keyword = string.Empty;
		OutputText = string.Empty;
		HtmlPreview = string.Empty;
		currentResult = null;
		StatusText = "Параметри пошуку та поле виводу очищено.";
	}

	// ---------- Трансформація та збереження ----------

	/// <summary>Перетворює результат пошуку (або весь документ) у HTML, показує та зберігає його.</summary>
	[RelayCommand]
	private Task TransformAsync() => GuardAsync(async () =>
	{
		if (xmlContent is null || xslContent is null)
		{
			await dialogs.ShowMessageAsync("Трансформація", "Оберіть і XML-файл, і XSL-файл.");
			return;
		}

		string source = currentResult is null ? xmlContent : FragmentXmlBuilder.BuildString(currentResult);
		string html = transformer.Transform(source, xslContent);
		HtmlPreview = html;

		string suggestedName = Path.GetFileNameWithoutExtension(XmlFileName) + ".html";
		string? path = await localFiles.SaveAsync(suggestedName, html);
		if (path is null)
		{
			StatusText = "HTML показано у вікні; у файл не збережено.";
			return;
		}

		logger.Log(EventLevel.Transformation, $"Збережено у файл {Path.GetFileName(path)}");
		StatusText = $"HTML збережено: {path}";
	});

	[RelayCommand]
	private Task SaveXmlToDriveAsync() => SaveFragmentToDriveAsync(() => new XmlFragmentExporter(cloud));

	[RelayCommand]
	private Task SaveHtmlToDriveAsync() => SaveFragmentToDriveAsync(() =>
		xslContent is null ? null : new HtmlFragmentExporter(cloud, xslContent));

	[RelayCommand]
	private void ShowLog()
	{
		string log = logger.ReadAll();
		OutputText = log.Length == 0
			? $"Протокол порожній.{Environment.NewLine}Файл: {logger.LogFilePath}"
			: $"Протокол подій ({logger.LogFilePath}):{Environment.NewLine}{log}";
		HtmlPreview = string.Empty;
	}

	// ---------- Вихід ----------

	[RelayCommand]
	private async Task ExitAsync()
	{
		if (await ConfirmExitAsync())
			await lifetime.QuitAsync();
	}

	public Task<bool> ConfirmExitAsync() =>
		dialogs.ConfirmAsync("Вихід", ExitQuestion, "Так", "Ні");

	// ---------- Допоміжні методи ----------

	private async Task SaveFragmentToDriveAsync(Func<FragmentExporter?> createExporter)
	{
		await GuardAsync(async () =>
		{
			if (currentResult is null)
			{
				await dialogs.ShowMessageAsync("Збереження", "Спочатку виконайте пошук: зберігається відфільтрований фрагмент.");
				return;
			}

			FragmentExporter? exporter = createExporter();
			if (exporter is null)
			{
				await dialogs.ShowMessageAsync("Збереження", "Для збереження в HTML оберіть XSL-файл.");
				return;
			}

			string suggestedName = Path.GetFileNameWithoutExtension(XmlFileName) + "_filtered" + exporter.Extension;
			string? name = await dialogs.PromptAsync("Збереження на Google Drive", "Назва файлу:", suggestedName);
			if (name is null)
				return;

			string savedName;
			try
			{
				savedName = await exporter.ExportAsync(currentResult, name);
			}
			catch (ArgumentException ex)
			{
				await dialogs.ShowMessageAsync("Збереження", ex.Message);
				return;
			}

			logger.Log(EventLevel.Saving, $"Збережено відфільтрований фрагмент у файл {savedName} (Google Drive)");
			StatusText = $"Фрагмент збережено на Google Drive: {savedName}";
		});
	}

	private async Task<TextFile?> PickFromDriveAsync(string extension)
	{
		StatusText = "Звернення до Google Drive…";
		IReadOnlyList<CloudFileInfo> files = await cloud.ListFilesAsync(extension);
		if (files.Count == 0)
		{
			StatusText = "На Google Drive немає відповідних файлів.";
			await dialogs.ShowMessageAsync("Google Drive",
				$"На Google Drive ще немає файлів {extension}, збережених цією програмою. " +
				"Відкрийте файл із диска та натисніть «Зберегти вхідні файли на Drive».");
			return null;
		}

		int? index = await dialogs.ChooseAsync($"Файли {extension} на Google Drive", files.Select(f => f.Name).ToList());
		if (index is null)
		{
			StatusText = "Вибір файлу скасовано.";
			return null;
		}

		CloudFileInfo chosen = files[index.Value];
		return new TextFile(chosen.Name, await cloud.DownloadTextAsync(chosen.Id));
	}

	private async Task UploadTextAsync(string fileName, string mimeType, string content)
	{
		using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(content));
		await cloud.SaveAsync(fileName, mimeType, stream);
	}

	/// <summary>
	/// «Підвантаження даних»: після вибору файлу списки значень атрибутів student і group
	/// заповнюються тими значеннями, які справді є у файлі.
	/// </summary>
	private void LoadXml(TextFile file)
	{
		SearchResult all = analyzer.Search(file.Content, SearchCriteria.Empty);

		xmlContent = file.Content;
		XmlFileName = file.Name;
		currentResult = null;
		OutputText = string.Empty;
		HtmlPreview = string.Empty;
		Keyword = string.Empty;

		ResetAttributeLists();
		foreach (string student in DistinctSorted(all.Works.Select(w => w.Student)))
			Students.Add(student);
		foreach (string group in DistinctSorted(all.Works.Select(w => w.Group)))
			Groups.Add(group);

		StatusText = $"Відкрито {file.Name}: записів — {all.Works.Count}, студентів — {Students.Count - 1}, груп — {Groups.Count - 1}.";
	}

	private void LoadXsl(TextFile file)
	{
		xslContent = file.Content;
		XslFileName = file.Name;
		StatusText = $"Відкрито XSL-файл {file.Name}.";
	}

	private void ResetAttributeLists()
	{
		// Перший пункт «усі» лишається на місці, щоб список не втрачав вибране значення.
		if (Students.Count == 0)
			Students.Add(AllOption);
		while (Students.Count > 1)
			Students.RemoveAt(Students.Count - 1);
		if (Groups.Count == 0)
			Groups.Add(AllOption);
		while (Groups.Count > 1)
			Groups.RemoveAt(Groups.Count - 1);
		SelectedStudent = AllOption;
		SelectedGroup = AllOption;
	}

	private SearchCriteria BuildCriteria() => new(
		SelectedStudent is null or AllOption ? null : SelectedStudent,
		SelectedGroup is null or AllOption ? null : SelectedGroup,
		Keyword);

	private void SelectStrategy(IXmlSearchStrategy strategy, bool selected)
	{
		if (!selected || analyzer.Strategy == strategy)
			return;

		analyzer.Strategy = strategy;
		OnPropertyChanged(nameof(IsSaxSelected));
		OnPropertyChanged(nameof(IsDomSelected));
		OnPropertyChanged(nameof(IsLinqSelected));
		OnPropertyChanged(nameof(StrategyName));
		StatusText = $"Обрано спосіб аналізу: {strategy.Name}.";
	}

	private static IEnumerable<string> DistinctSorted(IEnumerable<string> values) =>
		values.Where(v => v.Length > 0).Distinct().OrderBy(v => v, StringComparer.CurrentCulture);

	/// <summary>Виконує дію та показує користувачеві зрозуміле повідомлення про очікувані помилки.</summary>
	private async Task GuardAsync(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex) when (ex is InvalidXmlDocumentException or TransformationException or CloudStorageException or IOException)
		{
			StatusText = "Сталася помилка.";
			await dialogs.ShowMessageAsync("Помилка", ex.Message);
		}
	}
}
