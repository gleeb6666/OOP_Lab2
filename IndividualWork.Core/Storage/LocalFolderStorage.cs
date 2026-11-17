namespace IndividualWork.Core.Storage;

/// <summary>Зберігає файли в папку на локальному диску.</summary>
public sealed class LocalFolderStorage : IFileStorage
{
	private readonly string folder;

	public LocalFolderStorage(string folder)
	{
		this.folder = folder;
	}

	public async Task<string> SaveAsync(string fileName, string mimeType, Stream content, CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(folder);
		string path = Path.Combine(folder, fileName);
		await using var file = File.Create(path);
		await content.CopyToAsync(file, cancellationToken);
		return path;
	}
}
