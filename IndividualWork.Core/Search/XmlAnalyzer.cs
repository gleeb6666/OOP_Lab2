using System.Xml;
using IndividualWork.Core.Model;

namespace IndividualWork.Core.Search;

/// <summary>
/// Контекст шаблону «Стратегія»: виконує пошук тим способом, який зараз обрано,
/// і нічого не знає про те, як саме цей спосіб читає XML.
/// </summary>
public sealed class XmlAnalyzer
{
	public XmlAnalyzer(IXmlSearchStrategy strategy)
	{
		Strategy = strategy;
	}

	public IXmlSearchStrategy Strategy { get; set; }

	public SearchResult Search(string xml, SearchCriteria criteria)
	{
		try
		{
			return Strategy.Search(xml, criteria);
		}
		catch (XmlException ex)
		{
			throw new InvalidXmlDocumentException($"Файл не є коректним XML-документом: {ex.Message}", ex);
		}
	}

	public string DescribeQuery(SearchCriteria criteria) => Strategy.DescribeQuery(criteria);
}

public sealed class InvalidXmlDocumentException : Exception
{
	public InvalidXmlDocumentException(string message, Exception? innerException = null)
		: base(message, innerException)
	{
	}
}
