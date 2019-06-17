using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TM.Shared.Constants;
using TM.Shared.Filters;
using TM.Shared.Models;
using TM.WPF.Events;
using TM.WPF.Helpers;
using TM.WPF.Infrastructure;
using TM.WPF.ResourcesFiles;
using TM.WPF.UserControls;

namespace TM.WPF.ViewModels
{
    public class ManageFormsViewModel : Screen,
                                        IHandleWithTask<ChooseBankDialogEventArgs>,
                                        IHandle<UpdateFormEventArgs>,
                                        IReload
    {
        private readonly INotificationManager notificationManager;
        private readonly IEventAggregator eventAggregator;
        private readonly IRequestManager requestManager;
        private readonly IWindowManager windowManager;
        private bool userCtrlShowOwnerIsVisible;
        private bool chkBankIsActive;
        private string searchText;
        private string countOfForms;
        private FormFilter formFilter;
        private FormModel selectedForm;
        private StatusBarData statusBarData;
        private OwnerChooseModel ownerChooseModel;
        private BindableCollection<FormModel> formModels;

        public ManageFormsViewModel(
            IWindowManager windowManager,
            IBusyIndicator busyIndicator,
            IRequestManager requestManager,
            IEventAggregator eventAggregator,
            INotificationManager notificationManager)
        {
            this.eventAggregator = eventAggregator;
            this.eventAggregator.Subscribe(this);
            this.notificationManager = notificationManager;
            this.windowManager = windowManager;
            this.requestManager = requestManager;
            BusyIndicator = busyIndicator;

            ChkBankIsActive = false;
            formFilter = new FormFilter();
            FormModels = new BindableCollection<FormModel>();
        }

        public event EventHandler ScrollOrRefreshGrid;

        public IBusyIndicator BusyIndicator { get; set; }

        public string SelectedFormCellValue { get; set; }

        public string SearchText
        {
            get
            {
                return searchText;
            }
            set
            {
                searchText = value;
                NotifyOfPropertyChange();
            }
        }

        public StatusBarData StatusBarData
        {
            get
            {
                return statusBarData;
            }
            set
            {
                statusBarData = value;
                NotifyOfPropertyChange();
            }
        }

        public bool CanBtnActive
        {
            get
            {
                return SelectedForm != null;
            }
        }

        public FormModel SelectedForm
        {
            get
            {
                return selectedForm;
            }
            set
            {
                selectedForm = value;
                UpdateStatusBar(selectedForm);
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(() => CanBtnActive);
            }
        }

        public OwnerChooseModel OwnerChooseModel
        {
            get
            {
                return ownerChooseModel;
            }
            set
            {
                ownerChooseModel = value;
                NotifyOfPropertyChange();
            }
        }

        public BindableCollection<FormModel> FormModels
        {
            get
            {
                return formModels;
            }
            set
            {
                formModels = value;
                NotifyOfPropertyChange();
            }
        }

        public string CountOfForms
        {
            get
            {
                return countOfForms;
            }
            set
            {

                countOfForms = value;
                NotifyOfPropertyChange();
            }
        }

        public bool UserCtrlShowOwnerIsVisible
        {
            get
            {
                return userCtrlShowOwnerIsVisible;
            }
            set
            {
                userCtrlShowOwnerIsVisible = value;
                NotifyOfPropertyChange();
            }
        }

        public bool ChkBankIsActive
        {
            get
            {
                return chkBankIsActive;
            }
            set
            {
                chkBankIsActive = value;
                NotifyOfPropertyChange();
            }
        }

        public void BtnChooseBank()
        {
            dynamic settings = new ExpandoObject();
            settings.Owner = GetView();
            var chooseBankViewModel = IoC.Get<ChooseBankViewModel>();
            chooseBankViewModel.OwnerScreen = this;
            this.windowManager.ShowDialog(chooseBankViewModel, null, settings);
        }

        public void BtnEnter()
        {
            BtnEditForm();
        }

        public void BtnCreateForm()
        {
            FormModel newFormModel = new FormModel();
            newFormModel.OwnerChooseModel = OwnerChooseModel;
            InitializeEditFormAsync(newFormModel);
        }

        public void BtnEditForm()
        {
            if (SelectedForm != null)
            {
                InitializeEditFormAsync(SelectedForm);
            }
        }

        public async Task BtnDeleteForm()
        {
            if (SelectedForm != null)
            {
                try
                {
                    BusyIndicator.BlockUI();
                    bool validationResult = await ValidateDeletionOfFormAsync(SelectedForm.IdForm);
                    if (validationResult)
                    {
                        bool deleteForm = false;
                        notificationManager.Confirm(Resources.ApplicationShortName, Environment.NewLine + Resources.DeleteFormQuestion, () => { deleteForm = true; }, owner: GetView());
                        if (deleteForm)
                        {
                            await DeleteFormAsync();
                        }
                    }
                }
                finally
                {
                    BusyIndicator.UnblockUI();
                }
            }
        }

        public async Task ReloadAsync()
        {
            if (BusyIndicator != null && !BusyIndicator.IsBusy)
            {
                await InitializeAsync(false);
            }
        }

        public void CopyContextOfCell()
        {
            if (SelectedFormCellValue == null)
            {
                Clipboard.SetText(string.Empty);
            }
            else
            {
                Clipboard.SetText(SelectedFormCellValue);
            }
        }

        public async void ChkBankIsPressed()
        {
            if (!UserCtrlShowOwnerIsVisible)
            {
                UserCtrlShowOwnerIsVisible = true;
                BtnChooseBank();
                if (OwnerChooseModel == null)
                {
                    ChkBankIsActive = false;
                    UserCtrlShowOwnerIsVisible = false;
                }
            }
            else
            {
                ChkBankIsActive = false;
                UserCtrlShowOwnerIsVisible = false;
                formFilter.OwnerId = null;
                OwnerChooseModel = null;
                await InitializeAsync(false);
            }
        }

        public async Task Handle(ChooseBankDialogEventArgs model)
        {
            if (model != null && model.Recepient == this && model.ChooseOwner != null)
            {
                ChkBankIsActive = true;
                OwnerChooseModel = model.ChooseOwner;
                formFilter.OwnerId = OwnerChooseModel.Id;
                await InitializeAsync(false);
            }
        }

        public async void Handle(UpdateFormEventArgs updatedFormArgs)
        {
            if (updatedFormArgs == null || updatedFormArgs.FormId == null)
            {
                await InitializeAsync(false);
            }
            else
            {
                await UpdateGridAsync(updatedFormArgs);
            }

            UpdateHeaderView();
        }

        public void BtnCancel()
        {
            base.TryClose();
        }

        public void UpdateHeaderView()
        {
            if (FormModels.Any())
            {
                CountOfForms = string.Format(" {0} {1}", Resources.lblFrom, FormModels.Count.ToString());
            }
        }

        #region Protected Methods
        protected override async void OnViewReady(object view)
        {
            base.OnViewReady(view);
            await InitializeAsync(false);
        }

        protected override void OnDeactivate(bool close)
        {
            eventAggregator.Unsubscribe(this);
            base.OnDeactivate(close);
        }
        #endregion

        #region Private Methods
        private void UpdateStatusBar(FormModel selectedFormModel)
        {
            var statusBarData = new StatusBarData();

            if (selectedFormModel != null)
            {
                statusBarData.Id = selectedFormModel.IdForm.ToString();
                statusBarData.LabelContent = Resources.FormId;
                statusBarData.ModifyDate = selectedFormModel.ModifyDate.GetValueOrDefault();
                statusBarData.ModifyUser = string.IsNullOrWhiteSpace(selectedFormModel.ModifyUser) ? string.Empty : selectedFormModel.ModifyUser.TrimEnd();
                statusBarData.CreateDate = selectedFormModel.CreateDate;
                statusBarData.CreateUser = selectedFormModel.CreateUser.TrimEnd();
            }

            StatusBarData = statusBarData;
        }

        private async Task DeleteFormAsync()
        {
            var result = await requestManager.DeleteAsync($"{RoutingConstants.FormRoute}/{SelectedForm.IdForm}");
            if (result.IsError)
            {
                notificationManager.Alert(result.ErrorMessage, result.IsFatalError);
                await InitializeAsync(false);
            }
            else
            {
                notificationManager.ToastAlert(string.Format(Resources.FormDeleted, SelectedForm.FormNr));

                var form = FormModels.FirstOrDefault(x => x.IdForm == selectedForm.IdForm);
                if (form != null)
                {
                    FormModels.Remove(form);
                }

                UpdateHeaderView();
            }
        }

        private async Task UpdateGridAsync(UpdateFormEventArgs updatedFormArgs)
        {
            Guid callingOwnerId = updatedFormArgs.OwnerChooseModel.Id;
            Guid? formId = updatedFormArgs.FormId;

            string path = string.Format("{0}/{1}", RoutingConstants.FormRoute, formId.ToString());
            try
            {
                BusyIndicator.BlockUI();
                var result = await requestManager.GetAsync<FormModel>(path);
                if (result.Data == null || result.IsError)
                {
                    await InitializeAsync(true);
                }
                else
                {
                    FormModel resultForm = result.Data;
                    if (updatedFormArgs.IsNew)
                    {
                        bool scrollToFirst = false;
                        if (OwnerChooseModel != null)
                        {
                            if (OwnerChooseModel.Id == callingOwnerId)
                            {
                                FormModels.Add(resultForm);
                                SelectedForm = FormModels.FirstOrDefault(x => x.IdForm.Equals(resultForm.IdForm));
                            }
                            else
                            {
                                scrollToFirst = true;
                            }
                        }
                        else
                        {
                            FormModels.Add(resultForm);
                            SelectedForm = FormModels.FirstOrDefault(x => x.IdForm.Equals(resultForm.IdForm));
                        }

                        ScrollOrRefreshGrid(this, new CleanFilterEventArgs() { ScrollToFirst = scrollToFirst });
                    }
                    else
                    {
                        FormModel editedFormModel = FormModels.FirstOrDefault(x => x.IdForm == formId);
                        if (editedFormModel != null)
                        {
                            if (OwnerChooseModel != null && callingOwnerId != OwnerChooseModel.Id)
                            {
                                if (FormModels.Any())
                                {
                                    FormModels.Remove(editedFormModel);
                                }
                            }
                            else
                            {
                                int editedFormModelIndex = FormModels.IndexOf(editedFormModel);
                                if (editedFormModelIndex != -1)
                                {
                                    FormModels[editedFormModelIndex] = resultForm;
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                BusyIndicator.UnblockUI();
            }
        }

        private async Task InitializeAsync(bool busyIndicatorIsWorking)
        {
            FormModels.Clear();
            var filterQueryString = formFilter.ToQueryString();
            var url = string.Format("{0}?{1}", RoutingConstants.FormRoute, filterQueryString);
            RequestResponse<IEnumerable<FormModel>> response = new RequestResponse<IEnumerable<FormModel>>();
            try
            {
                if (!busyIndicatorIsWorking)
                {
                    BusyIndicator.BlockUI();
                }

                response = await requestManager
                .GetAsync<IEnumerable<FormModel>>(url);
                if (response != null && !response.IsError)
                {
                    FormModels.AddRange(response.Data);
                    UpdateHeaderView();
                }
                else
                {
                    notificationManager.Alert(response.ErrorMessage, response.IsFatalError);
                }
            }
            finally
            {
                if (!busyIndicatorIsWorking)
                {
                    BusyIndicator.UnblockUI();
                }
            }
        }

        private async void InitializeEditFormAsync(FormModel formModel)
        {
            bool canCall = true;
            try
            {
                if (formModel.IdForm != Guid.Empty)
                {
                    BusyIndicator.BlockUI();

                    string path = string.Format("{0}/{1}", RoutingConstants.FormRoute, formModel.IdForm.ToString());
                    RequestResponse<FormModel> response = await requestManager.GetAsync<FormModel>(path);
                    if (response.Data == null || response.IsError)
                    {
                        canCall = false;
                        await InitializeAsync(true);
                    }
                    else
                    {
                        formModel = response.Data;
                    }
                }
            }
            finally
            {
                BusyIndicator.UnblockUI();
            }

            if (canCall)
            {
                OpenEditForm(formModel);
            }
        }

        private void OpenEditForm(FormModel formModel)
        {
            dynamic settings = new ExpandoObject();
            settings.Owner = GetView();
            var editFormViewModel = IoC.Get<EditFormViewModel>();
            editFormViewModel.ReceiveModel(formModel);
            windowManager.ShowDialog(editFormViewModel, null, settings);
        }

        private async Task<bool> ValidateDeletionOfFormAsync(Guid id)
        {
            bool validationResult = id == null;
            if (!validationResult)
            {
                try
                {
                    string url = string.Format("{0}/{1}?id={2}", RoutingConstants.FormRoute, RoutingFragmentConstants.ValidateDeletionOfFormFragment, id);
                    BusyIndicator.BlockUI();
                    RequestResponse<TMValidationResult> response = await requestManager.GetAsync<TMValidationResult>(url);

                    if (response.IsError || response.Data == null)
                    {
                        notificationManager.Alert(response.ErrorMessage, string.Empty, response.IsFatalError);
                    }
                    else if (!response.Data.IsValid)
                    {
                        notificationManager.Alert(response.Data.ValidationMessage);
                    }
                    else
                    {
                        validationResult = true;
                    }
                }
                finally
                {
                    BusyIndicator.UnblockUI();
                }
            }

            return validationResult;
        }
        #endregion
    }
}
