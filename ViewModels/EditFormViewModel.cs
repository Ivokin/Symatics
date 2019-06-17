using Caliburn.Micro;
using System;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TM.Shared.Constants;
using TM.Shared.Enumerations;
using TM.Shared.Models;
using TM.WPF.DataModels;
using TM.WPF.Enumerations;
using TM.WPF.Events;
using TM.WPF.Helpers;
using TM.WPF.Infrastructure;

namespace TM.WPF.ViewModels
{
    public class EditFormViewModel : Screen, IHandle<ChooseBankDialogEventArgs>
    {
        private readonly IBusyIndicator busyIndicator;
        private readonly IWindowManager windowManager;
        private readonly IRequestManager requestManager;
        private readonly IEventAggregator eventAggregator;
        private readonly INotificationManager notificationManager;
        private EditedFormDataModel editedForm;
        private bool isFocus;

        public EditFormViewModel(
            INotificationManager notificationManager,
            IEventAggregator eventAggregator,
            IRequestManager requestManager,
            IWindowManager windowManager,
            IBusyIndicator busyIndicator,
            ICache cache)
        {
            this.busyIndicator = busyIndicator;
            this.windowManager = windowManager;
            this.requestManager = requestManager;
            this.notificationManager = notificationManager;
            this.eventAggregator = eventAggregator;
            this.eventAggregator.Subscribe(this);
            EditedForm = new EditedFormDataModel();
            EditedForm.KeyFormTypes = cache.GetKeyFormTypes().ToList();
        }

        public string HeaderName { get; set; }

        public EditedFormDataModel EditedForm
        {
            get
            {
                return editedForm;
            }
            set
            {
                editedForm = value;
                NotifyOfPropertyChange();
            }
        }

