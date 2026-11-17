using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace IndividualWork.Core.Model;

/// <summary>Збирає з результату пошуку коректний XML-документ тієї ж структури, що й вхідний.</summary>
public static class FragmentXmlBuilder
{
	public static XDocument Build(SearchResult fragment)
	{
		var root = new XElement(XmlNames.Root, ToAttributes(fragment.RootAttributes));
		foreach (WorkRecord work in fragment.Works)
			root.Add(ToElement(work));
		return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
	}

	public static string BuildString(SearchResult fragment)
	{
		using var stream = new MemoryStream();
		Write(fragment, stream);
		return Encoding.UTF8.GetString(stream.ToArray());
	}

	public static void Write(SearchResult fragment, Stream stream)
	{
		var settings = new XmlWriterSettings
		{
			Indent = true,
			Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			CloseOutput = false
		};
		using var writer = XmlWriter.Create(stream, settings);
		Build(fragment).Save(writer);
	}

	private static XElement ToElement(WorkRecord work)
	{
		var element = new XElement(XmlNames.Work, ToAttributes(work.Attributes));

		if (work.Teacher is not null)
			element.Add(new XElement(XmlNames.Teacher, Optional(XmlNames.Position, work.Teacher.Position), work.Teacher.Name));
		if (work.Topic is not null)
			element.Add(new XElement(XmlNames.Topic, work.Topic));
		if (work.Schedule is not null)
			element.Add(new XElement(XmlNames.Schedule,
				Optional(XmlNames.Day, work.Schedule.Day),
				Optional(XmlNames.Time, work.Schedule.Time)));

		element.Add(new XElement(XmlNames.Tasks, work.Tasks.Select(t =>
			new XElement(XmlNames.Task,
				Optional(XmlNames.Deadline, t.Deadline),
				Optional(XmlNames.Status, t.Status),
				t.Text))));

		element.Add(new XElement(XmlNames.Materials, work.Materials.Select(m =>
			new XElement(XmlNames.Material,
				Optional(XmlNames.Type, m.Type),
				Optional(XmlNames.Url, m.Url),
				m.Title))));

		element.Add(new XElement(XmlNames.Questions, work.Questions.Select(q =>
			new XElement(XmlNames.Question,
				Optional(XmlNames.Date, q.Date),
				new XElement(XmlNames.Text, q.Text),
				q.Answer is null ? null : new XElement(XmlNames.Answer, q.Answer)))));

		return element;
	}

	private static IEnumerable<XAttribute> ToAttributes(IEnumerable<XmlAttributeValue> attributes) =>
		attributes.Select(a => new XAttribute(a.Name, a.Value));

	private static XAttribute? Optional(string name, string? value) =>
		value is null ? null : new XAttribute(name, value);
}
