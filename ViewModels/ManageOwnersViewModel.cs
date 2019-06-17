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
using TM.WPF.DataModels;
using TM.WPF.Events;
using TM.WPF.Helpers;
using TM.WPF.Infrastructure;
using TM.WPF.ResourcesFiles;
using TM.WPF.UserControls;

namespace TM.WPF.ViewModels
{
    public class ManageOwnersViewModel : Conductor<IScreen>.Collection.OneActive
    {
        private readonly IWindowManager windowManager;
        private readonly IRequestManager requestManager;
        private readonly IMappingManager mappingManager;
        private readonly IEventAggregator eventAggregator;
        private readonly INotificationManager notificationManager;
        private OwnerModelFilter ownerFilter;
        private OwnerDataModel selectedOwner;
        private StatusBarData statusBarData;
        private EditBankViewModel editOwnerViewModel;
        private BindableCollection<OwnerDataModel> owners;
        private string countOfOwners;

        public ManageOwnersViewModel(
            IRequestManager requestManager,
            IWindowManager windowManager,
            INotificationManager notificationManager,
            IEventAggregator eventAggregator,
            IMappingManager mappingManager,
            IBusyIndicator busyIndicator)
        {
            this.mappingManager = mappingManager;
            this.windowManager = windowManager;
            this.eventAggregator = eventAggregator;
            this.requestManager = requestManager;
            this.notificationManager = notificationManager;
            BusyIndicator = busyIndicator;
            ownerFilter = new OwnerModelFilter();
            DisplayName = Resources.HeaderEditBank;
            Owners = new BindableCollection<OwnerDataModel>();
        }

        public event EventHandler ScrollToNewOwner;

        public event EventHandler CancelButtonClicked;

        public event EventHandler CleanFilter;

        public string SelectedFormCellValue { get; set; }

        public IBusyIndicator BusyIndicator { get; set; }

        public bool CanDelete
        {
            get
            {
                return SelectedOwner != null && !SelectedOwner.Standard;
            }
        }

        public OwnerDataModel SelectedOwner
        {
            get
            {
                return selectedOwner;
            }
            set
            {
                selectedOwner = value;
                UpdateStatusBar();
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(() => CanDelete);
            }
        }

        public string CountOfOwners
        {
            get
            {
                return countOfOwners;
            }
            private set
            {
                countOfOwners = value;
                NotifyOfPropertyChange();
            }
        }

        public BindableCollection<OwnerDataModel> Owners
        {
            get
            {
                return owners;
            }
            set
            {
                owners = value;
                NotifyOfPropertyChange();
            }
        }

        public StatusBarData StatusBarData
        {
            get
            {
                return statusBarData;
            }
            private set
            {
                statusBarData = value;
                NotifyOfPropertyChange();
            }
        }

        public async void BtnRevert()
        {
            if (SelectedOwner != null && SelectedOwner.IsNew && Owners.Any())
            {
                SelectedOwner.IsDirty = false;
                Owners.Remove(SelectedOwner);
            }
            else if (SelectedOwner != null && SelectedOwner.IsDirty && !SelectedOwner.IsNew)
            {
                await GetOwnerAndUpdateGridAsync(SelectedOwner, false);
            }
        }

        public async void BtnDelete()
        {
            if (SelectedOwner != null && SelectedOwner.IdOwner != null && SelectedOwner.IdOwner != Guid.Empty && !SelectedOwner.Standard)
            {
                bool shouldDeleteOwner = false;
                notificationManager.Confirm(Resources.ApplicationShortName, Environment.NewLine + Resources.DeleteOwnerQuestion, () => { shouldDeleteOwner = true; }, owner: GetView());
                if (shouldDeleteOwner)
                {
                    await DeleteOwnerAsync();
                }
            }
            else if (SelectedOwner != null && SelectedOwner.IdOwner == Guid.Empty)
            {
                SelectedOwner.IsDirty = false;
                Owners.Remove(SelectedOwner);
            }
        }

        public void BtnEnter()
        {
            if (SelectedOwner != null && SelectedOwner.IsDirty)
            {
                ConfirmChangesAsync(SelectedOwner, false, false);
            }
        }

        public void BtnCancel()
        {
            base.TryClose();
        }

