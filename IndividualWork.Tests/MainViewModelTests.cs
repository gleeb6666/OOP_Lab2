using IndividualWork.Core.Storage;
using IndividualWork.Core.ViewModels;

namespace IndividualWork.Tests;

public class MainViewModelTests
{
	private readonly FakeDialogs dialogs = new();
	private readonly FakeLocalFiles localFiles = new();
	private readonly FakeCloud cloud = new();
	private readonly FakeLifetime lifetime = new();
	private readonly MainViewModel viewModel;

	public MainViewModelTests()
	{
		viewModel = new MainViewModel(dialogs, localFiles, cloud, lifetime);
	}

	private async Task OpenSampleFilesAsync()
	{
		localFiles.NextPick = new TextFile("individual_work.xml", TestData.Xml);
		await viewModel.OpenXmlCommand.ExecuteAsync(null);
		localFiles.NextPick = new TextFile("individual_work.xsl", TestData.Xsl);
		await viewModel.OpenXslCommand.ExecuteAsync(null);
	}

	[Fact]
	public async Task OpeningXml_FillsAttributeListsFromFile()
	{
		Assert.Equal([MainViewModel.AllOption], viewModel.Students);

		await OpenSampleFilesAsync();

		Assert.Equal("individual_work.xml", viewModel.XmlFileName);
		Assert.Equal("individual_work.xsl", viewModel.XslFileName);
		Assert.Equal(
			[MainViewModel.AllOption, "Бондаренко Максим", "Коваленко Анна", "Максимов Гліб", "Мельник Софія", "Ткачук Олег", "Шевченко Данило"],
			viewModel.Students);
		Assert.Equal([MainViewModel.AllOption, "К-26", "К-27", "К-28"], viewModel.Groups);
		Assert.Equal(MainViewModel.AllOption, viewModel.SelectedStudent);
	}

	[Fact]
	public async Task OpeningBrokenXml_ShowsErrorAndKeepsState()
	{
		localFiles.NextPick = new TextFile("bad.xml", "<IndividualWork>");
		await viewModel.OpenXmlCommand.ExecuteAsync(null);

		Assert.Single(dialogs.Messages);
		Assert.Equal("не обрано", viewModel.XmlFileName);
	}

	[Fact]
	public async Task Search_WithoutFile_AsksToChooseFile()
	{
		await viewModel.SearchCommand.ExecuteAsync(null);

		Assert.Single(dialogs.Messages);
		Assert.Equal(string.Empty, viewModel.OutputText);
	}

	[Theory]
	[InlineData("SAX")]
	[InlineData("DOM")]
	[InlineData("LINQ to XML")]
	public async Task Search_UsesSelectedStrategyAndCriteria(string strategy)
	{
		await OpenSampleFilesAsync();
		viewModel.IsSaxSelected = strategy == "SAX";
		viewModel.IsDomSelected = strategy == "DOM";
		viewModel.IsLinqSelected = strategy == "LINQ to XML";
		viewModel.SelectedStudent = "Максимов Гліб";
		viewModel.SelectedGroup = "К-28";
		viewModel.Keyword = "google";

		await viewModel.SearchCommand.ExecuteAsync(null);

		Assert.Equal(strategy, viewModel.StrategyName);
		Assert.Contains($"Спосіб аналізу: {strategy}", viewModel.OutputText);
		Assert.Contains("Знайдено записів: 1", viewModel.OutputText);
		Assert.Contains("Інтеграція застосунку MAUI з Google Drive", viewModel.OutputText);
		Assert.DoesNotContain("SAX, DOM та LINQ to XML", viewModel.OutputText.Split('\n', 2)[1]);
	}

	[Fact]
	public async Task OnlyOneStrategyIsSelectedAtATime()
	{
		Assert.True(viewModel.IsDomSelected);

		viewModel.IsSaxSelected = true;
		viewModel.IsDomSelected = false; // так робить RadioButton, знімаючи попередню позначку

		Assert.True(viewModel.IsSaxSelected);
		Assert.False(viewModel.IsDomSelected);
		Assert.False(viewModel.IsLinqSelected);
		await Task.CompletedTask;
	}

