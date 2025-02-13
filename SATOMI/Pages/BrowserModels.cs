/*
 * BrowserUI.cs
 * 
 * This file defines a simple file browser UI model using C# and ObservableCollection.
 * It includes:
 * - `BrowserUI`: A static class holding a directory list model.
 * - `DirListModel`: A model representing a collection of file and folder views.
 * - `FileFolderView`: A class representing a file or folder with properties such as location, selection status, swipe actions, and child presence detection.
 * 
 * The implementation supports folder navigation, swipe actions based on the presence of child directories, and basic UI properties.
 * 
 * Author: s.harada@HIBMS
 */
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
        //public string SwipeText { get; }
        public Color SwipeColor { get; }
        public bool IsSelected { get; set; }
        public bool IsFolder { get; }
        public bool IsParent { get; }
        public string Name => IsParent ? "..（Return to parent folder）" : Path.GetFileName(Location);
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
                //SwipeText = "     cannot perform a swipe action.";
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
                        //SwipeText = HasChildren ? "     open directory" : "     not found child directory";
                        SwipeColor = HasChildren ? Colors.DarkBlue : Colors.Gray;
                    }
                    catch (Exception)
                    {
                        HasChildren = false;
                        CanSwipe = false;
                        //SwipeText = "     cannot perform a swipe action.";
                        SwipeColor = Colors.Gray;
                    }
                }
                else
                {
                    HasChildren = false;
                    //SwipeText = "     cannot perform a swipe action.";
                    SwipeColor = Colors.Gray;
                }
            }
        }
    }
}
