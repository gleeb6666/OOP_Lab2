using System.Runtime.CompilerServices;
using IndividualWork.Core.Logging;
using IndividualWork.Core.Search;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace IndividualWork.Tests;

internal static class TestData
{
	public static string Xml { get; } = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "individual_work.xml"));
	public static string Xsl { get; } = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "individual_work.xsl"));

	public static string LogPath { get; } = Path.Combine(Path.GetTempPath(), $"lr2-tests-{Guid.NewGuid():N}.log");

	public static IXmlSearchStrategy[] Strategies =>
		[new SaxSearchStrategy(), new DomSearchStrategy(), new LinqSearchStrategy()];

	// Протокол-одинак під час тестів пише у тимчасовий файл, а не у справжній протокол програми.
	[ModuleInitializer]
	internal static void RedirectLog() => EventLogger.Instance.LogFilePath = LogPath;

	public static string NewTempFolder()
	{
		string folder = Path.Combine(Path.GetTempPath(), $"lr2-tests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(folder);
		return folder;
	}
}
