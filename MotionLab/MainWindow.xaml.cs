using System.Windows;
using MotionLab.ViewModels;

namespace MotionLab
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}