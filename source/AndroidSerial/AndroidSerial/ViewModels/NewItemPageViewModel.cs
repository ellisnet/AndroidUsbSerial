using Acr.UserDialogs;
using Prism.Commands;
using Prism.Navigation;
using SharedSerial.Models;

namespace AndroidSerial.ViewModels
{
    public class NewItemPageViewModel : ViewModelBase
    {
        #region Bindable properties

        public Item Item { get; set; }

        #endregion

        #region Commands and their implementations

        private DelegateCommand _saveItemCommand;
        public DelegateCommand SaveItemCommand => _saveItemCommand
            ?? (_saveItemCommand = new DelegateCommand(async () => await NavigationService.GoBackAsync(new NavigationParameters
                { { NavParamKeys.NewItem, Item } })));

        #endregion

        public NewItemPageViewModel(
            INavigationService navigationService,
            IUserDialogs userDialogs)
            : base(navigationService, userDialogs)
        {
            Item = new Item
            {
                Text = "",
                Description = ""
            };
        }

        public override void Destroy()
        {
            Item = null;
            base.Destroy();
        }
    }
}
