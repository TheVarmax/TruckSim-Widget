using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Input;
using ETSOverlay;
using Xunit;

namespace TruckSimWidget.Tests
{
    public class SafeUrlLauncherTests
    {
        [Theory]
        [InlineData("https://billing.stripe.com/p/session/test", UrlPolicy.StripePortal)]
        [InlineData("https://BILLING.STRIPE.COM/p/session/test", UrlPolicy.StripePortal)]
        [InlineData("https://trucksim.uk/", UrlPolicy.OfficialWebsite)]
        [InlineData("https://trucksim.uk/donate", UrlPolicy.OfficialWebsite)]
        [InlineData("https://github.com/TheVarmax/TruckSim-Widget/releases", UrlPolicy.GitHubRelease)]
        [InlineData("https://example.com/path?q=a%20b", UrlPolicy.Markdown)]
        public void ValidLinksReachBrowserWithCanonicalHttpsUri(string value, UrlPolicy policy)
        {
            ProcessStartInfo? launched = null;
            var logs = new List<string>();
            Assert.True(SafeUrlLauncher.TryOpen(value, policy, logs.Add, info => launched = info));
            Assert.NotNull(launched);
            Assert.True(launched.UseShellExecute);
            Assert.Equal(new Uri(value).AbsoluteUri, launched.FileName);
            Assert.Empty(logs);
        }

        public static IEnumerable<object?[]> UnsafeLinks()
        {
            string?[] values = { null, "", " ", "not a URI", "/relative", "http://billing.stripe.com/test",
                "file:///C:/Windows/System32/calc.exe", @"\\host\share\file.exe", @"C:\Windows\calc.exe",
                "mailto:test@example.com", "steam://run/1", "javascript:alert(1)", "https://",
                "https://user@billing.stripe.com/test", "https://user:pass@billing.stripe.com/test",
                "https://@billing.stripe.com/test", "https://user@example.com/", "https://billing.stripe.com/\nfile.exe", " https://billing.stripe.com/",
                @"https://billing.stripe.com\@evil.com/" };
            foreach (var policy in Enum.GetValues<UrlPolicy>())
                foreach (var value in values) yield return new object?[] { value, policy };
        }

        [Theory]
        [MemberData(nameof(UnsafeLinks))]
        public void UnsafeLinksNeverReachShellAndLogWithoutInput(string? value, UrlPolicy policy)
        {
            var logs = new List<string>();
            bool launched = false;
            Assert.False(SafeUrlLauncher.TryOpen(value, policy, logs.Add, _ => launched = true));
            Assert.False(launched);
            Assert.Equal(new[] { "[URL] Blocked invalid or untrusted link." }, logs);
        }

        [Theory]
        [InlineData("https://evil.com")]
        [InlineData("https://billing.stripe.com.evil.com")]
        [InlineData("https://evil.com/?x=billing.stripe.com")]
        [InlineData("https://billing.stripe.com./test")]
        [InlineData("https://trucksim.uk")]
        public void StripeRejectsOtherHosts(string value)
        {
            Assert.False(SafeUrlLauncher.TryValidate(value, UrlPolicy.StripePortal, out var uri));
            Assert.Null(uri);
        }

        [Theory]
        [InlineData("https://trucksim.uk.evil.com", UrlPolicy.OfficialWebsite)]
        [InlineData("https://github.com.evil.com", UrlPolicy.GitHubRelease)]
        [InlineData("https://example.com", UrlPolicy.OfficialWebsite)]
        [InlineData("https://trucksim.uk", UrlPolicy.GitHubRelease)]
        [InlineData("https://example.com", (UrlPolicy)99)]
        public void HostPoliciesFailClosed(string value, UrlPolicy policy)
        {
            Assert.False(SafeUrlLauncher.TryValidate(value, policy, out _));
        }

        [Fact]
        public void BrowserFailureIsContainedAndSensitiveDetailsAreNotLogged()
        {
            var logs = new List<string>();
            Assert.False(SafeUrlLauncher.TryOpen("https://billing.stripe.com/p/session/secret", UrlPolicy.StripePortal,
                logs.Add, _ => throw new InvalidOperationException("secret details")));
            Assert.Equal(new[] { "[URL] Failed to open browser." }, logs);
        }

        [Theory]
        [InlineData("https://example.com", true)]
        [InlineData("file:///C:/Windows/System32/calc.exe", false)]
        [InlineData(@"\\host\share\file.exe", false)]
        [InlineData("steam://run/1", false)]
        public void MarkdownRendererRoutesLinkParametersThroughValidatedNavigation(string value, bool expected)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var document = new MdXaml.Markdown().Transform($"[link]({value})");
                    var paragraph = Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
                    var link = Assert.IsType<Hyperlink>(paragraph.Inlines.FirstInline);
                    Assert.Same(NavigationCommands.GoToPage, link.Command);
                    var logs = new List<string>();
                    bool launched = false;
                    Assert.Equal(expected, SafeUrlLauncher.TryOpen(Assert.IsType<string>(link.CommandParameter),
                        UrlPolicy.Markdown, logs.Add, _ => launched = true));
                    Assert.Equal(expected, launched);
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
