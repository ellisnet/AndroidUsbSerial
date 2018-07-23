using Acr.UserDialogs;
using SharedSerial.Models;
using Prism.Navigation;

namespace AndroidSerial.ViewModels
{
    public class ItemDetailPageViewModel : ViewModelBase
    {
        #region Bindable properties

        public string Title { get; set; }

        private Item _item;
        public Item Item
        {
            get => _item;
            set => SetProperty(ref _item, value);
        }

        #endregion

        public override void OnNavigatingTo(NavigationParameters parameters)
        {
            base.OnNavigatingTo(parameters);
            if (parameters?[NavParamKeys.DetailItem] is Item item)
            {
                Title = item.Text;
                NotifyPropertyChanged(nameof(Title));
                Item = item;
            }
        }

        public ItemDetailPageViewModel(
            INavigationService navigationService, 
            IUserDialogs userDialogs) 
            : base(navigationService, userDialogs)
        { }

        public override void Destroy()
        {
            _item = null;
            base.Destroy();
        }
    }
}
