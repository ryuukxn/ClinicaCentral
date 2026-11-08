using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ClinicaCentral
{
    public partial class receipt_picker : Window
    {
        private readonly string? _issuedBy;

        public receipt_picker(string? issuedBy = null)
        {
            InitializeComponent();
            _issuedBy = issuedBy;
            LoadReceipts();
        }

        public static void ShowFor(Window owner, string? issuedBy = null)
        {
            receipt_picker picker = new receipt_picker(issuedBy);
            picker.Owner = owner;
            picker.ShowDialog();
        }

        private void LoadReceipts()
        {
            try
            {
                List<ReceiptRecord> receipts = new ReceiptRepository(new DatabaseConnection()).GetAll();
                ReceiptList.ItemsSource = receipts;
                EmptyText.Visibility = receipts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                if (receipts.Count > 0)
                    ReceiptList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not load the receipts. Make sure MySQL is running in Laragon.\n\n" + ex.Message,
                    "Database error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSelected()
        {
            if (!(ReceiptList.SelectedItem is ReceiptRecord receipt))
            {
                MessageBox.Show("Select a receipt first.", "Print Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_issuedBy))
                receipt.IssuedBy = _issuedBy;

            receipt_preview preview = new receipt_preview(receipt);
            preview.Owner = this;

            if (preview.ShowDialog() == true)
                Close();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenSelected();
        }

        private void ReceiptList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ReceiptList.SelectedItem != null)
                OpenSelected();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
