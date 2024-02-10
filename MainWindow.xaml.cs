using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PresentaceSeznamu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string PATH_IN = @".\Import\";
        const string PATH_OUT = @".\Export\";

        private string _sortPropName = "Points";
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        private int _sourceCount = 0;

        private string SortPropName
        {
            get
            {
                return _sortPropName;
            }
            set
            {
                _sortPropName = value;
                RefreshSortAndView();
            }
        }
        private ListSortDirection SortDirection
        {
            get
            {
                return _sortDirection;
            }
            set
            {
                _sortDirection = value;
                RefreshSortAndView();
            }
        }

        CollectionView? GetPlayersView()
        {
            var source = ListView_Players?.ItemsSource;
            if (source is null) return null;
            else return (CollectionView)CollectionViewSource.GetDefaultView(source);
        }

        public MainWindow()
        {
            InitializeComponent();

            Directory.CreateDirectory(PATH_IN);
            Directory.CreateDirectory(PATH_OUT);
        }

        bool Filter(object item)
        {
            if (item is null) return false;
            var firstNameFilter = TextBox_FirstName.Text;
            var lastNameFilter = TextBox_LastName.Text;
            var teamFilter = TextBox_Team.Text;
            if (!String.IsNullOrEmpty(firstNameFilter))
            {
                if ((item as Player).FirstName.IndexOf(firstNameFilter, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            if (!String.IsNullOrEmpty(lastNameFilter))
            {
                if ((item as Player).LastName.IndexOf(lastNameFilter, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            if (!String.IsNullOrEmpty(teamFilter))
            {
                if ((item as Player).Team.IndexOf(teamFilter, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            return true;
        }

        private void Button_Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Comma Separated Values|*.csv|All Files|*.*"; // Filter files by extension
            dialog.InitialDirectory = System.IO.Path.GetFullPath(PATH_IN);
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                // Open document
                string filename = dialog.FileName;
                var playerList = PlayerCSVParser.ParseFile(filename);

                if (playerList is null)
                {
                    MessageBox.Show("Tvůj CSV soubor s hráči nelze otevřít, nebo je ve špatném formátu.");
                }
                else
                {
                    _sourceCount = playerList.Count;
                    ListView_Players.ItemsSource = playerList;
                    var view = GetPlayersView();
                    view.Filter = Filter;
                    RefreshSortAndView();
                }
            }
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e)
        {
            RefreshSortAndView();
        }

        private void RefreshSortAndView()
        {
            var view = GetPlayersView();
            if (view is null) return;

            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new(SortPropName, SortDirection));
            view.Refresh();

            Label_ListCount.Content = $"{view.Count} / {_sourceCount}";
            if (_sourceCount > 0) { Label_ListCount.Visibility = Visibility.Visible; }
            else { Label_ListCount.Visibility = Visibility.Hidden; }
        }

        private void Button_ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TextBox_FirstName.Clear();
            TextBox_LastName.Clear();
            TextBox_Team.Clear();
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            var view = GetPlayersView();
            if (view is null) return;
            if (view.Count < 1) return;

            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Filter = "Comma Separated Values|*.csv|All Files|*.*"; // Filter files by extension
            dialog.InitialDirectory = System.IO.Path.GetFullPath(PATH_OUT);
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                StringBuilder sb = new();
                sb.AppendLine(Player.GetCSVHeader());
                foreach (Player item in view)
                {
                    sb.AppendLine(item.ToCSVLine());
                }
                File.WriteAllText(dialog.FileName, sb.ToString());
            }
        }

        private void Sort_FirstName(object sender, RoutedEventArgs e)
        {
            SortPropName = "FirstName";
        }
        private void Sort_LastName(object sender, RoutedEventArgs e)
        {
            SortPropName = "LastName";
        }
        private void Sort_Team(object sender, RoutedEventArgs e)
        {
            SortPropName = "Team";
        }
        private void Sort_GamesPlayed(object sender, RoutedEventArgs e)
        {
            SortPropName = "GamesPlayed";
        }
        private void Sort_Goals(object sender, RoutedEventArgs e)
        {
            SortPropName = "Goals";
        }
        private void Sort_Assists(object sender, RoutedEventArgs e)
        {
            SortPropName = "Assists";
        }
        private void Sort_Points(object sender, RoutedEventArgs e)
        {
            SortPropName = "Points";
        }
        private void Sort_PointsPerGame(object sender, RoutedEventArgs e)
        {
            SortPropName = "PointsPerGame";
        }
        private void Sort_Asc(object sender, RoutedEventArgs e)
        {
            SortDirection = ListSortDirection.Ascending;
        }
        private void Sort_Desc(object sender, RoutedEventArgs e)
        {
            SortDirection = ListSortDirection.Descending;
        }
    }

    //tightly coupled with Player class
    public static class PlayerCSVParser
    {
        public static List<Player>? ParseFile(string filename)
        {
            //requires that the column separator is ';' and the first line contains column names

            //if the file cannot be read, this will return null
            //if all the required column names aren't found on the first line, this will return null
            //if a data line doesn't have enough columns, or a value is of an incompatible type, that line will be ignored

            //note: the entire file is loaded at once

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filename);
            }
            catch
            {
                return null;
            }
            if (lines.Length < 1) return null;

            string[] columnNames = lines[0].Split(';');
            int[]? colNumbers = GetRelevantColumnNameIndices(columnNames);
            if (colNumbers is null) return null;

            List<Player> result = new();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                var player = PlayerFromCSVLine(line, colNumbers);
                if (player is not null) result.Add(player);
            }

            return result;
        }

        /// <summary>
        /// Returns the indices of `Player, Team, GP, G, A` in order. Returns null if any of them are missing.
        /// </summary>
        public static int[]? GetRelevantColumnNameIndices(string[] columnNames)
        {
            //initialize to -1 to recognize missing column names later
            int[] colNumbers = new int[5];
            for (int i = 0; i < colNumbers.Length; i++)
            {
                colNumbers[i] = -1;
            }

            for (int i = 0; i < columnNames.Length; i++)
            {
                if (columnNames[i].ToUpper().Equals("PLAYER")) colNumbers[0] = i;
                if (columnNames[i].ToUpper().Equals("TEAM")) colNumbers[1] = i;
                if (columnNames[i].ToUpper().Equals("GP")) colNumbers[2] = i;
                if (columnNames[i].ToUpper().Equals("G")) colNumbers[3] = i;
                if (columnNames[i].ToUpper().Equals("A")) colNumbers[4] = i;
            }

            foreach (var colnum in colNumbers)
            {
                if (colnum == -1) return null;
            }

            return colNumbers;
        }

        public static Player? PlayerFromCSVLine(string line, int[] colNums)
        {
            string[] cols = line.Split(';');

            int maxRequired = -1;
            foreach (var colNum in colNums)
            {
                if (colNum > maxRequired) maxRequired = colNum;
            }
            if (cols.Length - 1 < maxRequired) return null;

            string firstName, lastName;
            string fullName = cols[colNums[0]];
            string[] names = fullName.Split(' ');
            if (names.Length < 2)
            {
                firstName = "???";
                lastName = names[0];
            }
            else
            {
                firstName = names[0];
                lastName = names[1];
            }

            string team = cols[colNums[1]];

            int gp;
            if (!int.TryParse(cols[colNums[2]], out gp)) return null;

            int g;
            if (!int.TryParse(cols[colNums[3]], out g)) return null;

            int a;
            if (!int.TryParse(cols[colNums[4]], out a)) return null;

            return new Player(firstName, lastName, team, gp, g, a);
        }
    }

    public record class Player(string FirstName, string LastName, string Team, int GamesPlayed, int Goals, int Assists)
    {
        public static string GetCSVHeader()
        {
            return "Player;Team;GP;G;A;P;PPG";
        }

        public string ToCSVLine()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(FirstName);
            sb.Append(' ');
            sb.Append(LastName);
            sb.Append(';');
            sb.Append(Team.ToUpper());
            sb.Append(';');
            sb.Append(GamesPlayed);
            sb.Append(';');
            sb.Append(Goals);
            sb.Append(';');
            sb.Append(Assists);
            sb.Append(';');
            sb.Append(Points);
            sb.Append(';');
            sb.Append(PointsPerGame);
            return sb.ToString();
        }

        //setters do nothing but without them the binding crashes
        public int Points { get { return Goals + Assists; } set { } }

        public double PointsPerGame { get { return (double)Points / GamesPlayed; } set { } }
    }
}