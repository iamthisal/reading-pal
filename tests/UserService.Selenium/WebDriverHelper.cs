using OpenQA.Selenium.Chrome;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace UserService.SeleniumTests
{
    public static class WebDriverHelper
    {
        public static ChromeOptions CreateOptions()
        {
            var options = new ChromeOptions();

            bool isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
            if (isCi)
            {
                options.AddArgument("--headless=new");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--window-size=1920,1080");
            }
            else
            {
                options.AddArgument("--start-maximized");
            }

            return options;
        }
    }
}

