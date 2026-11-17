using System.Text;
using System.Xml;
using IndividualWork.Core.Model;

namespace IndividualWork.Core.Search;

/// <summary>
/// Аналіз у стилі SAX: документ читається потоком, вузол за вузлом, і ніколи
/// не завантажується в пам'ять цілком. У .NET роль SAX виконує XmlReader.
/// </summary>
public sealed class SaxSearchStrategy : IXmlSearchStrategy
{
	public string Name => "SAX";

	public SearchResult Search(string xml, SearchCriteria criteria)
	{
		var settings = new XmlReaderSettings
		{
			IgnoreComments = true,
			IgnoreWhitespace = true,
			IgnoreProcessingInstructions = true
		};
		using var reader = XmlReader.Create(new StringReader(xml), settings);

		reader.MoveToContent();
		if (reader.NodeType != XmlNodeType.Element || reader.LocalName != XmlNames.Root)
			throw new InvalidXmlDocumentException($"Кореневий елемент має називатися {XmlNames.Root}.");

		var rootAttributes = ReadAttributes(reader);
		var works = new List<WorkRecord>();
		if (reader.IsEmptyElement)
			return new SearchResult(rootAttributes, works);

		int rootDepth = reader.Depth;
		reader.Read();
		while (!reader.EOF && reader.Depth > rootDepth)
		{
			if (reader.NodeType != XmlNodeType.Element)
			{
				reader.Read();
				continue;
			}
			if (reader.LocalName != XmlNames.Work)
			{
				reader.Skip();
				continue;
			}

			var attributes = ReadAttributes(reader);
			if (!criteria.MatchesAttributes(Find(attributes, XmlNames.Student), Find(attributes, XmlNames.Group)))
			{
				// Вузол не підходить за атрибутами — пропускаємо все його піддерево.
				reader.Skip();
				continue;
			}

			var handler = new WorkHandler(attributes, criteria);
			ReadSubtree(reader, handler);
			if (handler.KeywordFound)
				works.Add(handler.Build());
		}

		return new SearchResult(rootAttributes, works);
	}

	public string DescribeQuery(SearchCriteria criteria)
	{
		var conditions = new List<string>();
		if (criteria.Student is not null)
			conditions.Add($"атрибут {XmlNames.Student} = \"{criteria.Student}\"");
		if (criteria.Group is not null)
			conditions.Add($"атрибут {XmlNames.Group} = \"{criteria.Group}\"");
		if (criteria.Keyword is not null)
			conditions.Add($"піддерево містить \"{criteria.Keyword}\"");

		string filter = conditions.Count == 0 ? "без умов" : string.Join(" ТА ", conditions);
		return $"XmlReader: потокове читання елементів {XmlNames.Work}; умова: {filter}";
	}

	/// <summary>Читає піддерево поточного елемента й передає події обробнику.</summary>
	private static void ReadSubtree(XmlReader reader, WorkHandler handler)
	{
		if (reader.IsEmptyElement)
		{
			reader.Read();
			return;
		}

		int depth = reader.Depth;
		while (reader.Read())
		{
			switch (reader.NodeType)
			{
				case XmlNodeType.Element:
					string name = reader.LocalName;
					bool isEmpty = reader.IsEmptyElement;
					handler.StartElement(name, ReadAttributes(reader));
					if (isEmpty)
						handler.EndElement(name);
					break;

				case XmlNodeType.Text:
				case XmlNodeType.CDATA:
				case XmlNodeType.SignificantWhitespace:
					handler.Characters(reader.Value);
					break;

				case XmlNodeType.EndElement:
					if (reader.Depth == depth)
					{
						reader.Read();
						return;
					}
					handler.EndElement(reader.LocalName);
					break;
			}
		}
	}

	private static List<XmlAttributeValue> ReadAttributes(XmlReader reader)
	{
		var attributes = new List<XmlAttributeValue>();
		if (reader.MoveToFirstAttribute())
		{
			do
			{
				attributes.Add(new XmlAttributeValue(reader.LocalName, reader.Value));
			}
			while (reader.MoveToNextAttribute());
			reader.MoveToElement();
		}
		return attributes;
	}

	private static string? Find(List<XmlAttributeValue> attributes, string name) =>
		attributes.Find(a => a.Name == name)?.Value;

	/// <summary>Обробник подій SAX: збирає запис Work з послідовності подій.</summary>
	private sealed class WorkHandler
	{
		private readonly List<XmlAttributeValue> workAttributes;
		private readonly SearchCriteria criteria;
		private readonly StringBuilder text = new();
		private readonly List<TaskItem> tasks = [];
		private readonly List<Material> materials = [];
		private readonly List<QuestionItem> questions = [];

		private List<XmlAttributeValue> currentAttributes = [];
		private Teacher? teacher;
		private string? topic;
		private Schedule? schedule;
		private string? questionDate;
		private string? questionText;
		private string? questionAnswer;

		public WorkHandler(List<XmlAttributeValue> workAttributes, SearchCriteria criteria)
		{
			this.workAttributes = workAttributes;
			this.criteria = criteria;
			KeywordFound = criteria.Keyword is null || workAttributes.Exists(a => criteria.KeywordIn(a.Value));
		}

		public bool KeywordFound { get; private set; }

		public void StartElement(string name, List<XmlAttributeValue> attributes)
		{
			text.Clear();
			currentAttributes = attributes;
			if (!KeywordFound)
				KeywordFound = attributes.Exists(a => criteria.KeywordIn(a.Value));

			switch (name)
			{
				case XmlNames.Schedule:
					schedule = new Schedule(Find(attributes, XmlNames.Day), Find(attributes, XmlNames.Time));
					break;
				case XmlNames.Question:
					questionDate = Find(attributes, XmlNames.Date);
					questionText = null;
					questionAnswer = null;
					break;
			}
		}

		public void Characters(string value) => text.Append(value);

		public void EndElement(string name)
		{
			string value = text.ToString().Trim();
			text.Clear();
			if (!KeywordFound)
				KeywordFound = criteria.KeywordIn(value);

			switch (name)
			{
				case XmlNames.Teacher:
					teacher = new Teacher(value, Find(currentAttributes, XmlNames.Position));
					break;
				case XmlNames.Topic:
					topic = value;
					break;
				case XmlNames.Task:
					tasks.Add(new TaskItem(value, Find(currentAttributes, XmlNames.Deadline), Find(currentAttributes, XmlNames.Status)));
					break;
				case XmlNames.Material:
					materials.Add(new Material(value, Find(currentAttributes, XmlNames.Type), Find(currentAttributes, XmlNames.Url)));
					break;
				case XmlNames.Text:
					questionText = value;
					break;
				case XmlNames.Answer:
					questionAnswer = value;
					break;
				case XmlNames.Question:
					questions.Add(new QuestionItem(questionText ?? string.Empty, questionDate, questionAnswer));
					break;
			}
		}

		public WorkRecord Build() => new()
		{
			Attributes = workAttributes,
			Teacher = teacher,
			Topic = topic,
			Schedule = schedule,
			Tasks = tasks,
			Materials = materials,
			Questions = questions
		};
	}
}
