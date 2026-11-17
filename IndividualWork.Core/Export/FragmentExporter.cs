using IndividualWork.Core.Model;
using IndividualWork.Core.Storage;
using IndividualWork.Core.Transform;

namespace IndividualWork.Core.Export;

/// <summary>Продукт шаблону «Фабричний метод»: об'єкт, що записує фрагмент у відкритий файл.</summary>
public interface IFragmentWriter
{
	void Write(SearchResult fragment, Stream file);
}

/// <summary>
/// Творець шаблону «Фабричний метод». Тут зосереджено спільний код: відкрити файл,
/// записати в нього фрагмент і передати файл у сховище (Google Drive). Який саме
/// об'єкт писатиме дані, вирішують нащадки у фабричному методі <see cref="CreateWriter"/>.
/// </summary>
public abstract class FragmentExporter
{
	private readonly IFileStorage storage;

	protected FragmentExporter(IFileStorage storage)
	{
		this.storage = storage;
	}

	public abstract string Extension { get; }

	protected abstract string MimeType { get; }

	/// <summary>Фабричний метод: створює конкретний об'єкт запису.</summary>
	protected abstract IFragmentWriter CreateWriter();

	/// <summary>Зберігає фрагмент і повертає остаточну назву файлу.</summary>
	public async Task<string> ExportAsync(SearchResult fragment, string fileName, CancellationToken cancellationToken = default)
	{
		string name = NormalizeFileName(fileName);

		await using FileStream file = OpenFile();
		CreateWriter().Write(fragment, file);
		await file.FlushAsync(cancellationToken);
		file.Position = 0;

		await storage.SaveAsync(name, MimeType, file, cancellationToken);
		return name;
	}

	public string NormalizeFileName(string fileName)
	{
		string name = fileName.Trim();
		if (name.Length == 0)
			throw new ArgumentException("Назва файлу не може бути порожньою.", nameof(fileName));
		if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			throw new ArgumentException("Назва файлу містить недопустимі символи.", nameof(fileName));
		return name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? name : name + Extension;
	}

	/// <summary>Відкриває тимчасовий файл; після закриття він видаляється.</summary>
	private static FileStream OpenFile() =>
		new(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
			FileMode.CreateNew,
			FileAccess.ReadWrite,
			FileShare.None,
			bufferSize: 4096,
			FileOptions.DeleteOnClose | FileOptions.Asynchronous);
}

/// <summary>Конкретний творець: зберігає фрагмент у форматі .xml.</summary>
public sealed class XmlFragmentExporter : FragmentExporter
{
	public XmlFragmentExporter(IFileStorage storage) : base(storage)
	{
	}

	public override string Extension => ".xml";

	protected override string MimeType => "application/xml";

	protected override IFragmentWriter CreateWriter() => new XmlFragmentWriter();
}

/// <summary>Конкретний творець: зберігає фрагмент у форматі .html (через XSL-перетворення).</summary>
public sealed class HtmlFragmentExporter : FragmentExporter
{
	private readonly string xsl;

	public HtmlFragmentExporter(IFileStorage storage, string xsl) : base(storage)
	{
		this.xsl = xsl;
	}

	public override string Extension => ".html";

	protected override string MimeType => "text/html";

	protected override IFragmentWriter CreateWriter() => new HtmlFragmentWriter(xsl);
}

public sealed class XmlFragmentWriter : IFragmentWriter
{
	public void Write(SearchResult fragment, Stream file) => FragmentXmlBuilder.Write(fragment, file);
}

public sealed class HtmlFragmentWriter : IFragmentWriter
{
	private readonly string xsl;

	public HtmlFragmentWriter(string xsl)
	{
		this.xsl = xsl;
	}

	public void Write(SearchResult fragment, Stream file)
	{
		string html = new XslTransformer().Transform(FragmentXmlBuilder.BuildString(fragment), xsl);
		using var writer = new StreamWriter(file, new System.Text.UTF8Encoding(false), leaveOpen: true);
		writer.Write(html);
	}
}
