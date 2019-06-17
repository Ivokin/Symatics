using System.Windows;
using System.Windows.Input;
using Telerik.Windows.Controls;
using TM.WPF.ViewModels;

namespace TM.WPF.Views
{
    /// <summary>
    /// Interaction logic for EditFormView.xaml
    /// </summary>
    public partial class EditFormView : RadWindow
    {
        public EditFormView()
        {
            InitializeComponent();
            PreviewKeyUp += ConfirmViewPreviewKeyUp;
            Loaded += EditFormViewLoaded;
        }

        #region Private Methods
        private void ConfirmViewPreviewKeyUp(object sender, KeyEventArgs e)
        {
            var editFormViewModel = (EditFormViewModel)DataContext;
            Key keyPressed = e.Key;
            if (keyPressed == Key.Escape)
            {
                editFormViewModel.BtnCancel();
            }
            else if (keyPressed == Key.Enter)
            {
                if (editFormViewModel.EditedForm.IsValid)
                {
                    editFormViewModel.BtnSaveForm();
                }
            }
        }

        private void EditFormViewLoaded(object sender, RoutedEventArgs e)
        {
            if (FormNr != null)
            {
                FormNr.Focus();
                if (!string.IsNullOrEmpty(FormNr.Text))
                {
                    FormNr.CaretIndex = FormNr.Text.Length;
                }
            }
        }

        private void DescriptionPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
            }
        }
        #endregion
    }
}

