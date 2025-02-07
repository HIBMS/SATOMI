using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATOMI.Pages
{
    public static class BrowserUI
    {
        public static DirListModel DirListView = new DirListModel(); 
    }
    public class DirListModel 
    {
        public ObservableCollection<FileFolderView> Items { get; } = new();
    }
    public class FileFolderView
    {
        public string Location { get; }
        public string SwipeText { get; }
        public Color SwipeColor { get; }
        public bool IsSelected { get; set; }
        public bool IsFolder { get; }
        public bool IsParent { get; }
        public string Name => IsParent ? "..（Return to parent folder）" : Path.GetFileName(Location);
        public string Icon => IsParent ? "up_folder_icon.png" : (IsSelected ? "selected_folder_icon.png" : "folder_icon.png");
        public bool HasChildren { get; }
        public bool CanSwipe { get; }
        public bool Backbtn { get; }

        public FileFolderView(string location, bool isFolder, bool isSelected = false, bool isParent = false, bool back = false)
        {
            Location = location;
            IsFolder = isFolder;
            IsSelected = isSelected;
            IsParent = isParent;

            if (back)
            {
                Backbtn = true;
                CanSwipe = false;
                SwipeText = "     cannot perform a swipe action.";
                SwipeColor = Colors.Gray;
            }
            else
            {
                Backbtn = false;
                if (isFolder)
                {
                    try
                    {
                        HasChildren = Directory.GetDirectories(Location)
                                                            .Where(d => !d.Contains("System") &&
                                                            !d.EndsWith("Thumbs.db") &&
                                                            !d.Contains(".thumbnails"))
                                                            .Any();
                        CanSwipe = HasChildren;
                        SwipeText = HasChildren ? "     open directory" : "     not found child directory";
                        SwipeColor = HasChildren ? Colors.DarkBlue : Colors.Gray;
                    }
                    catch (Exception)
                    {
                        HasChildren = false;
                        CanSwipe = false;
                        SwipeText = "     cannot perform a swipe action.";
                        SwipeColor = Colors.Gray;
                    }
                }
                else
                {
                    HasChildren = false;
                    SwipeText = "     cannot perform a swipe action.";
                    SwipeColor = Colors.Gray;
                }
            }
        }
    }
}
