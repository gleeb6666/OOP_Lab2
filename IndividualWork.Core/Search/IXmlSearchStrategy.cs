using IndividualWork.Core.Model;

namespace IndividualWork.Core.Search;

/// <summary>
/// Шаблон «Стратегія»: спільний інтерфейс способів аналізу XML-документа.
/// Реалізації: <see cref="SaxSearchStrategy"/>, <see cref="DomSearchStrategy"/>,
/// <see cref="LinqSearchStrategy"/>.
/// </summary>
public interface IXmlSearchStrategy
{
	/// <summary>Назва способу для інтерфейсу користувача.</summary>
	string Name { get; }

	/// <summary>Знаходить записи Work, що відповідають параметрам пошуку.</summary>
	SearchResult Search(string xml, SearchCriteria criteria);

	/// <summary>Текст запиту, який цей спосіб сформував для заданих параметрів.</summary>
	string DescribeQuery(SearchCriteria criteria);
}
