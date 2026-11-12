using System;
using System.Windows;
using System.Windows.Controls;

namespace ClinicaCentral
{
    public partial class mr_staff : Window
    {
        private const int PageSize = 5;

        private readonly string _displayName;
        private readonly MedicalRecordRepository _records;
        private readonly PagedList<MedicalRecordRow> _pages = new PagedList<MedicalRecordRow>(PageSize);
        private readonly System.Collections.Generic.List<MedicalRecordRow> _all =
            new System.Collections.Generic.List<MedicalRecordRow>();

        public mr_staff() : this("Doctor") { }

        public mr_staff(string displayName)
        {
            InitializeComponent();

            _records = new MedicalRecordRepository(new DatabaseConnection());
            _displayName = displayName;
            WelcomeText.Text = $"Welcome back, {displayName}!";
            Loaded += (s, e) => LoadRecords();
        }

        private void LoadRecords()
        {
            try
            {
                _all.Clear();
                _all.AddRange(_records.GetForDoctor(_displayName));
                _pages.Load(_all);
                ShowPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The medical records could not be loaded. Make sure MySQL is running in Laragon and that medical_records.sql and medical_records_details.sql were both run.\n\n" + ex.Message,
                    "Connection problem", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowPage()
        {
            var rows = _pages.GetCurrentPage();

            RecordList.ItemsSource = rows;
            EmptyText.Visibility = _all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BuildPager();
        }

        private void BuildPager()
        {
            PagerPanel.Children.Clear();

            if (_all.Count == 0) return;

            PagerPanel.Children.Add(MakePagerButton("\u2039", "prev", false, _pages.HasPrevious));

            int first = Math.Max(1, _pages.PageNumber - 2);
            int last = Math.Min(_pages.TotalPages, first + 4);
            first = Math.Max(1, last - 4);

            for (int page = first; page <= last; page++)
                PagerPanel.Children.Add(MakePagerButton(page.ToString(), page, page == _pages.PageNumber, true));

            PagerPanel.Children.Add(MakePagerButton("\u203A", "next", false, _pages.HasNext));
        }

        private Button MakePagerButton(string text, object tag, bool active, bool enabled)
        {
            Button button = new Button();
            button.Content = text;
            button.Tag = tag;
            button.IsEnabled = enabled;
            button.Style = (Style)FindResource(active ? "PageButtonActive" : "PageButton");
            button.Click += Page_Click;
            return button;
        }

        private void Page_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button)) return;

            if (button.Tag is int page)
                _pages.GoTo(page);
            else if (button.Tag as string == "prev")
                _pages.Previous();
            else if (button.Tag as string == "next")
                _pages.Next();

            ShowPage();
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is int id)) return;

            MedicalRecordRow? record = _all.Find(r => r.Id == id);
            if (record == null) return;

            mr_view window = new mr_view(record);
            window.Owner = this;
            window.ShowDialog();
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            string page = (sender as Button)?.Tag as string ?? "";

            if (page == "Medical Records") return;

            if (page == "Dashboard")
            {
                new dashboard_staff(_displayName).Open();
                Close();
                return;
            }

            if (page == "Patient Records")
            {
                new pr_staff(_displayName).Open();
                Close();
                return;
            }

            if (page == "Schedule")
            {
                new sc_staff(_displayName).Open();
                Close();
                return;
            }

        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show("Do you want to log out?", "Log out",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes) return;

            new login_form().Open();
            Close();
        }
    }
}
