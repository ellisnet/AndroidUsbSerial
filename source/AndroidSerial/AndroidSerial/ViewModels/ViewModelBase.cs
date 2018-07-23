using System;
using Acr.UserDialogs;
using Prism.Mvvm;
using Prism.Navigation;

namespace AndroidSerial.ViewModels
{
    public class ViewModelBase : BindableBase, INavigationAware, IDestructible
    {
        protected INavigationService NavigationService;
        protected IUserDialogs DialogService;

        protected void NotifyPropertyChanged(string propertyName) => RaisePropertyChanged(propertyName);

        #region INavigationAware implementation

        public virtual void OnNavigatedFrom(NavigationParameters parameters) { }

        public virtual void OnNavigatedTo(NavigationParameters parameters) { }

        public virtual void OnNavigatingTo(NavigationParameters parameters) { }

        #endregion

        public ViewModelBase(INavigationService navigationService, IUserDialogs userDialogs)
        {
            NavigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            DialogService = userDialogs ?? throw new ArgumentNullException(nameof(userDialogs));
        }

        #region IDestructible implementation

        public virtual void Destroy() { }

        #endregion
    }

    //This special class will allow us to have IntelliSense while we are
    //  editing our XAML view files in Visual Studio with ReSharper.  I.e. it is for design-time only, 
    //  and does nothing at compile-time or run-time.
    //  For more info, check these pages -
    //  https://github.com/PrismLibrary/Prism/issues/986
    //  https://gist.github.com/nuitsjp/7478bfc7eba0f2a25b866fa2e7e9221d
    //  https://blog.nuits.jp/enable-intellisense-for-viewmodel-members-with-prism-for-xamarin-forms-2f274e7c6fb6
    public static class DesignTimeViewModelLocator
    {
        public static MainPageViewModel MainPage => null;
        public static ItemDetailPageViewModel ItemDetailPage => null;
        public static NewItemPageViewModel NewItemPage => null;
    }

    public static class NavParamKeys
    {
        public static string DetailItem => "DetailItem";
        public static string NewItem => "NewItem";
    }
}
