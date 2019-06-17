using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;
using Telerik.Windows.Controls.GridView.SearchPanel;
using TM.WPF.Events;
using TM.WPF.Infrastructure;
using TM.WPF.ViewModels;

namespace TM.WPF.Views
{
    /// <summary>
    /// Interaction logic for ManageForms.xaml
    /// </summary>
    public partial class ManageFormsView : BaseDataWindow
    {
        private bool canChangeSelection;
        private RadWatermarkTextBox textSearch;
        private ManageFormsViewModel manageFormsViewModel;

        public ManageFormsView()
        {
            InitializeComponent();
            Loaded += ManageFormsViewLoaded;
            grdFormModels.MouseDoubleClick += GrdFormModelsMouseDoubleClick;
            this.PreviewKeyUp += ConfirmViewPreviewKeyUp;
            canChangeSelection = true;
        }

        #region Protected Methods
        protected override Task HandleEscapeKey()
        {
            ((ManageFormsViewModel)DataContext).BtnCancel();
            return base.HandleEscapeKey();
        }
        #endregion

        #region Private Methods
        private void ManageFormsViewLoaded(object sender, RoutedEventArgs e)
        {
            manageFormsViewModel = (ManageFormsViewModel)DataContext;
            if (manageFormsViewModel != null)
            {
                manageFormsViewModel.ScrollOrRefreshGrid += ScrollOrRefreshGrid;
            }
        }

        private void ScrollOrRefreshGrid(object sender, EventArgs e)
        {
            var result = e as CleanFilterEventArgs;
            if (result != null)
            {
                if (result.ScrollToFirst)
                {
                    ClearClientFilters();
                    grdFormModels.Items.MoveCurrentToFirst();
                }
                else
                {
                    canChangeSelection = false;
                    ClearClientFilters();
                    canChangeSelection = true;
                }

                if (grdFormModels.Items.CurrentItem != null && grdFormModels.SelectedItem != null)
                {
                    grdFormModels.ScrollIntoViewAsync(grdFormModels.SelectedItem, grdFormModels.Columns[0], null);
                }
            }
        }

        private void GrdFormModelsMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement originalSender = e.OriginalSource as FrameworkElement;
            if (originalSender != null)
            {
                var row = originalSender.ParentOfType<GridViewRow>();
                if (row != null)
                {
                    manageFormsViewModel.BtnEditForm();
                }
            }
        }

        private void TextSearchLoaded(object sender, RoutedEventArgs e)
        {
            grdFormModels.Focus();
            textSearch = grdFormModels.ChildrenOfType<RadWatermarkTextBox>().FirstOrDefault(s => s.Name == "PART_SearchAsYouTypeTextBox");

            if (textSearch != null)
            {
                textSearch.Focus();
                textSearch.PreviewKeyDown += TextSearchPreviewKeyDown;
                grdFormModels.Items.CollectionChanged += FormCollectionChanged;
            }
        }

        private void FormCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (grdFormModels.Items.Count > 0 && canChangeSelection)
            {
                grdFormModels.Items.MoveCurrentToFirst();
                textSearch?.Focus();
            }

            CurrentCountAfterFilter.Text = grdFormModels.Items.Count.ToString();
        }

        private void ConfirmViewPreviewKeyUp(object sender, KeyEventArgs e)
        {
            Key keyPressed = e.Key;
            if (keyPressed == Key.Escape)
            {
                manageFormsViewModel.BtnCancel();
            }
        }

        private async void TextSearchPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!textSearch.IsFocused || grdFormModels.Items.Count == 0)
            {
                return;
            }

            Key keyPressed = e.Key;
            if (keyPressed == Key.Up)
            {
                if (!grdFormModels.Items.MoveCurrentToPrevious())
                {
                    grdFormModels.Items.MoveCurrentToFirst();
                }
            }
            else if (keyPressed == Key.Down)
            {
                if (!grdFormModels.Items.MoveCurrentToNext())
                {
                    grdFormModels.Items.MoveCurrentToLast();
                }
            }
            else if (keyPressed == Key.F5)
            {
                await manageFormsViewModel.ReloadAsync();
            }
            else if (keyPressed == Key.PageUp)
            {
                var movePageUpCommand = RadGridViewCommands.MovePageUp as RoutedUICommand;
                movePageUpCommand.Execute(null, grdFormModels);
            }
            else if (keyPressed == Key.Next || keyPressed == Key.PageDown)
            {
                var movePageDownCommand = RadGridViewCommands.MovePageDown as RoutedUICommand;
                movePageDownCommand.Execute(null, grdFormModels);
            }

            ScrollToSelectedItem();
        }

        private void FormsCurrentCellChanged(object sender, GridViewCurrentCellChangedEventArgs e)
        {
            var currentSelection = sender as RadGridView;
            if (currentSelection != null
                && currentSelection.CurrentCell?.Value != null
                && manageFormsViewModel != null)
            {
                manageFormsViewModel.SelectedFormCellValue = currentSelection.CurrentCell.Value.ToString().Trim();
            }
        }

        private void ClearClientFilters()
        {
            var clearSearchValue = GridViewSearchPanelCommands.ClearSearchValue as RoutedUICommand;
            clearSearchValue.Execute(null, grdFormModels.ChildrenOfType<GridViewSearchPanel>().FirstOrDefault());

            grdFormModels.FilterDescriptors.SuspendNotifications();
            foreach (GridViewColumn column in grdFormModels.Columns)
            {
                column.ClearFilters();
            }
            grdFormModels.FilterDescriptors.ResumeNotifications();
            textSearch.Focus();
        }

        private void ScrollToSelectedItem()
        {
            if (grdFormModels.Items.CurrentItem != null)
            {
                grdFormModels.CurrentCellInfo = new GridViewCellInfo(manageFormsViewModel.SelectedForm, grdFormModels.Columns[0]);
                grdFormModels.ScrollIntoView(manageFormsViewModel.SelectedForm);
                grdFormModels.Focus();
                textSearch?.Focus();
            }
        }
        #endregion
    }
}