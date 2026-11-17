using IndividualWork.Core.Model;

namespace IndividualWork.Core.Search;

/// <summary>
/// Параметри пошуку. Порожній параметр (null) у запиті не бере участі —
/// так запит формується динамічно з того, що заповнив користувач.
/// </summary>
public sealed class SearchCriteria
{
	public static SearchCriteria Empty { get; } = new(null, null, null);

	public SearchCriteria(string? student, string? group, string? keyword)
	{
		Student = Normalize(student);
		Group = Normalize(group);
		Keyword = Normalize(keyword);
	}

	public string? Student { get; }
	public string? Group { get; }
	public string? Keyword { get; }

	public bool IsEmpty => Student is null && Group is null && Keyword is null;

	public bool MatchesAttributes(string? student, string? group) =>
		(Student is null || Student == student) && (Group is null || Group == group);

	public bool KeywordIn(string? text) =>
		Keyword is not null
		&& text is not null
		&& text.Contains(Keyword, StringComparison.OrdinalIgnoreCase);

	/// <summary>Опис параметрів для протоколу подій.</summary>
	public string Describe()
	{
		var parts = new List<string>();
		if (Student is not null)
			parts.Add($"«{XmlNames.Student}» – {Student}");
		if (Group is not null)
			parts.Add($"«{XmlNames.Group}» – {Group}");
		if (Keyword is not null)
			parts.Add($"«keyword» – {Keyword}");
		return parts.Count == 0 ? "без обмежень" : string.Join(", ", parts);
	}

	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
