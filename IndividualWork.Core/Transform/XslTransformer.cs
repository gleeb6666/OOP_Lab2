using System.Text;
using System.Xml;
using System.Xml.Xsl;

namespace IndividualWork.Core.Transform;

/// <summary>Перетворює XML-документ у HTML за таблицею стилів XSL.</summary>
public sealed class XslTransformer
{
	public string Transform(string xml, string xsl)
	{
		try
		{
			var transform = new XslCompiledTransform();
			using (var stylesheet = XmlReader.Create(new StringReader(xsl)))
				transform.Load(stylesheet);

			using var input = XmlReader.Create(new StringReader(xml));
			using var output = new MemoryStream();
			// Параметри виводу (метод html, кодування) беруться з xsl:output таблиці стилів.
			using (var writer = XmlWriter.Create(output, transform.OutputSettings))
				transform.Transform(input, writer);

			Encoding encoding = transform.OutputSettings?.Encoding ?? Encoding.UTF8;
			return encoding.GetString(output.ToArray()).TrimStart('﻿');
		}
		catch (XsltException ex)
		{
			throw new TransformationException($"Помилка в XSL-файлі: {ex.Message}", ex);
		}
		catch (XmlException ex)
		{
			throw new TransformationException($"XML або XSL має неправильний формат: {ex.Message}", ex);
		}
	}
}

public sealed class TransformationException : Exception
{
	public TransformationException(string message, Exception? innerException = null)
		: base(message, innerException)
	{
	}
}