        public bool IsFocus
        {
            get
            {
                return isFocus;
            }
            set
            {
                isFocus = value;
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

        public void BtnCancel()
        {
            base.TryClose();
        }

        public async void BtnSaveForm()
        {
            if (EditedForm != null)
            {
                if (!EditedForm.IsNew)
                {
                    var newForm = new FormUpdateModel();
                    newForm.IdForm = EditedForm.IdForm;
                    newForm.OldFormNr = EditedForm.OldFormNr;
                    newForm.FormNr = EditedForm.FormNr;
                    newForm.Description = EditedForm.Description;
                    newForm.Thickness = EditedForm.Thickness;
                    newForm.Width = EditedForm.Width;
                    newForm.Length = EditedForm.Length;
                    newForm.Weight = EditedForm.Weight;
                    newForm.IdOwner = EditedForm.OwnerChooseModel.Id;
                    newForm.RowVersion = EditedForm.RowVersion;
                    await UpdateFormAsync(newForm, EditedForm.OwnerChooseModel);
                }
                else
                {
                    var newForm = new FormAddModel();
                    newForm.FormNr = EditedForm.FormNr;
                    newForm.Description = EditedForm.Description;
                    newForm.Thickness = EditedForm.Thickness;
                    newForm.Width = EditedForm.Width;
                    newForm.Length = EditedForm.Length;
                    newForm.Weight = EditedForm.Weight;
                    newForm.IdOwner = EditedForm.OwnerChooseModel.Id;
                    newForm.KeyFormType = (KeyFormTypes)EditedForm.SelectedKeyFormType.IdKeyFormType;
                    await AddFormAsync(newForm, EditedForm.OwnerChooseModel);
                }
            }
        }

        public void Handle(ChooseBankDialogEventArgs model)
        {
            if (model != null && model.Recepient == this && model.ChooseOwner != null)
            {
                EditedForm.OwnerChooseModel = model.ChooseOwner;
            }
        }

        public void ReceiveModel(FormModel formModel)
        {
            if (formModel == null)
            {
                throw new ArgumentNullException();
            }

            if (formModel.IdForm == Guid.Empty)
            {
                HeaderName = ResourcesFiles.Resources.NewForm;
                EditedForm.IsNew = true;
            }
            else
            {
                EditedForm.IsNew = false;
                HeaderName = ResourcesFiles.Resources.EditForm;
                EditedForm.IdForm = formModel.IdForm;
                EditedForm.Description = formModel.Description.Trim();
                EditedForm.Length = formModel.Length;
                EditedForm.Width = formModel.Width;
                EditedForm.Weight = formModel.Weight;
                EditedForm.Thickness = formModel.Thickness;
                EditedForm.FormNr = formModel.FormNr.Trim();
                EditedForm.OldFormNr = EditedForm.FormNr;
                EditedForm.RowVersion = formModel.RowVersion;
                EditedForm.SelectedKeyFormType = EditedForm.KeyFormTypes.FirstOrDefault(x => x.IdKeyFormType == (int)formModel.KeyFormType);
            }

            if (formModel.OwnerChooseModel != null)
            {
                EditedForm.OwnerChooseModel = formModel.OwnerChooseModel;
            }

            EditedForm.IsValidEnabled = true;
        }

        #region Protected Methods
        protected override void OnDeactivate(bool close)
        {
            eventAggregator.Unsubscribe(this);
            base.OnDeactivate(close);
        }
        #endregion

        #region Private Methods
        private async Task UpdateFormAsync(FormUpdateModel updateForm, OwnerChooseModel ownerChooseModel)
        {
            IsFocus = false;
            bool validationResult = updateForm != null ? await ValidateCreationOrEditOfFormAsync(updateForm.FormNr, updateForm.OldFormNr, updateForm.IdForm) : false;
            if (validationResult)
            {
                bool shouldClose = true;
                try
                {
                    UpdateFormEventArgs updateFormEventArgs = new UpdateFormEventArgs();
                    busyIndicator.BlockUI();
                    var result = await requestManager.PatchAsync(RoutingConstants.FormRoute, updateForm);
                    if (result.IsError)
                    {
                        bool isMissing = result.ErrorCode == (int)TMErrorCode.ObjectNotFoundError;
                        if (!isMissing)
                        {
                            notificationManager.Alert(result.ErrorMessage, result.IsFatalError);
                            shouldClose = false;

                            if (result.StatusCode == HttpStatusCode.Conflict && updateForm.IdForm != null)
                            {
                                EditedForm.IsValid = false;
                                EditedForm.IsValidEnabled = false;
                                ReceiveModel(await GetFormAsync(updateForm.IdForm));
                                updateFormEventArgs = NewUpdateFormEventArgs(EditedForm.IdForm, ownerChooseModel, false);
                            }
                        }
                    }
                    else
                    {
                        updateFormEventArgs = NewUpdateFormEventArgs(EditedForm.IdForm, ownerChooseModel, false);
                    }

                    eventAggregator.PublishOnUIThread(updateFormEventArgs);
                }
                finally
                {
                    busyIndicator.UnblockUI();
                }

                if (shouldClose)
                {
                    BtnCancel();
                }
            }
            IsFocus = true;
        }

        private UpdateFormEventArgs NewUpdateFormEventArgs(Guid idForm, OwnerChooseModel ownerChooseModel, bool isNew)
        {
            UpdateFormEventArgs updateFormEventArgs = new UpdateFormEventArgs()
            {
                FormId = idForm,
                OwnerChooseModel = ownerChooseModel,
                IsNew = isNew
            };

            return updateFormEventArgs;
        }

        private async Task<FormModel> GetFormAsync(Guid idForm)
        {
            if (idForm == Guid.Empty)
            {
                throw new ArgumentNullException();
            }

            string path = string.Format("{0}/{1}", RoutingConstants.FormRoute, idForm.ToString());
            RequestResponse<FormModel> response = new RequestResponse<FormModel>();
            response = await requestManager.GetAsync<FormModel>(path);
            if (response.Data == null || response.IsError)
            {
                notificationManager.Alert(response.ErrorMessage, response.IsFatalError);
                BtnCancel();
            }

            return response.Data;
        }

        private async Task AddFormAsync(FormAddModel newForm, OwnerChooseModel ownerChooseModel)
        {
            IsFocus = false;
            bool validationResult = await ValidateCreationOrEditOfFormAsync(newForm.FormNr, null, newForm.IdForm);
            if (validationResult)
            {
                try
                {
                    busyIndicator.BlockUI();
                    var result = await requestManager.PostAsync<FormModel>(RoutingConstants.FormRoute, newForm);
                    if (result != null && !result.IsError)
                    {
                        eventAggregator.PublishOnUIThread(NewUpdateFormEventArgs(result.Data.IdForm, ownerChooseModel, true));
                        BtnCancel();
                    }
                    else
                    {
                        notificationManager.Alert(result.ErrorMessage, result.IsFatalError);
                    }
                }
                finally
                {
                    busyIndicator.UnblockUI();
                }
            }
            IsFocus = true;
        }

        private async Task<bool> ValidateCreationOrEditOfFormAsync(string formNr, string excludeSelfNr, Guid id)
        {
            //Currently we only validate formNr if we ever validate other things too remove this next line.
            bool validationResult = formNr.Equals(excludeSelfNr);
            if (!validationResult)
            {
                string url = string.Format("{0}/{1}?formNr={2}&id={3}", RoutingConstants.FormRoute, RoutingFragmentConstants.ValidateCreationOrEditOfFormFragment, formNr, id);
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

            return validationResult;
        }
        #endregion
    }
}