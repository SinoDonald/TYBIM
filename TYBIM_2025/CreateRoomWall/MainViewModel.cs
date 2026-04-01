using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace TYBIM_2025.CreateRoomWall
{
    // 基本節點類別 (包含名稱與勾選狀態)
    public class TreeNode : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; }

        public virtual bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // 房間節點
    public class RoomNode : TreeNode
    {
        public Room RoomElement { get; set; }
        public LevelNode ParentLevel { get; set; }

        public override bool IsSelected
        {
            get => base.IsSelected;
            set
            {
                base.IsSelected = value;
                // 當房間勾選狀態改變時，檢查是否要更新父節點(樓層)的狀態
                ParentLevel?.CheckStatus();
            }
        }
    }

    // 樓層節點
    public class LevelNode : TreeNode
    {
        public ObservableCollection<RoomNode> Rooms { get; set; } = new ObservableCollection<RoomNode>();

        public override bool IsSelected
        {
            get => base.IsSelected;
            set
            {
                base.IsSelected = value;
                // 當樓層被勾選/取消時，同步更新底下所有房間
                foreach (var room in Rooms)
                {
                    room.IsSelected = value;
                }
            }
        }

        // 檢查子節點狀態來更新自己 (例如房間全勾，樓層就自動打勾)
        public void CheckStatus()
        {
            bool allSelected = Rooms.All(r => r.IsSelected);
            if (base.IsSelected != allSelected)
            {
                base.IsSelected = allSelected;
            }
        }
    }

    // 整個視窗的主 ViewModel
    public class MainViewModel
    {
        public ObservableCollection<LevelNode> Levels { get; set; } = new ObservableCollection<LevelNode>();
        public ObservableCollection<WallType> WallTypes { get; set; } = new ObservableCollection<WallType>();
        public WallType SelectedWallType { get; set; }
    }
}