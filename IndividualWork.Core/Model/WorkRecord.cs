namespace IndividualWork.Core.Model;

public sealed record XmlAttributeValue(string Name, string Value);

public sealed record Teacher(string Name, string? Position);

public sealed record Schedule(string? Day, string? Time);

public sealed record TaskItem(string Text, string? Deadline, string? Status);

public sealed record Material(string Title, string? Type, string? Url);

public sealed record QuestionItem(string Text, string? Date, string? Answer);

/// <summary>Один запис індивідуальної роботи — вузол Work вхідного XML.</summary>
public sealed class WorkRecord
{
	public IReadOnlyList<XmlAttributeValue> Attributes { get; init; } = [];
	public Teacher? Teacher { get; init; }
	public string? Topic { get; init; }
	public Schedule? Schedule { get; init; }
	public IReadOnlyList<TaskItem> Tasks { get; init; } = [];
	public IReadOnlyList<Material> Materials { get; init; } = [];
	public IReadOnlyList<QuestionItem> Questions { get; init; } = [];

	public string Student => GetAttribute(XmlNames.Student) ?? string.Empty;
	public string Group => GetAttribute(XmlNames.Group) ?? string.Empty;

	public string? GetAttribute(string name) =>
		Attributes.FirstOrDefault(a => a.Name == name)?.Value;
}

/// <summary>Результат пошуку: атрибути кореня та знайдені записи.</summary>
public sealed record SearchResult(
	IReadOnlyList<XmlAttributeValue> RootAttributes,
	IReadOnlyList<WorkRecord> Works);
