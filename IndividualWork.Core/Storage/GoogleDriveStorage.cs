using System.Runtime.ExceptionServices;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace IndividualWork.Core.Storage;

/// <summary>
/// Робота з Google Drive. Використовується доступ drive.file: програма бачить
/// лише ті файли, які сама створила на диску користувача.
/// </summary>
public sealed class GoogleDriveStorage : ICloudFileStorage
{
	public const string ApplicationName = "IndividualWorkXml";
	private const string FileFields = "id, name, modifiedTime";

	private readonly Func<CancellationToken, Task<Stream>> openClientSecrets;
	private readonly string tokenDirectory;
	private DriveService? service;

	public GoogleDriveStorage(Func<CancellationToken, Task<Stream>> openClientSecrets, string tokenDirectory)
	{
		this.openClientSecrets = openClientSecrets;
		this.tokenDirectory = tokenDirectory;
	}

	public async Task<IReadOnlyList<CloudFileInfo>> ListFilesAsync(string extension, CancellationToken cancellationToken = default)
	{
		return await RunAsync(async drive =>
		{
			var request = drive.Files.List();
			request.Q = "trashed = false";
			request.Spaces = "drive";
			request.OrderBy = "modifiedTime desc";
			request.PageSize = 200;
			request.Fields = $"files({FileFields})";

			var result = await request.ExecuteAsync(cancellationToken);
			return (IReadOnlyList<CloudFileInfo>)result.Files
				.Where(f => f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
				.Select(ToInfo)
				.ToList();
		}, cancellationToken);
	}

	public async Task<string> DownloadTextAsync(string fileId, CancellationToken cancellationToken = default)
	{
		return await RunAsync(async drive =>
		{
			using var content = new MemoryStream();
			var progress = await drive.Files.Get(fileId).DownloadAsync(content, cancellationToken);
			if (progress.Status != DownloadStatus.Completed)
				throw new CloudStorageException("Не вдалося завантажити файл з Google Drive.", progress.Exception);

			content.Position = 0;
			using var reader = new StreamReader(content, detectEncodingFromByteOrderMarks: true);
			return await reader.ReadToEndAsync(cancellationToken);
		}, cancellationToken);
	}

	/// <summary>Зберігає файл на Google Drive; файл із такою самою назвою оновлюється.</summary>
	public async Task<string> SaveAsync(string fileName, string mimeType, Stream content, CancellationToken cancellationToken = default)
	{
		return await RunAsync(async drive =>
		{
			string? existingId = await FindByNameAsync(drive, fileName, cancellationToken);
			if (existingId is null)
			{
				var metadata = new DriveFile { Name = fileName, MimeType = mimeType };
				var create = drive.Files.Create(metadata, content, mimeType);
				create.Fields = FileFields;
				EnsureUploaded(await create.UploadAsync(cancellationToken));
			}
			else
			{
				var update = drive.Files.Update(new DriveFile(), existingId, content, mimeType);
				update.Fields = FileFields;
				EnsureUploaded(await update.UploadAsync(cancellationToken));
			}
			return $"Google Drive: {fileName}";
		}, cancellationToken);
	}

	private static async Task<string?> FindByNameAsync(DriveService drive, string fileName, CancellationToken cancellationToken)
	{
		var request = drive.Files.List();
		string escaped = fileName.Replace("\\", "\\\\").Replace("'", "\\'");
		request.Q = $"name = '{escaped}' and trashed = false";
		request.Spaces = "drive";
		request.PageSize = 1;
		request.Fields = "files(id)";

		var result = await request.ExecuteAsync(cancellationToken);
		return result.Files.FirstOrDefault()?.Id;
	}

	private async Task<DriveService> GetServiceAsync(CancellationToken cancellationToken)
	{
		if (service is not null)
			return service;

		GoogleClientSecrets secrets;
		try
		{
			await using var stream = await openClientSecrets(cancellationToken);
			secrets = await GoogleClientSecrets.FromStreamAsync(stream, cancellationToken);
		}
		catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
		{
			throw new CloudStorageException(
				"Не знайдено файл client_secret.json з ключем доступу до Google Drive. " +
				"Як його отримати — див. GOOGLE_DRIVE_SETUP.md у папці проєкту.", ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not CloudStorageException)
		{
			throw new CloudStorageException("Файл client_secret.json пошкоджений або має неправильний формат.", ex);
		}

		var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
			secrets.Secrets,
			[DriveService.ScopeConstants.DriveFile],
			"user",
			cancellationToken,
			new FileDataStore(tokenDirectory, fullPath: true));

		service = new DriveService(new BaseClientService.Initializer
		{
			HttpClientInitializer = credential,
			ApplicationName = ApplicationName
		});
		return service;
	}

	private async Task<T> RunAsync<T>(Func<DriveService, Task<T>> action, CancellationToken cancellationToken)
	{
		try
		{
			return await action(await GetServiceAsync(cancellationToken));
		}
		catch (OperationCanceledException ex)
		{
			throw new CloudStorageException("Операцію з Google Drive скасовано або перевищено час очікування.", ex);
		}
		catch (TokenResponseException ex)
		{
			throw new CloudStorageException($"Google не надав доступ: {ex.Error?.ErrorDescription ?? ex.Error?.Error ?? ex.Message}", ex);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			throw new CloudStorageException("Файл не знайдено на Google Drive (можливо, його видалено).", ex);
		}
		catch (GoogleApiException ex)
		{
			throw new CloudStorageException($"Помилка Google Drive: {ex.Error?.Message ?? ex.Message}", ex);
		}
		catch (HttpRequestException ex)
		{
			throw new CloudStorageException("Немає з'єднання з Google Drive. Перевірте підключення до інтернету.", ex);
		}
	}

	private static void EnsureUploaded(IUploadProgress progress)
	{
		if (progress.Status == UploadStatus.Completed)
			return;
		if (progress.Exception is GoogleApiException or HttpRequestException)
			ExceptionDispatchInfo.Throw(progress.Exception);
		throw new CloudStorageException("Не вдалося завантажити файл на Google Drive.", progress.Exception);
	}

	private static CloudFileInfo ToInfo(DriveFile file) =>
		new(file.Id, file.Name, file.ModifiedTimeDateTimeOffset);
}
