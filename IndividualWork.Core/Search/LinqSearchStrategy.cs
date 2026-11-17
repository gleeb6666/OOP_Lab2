using System.Xml.Linq;
using IndividualWork.Core.Model;

namespace IndividualWork.Core.Search;

/// <summary>
/// Аналіз через LINQ to XML: документ завантажується в XDocument,
/// а запит збирається ланцюжком Where — по одному на кожну задану умову.
/// </summary>
public sealed class LinqSearchStrategy : IXmlSearchStrategy
{
	public string Name => "LINQ to XML";

	public SearchResult Search(string xml, SearchCriteria criteria)
	{
		XDocument document = XDocument.Parse(xml);
		XElement? root = document.Root;
		if (root is null || root.Name.LocalName != XmlNames.Root)
			throw new InvalidXmlDocumentException($"Кореневий елемент має називатися {XmlNames.Root}.");

		IEnumerable<XElement> query = root.Elements(XmlNames.Work);
		if (criteria.Student is not null)
			query = query.Where(w => (string?)w.Attribute(XmlNames.Student) == criteria.Student);
		if (criteria.Group is not null)
			query = query.Where(w => (string?)w.Attribute(XmlNames.Group) == criteria.Group);
		if (criteria.Keyword is not null)
			query = query.Where(w => ContainsKeyword(w, criteria));

		return new SearchResult(ReadAttributes(root), query.Select(ToRecord).ToList());
	}

	public string DescribeQuery(SearchCriteria criteria)
	{
		var conditions = new List<string>();
		if (criteria.Student is not null)
			conditions.Add($"(string)w.Attribute(\"{XmlNames.Student}\") == \"{criteria.Student}\"");
		if (criteria.Group is not null)
			conditions.Add($"(string)w.Attribute(\"{XmlNames.Group}\") == \"{criteria.Group}\"");
		if (criteria.Keyword is not null)
			conditions.Add($"ContainsKeyword(w, \"{criteria.Keyword}\")");

		string where = conditions.Count == 0 ? string.Empty : $" where {string.Join(" && ", conditions)}";
		return $"LINQ: from w in root.Elements(\"{XmlNames.Work}\"){where} select w";
	}

	private static bool ContainsKeyword(XElement work, SearchCriteria criteria) =>
		work.DescendantsAndSelf().Any(element =>
			element.Attributes().Any(a => criteria.KeywordIn(a.Value))
			|| element.Nodes().OfType<XText>().Any(t => criteria.KeywordIn(t.Value)));

	private static WorkRecord ToRecord(XElement work)
	{
		XElement? teacher = work.Element(XmlNames.Teacher);
		XElement? schedule = work.Element(XmlNames.Schedule);

		return new WorkRecord
		{
			Attributes = ReadAttributes(work),
			Teacher = teacher is null
				? null
				: new Teacher(teacher.Value.Trim(), (string?)teacher.Attribute(XmlNames.Position)),
			Topic = work.Element(XmlNames.Topic)?.Value.Trim(),
			Schedule = schedule is null
				? null
				: new Schedule((string?)schedule.Attribute(XmlNames.Day), (string?)schedule.Attribute(XmlNames.Time)),
			Tasks = work.Elements(XmlNames.Tasks).Take(1).Elements(XmlNames.Task)
				.Select(t => new TaskItem(
					t.Value.Trim(),
					(string?)t.Attribute(XmlNames.Deadline),
					(string?)t.Attribute(XmlNames.Status)))
				.ToList(),
			Materials = work.Elements(XmlNames.Materials).Take(1).Elements(XmlNames.Material)
				.Select(m => new Material(
					m.Value.Trim(),
					(string?)m.Attribute(XmlNames.Type),
					(string?)m.Attribute(XmlNames.Url)))
				.ToList(),
			Questions = work.Elements(XmlNames.Questions).Take(1).Elements(XmlNames.Question)
				.Select(q => new QuestionItem(
					q.Element(XmlNames.Text)?.Value.Trim() ?? string.Empty,
					(string?)q.Attribute(XmlNames.Date),
					q.Element(XmlNames.Answer)?.Value.Trim()))
				.ToList()
		};
	}

	private static List<XmlAttributeValue> ReadAttributes(XElement element) =>
		element.Attributes()
			.Where(a => !a.IsNamespaceDeclaration)
			.Select(a => new XmlAttributeValue(a.Name.LocalName, a.Value))
			.ToList();
}
