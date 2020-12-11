﻿using Sentry.Integrations;
using Sentry.Protocol;
using Xamarin.Forms.Internals;
using Sentry.Xamarin.Forms.Internals;
using Xamarin.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using Sentry.Xamarin.Forms.Extensions;
using System.Linq;

namespace Sentry.Xamarin.Forms
{
    public class SentryXamarinFormsIntegration : ISdkIntegration
    {
        private NativeExceptionHandler _nativeHandler;
        private string _previousPageName;
        internal static bool LogXamlErrors { get; set; } = true;
        internal static DelegateLogListener XamlLogger;

        public void Register(IHub hub, SentryOptions options)
        {
            options.AddEventProcessor(new XamarinFormsEventProcessor(options));

            _nativeHandler = new NativeExceptionHandler();

#if !NETSTANDARD
            options.AddEventProcessor(new NativeEventProcessor(options));
#endif
            XamlLogger = new DelegateLogListener((arg1, arg2) =>
            {
                if (LogXamlErrors)
                {
                    SentrySdk.AddBreadcrumb(arg2, $"XamlLogger.{arg1}", level: BreadcrumbLevel.Warning);
                }
            });

            if (LogXamlErrors)
            {
                Log.Listeners.Add(XamlLogger);
            }

            //If initialized from the Android/IOS project, the current application is not going to be set in time, so wait a bit...
            Task.Run(async () =>
            {
                for (int i = 0; i < 5 && Application.Current is null; i++)
                {
                    await Task.Delay(1000);
                }
                if (Application.Current is null)
                {
                    options.DiagnosticLogger.Log(SentryLevel.Warning, "Sentry.Xamarin.Forms timeout for tracking Application.Current. Navigation tracking is going to be disabled");
                }
                else
                {
                    Application.Current.PageAppearing += Current_PageAppearing;
                    Application.Current.PageDisappearing += Current_PageDisappearing;
                    Application.Current.RequestedThemeChanged += Current_RequestedThemeChanged;
                }
            });
        }

        private void Current_RequestedThemeChanged(object sender, AppThemeChangedEventArgs e)
        {
            SentrySdk.AddBreadcrumb(e.RequestedTheme.ToString(), "AppTheme.Change", level: BreadcrumbLevel.Info);
        }

        private void Current_PageDisappearing(object sender, Page e)
        {
            var type = e.GetType();
            if (type.BaseType.Name.StartsWith("PopupPage"))
            {
                SentrySdk.AddBreadcrumb($"{type.Name} Disappearing.",
                    "Xamarin.Popup", level: BreadcrumbLevel.Info);
            }
        }

        private void Current_PageAppearing(object sender, Page e)
        {
            var pageType = e.GetType();
            if (_previousPageName != null && _previousPageName != pageType.Name)
            {
                if (pageType.Name is "NavigationPage")
                {
                    return;
                }
                if (pageType.BaseType.Name is "PopupPage")
                {
                    SentrySdk.AddBreadcrumb($"{pageType.Name} Appearing.",
                        "Xamarin.Popup", level: BreadcrumbLevel.Info);
                    return;
                }
                else
                {
                    SentrySdk.AddBreadcrumb(null,
                        "navigation",
                        "navigation",
                        new Dictionary<string, string>() { { "from", $"/{_previousPageName}" }, { "to", $"/{pageType.Name}" } });
                }
            }
            _previousPageName = pageType.Name;
        }
    }
}
