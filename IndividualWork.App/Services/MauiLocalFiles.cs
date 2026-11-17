using System.Text;
using CommunityToolkit.Maui.Storage;
using IndividualWork.Core.ViewModels;

namespace IndividualWork.App.Services;

/// <summary>Системні вікна вибору та збереження файлів.</summary>
public sealed class MauiLocalFiles : ILocalFiles
{
	public async Task<TextFile?> PickAsync(string title, string extension)
	{
		var fileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
		{
			[DevicePlatform.WinUI] = [extension]
		});

		FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
		{
			PickerTitle = title,
			FileTypes = fileType
		});
		if (file is null)
			return null;

		await using Stream stream = await file.OpenReadAsync();
		using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		return new TextFile(file.FileName, await reader.ReadToEndAsync());
	}

	public async Task<string?> SaveAsync(string suggestedName, string content)
	{
		using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(content));
		FileSaverResult result = await FileSaver.Default.SaveAsync(suggestedName, stream);
		if (result.IsSuccessful)
			return result.FilePath;
		if (IsCancellation(result.Exception))
			return null;
		throw new IOException($"Не вдалося зберегти файл: {result.Exception?.Message}", result.Exception);
	}

	private static bool IsCancellation(Exception? exception) =>
		exception is null or OperationCanceledException
		|| exception.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase);
}
