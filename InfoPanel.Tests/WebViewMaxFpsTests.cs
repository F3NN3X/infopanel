using InfoPanel.Models;
using InfoPanel.Views.Common;
using Xunit;

namespace InfoPanel.Tests
{
    public class WebViewMaxFpsTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(-5, 0)]
        [InlineData(30, 30)]
        [InlineData(1000, Profile.MaxWebViewMaxFps)]
        public void Max_fps_is_off_or_within_range(int value, int expected)
        {
            var profile = new Profile { WebViewMaxFps = value };

            Assert.Equal(expected, profile.WebViewMaxFps);
        }

        [Fact]
        public void Script_paces_frames_at_the_given_rate()
        {
            var script = DisplayWindow.WebViewMaxFpsScript(30);

            Assert.Contains("const interval = 1000 / 30;", script);
            Assert.Contains("window.requestAnimationFrame = ", script);
            Assert.Contains("window.cancelAnimationFrame = ", script);
        }
    }
}
