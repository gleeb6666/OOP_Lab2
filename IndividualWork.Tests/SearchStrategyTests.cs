using System.Xml.Linq;
using IndividualWork.Core.Model;
using IndividualWork.Core.Search;

namespace IndividualWork.Tests;

public class SearchStrategyTests
{
	public static TheoryData<string?, string?, string?, int> Cases => new()
	{
		{ null, null, null, 10 },
		{ "Максимов Гліб", null, null, 2 },
		{ null, "К-27", null, 4 },
		{ "Коваленко Анна", "К-28", null, 2 },
		{ "Коваленко Анна", "К-27", null, 0 },
		{ null, null, "drive", 1 },
		{ null, null, "онлайн", 2 },
		{ null, null, "ОДИНАК", 1 },
		{ "Максимов Гліб", null, "XSL", 1 },
		{ null, "К-26", "Git", 1 },
		{ null, null, "такого слова немає", 0 },
		{ "Невідомий Студент", null, null, 0 }
	};

	[Theory]
	[MemberData(nameof(Cases))]
	public void AllStrategies_FindExpectedNumberOfRecords(string? student, string? group, string? keyword, int expected)
	{
		var criteria = new SearchCriteria(student, group, keyword);
		foreach (IXmlSearchStrategy strategy in TestData.Strategies)
			Assert.Equal(expected, strategy.Search(TestData.Xml, criteria).Works.Count);
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void AllStrategies_ReturnIdenticalData(string? student, string? group, string? keyword, int expected)
	{
		var criteria = new SearchCriteria(student, group, keyword);
		string[] fragments = TestData.Strategies
			.Select(s => FragmentXmlBuilder.BuildString(s.Search(TestData.Xml, criteria)))
			.ToArray();

		Assert.Equal(fragments[0], fragments[1]);
		Assert.Equal(fragments[0], fragments[2]);
		Assert.Equal(expected, XDocument.Parse(fragments[0]).Root!.Elements(XmlNames.Work).Count());
	}

	[Fact]
	public void FullFragment_PreservesEverythingFromSourceDocument()
	{
		XElement source = XDocument.Parse(TestData.Xml).Root!;
		foreach (IXmlSearchStrategy strategy in TestData.Strategies)
		{
			XElement rebuilt = FragmentXmlBuilder.Build(strategy.Search(TestData.Xml, SearchCriteria.Empty)).Root!;
			Assert.True(XNode.DeepEquals(Normalize(source), Normalize(rebuilt)), strategy.Name);
		}
	}

	[Fact]
	public void SourceFile_MeetsAssignmentRequirements()
	{
		SearchResult all = new LinqSearchStrategy().Search(TestData.Xml, SearchCriteria.Empty);

		// Кількість атрибутів у головного вузла різна у різних вузлів.
		Assert.True(all.Works.Select(w => w.Attributes.Count).Distinct().Count() >= 3);
		// Щонайменше два рівні вкладеності нижче кореня.
		Assert.Contains(all.Works, w => w.Tasks.Count > 0 && w.Questions.Any(q => q.Answer is not null));
	}

	[Fact]
	public void Result_ContainsNestedData()
	{
		foreach (IXmlSearchStrategy strategy in TestData.Strategies)
		{
			WorkRecord work = strategy.Search(TestData.Xml, new SearchCriteria(null, null, "рекурсивний спуск")).Works.Single();

			Assert.Equal("Шевченко Данило", work.Student);
			Assert.Equal("К-27", work.Group);
			Assert.Equal("асистент", work.Teacher!.Position);
			Assert.Equal("середа", work.Schedule!.Day);
			Assert.Equal(2, work.Tasks.Count);
			Assert.Equal("прострочено", work.Tasks[1].Status);
			Assert.Single(work.Materials);
			Assert.Empty(work.Questions);
		}
	}

	[Fact]
	public void AttributeValuesWithQuotes_AreHandledByAllStrategies()
	{
		const string xml = """
			<IndividualWork>
			  <Work id="1" student="О'Коннор &quot;Шон&quot;" group="К-28"><Topic>Тема</Topic></Work>
			  <Work id="2" student="Інший" group="К-28"/>
			</IndividualWork>
			""";
		var criteria = new SearchCriteria("О'Коннор \"Шон\"", null, null);

		foreach (IXmlSearchStrategy strategy in TestData.Strategies)
			Assert.Equal("1", strategy.Search(xml, criteria).Works.Single().GetAttribute("id"));
	}

	[Fact]
	public void DomStrategy_BuildsXPathOnlyFromGivenConditions()
	{
		Assert.Equal("/IndividualWork/Work", DomSearchStrategy.BuildXPath(SearchCriteria.Empty));
		Assert.Equal("/IndividualWork/Work[@group='К-28']", DomSearchStrategy.BuildXPath(new SearchCriteria(null, "К-28", null)));
		Assert.Equal(
			"/IndividualWork/Work[@student='Максимов Гліб' and @group='К-28']",
			DomSearchStrategy.BuildXPath(new SearchCriteria("Максимов Гліб", "К-28", "слово")));
	}

	[Fact]
	public void Analyzer_SwitchesStrategyAtRuntime()
	{
		var analyzer = new XmlAnalyzer(new SaxSearchStrategy());
		var criteria = new SearchCriteria(null, "К-26", null);
		Assert.StartsWith("XmlReader", analyzer.DescribeQuery(criteria));

		analyzer.Strategy = new DomSearchStrategy();
		Assert.StartsWith("XPath", analyzer.DescribeQuery(criteria));

		analyzer.Strategy = new LinqSearchStrategy();
		Assert.StartsWith("LINQ", analyzer.DescribeQuery(criteria));
		Assert.Equal(2, analyzer.Search(TestData.Xml, criteria).Works.Count);
	}

	[Fact]
	public void Analyzer_ReportsBrokenXml()
	{
		foreach (IXmlSearchStrategy strategy in TestData.Strategies)
		{
			var analyzer = new XmlAnalyzer(strategy);
			Assert.Throws<InvalidXmlDocumentException>(() => analyzer.Search("<IndividualWork><Work>", SearchCriteria.Empty));
			Assert.Throws<InvalidXmlDocumentException>(() => analyzer.Search("це не xml", SearchCriteria.Empty));
			Assert.Throws<InvalidXmlDocumentException>(() => analyzer.Search("<Other/>", SearchCriteria.Empty));
		}
	}

	[Fact]
	public void Criteria_IgnoreBlankValues()
	{
		var criteria = new SearchCriteria("  ", "", null);
		Assert.True(criteria.IsEmpty);
		Assert.Equal("без обмежень", criteria.Describe());
		Assert.Equal("«student» – А, «keyword» – б", new SearchCriteria(" А ", null, " б ").Describe());
	}

	/// <summary>Прибирає коментарі та зайві пробіли, щоб порівнювати лише зміст.</summary>
	private static XElement Normalize(XElement element) =>
		new(element.Name,
			element.Attributes().Where(a => !a.IsNamespaceDeclaration),
			element.HasElements
				? element.Elements().Select(Normalize)
				: element.Value.Trim().Length == 0 ? null : element.Value.Trim());
}