        public void BtnCreate()
        {
            OwnerDataModel newOwner = new OwnerDataModel()
            {
                SystemCode = KeyConstants.KeySystemCodeBV,
                IsDirty = true,
                IsNew = true
            };

            CleanFilter(this, new CleanFilterEventArgs() { ScrollToFirst = true });
            Owners.Insert(0, newOwner);
            SelectedOwner = newOwner;
        }

        public void BtnSave()
        {
            if (SelectedOwner != null)
            {
                ConfirmChangesAsync(SelectedOwner, true, true);
            }
        }

        public void CopyContextOfCell()
        {
            if (string.IsNullOrWhiteSpace(SelectedFormCellValue))
            {
                Clipboard.SetText(string.Empty);
            }
            else
            {
                Clipboard.SetText(SelectedFormCellValue);
            }
        }

        public async void ConfirmChangesAsync(OwnerDataModel selectedDirtyOwner, bool updateGrid, bool shouldMoveToPrevious)
        {
            bool confirm = false;
            bool cancel = false;

            System.Action DialogYesBtnClicked = () =>
            {
                confirm = true;
            };

            System.Action DialogCancelBtnClicked = () =>
            {
                cancel = true;
            };

            dynamic settings = new ExpandoObject();
            settings.Owner = GetView();
            notificationManager.ShowThreeButtonConfirmation(settings, Resources.SaveTitle, Resources.SaveTemplateChangesConfirmation, DialogYesBtnClicked, null, DialogCancelBtnClicked);
            bool isValid = false;
            if (confirm && selectedDirtyOwner != null && selectedDirtyOwner.IsValid)
            {
                isValid = await ValidateOwnerAsync(selectedDirtyOwner);
            }

            if (confirm && isValid)
            {
                await SaveChangesAsync(selectedDirtyOwner, updateGrid);
            }
            else if (cancel)
            {
                CancelButtonClicked(this, new MoveToPreviousSelectionArgs() { ShouldMoveToPrevious = !shouldMoveToPrevious, SelectedOwnerDataModel = selectedDirtyOwner });
            }
            else
            {
                await RestoreGridAsync(selectedDirtyOwner);
            }
        }

        public async Task ReloadAsync()
        {
            if (!BusyIndicator.IsBusy)
            {
                await InitializeAsync(false);
            }
        }

        public void UpdateHeaderView()
        {
            if (Owners.Any())
            {
                CountOfOwners = string.Format(" {0} {1}", Resources.lblFrom, Owners.Count.ToString());
            }
        }

        #region Protected Methods
        protected override void OnDeactivate(bool close)
        {
            if (SelectedOwner != null && SelectedOwner.IsDirty)
            {
                ConfirmChangesAsync(SelectedOwner, false, false);
            }

            eventAggregator.Unsubscribe(this);
            base.OnDeactivate(close);
        }

        protected override async void OnViewReady(object view)
        {
            base.OnViewReady(view);
            await InitializeAsync(false);
            InitializeTabs();
        }
        #endregion

        #region Private methods

        private void UpdateStatusBar()
        {
            if (SelectedOwner != null)
            {
                var statusBarData = new StatusBarData();
                statusBarData.Id = SelectedOwner.IdOwner.ToString();
                statusBarData.LabelContent = Resources.OwnerId;
                statusBarData.ModifyDate = SelectedOwner.ModifyDate.GetValueOrDefault();
                statusBarData.ModifyUser = SelectedOwner.ModifyUser;
                statusBarData.CreateDate = SelectedOwner.CreateDate;
                statusBarData.CreateUser = SelectedOwner.CreateUser;
                StatusBarData = statusBarData;
                SelectedOwner.IsDirtyNotificationEnabled = true;
            }
            else
            {
                StatusBarData = null;
            }

            ReinitializeTabs();
        }

        private void ReinitializeTabs()
        {
            if (editOwnerViewModel != null)
            {
                editOwnerViewModel.Bank = SelectedOwner;
            }
        }

        private void InitializeTabs()
        {
            editOwnerViewModel = IoC.Get<EditBankViewModel>();
            editOwnerViewModel.Bank = SelectedOwner;
            Items.Add(editOwnerViewModel);
            ActiveItem = Items[0];
        }