	[Fact]
	public async Task Clear_ResetsOutputAndSearchParameters()
	{
		await OpenSampleFilesAsync();
		viewModel.SelectedStudent = "Коваленко Анна";
		viewModel.SelectedGroup = "К-28";
		viewModel.Keyword = "xslt";
		await viewModel.SearchCommand.ExecuteAsync(null);
		await viewModel.TransformCommand.ExecuteAsync(null);
		Assert.NotEqual(string.Empty, viewModel.OutputText);
		Assert.NotEqual(string.Empty, viewModel.HtmlPreview);

		viewModel.ClearCommand.Execute(null);

		Assert.Equal(MainViewModel.AllOption, viewModel.SelectedStudent);
		Assert.Equal(MainViewModel.AllOption, viewModel.SelectedGroup);
		Assert.Equal(string.Empty, viewModel.Keyword);
		Assert.Equal(string.Empty, viewModel.OutputText);
		Assert.Equal(string.Empty, viewModel.HtmlPreview);
		Assert.Equal("individual_work.xml", viewModel.XmlFileName);
		Assert.Equal(7, viewModel.Students.Count);
	}

	[Fact]
	public async Task Transform_ShowsAndSavesHtmlOfFilteredFragment()
	{
		await OpenSampleFilesAsync();
		viewModel.SelectedGroup = "К-26";
		await viewModel.SearchCommand.ExecuteAsync(null);

		await viewModel.TransformCommand.ExecuteAsync(null);

		Assert.Contains("Записів: 2", viewModel.HtmlPreview);
		Assert.Equal("individual_work.html", localFiles.SavedName);
		Assert.Equal(viewModel.HtmlPreview, localFiles.SavedContent);
	}

	[Fact]
	public async Task Transform_WithoutSearch_UsesWholeDocument()
	{
		await OpenSampleFilesAsync();

		await viewModel.TransformCommand.ExecuteAsync(null);

		Assert.Contains("Записів: 10", viewModel.HtmlPreview);
	}

	[Fact]
	public async Task Transform_WithoutXsl_AsksForFiles()
	{
		localFiles.NextPick = new TextFile("individual_work.xml", TestData.Xml);
		await viewModel.OpenXmlCommand.ExecuteAsync(null);

		await viewModel.TransformCommand.ExecuteAsync(null);

		Assert.Single(dialogs.Messages);
		Assert.Equal(string.Empty, viewModel.HtmlPreview);
	}

	[Fact]
	public async Task SaveToDrive_UploadsFilteredFragmentInBothFormats()
	{
		await OpenSampleFilesAsync();
		viewModel.SelectedStudent = "Мельник Софія";
		await viewModel.SearchCommand.ExecuteAsync(null);

		dialogs.PromptAnswer = "sofia";
		await viewModel.SaveXmlToDriveCommand.ExecuteAsync(null);
		await viewModel.SaveHtmlToDriveCommand.ExecuteAsync(null);

		Assert.Equal(["sofia.xml", "sofia.html"], cloud.Saved.Keys);
		Assert.Contains("student=\"Мельник Софія\"", cloud.Saved["sofia.xml"]);
		Assert.DoesNotContain("Максимов", cloud.Saved["sofia.xml"]);
		Assert.Contains("Записів: 2", cloud.Saved["sofia.html"]);
	}

	[Fact]
	public async Task SaveToDrive_BeforeSearch_IsRefused()
	{
		await OpenSampleFilesAsync();

		await viewModel.SaveXmlToDriveCommand.ExecuteAsync(null);

		Assert.Single(dialogs.Messages);
		Assert.Empty(cloud.Saved);
	}

