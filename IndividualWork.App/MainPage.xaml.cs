using IndividualWork.Core.ViewModels;

namespace IndividualWork.App;

/// <summary>
/// Подання головного вікна. Обробників подій тут немає: уся поведінка —
/// у <see cref="MainViewModel"/>, з яким вікно пов'язане прив'язками даних.
/// </summary>
public partial class MainPage : ContentPage
{
	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