        private async Task InitializeAsync(bool isBusyIndicatorOn)
        {
            if (SelectedOwner != null)
            {
                SelectedOwner.IsDirty = false;
            }

            try
            {
                if (!isBusyIndicatorOn)
                {
                    BusyIndicator.BlockUI();
                }

                Owners.Clear();
                ownerFilter.Internal = false;
                var filterQueryString = ownerFilter.ToQueryString();
                var url = string.Format("{0}/{1}?{2}", RoutingConstants.OwnerRoute, RoutingFragmentConstants.GetOwnersWithParentOwnerCode, filterQueryString);
                RequestResponse<IEnumerable<OwnerModel>> response = await requestManager.GetAsync<IEnumerable<OwnerModel>>(url);
                if (response != null && response.IsError)
                {
                    notificationManager.Alert(response.ErrorMessage, response.IsFatalError);
                }
                else
                {
                    List<OwnerDataModel> ownerModels = response.Data.Select(owner => mappingManager.MapToOwnerDataModel(owner)).ToList();
                    Owners.AddRange(ownerModels);
                }
                UpdateHeaderView();
            }
            finally
            {
                if (!isBusyIndicatorOn)
                {
                    BusyIndicator.UnblockUI();
                }
            }
        }

        private async Task DeleteOwnerAsync()
        {
            bool isValid = false;
            if (SelectedOwner != null && SelectedOwner.IdOwner != null)
            {
                isValid = await ValidateDeletionOfOwnerAsync();
            }

            if (isValid)
            {
                try
                {
                    BusyIndicator.BlockUI();
                    var result = await requestManager.DeleteAsync($"{RoutingConstants.OwnerRoute}/{SelectedOwner.IdOwner}");
                    if (result != null && result.IsError)
                    {
                        notificationManager.Alert(result.ErrorMessage, result.IsFatalError);
                    }
                    else if (!result.IsError)
                    {
                        notificationManager.ToastAlert(string.Format(Resources.BankDeleted, SelectedOwner.OwnerCode));
                        var owner = Owners.FirstOrDefault(x => x.IdOwner == SelectedOwner.IdOwner);
                        if (owner != null)
                        {
                            Owners.Remove(owner);
                        }
                    }
                }
                finally
                {
                    BusyIndicator.UnblockUI();
                }
            }
        }

        private async Task<bool> ValidateOwnerAsync(OwnerDataModel selectedDirtyOwner)
        {
            string url = string.Format("{0}/{1}?id={2}&ownerCode={3}&parentsOwnerCode={4}&ownersSystemCode={5}", RoutingConstants.OwnerRoute, RoutingFragmentConstants.ValidateCreationOrEditOfAnOwner, selectedDirtyOwner.IdOwner, selectedDirtyOwner.OwnerCode, selectedDirtyOwner.OwnerCodeOfParent, selectedDirtyOwner.SystemCode);
            bool validationResult = false;
            try
            {
                BusyIndicator.BlockUI();
                RequestResponse<TMValidationResult> response = await requestManager.GetAsync<TMValidationResult>(url);
                if (response != null && response.IsError || response.Data == null)
                {
                    notificationManager.Alert(response.ErrorMessage, string.Empty, response.IsFatalError);
                }

                validationResult = response.Data.IsValid;
                if (!validationResult)
                {
                    notificationManager.Alert(response.Data.ValidationMessage);
                }
            }
            finally
            {
                BusyIndicator.UnblockUI();
            }

            return validationResult;
        }

        private async Task<bool> ValidateDeletionOfOwnerAsync()
        {
            string url = string.Format("{0}/{1}?id={2}", RoutingConstants.OwnerRoute, RoutingFragmentConstants.ValidateDeletionOfAnOwner, SelectedOwner.IdOwner);
            bool validationResult = false;

            try
            {
                BusyIndicator.BlockUI();
                RequestResponse<TMValidationResult> response = await requestManager.GetAsync<TMValidationResult>(url);
                if (response.IsError || response.Data == null)
                {
                    notificationManager.Alert(response.ErrorMessage, string.Empty, response.IsFatalError);
                }

                validationResult = response.Data.IsValid;
                if (!validationResult)
                {
                    notificationManager.Alert(response.Data.ValidationMessage);
                }
            }
            finally
            {
                BusyIndicator.UnblockUI();
            }

            return validationResult;
        }

