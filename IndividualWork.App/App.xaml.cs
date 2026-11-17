namespace IndividualWork.App;

public partial class App : Application
{
	private readonly MainPage mainPage;

	public App(MainPage mainPage)
	{
		InitializeComponent();
		this.mainPage = mainPage;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(mainPage)
		{
			Title = "ЛР2. Індивідуальна робота кафедри зі студентами",
			Width = 1280,
			Height = 800
		};
	}
}
