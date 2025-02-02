﻿using Files.Enums;
using Files.EventArguments;
using Files.Filesystem;
using Files.Helpers;
using Files.Helpers.XamlHelpers;
using Files.Interacts;
using Files.UserControls.Selection;
using Files.ViewModels;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Uwp.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Files.Views.LayoutModes
{
    public sealed partial class DetailsLayoutBrowser : BaseLayout
    {
        public string oldItemName;
        
        private ColumnsViewModel columnsViewModel = new ColumnsViewModel();

        public ColumnsViewModel ColumnsViewModel
        {
            get => columnsViewModel;
            set
            {
                if (value != columnsViewModel)
                {
                    columnsViewModel = value;
                    NotifyPropertyChanged(nameof(ColumnsViewModel));
                }
            }
        }

        private RelayCommand<string> UpdateSortOptionsCommand { get; set; }

        private DispatcherQueueTimer renameDoubleClickTimer;
        private DispatcherQueueTimer renameDoubleClickTimeoutTimer;

        public DetailsLayoutBrowser() : base()
        {
            InitializeComponent();
            this.DataContext = this;

            var selectionRectangle = RectangleSelection.Create(FileList, SelectionRectangle, FileList_SelectionChanged);
            selectionRectangle.SelectionEnded += SelectionRectangle_SelectionEnded;
            renameDoubleClickTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            renameDoubleClickTimeoutTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        }

        protected override void HookEvents()
        {
            UnhookEvents();
            ItemManipulationModel.FocusFileListInvoked += ItemManipulationModel_FocusFileListInvoked;
            ItemManipulationModel.SelectAllItemsInvoked += ItemManipulationModel_SelectAllItemsInvoked;
            ItemManipulationModel.ClearSelectionInvoked += ItemManipulationModel_ClearSelectionInvoked;
            ItemManipulationModel.InvertSelectionInvoked += ItemManipulationModel_InvertSelectionInvoked;
            ItemManipulationModel.AddSelectedItemInvoked += ItemManipulationModel_AddSelectedItemInvoked;
            ItemManipulationModel.RemoveSelectedItemInvoked += ItemManipulationModel_RemoveSelectedItemInvoked;
            ItemManipulationModel.FocusSelectedItemsInvoked += ItemManipulationModel_FocusSelectedItemsInvoked;
            ItemManipulationModel.StartRenameItemInvoked += ItemManipulationModel_StartRenameItemInvoked;
            ItemManipulationModel.ScrollIntoViewInvoked += ItemManipulationModel_ScrollIntoViewInvoked;
        }

        private void ItemManipulationModel_ScrollIntoViewInvoked(object sender, ListedItem e)
        {
            FileList.ScrollIntoView(e);
        }

        private void ItemManipulationModel_StartRenameItemInvoked(object sender, EventArgs e)
        {
            StartRenameItem();
        }

        private void ItemManipulationModel_FocusSelectedItemsInvoked(object sender, EventArgs e)
        {
            if (SelectedItems.Any())
            {
                FileList.ScrollIntoView(SelectedItems.Last());
            }
        }

        private void ItemManipulationModel_AddSelectedItemInvoked(object sender, ListedItem e)
        {
            if (FileList?.Items.Contains(e) ?? false)
            {
                FileList.SelectedItems.Add(e);
            }
        }

        private void ItemManipulationModel_RemoveSelectedItemInvoked(object sender, ListedItem e)
        {
            if (FileList?.Items.Contains(e) ?? false)
            {
                FileList.SelectedItems.Remove(e);
            }
        }

        private void ItemManipulationModel_InvertSelectionInvoked(object sender, EventArgs e)
        {
            if (SelectedItems.Count < GetAllItems().Cast<ListedItem>().Count() / 2)
            {
                var oldSelectedItems = SelectedItems.ToList();
                ItemManipulationModel.SelectAllItems();
                ItemManipulationModel.RemoveSelectedItems(oldSelectedItems);
            }
            else
            {
                List<ListedItem> newSelectedItems = GetAllItems()
                    .Cast<ListedItem>()
                    .Except(SelectedItems)
                    .ToList();

                ItemManipulationModel.SetSelectedItems(newSelectedItems);
            }
        }

        private void ItemManipulationModel_ClearSelectionInvoked(object sender, EventArgs e)
        {
            FileList.SelectedItems.Clear();
        }

        private void ItemManipulationModel_SelectAllItemsInvoked(object sender, EventArgs e)
        {
            FileList.SelectAll();
        }

        private void ItemManipulationModel_FocusFileListInvoked(object sender, EventArgs e)
        {
            FileList.Focus(FocusState.Programmatic);
        }

        protected override void UnhookEvents()
        {
            if (ItemManipulationModel != null)
            {
                ItemManipulationModel.FocusFileListInvoked -= ItemManipulationModel_FocusFileListInvoked;
                ItemManipulationModel.SelectAllItemsInvoked -= ItemManipulationModel_SelectAllItemsInvoked;
                ItemManipulationModel.ClearSelectionInvoked -= ItemManipulationModel_ClearSelectionInvoked;
                ItemManipulationModel.InvertSelectionInvoked -= ItemManipulationModel_InvertSelectionInvoked;
                ItemManipulationModel.AddSelectedItemInvoked -= ItemManipulationModel_AddSelectedItemInvoked;
                ItemManipulationModel.RemoveSelectedItemInvoked -= ItemManipulationModel_RemoveSelectedItemInvoked;
                ItemManipulationModel.FocusSelectedItemsInvoked -= ItemManipulationModel_FocusSelectedItemsInvoked;
                ItemManipulationModel.StartRenameItemInvoked -= ItemManipulationModel_StartRenameItemInvoked;
                ItemManipulationModel.ScrollIntoViewInvoked -= ItemManipulationModel_ScrollIntoViewInvoked;
            }
        }

        protected override void InitializeCommandsViewModel()
        {
            CommandsViewModel = new BaseLayoutCommandsViewModel(new BaseLayoutCommandImplementationModel(ParentShellPageInstance, ItemManipulationModel));
        }

        protected override void OnNavigatedTo(NavigationEventArgs eventArgs)
        {
            base.OnNavigatedTo(eventArgs);
            if (ParentShellPageInstance.InstanceViewModel?.FolderSettings.ColumnsViewModel != null)
            {
                ColumnsViewModel = ParentShellPageInstance.InstanceViewModel.FolderSettings.ColumnsViewModel;
            }

            currentIconSize = FolderSettings.GetIconSize();
            FolderSettings.LayoutModeChangeRequested -= FolderSettings_LayoutModeChangeRequested;
            FolderSettings.LayoutModeChangeRequested += FolderSettings_LayoutModeChangeRequested;
            FolderSettings.GridViewSizeChangeRequested -= FolderSettings_GridViewSizeChangeRequested;
            FolderSettings.GridViewSizeChangeRequested += FolderSettings_GridViewSizeChangeRequested;
            ParentShellPageInstance.FilesystemViewModel.PageTypeUpdated -= FilesystemViewModel_PageTypeUpdated;
            ParentShellPageInstance.FilesystemViewModel.PageTypeUpdated += FilesystemViewModel_PageTypeUpdated;

            ColumnsViewModel.TotalWidth = Math.Max(800, RootGrid.Width);

            var parameters = (NavigationArguments)eventArgs.Parameter;
            if (parameters.IsLayoutSwitch)
            {
                ReloadItemIcons();
            }

            UpdateSortOptionsCommand = new RelayCommand<string>(x =>
            {
                var val = Enum.Parse<SortOption>(x);
                if (ParentShellPageInstance.FilesystemViewModel.folderSettings.DirectorySortOption == val)
                {
                    ParentShellPageInstance.FilesystemViewModel.folderSettings.DirectorySortDirection = (SortDirection)(((int)ParentShellPageInstance.FilesystemViewModel.folderSettings.DirectorySortDirection + 1) % 2);
                }
                else
                {
                    ParentShellPageInstance.FilesystemViewModel.folderSettings.DirectorySortOption = val;
                    ParentShellPageInstance.FilesystemViewModel.folderSettings.DirectorySortDirection = SortDirection.Ascending;
                }
            });

            FilesystemViewModel_PageTypeUpdated(null, new PageTypeUpdatedEventArgs()
            {
                IsTypeCloudDrive = InstanceViewModel.IsPageTypeCloudDrive,
                IsTypeRecycleBin = InstanceViewModel.IsPageTypeRecycleBin
            });
        }

        private void FilesystemViewModel_PageTypeUpdated(object sender, PageTypeUpdatedEventArgs e)
        {
            // This code updates which colulmns are hidden and which ones are shwn
            if (!e.IsTypeRecycleBin)
            {
                ColumnsViewModel.DateDeletedColumn.Hide();
                ColumnsViewModel.OriginalPathColumn.Hide();
            }
            else
            {
                ColumnsViewModel.OriginalPathColumn.Show();
                ColumnsViewModel.DateDeletedColumn.Show();
            }

            if (!e.IsTypeCloudDrive)
            {
                ColumnsViewModel.StatusColumn.Hide();
            }
            else
            {
                ColumnsViewModel.StatusColumn.Show();
            }

            ColumnsViewModel.TotalWidth = Math.Max(RootGrid.ActualWidth, Column1.ActualWidth + Column2.ActualWidth + Column3.ActualWidth + Column4.ActualWidth + Column5.ActualWidth
        + Column6.ActualWidth + Column7.ActualWidth);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            var selectorItems = new List<SelectorItem>();
            DependencyObjectHelpers.FindChildren<SelectorItem>(selectorItems, FileList);
            foreach (SelectorItem gvi in selectorItems)
            {
                base.UninitializeDrag(gvi);
                gvi.PointerPressed -= FileListGridItem_PointerPressed;
            }
            selectorItems.Clear();
            base.OnNavigatingFrom(e);
            FolderSettings.LayoutModeChangeRequested -= FolderSettings_LayoutModeChangeRequested;
            FolderSettings.GridViewSizeChangeRequested -= FolderSettings_GridViewSizeChangeRequested;
            ParentShellPageInstance.FilesystemViewModel.PageTypeUpdated -= FilesystemViewModel_PageTypeUpdated;
        }

        private async void SelectionRectangle_SelectionEnded(object sender, EventArgs e)
        {
            await Task.Delay(200);
            FileList.Focus(FocusState.Programmatic);
        }

        private void FolderSettings_LayoutModeChangeRequested(object sender, LayoutModeEventArgs e)
        {
        }

        private void StackPanel_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var parentContainer = DependencyObjectHelpers.FindParent<ListViewItem>(e.OriginalSource as DependencyObject);
            if (!parentContainer.IsSelected)
            {
                ItemManipulationModel.SetSelectedItem(FileList.ItemFromContainer(parentContainer) as ListedItem);
            }
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItems = FileList.SelectedItems.Cast<ListedItem>().Where(x => x != null).ToList();
        }

        private ListedItem renamingItem;

        private void StartRenameItem()
        {
            renamingItem = SelectedItem;
            int extensionLength = renamingItem.FileExtension?.Length ?? 0;
            ListViewItem gridViewItem = FileList.ContainerFromItem(renamingItem) as ListViewItem;
            TextBox textBox = null;

            TextBlock textBlock = gridViewItem.FindDescendant("ItemName") as TextBlock;
            textBox = gridViewItem.FindDescendant("ItemNameTextBox") as TextBox;
            //TextBlock textBlock = (gridViewItem.ContentTemplateRoot as Grid).FindName("ItemName") as TextBlock;
            //textBox = (gridViewItem.ContentTemplateRoot as Grid).FindName("TileViewTextBoxItemName") as TextBox;
            textBox.Text = textBlock.Text;
            oldItemName = textBlock.Text;
            textBlock.Visibility = Visibility.Collapsed;
            textBox.Visibility = Visibility.Visible;

            textBox.Focus(FocusState.Pointer);
            textBox.LostFocus += RenameTextBox_LostFocus;
            textBox.KeyDown += RenameTextBox_KeyDown;

            int selectedTextLength = SelectedItem.ItemName.Length;
            if (!SelectedItem.IsShortcutItem && App.AppSettings.ShowFileExtensions)
            {
                selectedTextLength -= extensionLength;
            }
            textBox.Select(0, selectedTextLength);
            IsRenamingItem = true;
        }

        private void ListViewTextBoxItemName_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;

            if (FilesystemHelpers.ContainsRestrictedCharacters(textBox.Text))
            {
                FileNameTeachingTip.Visibility = Visibility.Visible;
                FileNameTeachingTip.IsOpen = true;
            }
            else
            {
                FileNameTeachingTip.IsOpen = false;
                FileNameTeachingTip.Visibility = Visibility.Collapsed;
            }
        }

        private void RenameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                TextBox textBox = sender as TextBox;
                textBox.LostFocus -= RenameTextBox_LostFocus;
                textBox.Text = oldItemName;
                EndRename(textBox);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                TextBox textBox = sender as TextBox;
                textBox.LostFocus -= RenameTextBox_LostFocus;
                CommitRename(textBox);
                e.Handled = true;
            }
        }

        private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // This check allows the user to use the text box context menu without ending the rename
            if (!(FocusManager.GetFocusedElement() is AppBarButton))
            {
                TextBox textBox = e.OriginalSource as TextBox;
                CommitRename(textBox);
            }
        }

        private async void CommitRename(TextBox textBox)
        {
            EndRename(textBox);
            string newItemName = textBox.Text.Trim().TrimEnd('.');

            bool successful = await UIFilesystemHelpers.RenameFileItemAsync(renamingItem, oldItemName, newItemName, ParentShellPageInstance);
            if (!successful)
            {
                renamingItem.ItemName = oldItemName;
            }
        }

        private void EndRename(TextBox textBox)
        {
            ListViewItem gridViewItem = FileList.ContainerFromItem(renamingItem) as ListViewItem;
            if (gridViewItem == null)
            {
                // Navigating away, do nothing
            }
            else
            {
                TextBlock textBlock = gridViewItem.FindDescendant("ItemName") as TextBlock;
                textBox.Visibility = Visibility.Collapsed;
                textBlock.Visibility = Visibility.Visible;
            }

            textBox.LostFocus -= RenameTextBox_LostFocus;
            textBox.KeyDown -= RenameTextBox_KeyDown;
            FileNameTeachingTip.IsOpen = false;
            IsRenamingItem = false;
        }

        private void FileList_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var ctrlPressed = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var shiftPressed = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            if (e.Key == VirtualKey.Enter && !e.KeyStatus.IsMenuKeyDown)
            {
                if (!IsRenamingItem)
                {
                    NavigationHelpers.OpenSelectedItems(ParentShellPageInstance, false);
                    e.Handled = true;
                }
            }
            else if (e.Key == VirtualKey.Enter && e.KeyStatus.IsMenuKeyDown)
            {
                FilePropertiesHelpers.ShowProperties(ParentShellPageInstance);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Space)
            {
                if (!IsRenamingItem && !ParentShellPageInstance.NavigationToolbar.IsEditModeEnabled)
                {
                    if (InteractionViewModel.IsQuickLookEnabled)
                    {
                        QuickLookHelpers.ToggleQuickLook(ParentShellPageInstance);
                    }
                    e.Handled = true;
                }
            }
            else if (e.KeyStatus.IsMenuKeyDown && (e.Key == VirtualKey.Left || e.Key == VirtualKey.Right || e.Key == VirtualKey.Up))
            {
                // Unfocus the GridView so keyboard shortcut can be handled
                (ParentShellPageInstance.NavigationToolbar as Control)?.Focus(FocusState.Pointer);
            }
            else if (ctrlPressed && shiftPressed && (e.Key == VirtualKey.Left || e.Key == VirtualKey.Right || e.Key == VirtualKey.W))
            {
                // Unfocus the ListView so keyboard shortcut can be handled (ctrl + shift + W/"->"/"<-")
                (ParentShellPageInstance.NavigationToolbar as Control)?.Focus(FocusState.Pointer);
            }
            else if (e.KeyStatus.IsMenuKeyDown && shiftPressed && e.Key == VirtualKey.Add)
            {
                // Unfocus the ListView so keyboard shortcut can be handled (alt + shift + "+")
                (ParentShellPageInstance.NavigationToolbar as Control)?.Focus(FocusState.Pointer);
            }
        }

        protected override void Page_CharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (ParentShellPageInstance != null)
            {
                if (ParentShellPageInstance.CurrentPageType == typeof(DetailsLayoutBrowser) && !IsRenamingItem)
                {
                    // Don't block the various uses of enter key (key 13)
                    var focusedElement = FocusManager.GetFocusedElement() as FrameworkElement;
                    if (args.KeyCode == 13
                        || focusedElement is Button
                        || focusedElement is TextBox
                        || focusedElement is PasswordBox
                        || DependencyObjectHelpers.FindParent<ContentDialog>(focusedElement) != null)
                    {
                        return;
                    }

                    base.Page_CharacterReceived(sender, args);
                    FileList.Focus(FocusState.Keyboard);
                }
            }
        }

        protected override ListedItem GetItemFromElement(object element)
        {
            return (element as ListViewItem).DataContext as ListedItem;
        }

        private void FileListGridItem_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.KeyModifiers == VirtualKeyModifiers.Control)
            {
                if ((sender as SelectorItem).IsSelected)
                {
                    (sender as SelectorItem).IsSelected = false;
                    // Prevent issues arising caused by the default handlers attempting to select the item that has just been deselected by ctrl + click
                    e.Handled = true;
                }
            }
            else if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
            {
                if (!(sender as SelectorItem).IsSelected)
                {
                    (sender as SelectorItem).IsSelected = true;
                }
            }
        }

        private uint currentIconSize;

        private void FolderSettings_GridViewSizeChangeRequested(object sender, EventArgs e)
        {
            var requestedIconSize = FolderSettings.GetIconSize(); // Get new icon size

            // Prevents reloading icons when the icon size hasn't changed
            if (requestedIconSize != currentIconSize)
            {
                currentIconSize = requestedIconSize; // Update icon size before refreshing
                ReloadItemIcons();
            }
        }

        private async void ReloadItemIcons()
        {
            ParentShellPageInstance.FilesystemViewModel.CancelExtendedPropertiesLoading();
            foreach (ListedItem listedItem in ParentShellPageInstance.FilesystemViewModel.FilesAndFolders.ToList())
            {
                listedItem.ItemPropertiesInitialized = false;
                if (FileList.ContainerFromItem(listedItem) != null)
                {
                    listedItem.ItemPropertiesInitialized = true;
                    await ParentShellPageInstance.FilesystemViewModel.LoadExtendedItemProperties(listedItem, currentIconSize);
                }
            }
        }

        private async void FileList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var ctrlPressed = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var shiftPressed = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            // Skip code if the control or shift key is pressed or if the user is using multiselect
            if (ctrlPressed || shiftPressed || InteractionViewModel.MultiselectEnabled)
            {
                return;
            }

            // Check if the setting to open items with a single click is turned on
            if (AppSettings.OpenItemsWithOneclick)
            {
                await Task.Delay(200); // The delay gives time for the item to be selected
                NavigationHelpers.OpenSelectedItems(ParentShellPageInstance, false);
            }
        }

        private async void FileList_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
            }
            args.ItemContainer.DataContext = args.Item;
            InitializeDrag(args.ItemContainer);

            if (args.Item is ListedItem item && !item.ItemPropertiesInitialized)
            {
                args.ItemContainer.PointerPressed += FileListGridItem_PointerPressed;
                args.ItemContainer.CanDrag = args.ItemContainer.IsSelected; // Update CanDrag

                item.ItemPropertiesInitialized = true;
                await ParentShellPageInstance.FilesystemViewModel.LoadExtendedItemProperties(item, currentIconSize);
            }
        }

        private void FileList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ResetDoubleClick();

            // Skip opening selected items if the double tap doesn't capture an item
            if ((e.OriginalSource as FrameworkElement)?.DataContext is ListedItem && !AppSettings.OpenItemsWithOneclick)
            {
                if (!InteractionViewModel.MultiselectEnabled)
                {
                    NavigationHelpers.OpenSelectedItems(ParentShellPageInstance, false);
                }
            }

            renameDoubleClickTimer.Stop();
        }

        #region IDisposable

        public override void Dispose()
        {
            UnhookEvents();
            CommandsViewModel?.Dispose();
        }

        #endregion IDisposable

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            // This is the best way I could find to set the context flyout, as doing it in the styles isn't possible
            // because you can't use bindings in the setters
            DependencyObject item = VisualTreeHelper.GetParent(sender as Grid);
            while (!(item is ListViewItem))
                item = VisualTreeHelper.GetParent(item);
            var itemContainer = item as ListViewItem;
            itemContainer.ContextFlyout = ItemContextMenuFlyout;
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // This prevents the drag selection rectangle from appearing when resizing the columns
            e.Handled = true;
        }

        private void ItemNameGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CheckDoubleClickToRename();
        }

        private int clickCount = 0;
        private void CheckDoubleClickToRename()
        {
            if (clickCount < 1)
            {
                if (renameDoubleClickTimer.IsRunning || AppSettings.OpenItemsWithOneclick)
                {
                    ResetDoubleClick();
                }
                else
                {
                    clickCount++;
                    renameDoubleClickTimer.Debounce(() =>
                    {
                        renameDoubleClickTimer.Stop();
                    }, TimeSpan.FromMilliseconds(510));

                    if (!renameDoubleClickTimeoutTimer.IsRunning)
                    {
                        renameDoubleClickTimeoutTimer.Debounce(() =>
                        {
                            ResetDoubleClick();
                        }, TimeSpan.FromMilliseconds(2000));
                    }
                }
            }
            else
            {
                ResetDoubleClick();
                StartRenameItem();
            }
        }

        private void ResetDoubleClick()
        {
            renameDoubleClickTimeoutTimer.Stop();
            renameDoubleClickTimer.Stop();
            clickCount = 0;
        }

        private void GridSplitter_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            UpdateColumnLayout();
        }

        private void UpdateColumnLayout()
        {
            ColumnsViewModel.IconColumn.UserLength = new GridLength(Column1.ActualWidth, GridUnitType.Pixel);
            ColumnsViewModel.NameColumn.UserLength = new GridLength(Column2.ActualWidth, GridUnitType.Pixel);
            ColumnsViewModel.OriginalPathColumn.UserLength = new GridLength(Column3.ActualWidth, GridUnitType.Pixel);
            ColumnsViewModel.DateDeletedColumn.UserLength = new GridLength(Column4.ActualWidth, GridUnitType.Pixel);
            ColumnsViewModel.StatusColumn.UserLength = new GridLength(Column5.ActualWidth, GridUnitType.Pixel);
            ColumnsViewModel.DateModifiedColumn.UserLength = new GridLength(Column6.ActualWidth, GridUnitType.Pixel);
            ColumnsViewModel.ItemTypeColumn.UserLength = new GridLength(Column7.ActualWidth, GridUnitType.Pixel);
            ColumnsViewModel.TotalWidth = Math.Max(RootGrid.ActualWidth, Column1.ActualWidth + Column2.ActualWidth + Column3.ActualWidth + Column4.ActualWidth + Column5.ActualWidth
                    + Column6.ActualWidth + Column7.ActualWidth);
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateColumnLayout();
        }

        private void GridSplitter_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            ParentShellPageInstance.InstanceViewModel.FolderSettings.ColumnsViewModel = ColumnsViewModel;
        }

        private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            ParentShellPageInstance.InstanceViewModel.FolderSettings.ColumnsViewModel = ColumnsViewModel;
        }
    }
}