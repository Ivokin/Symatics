using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TM.Service.Core.Infrastructure.Extensions;
using TM.Service.Core.Infrastructure;
using TM.Service.Core.Repositories;
using TM.Shared.Filters;
using TM.Shared.Models;
using TM.Service.ORM.Models;
using ServerShared.Exceptions;
using ServerShared.Errors;
using ServerShared.Core.Infrastructure;
using TM.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace TM.Service.Core.Connectors
{
    public class FormConnector : IFormConnector
    {
        private readonly IUnitOfWork context;
        private readonly IMappingManager mappingManager;
        private readonly IDateTimeManager dateTimeManager;

        public FormConnector(IUnitOfWorkFactory unitOfWorkFactory,
            IDateTimeManager dateTimeManager,
            IMappingManager mappingManager)
        {
            this.dateTimeManager = dateTimeManager;
            this.mappingManager = mappingManager;
            context = unitOfWorkFactory.Create();
        }

        public async Task<FormModel> GetAsync(Guid id)
        {
            ValidateFormId(id);
            string[] properties = { nameof(Form.IdOwnerNavigation), nameof(Form.IdKeyFormTypeNavigation) };
            var existingEntity = await context
                .FormRepository
                .FirstOrDefaultAsync(x => x.IdForm.Equals(id), properties);
            if (existingEntity == null)
            {
                throw new TMNotFoundException(string.Format(ErrorMessages.ObjectNotFound, nameof(Form)));
            }

            return mappingManager.Create(existingEntity);
        }

        public async Task<IEnumerable<FormModel>> GetAsync(FormFilter filter)
        {
            if (filter == null)
            {
                throw new TMNotFoundException(string.Format(ErrorMessages.CollectionEmpty, nameof(filter)));
            }

            var filters = filter.Create();
            string[] properties = new string[] { nameof(Form.IdOwnerNavigation), nameof(Form.IdKeyFormTypeNavigation) };
            var data = await context.FormRepository.GetAsync(filters, properties);
            return data.Select(f => mappingManager.Create(f));
        }

        public async Task<Form> AddAsync(FormAddModel form, string currentUser)
        {
            ValidateFormNr(form.FormNr);
            ValidateIdOwner(form.IdOwner);
            ValidateDescription(form.Description);

            if (form == null)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.ObjectNull, nameof(form)));
            }

            if (string.IsNullOrEmpty(currentUser))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(currentUser)));
            }

            bool identicalFormNrExists = await context.FormRepository.CheckIfIdenticalFormNrExistAsync(form.FormNr);
            if (identicalFormNrExists)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(form.FormNr)));
            }

            var date = dateTimeManager.GetDateTime();
            form.CreateDate = date;
            form.CreateUser = currentUser;
            Form newForm = mappingManager.Create(form);
            context.FormRepository.Add(newForm);
            await context.SaveChangesAsync();
            return newForm;
        }

        public async Task UpdateAsync(FormUpdateModel formModel, string currentUser)
        {
            if (formModel == null)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.ObjectNull, nameof(Form)));
            }

            ValidateFormId(formModel.IdForm);
            ValidateIdOwner(formModel.IdOwner);
            ValidateFormNr(formModel.FormNr);
            ValidateDescription(formModel.Description);

            if (string.IsNullOrEmpty(currentUser))
            {
                throw new TMNotFoundException(string.Format(ErrorMessages.PropertyIsRequired, nameof(currentUser)));
            }

            var form = await context.FormRepository.FindAsync(formModel.IdForm);
            if (form == null)
            {
                throw new TMNotFoundException(string.Format(ErrorMessages.ObjectNotFound, nameof(Form)), ErrorCode.ObjectNotFound);
            }

            if (formModel.OldFormNr != formModel.FormNr)
            {
                bool identicalFormNrExists = await context.FormRepository.CheckIfIdenticalFormNrExistAsync(formModel.FormNr, formModel.IdForm);
                if (identicalFormNrExists)
                {
                    throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(formModel.FormNr)));
                }
            }

            form.FormNr = formModel.FormNr;
            form.IdOwner = formModel.IdOwner;
            form.Description = formModel.Description;
            form.Thickness = formModel.Thickness;
            form.Length = formModel.Length;
            form.Width = formModel.Width;
            form.Weight = formModel.Weight;
            form.ModifyUser = currentUser;
            form.ModifyDate = dateTimeManager.GetDateTime();

            context.FormRepository.Context.Entry(form).OriginalValues[nameof(Form.RowVersion)] = formModel.RowVersion;
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            ValidateFormId(id);
            await context.FormRepository.DeleteAsync(id);
            await context.SaveChangesAsync();
        }

        public async Task<TMValidationResult> ValidateCreationOrEditOfFormAsync(string formNr, Guid id)
        {
            if (string.IsNullOrEmpty(formNr))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(Form.FormNr)));
            }

            if (id == null)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(Form.IdForm)));
            }

            TMValidationResult tmValidationResult = new TMValidationResult() { IsValid = true };
            bool identicalFormNrExists = await context.FormRepository.CheckIfIdenticalFormNrExistAsync(formNr, id);
            if (identicalFormNrExists)
            {
                tmValidationResult.IsValid = false;
                tmValidationResult.ValidationMessage = ResourceFiles.Resources.FormInUse;
            }

            return tmValidationResult;
        }

        public async Task<TMValidationResult> ValidateDeletionOfFormAsync(Guid id)
        {
            TMValidationResult tmValidationResult = new TMValidationResult() { IsValid = true };
            bool result = await context.AuftragRepository.CheckIfDependencyBetweenAuftragAndFormExistsAsync(id);
            if (result)
            {
                tmValidationResult.IsValid = false;
                tmValidationResult.ValidationMessage = ResourceFiles.Resources.FormInUseOfAuftrag;
            }

            return tmValidationResult;
        }

        #region Private Methods
        private void ValidateIdOwner(Guid? idOwner)
        {
            if (!idOwner.HasValue || idOwner == Guid.Empty)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(Form.IdOwner)));
            }
        }

        private void ValidateFormNr(string formNr)
        {
            if (string.IsNullOrEmpty(formNr))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(Form.FormNr)));
            }

            if (formNr.Length != NumericConstants.FormNrLength || formNr.Contains(' '))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(Form.FormNr)));
            }
        }

        private void ValidateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(description)));
            }

            if (description.Length > NumericConstants.FormDescriptionMaxLength)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(description)));
            }
        }

        private void ValidateFormId(Guid formId)
        {
            if (formId == null || formId == Guid.Empty)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(Form.IdForm)));
            }
        }
        #endregion
    }
}
