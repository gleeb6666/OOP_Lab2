using System.Xml;
using IndividualWork.Core.Model;

namespace IndividualWork.Core.Search;

/// <summary>
/// Аналіз через DOM: документ цілком завантажується в дерево XmlDocument,
/// а вузли відбираються динамічно сформованим запитом XPath.
/// </summary>
public sealed class DomSearchStrategy : IXmlSearchStrategy
{
	public string Name => "DOM";

	public SearchResult Search(string xml, SearchCriteria criteria)
	{
		var document = new XmlDocument();
		document.LoadXml(xml);

		XmlElement? root = document.DocumentElement;
		if (root is null || root.LocalName != XmlNames.Root)
			throw new InvalidXmlDocumentException($"Кореневий елемент має називатися {XmlNames.Root}.");

		var works = new List<WorkRecord>();
		XmlNodeList? nodes = document.SelectNodes(BuildXPath(criteria));
		if (nodes is not null)
		{
			foreach (XmlElement node in nodes)
			{
				if (criteria.Keyword is null || ContainsKeyword(node, criteria))
					works.Add(ToRecord(node));
			}
		}

		return new SearchResult(ReadAttributes(root), works);
	}

	public string DescribeQuery(SearchCriteria criteria)
	{
		string query = "XPath: " + BuildXPath(criteria);
		return criteria.Keyword is null
			? query
			: $"{query}; далі обхід піддерева: містить \"{criteria.Keyword}\"";
	}

	/// <summary>Формує XPath лише з тих умов, які задав користувач.</summary>
	public static string BuildXPath(SearchCriteria criteria)
	{
		var conditions = new List<string>();
		if (criteria.Student is not null)
			conditions.Add($"@{XmlNames.Student}={ToXPathLiteral(criteria.Student)}");
		if (criteria.Group is not null)
			conditions.Add($"@{XmlNames.Group}={ToXPathLiteral(criteria.Group)}");

		string path = $"/{XmlNames.Root}/{XmlNames.Work}";
		return conditions.Count == 0 ? path : $"{path}[{string.Join(" and ", conditions)}]";
	}

	/// <summary>Записує рядок як літерал XPath 1.0 (у ньому немає екранування лапок).</summary>
	private static string ToXPathLiteral(string value)
	{
		if (!value.Contains('\''))
			return $"'{value}'";
		if (!value.Contains('"'))
			return $"\"{value}\"";
		return "concat('" + value.Replace("'", "', \"'\", '") + "')";
	}

	private static bool ContainsKeyword(XmlNode node, SearchCriteria criteria)
	{
		if (node is XmlCharacterData data and (XmlText or XmlCDataSection))
			return criteria.KeywordIn(data.Value);
		if (node is not XmlElement element)
			return false;

		foreach (XmlAttribute attribute in element.Attributes)
		{
			if (criteria.KeywordIn(attribute.Value))
				return true;
		}
		foreach (XmlNode child in element.ChildNodes)
		{
			if (ContainsKeyword(child, criteria))
				return true;
		}
		return false;
	}

	private static WorkRecord ToRecord(XmlElement work)
	{
		XmlElement? teacher = work[XmlNames.Teacher];
		XmlElement? topic = work[XmlNames.Topic];
		XmlElement? schedule = work[XmlNames.Schedule];

		return new WorkRecord
		{
			Attributes = ReadAttributes(work),
			Teacher = teacher is null ? null : new Teacher(TextOf(teacher), Attribute(teacher, XmlNames.Position)),
			Topic = topic is null ? null : TextOf(topic),
			Schedule = schedule is null
				? null
				: new Schedule(Attribute(schedule, XmlNames.Day), Attribute(schedule, XmlNames.Time)),
			Tasks = Children(work, XmlNames.Tasks, XmlNames.Task)
				.Select(t => new TaskItem(TextOf(t), Attribute(t, XmlNames.Deadline), Attribute(t, XmlNames.Status)))
				.ToList(),
			Materials = Children(work, XmlNames.Materials, XmlNames.Material)
				.Select(m => new Material(TextOf(m), Attribute(m, XmlNames.Type), Attribute(m, XmlNames.Url)))
				.ToList(),
			Questions = Children(work, XmlNames.Questions, XmlNames.Question)
				.Select(q =>
				{
					XmlElement? text = q[XmlNames.Text];
					XmlElement? answer = q[XmlNames.Answer];
					return new QuestionItem(
						text is null ? string.Empty : TextOf(text),
						Attribute(q, XmlNames.Date),
						answer is null ? null : TextOf(answer));
				})
				.ToList()
		};
	}

	private static IEnumerable<XmlElement> Children(XmlElement work, string container, string item)
	{
		XmlElement? parent = work[container];
		return parent is null
			? []
			: parent.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == item);
	}

	private static List<XmlAttributeValue> ReadAttributes(XmlElement element) =>
		element.Attributes.Cast<XmlAttribute>()
			.Select(a => new XmlAttributeValue(a.LocalName, a.Value))
			.ToList();

	private static string? Attribute(XmlElement element, string name) =>
		element.HasAttribute(name) ? element.GetAttribute(name) : null;

	private static string TextOf(XmlElement element) => element.InnerText.Trim();
}
