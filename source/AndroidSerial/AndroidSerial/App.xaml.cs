using AndroidSerial.Views;
using Prism;
using Prism.DryIoc;
using Prism.Ioc;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using Acr.UserDialogs;

[assembly: XamlCompilation (XamlCompilationOptions.Compile)]

namespace AndroidSerial
{
	public partial class App : PrismApplication
	{
		public App (IPlatformInitializer initializer) : base(initializer) { }

	    protected override async void OnInitialized()
	    {
	        InitializeComponent();
	        await NavigationService.NavigateAsync($"{nameof(NavigationPage)}/{nameof(MainPage)}");
	    }

	    protected override void RegisterTypes(IContainerRegistry containerRegistry)
	    {
	        containerRegistry.RegisterForNavigation<NavigationPage>();
            containerRegistry.RegisterForNavigation<MainPage>();
            containerRegistry.RegisterForNavigation<NewItemPage>();
	        containerRegistry.RegisterForNavigation<ItemDetailPage>();

            containerRegistry.RegisterInstance(typeof(IUserDialogs), UserDialogs.Instance);
	    }

        protected override void OnStart ()
		{
			// Handle when your app starts
		}

		protected override void OnSleep ()
		{
			// Handle when your app sleeps
		}

	    protected override void OnResume ()
		{
			// Handle when your app resumes
		}
	}
}
