using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace FalconsFactionMonitor.Helpers
{
    public class BaseWindow : Window
    {
        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            await FadeInAsync(0.0, 1.0, 300);
        }

        private async Task FadeInAsync(double from, double to, int durationMs)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                FillBehavior = FillBehavior.Stop
            };

            var tcs = new TaskCompletionSource<bool>();

            animation.Completed += (s, e) =>
            {
                this.Opacity = to;
                tcs.SetResult(true);
            };

            this.BeginAnimation(Window.OpacityProperty, animation);
            await tcs.Task;
        }
    }
}
