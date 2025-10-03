using System;
using System.Threading.Tasks;
using System.Windows;
using IEC60870.TestHarness.ViewModels;

namespace IEC60870.TestHarness.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(Dispatcher);
        DataContext = _viewModel;
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_viewModel.DisconnectCommand.CanExecute(null))
        {
            try
            {
                await _viewModel.DisconnectCommand.ExecuteAsync(null);
            }
            catch
            {
            }
        }
    }
}
