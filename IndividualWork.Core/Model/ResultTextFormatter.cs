using System.Text;

namespace IndividualWork.Core.Model;

/// <summary>Готує текст результатів пошуку для поля виводу.</summary>
public static class ResultTextFormatter
{
	public static string Format(SearchResult result)
	{
		var text = new StringBuilder();
		text.AppendLine($"Знайдено записів: {result.Works.Count}");

		foreach (WorkRecord work in result.Works)
		{
			text.AppendLine();
			text.AppendLine($"■ {work.Student}, група {work.Group}");
			text.AppendLine($"  Атрибути ({work.Attributes.Count}): " +
				string.Join("; ", work.Attributes.Select(a => $"{a.Name}={a.Value}")));

			if (work.Teacher is not null)
				text.AppendLine($"  Викладач: {work.Teacher.Name}{InBrackets(work.Teacher.Position)}");
			if (work.Topic is not null)
				text.AppendLine($"  Тема: {work.Topic}");
			if (work.Schedule is not null)
				text.AppendLine($"  Графік: {work.Schedule.Day} {work.Schedule.Time}".TrimEnd());

			if (work.Tasks.Count > 0)
			{
				text.AppendLine("  Завдання:");
				foreach (TaskItem task in work.Tasks)
					text.AppendLine($"    • {task.Text} — до {task.Deadline ?? "?"}, {task.Status ?? "статус не вказано"}");
			}

			if (work.Materials.Count > 0)
			{
				text.AppendLine("  Допоміжні матеріали:");
				foreach (Material material in work.Materials)
					text.AppendLine($"    • {material.Title}{InBrackets(material.Type)}{(material.Url is null ? "" : " — " + material.Url)}");
			}

			if (work.Questions.Count > 0)
			{
				text.AppendLine("  Запитання та відповіді:");
				foreach (QuestionItem question in work.Questions)
				{
					text.AppendLine($"    • {(question.Date is null ? "" : question.Date + ": ")}{question.Text}");
					text.AppendLine($"      Відповідь: {question.Answer ?? "ще немає"}");
				}
			}
		}

		return text.ToString().TrimEnd();
	}

	private static string InBrackets(string? value) => value is null ? string.Empty : $" ({value})";
}
