using System.Windows;

namespace TYBIM.CreateRoomWall
{
    /// <summary>
    /// RoomSelectionWindow.xaml 的互動邏輯
    /// </summary>
    public partial class RoomSelectionWindow : Window
    {
        public RoomSelectionWindow()
        {
            InitializeComponent();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
