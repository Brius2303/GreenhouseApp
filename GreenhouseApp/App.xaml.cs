namespace GreenhouseApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new MainPage();
        }
        protected override void OnStart()
        {
            base.OnStart();
            // Код для начала работы приложения
            Preferences.Remove("SelectedGreenhouseIp");
            Preferences.Remove("SelectedGreenhousePort");
        }
    }
}
