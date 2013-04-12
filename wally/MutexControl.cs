using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace wally
{
    public static class ControlAnimationExtensionMethods
    {
        public static void FadeIn(this UIElement targetControl)
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(1.5)));
            Storyboard.SetTarget(fadeInAnimation, targetControl);
            Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));
            Storyboard sb = new Storyboard();
            sb.Children.Add(fadeInAnimation);
            sb.Begin();
        }

        public static void FadeOut(this UIElement targetControl)
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(4)));
            Storyboard.SetTarget(fadeInAnimation, targetControl);
            Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));
            Storyboard sb = new Storyboard();
            sb.Children.Add(fadeInAnimation);
            sb.Begin();

        }
    }
}