        private async Task SaveChangesAsync(OwnerDataModel selectedDirtyOwner, bool updateGrid)
        {
            try
            {
                BusyIndicator.BlockUI();
                if (selectedDirtyOwner == null)
                {
                    await InitializeAsync(true);
                    throw new ArgumentNullException();
                }

                OwnerModel owner = mappingManager.MapToOwnerModel(selectedDirtyOwner);
                RequestResponse response = new RequestResponse();
                if (selectedDirtyOwner.IsNew)
                {
                    response = await requestManager.PostAsync<OwnerModel>(RoutingConstants.OwnerRoute, owner);
                    updateGrid = false;
                }
                else
                {
                    response = await requestManager.PatchAsync(RoutingConstants.OwnerRoute, owner);
                }

                if (response != null && response.IsError)
                {
                    notificationManager.Alert(response.ErrorMessage, response.IsFatalError);
                    await InitializeAsync(true);
                }
                else if (updateGrid)
                {
                    await GetOwnerAndUpdateGridAsync(selectedDirtyOwner, true);
                }
                else if (!updateGrid)
                {
                    selectedDirtyOwner = mappingManager.MapToOwnerDataModel((response as RequestResponse<OwnerModel>).Data);
                    Owners.RemoveAt(0);
                    Owners.Insert(0, selectedDirtyOwner);
                    ScrollToNewOwner(this, new SelectAndMoveToNewItemInGridArgs() { NewItem = selectedDirtyOwner });
                }
            }
            finally
            {
                BusyIndicator.UnblockUI();
            }
        }

        private async Task GetOwnerAndUpdateGridAsync(OwnerDataModel selectedDirtyOwner, bool isBusyIndicatorOn)
        {
            try
            {
                if (!isBusyIndicatorOn)
                {
                    BusyIndicator.BlockUI();
                }

                if (selectedDirtyOwner == null || selectedDirtyOwner.IdOwner == null)
                {
                    await InitializeAsync(true);
                    throw new ArgumentNullException();
                }

                OwnerDataModel resultOwner = await GetOwnerAsync(selectedDirtyOwner.IdOwner, isBusyIndicatorOn);
                if (resultOwner != null)
                {
                    int indexOfOwner = Owners.IndexOf(selectedDirtyOwner);
                    SelectedOwner.IsDirty = false;
                    Owners.RemoveAt(indexOfOwner);
                    Owners.Insert(indexOfOwner, resultOwner);
                }
            }
            finally
            {
                if (!isBusyIndicatorOn)
                {
                    BusyIndicator.UnblockUI();
                }
            }
        }

        private async Task RestoreGridAsync(OwnerDataModel selectedDirtyOwner)
        {
            if (selectedDirtyOwner != null && selectedDirtyOwner.IsNew)
            {
                selectedDirtyOwner.IsDirty = false;
                Owners.Remove(selectedDirtyOwner);
                SelectedOwner = Owners.FirstOrDefault();
            }
            else
            {
                await GetOwnerAndUpdateGridAsync(selectedDirtyOwner, false);
            }
        }

        private async Task<OwnerDataModel> GetOwnerAsync(Guid idOwner, bool isBusyIndicatorOn)
        {
            OwnerDataModel owner = new OwnerDataModel();
            try
            {
                if (!isBusyIndicatorOn)
                {
                    BusyIndicator.BlockUI();
                }

                string url = string.Format("{0}/{1}?id={2}", RoutingConstants.OwnerRoute, RoutingFragmentConstants.GetOwnerWithParentOwnerCode, idOwner);
                var response = await requestManager.GetAsync<OwnerModel>(url);
                if (response != null && response.IsError)
                {
                    notificationManager.Alert(response.ErrorMessage, response.IsFatalError);
                    await InitializeAsync(isBusyIndicatorOn);
                    owner = null;
                }
                else if (!response.IsError)
                {
                    owner = mappingManager.MapToOwnerDataModel(response.Data);
                }
            }
            finally
            {
                if (!isBusyIndicatorOn)
                {
                    BusyIndicator.UnblockUI();
                }
            }
            return owner;
        }

        #endregion
    }
}
