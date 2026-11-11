using System.Windows;
using Jwmv.Gui.ViewModels;

namespace Jwmv.Gui;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
