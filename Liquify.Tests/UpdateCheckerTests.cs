using pyrochild.effects.common;
using System;
using Xunit;

namespace pyrochild.effects.liquify.tests
{
    public class UpdateCheckerTests
    {
        private static readonly Version Current = new Version(5, 1, 2610, 614);

        [Theory]
        [InlineData("5.1.2610.0615")]
        [InlineData("5.1.2610.615")]
        [InlineData("5.1.2611.0100\r\n")]
        [InlineData("5.2.2601.0100")]
        public void NewerVersionIsReported(string text)
        {
            Assert.Equal(Version.Parse(text.Trim()), UpdateChecker.ParseNewer(text, Current));
        }

        // release.yml writes the version with its leading zero, as it appears in the tag; it
        // has to compare the same with or without
        [Theory]
        [InlineData("5.1.2610.0614")]
        [InlineData("5.1.2610.614")]
        [InlineData("5.1.2609.3023")]
        [InlineData("5.0.2612.3123")]
        public void SameOrOlderVersionIsNot(string text)
        {
            Assert.Null(UpdateChecker.ParseNewer(text, Current));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Not Found")]
        [InlineData("<html>")]
        public void AnythingElseIsIgnored(string text)
        {
            Assert.Null(UpdateChecker.ParseNewer(text, Current));
        }

        [Fact]
        public void TheHigherOfReleaseAndBetaWins()
        {
            AvailableUpdate release = new AvailableUpdate(new Version(5, 1, 2610, 700), "release");
            AvailableUpdate olderBeta = new AvailableUpdate(new Version(5, 1, 2610, 650), "beta");
            AvailableUpdate sameBeta = new AvailableUpdate(new Version(5, 1, 2610, 700), "beta");
            AvailableUpdate newerBeta = new AvailableUpdate(new Version(5, 1, 2610, 800), "beta");

            Assert.Same(release, UpdateChecker.Newer(release, olderBeta));
            Assert.Same(release, UpdateChecker.Newer(release, sameBeta));
            Assert.Same(newerBeta, UpdateChecker.Newer(release, newerBeta));
            Assert.Same(release, UpdateChecker.Newer(release, null));
            Assert.Same(olderBeta, UpdateChecker.Newer(null, olderBeta));
            Assert.Null(UpdateChecker.Newer(null, null));
        }

        private const string Repository = "https://github.com/someone/plugin/";

        // the parts of the API's release list that are read
        private static string ReleaseList(string page, params string[] assetUrls)
        {
            string assets = string.Join(",", Array.ConvertAll(assetUrls, url =>
                "{\"name\":\"" + url.Substring(url.LastIndexOf('/') + 1) + "\",\"browser_download_url\":\"" + url + "\"}"));
            return "[{\"html_url\":\"" + page + "\",\"prerelease\":true,\"assets\":[" + assets + "]}]";
        }

        [Fact]
        public void ReleaseWithVersionFileIsFound()
        {
            string json = ReleaseList(
                Repository + "releases/tag/v1-beta",
                Repository + "releases/download/v1-beta/Plugin.zip",
                Repository + "releases/download/v1-beta/version.txt");

            Assert.True(UpdateChecker.TryFindRelease(json, Repository, out string versionUrl, out string pageUrl));
            Assert.Equal(Repository + "releases/download/v1-beta/version.txt", versionUrl);
            Assert.Equal(Repository + "releases/tag/v1-beta", pageUrl);
        }

        [Fact]
        public void ReleaseWithoutVersionFileIsNot()
        {
            string json = ReleaseList(Repository + "releases/tag/v1", Repository + "releases/download/v1/Plugin.zip");

            Assert.False(UpdateChecker.TryFindRelease(json, Repository, out _, out _));
        }

        // the page is opened in the browser, so it has to be the repository's own
        [Theory]
        [InlineData("https://example.com/releases/tag/v1", "https://github.com/someone/plugin/releases/download/v1/version.txt")]
        [InlineData("https://github.com/someone/plugin/releases/tag/v1", "https://example.com/version.txt")]
        public void AddressesOutsideTheRepositoryAreRefused(string page, string asset)
        {
            Assert.False(UpdateChecker.TryFindRelease(ReleaseList(page, asset), Repository, out _, out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("{\"message\":\"API rate limit exceeded\"}")]
        [InlineData("<html>")]
        public void AnythingButAReleaseListIsIgnored(string json)
        {
            Assert.False(UpdateChecker.TryFindRelease(json, Repository, out _, out _));
        }
    }
}
