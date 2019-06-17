using Caliburn.Micro;
using System;
using TM.WPF.DataModels;
using TM.WPF.Events;
using TM.WPF.ResourcesFiles;

namespace TM.WPF.ViewModels
{
    public class EditBankViewModel : Screen
    {
        private OwnerDataModel bank;

        public EditBankViewModel()
        {
            DisplayName = Resources.HeaderEditBank;
        }

        public event EventHandler SetFocus;

        public OwnerDataModel Bank
        {
            get { return bank; }
            set
            {
                bank = value;
                NotifyOfPropertyChange();
                SetFocusInEditBankView();
            }
        }

        public void SetFocusInEditBankView()
        {
            if (Bank != null && Bank.IsDirty)
            {
                SetFocus(this, new SetFocusInEditBankViewArgs());
            }
        }
    }
}
