using System;
using System.Threading.Tasks;
using System.Windows;

namespace FalconsFactionMonitor.Windows
{
    public static class Animations
    {
        public static async Task FadeWindowAsync(Window window, double from, double to, int durationMs)
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
            };

            var tcs = new TaskCompletionSource<bool>();

            animation.Completed += (s, e) =>
            {
                window.Opacity = to;
                tcs.SetResult(true);
            };

            window.BeginAnimation(Window.OpacityProperty, animation);

            await tcs.Task;
        }
    }
}
