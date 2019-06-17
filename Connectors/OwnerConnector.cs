using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TM.Service.Core.Infrastructure;
using TM.Service.Core.Infrastructure.Extensions;
using TM.Service.Core.Repositories;
using TM.Service.Core.ResourceFiles;
using TM.Service.ORM.Models;
using TM.Shared.Filters;
using TM.Shared.Models;
using ServerShared.Errors;
using ServerShared.Exceptions;
using ServerShared.Core.Infrastructure;

namespace TM.Service.Core.Connectors
{
    public class OwnerConnector : IOwnerConnector
    {
        private readonly IUnitOfWork context;
        private readonly IMappingManager mappingManager;
        private readonly IDateTimeManager dateTimeManager;

        public OwnerConnector(IUnitOfWorkFactory unitOfWorkFactory,
            IMappingManager mappingManager,
            IDateTimeManager dateTimeManager)
        {
            context = unitOfWorkFactory.Create();
            this.mappingManager = mappingManager;
            this.dateTimeManager = dateTimeManager;
        }

        public async Task UpdateAsync(OwnerModel owner, string currentUser)
        {
            ValidateOwner(owner);
            if (owner.IdOwner == null || owner.IdOwner == Guid.Empty)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(owner.IdOwner)));
            }

            ValidateCurrentUser(currentUser);

            bool isOwnerCodeUnique = await context.OwnerRepository.IsOwnerCodeUniqueAsync(owner.IdOwner, owner.OwnerCode);
            if (!isOwnerCodeUnique)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.OwnerCode)));
            }

            var currentOwner = await context.OwnerRepository.FindAsync(owner.IdOwner);
            if (currentOwner == null)
            {
                throw new TMNotFoundException(string.Format(ErrorMessages.ObjectNotFound, nameof(owner)));
            }

            if (!string.IsNullOrEmpty(owner.OwnerCodeOfParent))
            {
                Owner newMainOwner = await context.OwnerRepository.FindOwnerByOwnerCodeAsync(owner.OwnerCodeOfParent);
                if (newMainOwner == null || newMainOwner.IdMainOwner != null || newMainOwner.SystemCode != owner.SystemCode)
                {
                    throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.OwnerCodeOfParent)));
                }

                currentOwner.IdMainOwner = newMainOwner.IdOwner;
            }

            //This code checks if owner is a branch of a bank and if they have different system codes potentially 
            //this whole changing of system code option could be removed (depending on what Patrick says).
            if (owner.IdMainOwner.HasValue)
            {
                Owner mainOwner = await context.OwnerRepository.FindAsync((Guid)owner.IdMainOwner);
                if (mainOwner == null || string.IsNullOrEmpty(mainOwner.SystemCode) || mainOwner.SystemCode != owner.SystemCode)
                {
                    throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.SystemCode)));
                }
            }

            currentOwner.OwnerCode = owner.OwnerCode;
            currentOwner.SystemCode = owner.SystemCode;
            currentOwner.Name1 = owner.Name1;
            currentOwner.Name2 = owner.Name2;
            currentOwner.ModifyUser = currentUser;
            currentOwner.ModifyDate = dateTimeManager.GetDateTime();

            context.OwnerRepository.Context.Entry(currentOwner).OriginalValues[nameof(Owner.RowVersion)] = owner.RowVersion;
            await context.SaveChangesAsync();
        }

        public async Task<List<OwnerModel>> GetOwnersWithParentOwnerCodeAsync(OwnerModelFilter filter)
        {
            return await context.OwnerRepository.GetOwnersWithParentOwnerCodeAsync(filter.Create());
        }

        public async Task<List<OwnerModel>> GetOwnersByFilterAsync(OwnerFilter ownerFilter)
        {
            List<Owner> owners = await context.OwnerRepository.GetOwnersAsync(ownerFilter.Create());
            return owners.Select(owner => mappingManager.Create(owner)).ToList();
        }

        public async Task<IEnumerable<OwnerModel>> GetAsync()
        {
            List<Owner> owners = await context
                .OwnerRepository
                .GetAsync();

            return owners.Select(owner => mappingManager.Create(owner));
        }

        public async Task<OwnerModel> GetAsync(Guid id)
        {
            if (id == Guid.Empty || id == null)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(id)));
            }

            var existingEntity = await context
                    .OwnerRepository
                    .FindAsync(id);
            if (existingEntity == null)
            {
                throw new TMNotFoundException(string.Format(ErrorMessages.ObjectNotFound, nameof(Owner)));
            }

            return mappingManager.Create(existingEntity);
        }

        public async Task<OwnerModel> GetOwnerWithParentOwnerCodeAsync(Guid id)
        {
            if (id == Guid.Empty || id == null)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(id)));
            }

            OwnerModel existingEntity = mappingManager.Create(await context.OwnerRepository.FindAsync(id));
            if (existingEntity == null)
            {
                throw new TMNotFoundException(string.Format(ErrorMessages.ObjectNotFound, nameof(Owner)));
            }

            if (existingEntity.IdMainOwner != null)
            {
                Owner parent = await context.OwnerRepository.FindAsync(existingEntity.IdMainOwner);
                if (parent != null && !string.IsNullOrEmpty(parent.OwnerCode))
                {
                    existingEntity.OwnerCodeOfParent = parent.OwnerCode;
                }
            }

            return existingEntity;
        }

        public async Task<IEnumerable<OwnerChooseModel>> GetAsync(OwnerFilter filter)
        {
            var data = await context.OwnerRepository.GetOwnersAsync(filter.Create());
            return data.Select(owner => mappingManager.CreateOwnerChoose(owner));
        }

        public async Task<OwnerModel> GetStandardOwnerAsync(string systemCode)
        {
            var filter = new OwnerFilter
            {
                Standard = true,
                SystemCode = systemCode
            };

            var data = await context.OwnerRepository.GetOwnersAsync(filter.Create());

            if (data.Count < 1)
            {
                throw new TMConfigurationException(string.Format(nameof(Resources.ERR_31_ErrorInConfigurationData), Resources.NoStandardOwnerConfigured), ErrorCode.ConfigurationDataError);
            }

            if (data.Count > 1)
            {
                throw new TMConfigurationException(string.Format(nameof(Resources.ERR_31_ErrorInConfigurationData), Resources.MoreThanOneStandardBank), ErrorCode.ConfigurationDataError);
            }

            return mappingManager.Create(data.First());
        }

        public async Task<OwnerModel> AddAsync(OwnerModel owner, string currentUser)
        {
            ValidateCurrentUser(currentUser);
            ValidateOwner(owner);

            bool isOwnerCodeUsed = await context.OwnerRepository.CheckIfOwnerExistsAsync(owner.OwnerCode);
            if (isOwnerCodeUsed)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.OwnerCode)));
            }

            Owner newOwner = new Owner();
            if (!string.IsNullOrEmpty(owner.OwnerCodeOfParent))
            {
                Owner newMainOwner = await context.OwnerRepository.FindOwnerByOwnerCodeAsync(owner.OwnerCodeOfParent);
                if (newMainOwner == null || newMainOwner.IdMainOwner != null || newMainOwner.SystemCode != owner.SystemCode)
                {
                    throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.OwnerCodeOfParent)));
                }

                newOwner.IdMainOwner = newMainOwner.IdOwner;
            }

            var dateNow = dateTimeManager.GetDateTime();
            newOwner.CreateDate = dateNow;
            newOwner.CreateUser = currentUser;
            newOwner.Name1 = owner.Name1;
            newOwner.Name2 = owner.Name2;
            newOwner.SystemCode = owner.SystemCode;
            newOwner.OwnerCode = owner.OwnerCode;

            context.OwnerRepository.Add(newOwner);
            await context.SaveChangesAsync();
            return mappingManager.Create(newOwner);
        }

        public async Task DeleteAsync(Guid id)
        {
            if (id == null || id == Guid.Empty)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(id)));
            }

            Owner owner = await context.OwnerRepository.FindAsync(id);
            if (owner == null)
            {
                throw new TMNotFoundException(string.Format(ErrorMessages.ObjectNotFound, nameof(owner)));
            }

            if (owner.Standard)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.Standard)));
            }

            bool result = await context.OwnerRepository.FindIfOwnerHasBranchesAsync(id);
            if (result)
            {
                throw new InvalidOperationException(Resources.BankHasBranch);
            }

            result = await FindIfOwnerDependenciesExistAsync(owner.IdOwner);
            if (result)
            {
                throw new InvalidOperationException(Resources.BankHasTemplate);
            }

            await context.OwnerRepository.DeleteAsync(id);
            await context.SaveChangesAsync();
        }

        public async Task<TMValidationResult> ValidateCreationOrEditOfOwnerAsync(Guid id, string ownerCode, string parentsOwnerCode, string ownersSystemCode)
        {
            if (string.IsNullOrEmpty(ownerCode))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(ownerCode)));
            }

            if (string.IsNullOrEmpty(ownersSystemCode))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(ownersSystemCode)));
            }

            string message = string.Empty;
            bool result = await context.OwnerRepository.IsOwnerCodeUniqueAsync(id, ownerCode);
            if (!result)
            {
                //Need translation
                message = "Bank Code already exists.";
            }

            if (!string.IsNullOrEmpty(parentsOwnerCode) && result)
            {
                Owner parentOwner = await context.OwnerRepository.FindOwnerByOwnerCodeAsync(parentsOwnerCode);
                if (parentOwner == null)
                {
                    result = false;
                    //Need translation
                    message = "!Parent Bank does not exist.TODO TRANSLATE THIS!";
                }
                else if (parentOwner.SystemCode != ownersSystemCode)
                {
                    result = false;
                    //Need translation
                    message = "!Parent Bank belongs to other Mandant.TODO TRANSLATE THIS!";
                }
            }

            TMValidationResult validationResult = new TMValidationResult();
            validationResult.IsValid = result;
            validationResult.ValidationMessage = message;
            return validationResult;
        }

        public async Task<TMValidationResult> ValidateDeletionOfOwnerAsync(Guid id)
        {
            if (id == null)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(id)));
            }

            TMValidationResult validationResult = new TMValidationResult() { IsValid = true };
            string message = string.Empty;
            bool result = await context.OwnerRepository.FindIfOwnerHasBranchesAsync(id);
            if (result)
            {
                message = Resources.BankHasBranch;
            }
            else
            {
                result = await FindIfOwnerDependenciesExistAsync(id);
                message = Resources.BankHasTemplate;
            }
            validationResult.IsValid = !result;
            validationResult.ValidationMessage = message;
            return validationResult;
        }

        #region Private Methods
        private async Task<bool> FindIfOwnerDependenciesExistAsync(Guid id)
        {
            if (id == null)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(id)));
            }

            bool result = await context.OwnerRepository.FindIfOwnerAuftragDependencyExistsAsync(id);
            result = !result && await context.OwnerRepository.FindIfOwnerGruppeDependencyExistsAsync(id);
            result = !result && await context.OwnerRepository.FindIfOwnerTemplateDependencyExistsAsync(id);
            result = !result && await context.OwnerRepository.FindIfOwnerModuleDependencyExistsAsync(id);
            result = !result && await context.OwnerRepository.FindIfOwnerPositionDependencyExistsAsync(id);
            return result;
        }

        private void ValidateOwner(OwnerModel owner)
        {
            if (owner == null)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(owner)));
            }

            if (string.IsNullOrEmpty(owner.Name1))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(owner.Name1)));
            }

            if (owner.Name1.Length > 30)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.Name1)));
            }

            if (!string.IsNullOrEmpty(owner.Name2) && owner.Name2.Length > 30)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.Name2)));
            }

            if (string.IsNullOrEmpty(owner.SystemCode))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(owner.SystemCode)));
            }

            if (string.IsNullOrEmpty(owner.OwnerCode))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsRequired, nameof(owner.OwnerCode)));
            }

            if (owner.OwnerCode.Contains(" "))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.OwnerCode)));
            }

            if (owner.OwnerCode.Trim().Length > 15)
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(owner.OwnerCode)));
            }
        }

        private void ValidateCurrentUser(string currentUser)
        {
            if (string.IsNullOrEmpty(currentUser))
            {
                throw new TMNotValidException(string.Format(ErrorMessages.PropertyIsIncorrect, nameof(currentUser)));
            }
        }

        #endregion
    }
}
