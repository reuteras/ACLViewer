using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;

namespace ACLViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<ACEInfo> aceEntries = new ObservableCollection<ACEInfo>();
        private string currentObjectPath;
        private bool isFolder;
        private ThemeManager themeManager;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize theme manager
            themeManager = new ThemeManager(this);
            themeManager.ApplyTheme();

            // Set up ACL ListView
            aclListView.ItemsSource = aceEntries;

            // Set up the tree view explorer
            InitializeExplorer();

            // Set up event handlers
            aclListView.SelectionChanged += AclListView_SelectionChanged;
        }

        private void InitializeExplorer()
        {
            // Set up root drives
            PopulateRootDrives();

            // Handle tree view selection
            fileTreeView.SelectedItemChanged += FileTreeView_SelectedItemChanged;
        }

        private void PopulateRootDrives()
        {
            var drives = DriveInfo.GetDrives();
            
            foreach (var drive in drives)
            {
                if (drive.IsReady)
                {
                    var item = CreateTreeViewItem(drive.Name, true);
                    
                    // Set appropriate icon based on drive type
                    string iconKey;
                    switch (drive.DriveType)
                    {
                        case DriveType.Fixed:
                            iconKey = "HardDrive";
                            break;
                        case DriveType.Removable:
                            iconKey = "Removable";
                            break;
                        case DriveType.CDRom:
                            iconKey = "CDROM";
                            break;
                        case DriveType.Network:
                            iconKey = "Network";
                            break;
                        default:
                            iconKey = "Folder";
                            break;
                    }
                    
                    item.Tag = drive.RootDirectory.FullName;
                    
                    // Add dummy node to allow expansion
                    AddDummyNode(item);
                    
                    fileTreeView.Items.Add(item);
                }
            }
        }

        private TreeViewItem CreateTreeViewItem(string text, bool isDirectory)
        {
            var item = new TreeViewItem
            {
                Header = text,
                IsExpanded = false
            };

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Add appropriate icon
            var icon = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0),
                Source = new BitmapImage(new Uri($"pack://application:,,,/Images/{(isDirectory ? "folder" : "file")}.png", UriKind.Absolute))
            };
            
            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(new TextBlock { Text = text });
            
            item.Header = stackPanel;
            return item;
        }

        private void AddDummyNode(TreeViewItem item)
        {
            item.Items.Add(new TreeViewItem { Header = "Loading..." });
        }

        private void ExpandDirectoryNode(TreeViewItem item)
        {
            if (item == null) return;

            // Clear the dummy node or any existing children
            item.Items.Clear();

            if (item.Tag is string path)
            {
                try
                {
                    // Add directories
                    foreach (var directory in Directory.GetDirectories(path).OrderBy(d => d))
                    {
                        var directoryInfo = new DirectoryInfo(directory);
                        var subItem = CreateTreeViewItem(directoryInfo.Name, true);
                        subItem.Tag = directory;
                        
                        try
                        {
                            // Check if the directory has children
                            if (Directory.GetDirectories(directory).Length > 0 || Directory.GetFiles(directory).Length > 0)
                            {
                                AddDummyNode(subItem);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Add a dummy node anyway if we can't access it
                            AddDummyNode(subItem);
                            subItem.Foreground = Brushes.Gray;
                        }
                        
                        item.Items.Add(subItem);
                    }

                    // Add files only to the parent item's filesystem view (not to tree)
                    // This would be handled in a separate file list view
                }
                catch (UnauthorizedAccessException)
                {
                    var errorItem = new TreeViewItem
                    {
                        Header = "Access Denied",
                        Foreground = Brushes.Red
                    };
                    item.Items.Add(errorItem);
                }
                catch (Exception ex)
                {
                    var errorItem = new TreeViewItem
                    {
                        Header = $"Error: {ex.Message}",
                        Foreground = Brushes.Red
                    };
                    item.Items.Add(errorItem);
                }
            }
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (fileTreeView.SelectedItem is TreeViewItem item && item.Tag is string path)
            {
                currentObjectPath = path;
                
                // Clear ACL view
                aceEntries.Clear();
                
                // Determine if item is folder or file
                isFolder = Directory.Exists(path);
                
                // Get and display ACL information
                GetSecurityInfo(path);
                
                // Update object name and owner information
                UpdateObjectInfo(path);
                
                // If this is the first time expanding the node, populate its children
                if (item.Items.Count == 1 && item.Items[0] is TreeViewItem dummyItem && dummyItem.Header.ToString() == "Loading...")
                {
                    ExpandDirectoryNode(item);
                }
            }
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                ExpandDirectoryNode(item);
            }
        }

        private void UpdateObjectInfo(string path)
        {
            try
            {
                // Update the object name
                objectPathTextBlock.Text = path;

                // Get the owner information
                var fileInfo = new FileInfo(path);
                var security = fileInfo.GetAccessControl();
                var ownerSid = security.GetOwner(typeof(SecurityIdentifier));
                var owner = ownerSid.Translate(typeof(NTAccount)).Value;
                
                ownerTextBlock.Text = owner;
            }
            catch (Exception ex)
            {
                ownerTextBlock.Text = $"Error: {ex.Message}";
            }
        }

        private void GetSecurityInfo(string path)
        {
            try
            {
                if (isFolder)
                {
                    var dirInfo = new DirectoryInfo(path);
                    var security = dirInfo.GetAccessControl();
                    var rules = security.GetAccessRules(true, true, typeof(NTAccount));
                    
                    foreach (FileSystemAccessRule rule in rules)
                    {
                        var aceInfo = new ACEInfo
                        {
                            Type = rule.AccessControlType.ToString(),
                            Principal = rule.IdentityReference.Value,
                            Access = GetAccessRightDescription(rule.FileSystemRights),
                            Inherited = rule.IsInherited.ToString(),
                            AppliesTo = rule.InheritanceFlags.ToString(),
                            Propagate = (rule.PropagationFlags == PropagationFlags.None).ToString(),
                            AccessMask = ((int)rule.FileSystemRights).ToString("X8")
                        };
                        
                        aceEntries.Add(aceInfo);
                    }
                }
                else
                {
                    var fileInfo = new FileInfo(path);
                    var security = fileInfo.GetAccessControl();
                    var rules = security.GetAccessRules(true, true, typeof(NTAccount));
                    
                    foreach (FileSystemAccessRule rule in rules)
                    {
                        var aceInfo = new ACEInfo
                        {
                            Type = rule.AccessControlType.ToString(),
                            Principal = rule.IdentityReference.Value,
                            Access = GetAccessRightDescription(rule.FileSystemRights),
                            Inherited = rule.IsInherited.ToString(),
                            AppliesTo = "This file only",
                            Propagate = "-",
                            AccessMask = ((int)rule.FileSystemRights).ToString("X8")
                        };
                        
                        aceEntries.Add(aceInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving security information: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetAccessRightDescription(FileSystemRights rights)
        {
            // Map common combinations to standard permission sets
            if ((rights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
                return "Full Control";
            
            if ((rights & (FileSystemRights.ReadData | FileSystemRights.ExecuteFile | FileSystemRights.ReadAttributes | FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadPermissions)) == 
                (FileSystemRights.ReadData | FileSystemRights.ExecuteFile | FileSystemRights.ReadAttributes | FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadPermissions))
                return "Read & Execute";
                
            if ((rights & (FileSystemRights.ReadData | FileSystemRights.WriteData | FileSystemRights.ExecuteFile | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.Delete)) ==
                (FileSystemRights.ReadData | FileSystemRights.WriteData | FileSystemRights.ExecuteFile | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.Delete))
                return "Modify";
                
            if ((rights & (FileSystemRights.ReadData | FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadAttributes | FileSystemRights.ReadPermissions)) ==
                (FileSystemRights.ReadData | FileSystemRights.ReadExtendedAttributes | FileSystemRights.ReadAttributes | FileSystemRights.ReadPermissions))
                return "Read";
                
            if ((rights & FileSystemRights.WriteData) == FileSystemRights.WriteData)
                return "Write";
                
            // If no standard combination matches, return "Special"
            return "Special";
        }

        private void AclListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (aclListView.SelectedItem is ACEInfo selectedAce)
            {
                // Update the ACE details panel
                aceTypeTextBlock.Text = selectedAce.Type;
                acePrincipalTextBlock.Text = selectedAce.Principal;
                aceAppliesToTextBlock.Text = selectedAce.AppliesTo;
                
                // Display permissions in the checkboxes
                DisplayPermissions(selectedAce);
                
                // Show the ACE details panel
                aceDetailsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                // Hide the ACE details panel if nothing is selected
                aceDetailsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void DisplayPermissions(ACEInfo ace)
        {
            // Parse the access mask
            int accessMask = int.Parse(ace.AccessMask, NumberStyles.HexNumber);
            
            // Clear all checkboxes first
            ClearAllPermissionCheckboxes();
            
            // Set checkboxes based on permissions
            if ((accessMask & 0x1F01FF) == 0x1F01FF) // FILE_ALL_ACCESS
                fullControlCheckBox.IsChecked = true;
                
            if ((accessMask & 0x20) == 0x20) // FILE_EXECUTE
                executeCheckBox.IsChecked = true;
                
            if ((accessMask & 0x1) == 0x1) // FILE_READ_DATA
                readDataCheckBox.IsChecked = true;
                
            if ((accessMask & 0x80) == 0x80) // FILE_READ_ATTRIBUTES
                readAttributesCheckBox.IsChecked = true;
                
            if ((accessMask & 0x8) == 0x8) // FILE_READ_EA
                readEACheckBox.IsChecked = true;
                
            if ((accessMask & 0x2) == 0x2) // FILE_WRITE_DATA
                writeDataCheckBox.IsChecked = true;
                
            if ((accessMask & 0x4) == 0x4) // FILE_APPEND_DATA
                appendDataCheckBox.IsChecked = true;
                
            if ((accessMask & 0x100) == 0x100) // FILE_WRITE_ATTRIBUTES
                writeAttributesCheckBox.IsChecked = true;
                
            if ((accessMask & 0x10) == 0x10) // FILE_WRITE_EA
                writeEACheckBox.IsChecked = true;
                
            if ((accessMask & 0x40) == 0x40) // FILE_DELETE_CHILD
                deleteChildCheckBox.IsChecked = true;
                
            if ((accessMask & 0x10000) == 0x10000) // DELETE
                deleteCheckBox.IsChecked = true;
                
            if ((accessMask & 0x20000) == 0x20000) // READ_CONTROL
                readPermissionsCheckBox.IsChecked = true;
                
            if ((accessMask & 0x40000) == 0x40000) // WRITE_DAC
                changePermissionsCheckBox.IsChecked = true;
                
            if ((accessMask & 0x80000) == 0x80000) // WRITE_OWNER
                takeOwnershipCheckBox.IsChecked = true;
                
            // Set the propagate checkbox
            propagateCheckBox.IsChecked = ace.Propagate == "True";
            
            // Generic rights
            if ((accessMask & 0x80000000) == 0x80000000) // GENERIC_READ
            {
                readPermissionsCheckBox.IsChecked = true;
                readDataCheckBox.IsChecked = true;
                readAttributesCheckBox.IsChecked = true;
                readEACheckBox.IsChecked = true;
            }
            
            if ((accessMask & 0x40000000) == 0x40000000) // GENERIC_WRITE
            {
                readPermissionsCheckBox.IsChecked = true;
                writeDataCheckBox.IsChecked = true;
                writeAttributesCheckBox.IsChecked = true;
                writeEACheckBox.IsChecked = true;
                appendDataCheckBox.IsChecked = true;
            }
            
            if ((accessMask & 0x20000000) == 0x20000000) // GENERIC_EXECUTE
            {
                readPermissionsCheckBox.IsChecked = true;
                readAttributesCheckBox.IsChecked = true;
                executeCheckBox.IsChecked = true;
            }
        }

        private void ClearAllPermissionCheckboxes()
        {
            fullControlCheckBox.IsChecked = false;
            executeCheckBox.IsChecked = false;
            readDataCheckBox.IsChecked = false;
            readAttributesCheckBox.IsChecked = false;
            readEACheckBox.IsChecked = false;
            writeDataCheckBox.IsChecked = false;
            appendDataCheckBox.IsChecked = false;
            writeAttributesCheckBox.IsChecked = false;
            writeEACheckBox.IsChecked = false;
            deleteChildCheckBox.IsChecked = false;
            deleteCheckBox.IsChecked = false;
            readPermissionsCheckBox.IsChecked = false;
            changePermissionsCheckBox.IsChecked = false;
            takeOwnershipCheckBox.IsChecked = false;
            propagateCheckBox.IsChecked = false;
        }
    }

    public class ACEInfo
    {
        public string Type { get; set; }
        public string Principal { get; set; }
        public string Access { get; set; }
        public string Inherited { get; set; }
        public string AppliesTo { get; set; }
        public string Propagate { get; set; }
        public string AccessMask { get; set; }
    }

    public class ThemeManager
    {
        private Window window;
        private bool isDarkMode;

        [DllImport("UXTheme.dll", SetLastError = true)]
        private static extern bool ShouldAppsUseDarkMode();

        public ThemeManager(Window window)
        {
            this.window = window;
            this.isDarkMode = IsDarkModeEnabled();
        }

        private bool IsDarkModeEnabled()
        {
            try
            {
                // Try to use the native API first
                return ShouldAppsUseDarkMode();
            }
            catch
            {
                // Fallback method: check registry
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        if (key != null)
                        {
                            var value = key.GetValue("AppsUseLightTheme");
                            return value != null && (int)value == 0;
                        }
                    }
                }
                catch
                {
                    // If all else fails, default to light mode
                    return false;
                }
            }
            
            return false;
        }

        public void ApplyTheme()
        {
            var resources = window.Resources;
            
            if (isDarkMode)
            {
                // Dark theme colors
                resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                resources["ForegroundColor"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                resources["PanelColor"] = new SolidColorBrush(Color.FromRgb(48, 48, 48));
                resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                resources["SeparatorColor"] = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            }
            else
            {
                // Light theme colors
                resources["BackgroundColor"] = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                resources["ForegroundColor"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                resources["PanelColor"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                resources["SeparatorColor"] = new SolidColorBrush(Color.FromRgb(230, 230, 230));
            }
        }
    }
}
