using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;
using Telerik.Windows.Controls.GridView.SearchPanel;
using TM.WPF.DataModels;
using TM.WPF.Events;
using TM.WPF.Infrastructure;
using TM.WPF.ViewModels;

namespace TM.WPF.Views
{
    /// <summary>
    /// Interaction logic for ManageBanks.xaml
    /// </summary>
    public partial class ManageOwnersView : BaseDataWindow
    {
        private RadWatermarkTextBox textSearch;
        private ManageOwnersViewModel manageOwnersViewModel;
        private OwnerDataModel previousDataOwnerModel;
        private bool shouldMoveToPrevious;
        private bool shouldMoveToFirst;

        public ManageOwnersView()
        {
            InitializeComponent();
            shouldMoveToPrevious = false;
            shouldMoveToFirst = true;
            PreviewKeyUp += ConfirmViewPreviewKeyUp;
            Loaded += ManageOwnersViewLoaded;
        }

        #region Protected Methods
        protected override Task HandleEscapeKey()
        {
            ((ManageOwnersViewModel)DataContext).BtnCancel();
            return base.HandleEscapeKey();
        }

        protected override Task HandleEnterKey()
        {
            manageOwnersViewModel.BtnEnter();
            return base.HandleEnterKey();
        }

        #endregion

        #region Private Methods

        private void ManageOwnersViewLoaded(object sender, RoutedEventArgs e)
        {
            manageOwnersViewModel = (ManageOwnersViewModel)DataContext;
            if (manageOwnersViewModel != null)
            {
                manageOwnersViewModel.CancelButtonClicked += CancelButtonClicked;
                manageOwnersViewModel.CleanFilter += RemoveGridFilters;
                manageOwnersViewModel.ScrollToNewOwner += ScrollToNewBank;
            }
        }

        private void ScrollToNewBank(object sender, EventArgs e)
        {
            var eventArgs = (OwnerDataModel)(e as SelectAndMoveToNewItemInGridArgs).NewItem;
            if (eventArgs != null)
            {
                ClearClientFilters();
                GrdBanks.SelectedItem = eventArgs;
                ScrollToSelectedItem();
            }
        }

        private void RemoveGridFilters(object sender, EventArgs e)
        {
            var eventArgs = (e as CleanFilterEventArgs).ScrollToFirst;
            if (eventArgs)
            {
                ClearClientFilters();
            }
        }

        private void ClearClientFilters()
        {
            var clearSearchValue = GridViewSearchPanelCommands.ClearSearchValue as RoutedUICommand;
            clearSearchValue.Execute(null, GrdBanks.ChildrenOfType<GridViewSearchPanel>().FirstOrDefault());

            GrdBanks.FilterDescriptors.SuspendNotifications();
            foreach (GridViewColumn column in GrdBanks.Columns)
            {
                column.ClearFilters();
            }
            GrdBanks.FilterDescriptors.ResumeNotifications();
        }

        private void CancelButtonClicked(object sender, EventArgs e)
        {
            var result = e as MoveToPreviousSelectionArgs;
            shouldMoveToPrevious = result.ShouldMoveToPrevious;
            previousDataOwnerModel = result.SelectedOwnerDataModel;
        }

        private void TextSearchLoaded(object sender, RoutedEventArgs e)
        {
            GrdBanks.Focus();
            textSearch = GrdBanks.ChildrenOfType<RadWatermarkTextBox>().FirstOrDefault(s => s.Name == "PART_SearchAsYouTypeTextBox");
            if (textSearch != null)
            {
                textSearch.Focus();
                textSearch.PreviewKeyDown += TextSearchPreviewKeyDown; ;
                GrdBanks.Items.CollectionChanged += GrdBankCollectionChanged;
                manageOwnersViewModel = (ManageOwnersViewModel)DataContext;
            }

            var closeButton = GrdBanks.ChildrenOfType<RadPathButton>().FirstOrDefault(b => b.Name == "CloseButton");
            if (closeButton != null)
            {
                closeButton.Visibility = Visibility.Collapsed;
            }
        }

        private void GrdBankCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (GrdBanks.Items.Count > 0 && shouldMoveToFirst)
            {
                GrdBanks.Items.MoveCurrentToFirst();
            }

            CurrentCountAfterFilter.Text = GrdBanks.Items.Count.ToString();
        }

        private void ConfirmViewPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                manageOwnersViewModel.BtnCancel();
            }
        }

        private void GrdBanksCurrentCellChanged(object sender, GridViewCurrentCellChangedEventArgs e)
        {
            var currentSelection = sender as RadGridView;
            if (currentSelection != null
                && currentSelection.CurrentCell?.Value != null
                && manageOwnersViewModel != null)
            {
                manageOwnersViewModel.SelectedFormCellValue = currentSelection.CurrentCell.Value.ToString().Trim();
            }
        }

        private async void TextSearchPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!textSearch.IsFocused || GrdBanks.Items.Count == 0)
            {
                return;
            }

            Key keyPressed = e.Key;
            if (keyPressed == Key.Up)
            {
                if (!GrdBanks.Items.MoveCurrentToPrevious())
                {
                    GrdBanks.Items.MoveCurrentToFirst();
                }
            }
            else if (keyPressed == Key.F5)
            {
                await manageOwnersViewModel.ReloadAsync();
            }
            else if (keyPressed == Key.Down)
            {
                if (!GrdBanks.Items.MoveCurrentToNext())
                {
                    GrdBanks.Items.MoveCurrentToLast();
                }
            }
            else if (keyPressed == Key.PageUp)
            {
                var movePageUpCommand = RadGridViewCommands.MovePageUp as RoutedUICommand;
                movePageUpCommand.Execute(null, GrdBanks);
            }
            else if (keyPressed == Key.Next || keyPressed == Key.PageDown)
            {
                var movePageDownCommand = RadGridViewCommands.MovePageDown as RoutedUICommand;
                movePageDownCommand.Execute(null, GrdBanks);
            }

            ScrollToSelectedItem();
        }

        private void ScrollToSelectedItem()
        {
            if (GrdBanks.Items.CurrentItem != null)
            {
                GrdBanks.CurrentCellInfo = new GridViewCellInfo(GrdBanks.Items.CurrentItem, GrdBanks.Columns[0]);
                GrdBanks.ScrollIntoView(GrdBanks.SelectedItem);
                GrdBanks.Focus();
                textSearch.Focus();
            }
        }

        private void ClearButtonClick(object sender, RoutedEventArgs e)
        {
            var clearSearchValue = GridViewSearchPanelCommands.ClearSearchValue as RoutedUICommand;
            clearSearchValue.Execute(null, GrdBanks.ChildrenOfType<GridViewSearchPanel>().FirstOrDefault());
            textSearch.Focus();
        }

        private void GrdBanksSelectionChanging(object sender, SelectionChangingEventArgs e)
        {
            shouldMoveToFirst = true;
            var currentItem = (e.Source as RadGridView).SelectedItem as OwnerDataModel;
            if (currentItem != null && currentItem.IsDirty && !shouldMoveToPrevious)
            {
                shouldMoveToFirst = false;
                manageOwnersViewModel.ConfirmChangesAsync(currentItem, true, false);
            }
        }

        private void GrdBanksSelectionChanged(object sender, SelectionChangeEventArgs e)
        {
            if (shouldMoveToPrevious)
            {
                GrdBanks.SelectedItem = previousDataOwnerModel;
                shouldMoveToPrevious = false;
            }
        }
    }
    #endregion
}

