namespace IndividualWork.Core.Storage;

/// <summary>Сховище, у яке можна зберегти файл (Google Drive або локальна папка).</summary>
public interface IFileStorage
{
	/// <summary>Зберігає файл і повертає опис місця, де він опинився.</summary>
	Task<string> SaveAsync(string fileName, string mimeType, Stream content, CancellationToken cancellationToken = default);
}

public sealed record CloudFileInfo(string Id, string Name, DateTimeOffset? ModifiedTime);

/// <summary>Хмарне сховище: крім збереження, уміє показати список файлів і прочитати файл.</summary>
public interface ICloudFileStorage : IFileStorage
{
	Task<IReadOnlyList<CloudFileInfo>> ListFilesAsync(string extension, CancellationToken cancellationToken = default);

	Task<string> DownloadTextAsync(string fileId, CancellationToken cancellationToken = default);
}

public sealed class CloudStorageException : Exception
{
	public CloudStorageException(string message, Exception? innerException = null)
		: base(message, innerException)
	{
	}
}
