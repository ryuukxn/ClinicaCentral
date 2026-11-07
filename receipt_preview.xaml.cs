using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class receipt_preview : Window
    {
        private readonly ReceiptRecord _receipt;

        public bool WasPrinted { get; private set; }

        public receipt_preview(ReceiptRecord receipt)
        {
            InitializeComponent();

            _receipt = receipt;
            DataContext = _receipt;
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog dialog = new PrintDialog();

            if (dialog.ShowDialog() != true) return;

            dialog.PrintVisual(ReceiptCard, "Receipt " + _receipt.Code);
            WasPrinted = true;
            DialogResult = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