	[Fact]
	public async Task DriveFiles_CanBeUploadedAndOpenedAgain()
	{
		await OpenSampleFilesAsync();
		await viewModel.UploadSourcesCommand.ExecuteAsync(null);
		Assert.Equal(["individual_work.xml", "individual_work.xsl"], cloud.Saved.Keys);

		var second = new MainViewModel(dialogs, localFiles, cloud, lifetime);
		dialogs.ChoiceIndex = 0;
		await second.OpenXmlFromDriveCommand.ExecuteAsync(null);
		await second.OpenXslFromDriveCommand.ExecuteAsync(null);

		Assert.Equal("individual_work.xml", second.XmlFileName);
		Assert.Equal("individual_work.xsl", second.XslFileName);
		Assert.Equal(4, second.Groups.Count);
	}

	[Fact]
	public async Task CloudErrors_AreShownToUser()
	{
		cloud.Fail = true;

		await viewModel.OpenXmlFromDriveCommand.ExecuteAsync(null);

		Assert.Equal("Немає з'єднання", Assert.Single(dialogs.Messages));
	}

	[Theory]
	[InlineData(true, 1)]
	[InlineData(false, 0)]
	public async Task Exit_AsksForConfirmation(bool answer, int expectedQuits)
	{
		dialogs.ConfirmAnswer = answer;

		await viewModel.ExitCommand.ExecuteAsync(null);

		Assert.Equal(MainViewModel.ExitQuestion, dialogs.LastConfirmQuestion);
		Assert.Equal(("Так", "Ні"), dialogs.LastConfirmButtons);
		Assert.Equal(expectedQuits, lifetime.QuitCount);
	}

	private sealed class FakeDialogs : IUserDialogs
	{
		public List<string> Messages { get; } = [];
		public bool ConfirmAnswer { get; set; }
		public string? LastConfirmQuestion { get; private set; }
		public (string, string) LastConfirmButtons { get; private set; }
		public string? PromptAnswer { get; set; }
		public int? ChoiceIndex { get; set; }

		public Task ShowMessageAsync(string title, string message)
		{
			Messages.Add(message);
			return Task.CompletedTask;
		}

		public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
		{
			LastConfirmQuestion = message;
			LastConfirmButtons = (accept, cancel);
			return Task.FromResult(ConfirmAnswer);
		}

		public Task<string?> PromptAsync(string title, string message, string initialValue) =>
			Task.FromResult(PromptAnswer);

		public Task<int?> ChooseAsync(string title, IReadOnlyList<string> options) =>
			Task.FromResult(ChoiceIndex);
	}

	private sealed class FakeLocalFiles : ILocalFiles
	{
		public TextFile? NextPick { get; set; }
		public string? SavedName { get; private set; }
		public string? SavedContent { get; private set; }

		public Task<TextFile?> PickAsync(string title, string extension) => Task.FromResult(NextPick);

		public Task<string?> SaveAsync(string suggestedName, string content)
		{
			SavedName = suggestedName;
			SavedContent = content;
			return Task.FromResult<string?>(Path.Combine("C:\\out", suggestedName));
		}
	}

	private sealed class FakeCloud : ICloudFileStorage
	{
		public Dictionary<string, string> Saved { get; } = [];
		public bool Fail { get; set; }

		public async Task<string> SaveAsync(string fileName, string mimeType, Stream content, CancellationToken cancellationToken = default)
		{
			using var reader = new StreamReader(content, leaveOpen: true);
			Saved[fileName] = await reader.ReadToEndAsync(cancellationToken);
			return fileName;
		}

		public Task<IReadOnlyList<CloudFileInfo>> ListFilesAsync(string extension, CancellationToken cancellationToken = default)
		{
			if (Fail)
				throw new CloudStorageException("Немає з'єднання");
			return Task.FromResult<IReadOnlyList<CloudFileInfo>>(Saved.Keys
				.Where(k => k.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
				.Select(k => new CloudFileInfo(k, k, null))
				.ToList());
		}

		public Task<string> DownloadTextAsync(string fileId, CancellationToken cancellationToken = default) =>
			Task.FromResult(Saved[fileId]);
	}

	private sealed class FakeLifetime : IAppLifetime
	{
		public int QuitCount { get; private set; }

		public Task QuitAsync()
		{
			QuitCount++;
			return Task.CompletedTask;
		}
	}
}
