using System.Text.RegularExpressions;
using System.Xml.Linq;
using IndividualWork.Core.Export;
using IndividualWork.Core.Logging;
using IndividualWork.Core.Model;
using IndividualWork.Core.Search;
using IndividualWork.Core.Storage;
using IndividualWork.Core.Transform;

namespace IndividualWork.Tests;

public class TransformExportLoggingTests
{
	private static SearchResult Fragment(string group) =>
		new DomSearchStrategy().Search(TestData.Xml, new SearchCriteria(null, group, null));

	[Fact]
	public void Transformer_ProducesHtmlTable()
	{
		string html = new XslTransformer().Transform(TestData.Xml, TestData.Xsl);

		Assert.Contains("<table", html);
		Assert.Contains("Максимов Гліб", html);
		Assert.Contains("Записів: 10", html);
		Assert.Contains("charset=utf-8", html, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(11, Regex.Matches(html, "<tr>").Count);
	}

	[Fact]
	public void Transformer_WorksOnFilteredFragment()
	{
		string fragmentXml = FragmentXmlBuilder.BuildString(Fragment("К-26"));
		string html = new XslTransformer().Transform(fragmentXml, TestData.Xsl);

		Assert.Contains("Записів: 2", html);
		Assert.Contains("Ткачук Олег", html);
		Assert.DoesNotContain("Максимов Гліб", html);
	}

	[Fact]
	public void Transformer_ReportsBrokenStylesheet()
	{
		var transformer = new XslTransformer();
		Assert.Throws<TransformationException>(() => transformer.Transform(TestData.Xml, "<не xsl"));
		Assert.Throws<TransformationException>(() => transformer.Transform(TestData.Xml,
			"<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><xsl:unknown/></xsl:stylesheet>"));
	}

	[Fact]
	public async Task XmlExporter_SavesWellFormedFragment()
	{
		string folder = TestData.NewTempFolder();
		FragmentExporter exporter = new XmlFragmentExporter(new LocalFolderStorage(folder));

		string name = await exporter.ExportAsync(Fragment("К-28"), "result");

		Assert.Equal("result.xml", name);
		XElement root = XDocument.Load(Path.Combine(folder, name)).Root!;
		Assert.Equal(XmlNames.Root, root.Name.LocalName);
		Assert.Equal(4, root.Elements(XmlNames.Work).Count());
		Assert.All(root.Elements(XmlNames.Work), w => Assert.Equal("К-28", (string?)w.Attribute(XmlNames.Group)));
	}

	[Fact]
	public async Task HtmlExporter_SavesTransformedFragment()
	{
		string folder = TestData.NewTempFolder();
		FragmentExporter exporter = new HtmlFragmentExporter(new LocalFolderStorage(folder), TestData.Xsl);

		string name = await exporter.ExportAsync(Fragment("К-27"), "result.HTML");

		Assert.Equal("result.HTML", name);
		string html = await File.ReadAllTextAsync(Path.Combine(folder, name));
		Assert.Contains("Записів: 4", html);
		Assert.Contains("Мельник Софія", html);
	}

	[Fact]
	public async Task Exporter_RejectsBadFileNames()
	{
		FragmentExporter exporter = new XmlFragmentExporter(new LocalFolderStorage(TestData.NewTempFolder()));

		await Assert.ThrowsAsync<ArgumentException>(() => exporter.ExportAsync(Fragment("К-28"), "   "));
		await Assert.ThrowsAsync<ArgumentException>(() => exporter.ExportAsync(Fragment("К-28"), "a<b>.xml"));
	}

	[Fact]
	public void Logger_IsSingleInstance()
	{
		Assert.Same(EventLogger.Instance, EventLogger.Instance);
		Assert.Empty(typeof(EventLogger).GetConstructors());
	}

	[Fact]
	public void Logger_WritesTimeLevelAndMessage()
	{
		EventLogger logger = EventLogger.Instance;
		string marker = Guid.NewGuid().ToString("N");

		logger.Log(EventLevel.Filtering, $"Параметри: {marker}");
		logger.Log(EventLevel.Transformation, $"Збережено у файл {marker}.html");
		logger.Log(EventLevel.Saving, $"Збережено відфільтрований фрагмент у файл {marker}.xml");

		string[] lines = File.ReadAllLines(TestData.LogPath).Where(l => l.Contains(marker)).ToArray();
		Assert.Equal(3, lines.Length);
		Assert.Matches(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2} Фільтрація\. Параметри: ", lines[0]);
		Assert.Matches(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2} Трансформація\. Збережено у файл ", lines[1]);
		Assert.Matches(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2} Збереження\. Збережено ", lines[2]);
		Assert.Contains(marker, logger.ReadAll());
	}
}
