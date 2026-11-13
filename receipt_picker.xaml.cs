using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ClinicaCentral
{
    public partial class receipt_picker : Window
    {
        private readonly User _user;

        public receipt_picker(User user)
        {
            InitializeComponent();
            _user = user ?? throw new ArgumentNullException(nameof(user));
            LoadReceipts();
        }

        public static void ShowFor(Window owner, User user)
        {
            receipt_picker picker = new receipt_picker(user);
            picker.Owner = owner;
            picker.ShowDialog();
        }

        private void LoadReceipts()
        {
            try
            {
                List<ReceiptRecord> receipts = new ReceiptRepository(new DatabaseConnection()).GetAll(_user);
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

            try
            {
                receipt_preview preview = new receipt_preview(receipt.Id, _user);
                preview.Owner = this;
                if (preview.ShowDialog() == true)
                    LoadReceipts();
            }
            catch (Exception ex)
            {
                MessageBox.Show("The receipt could not be opened.\n\n" + ex.Message,
                    "Receipt access", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
