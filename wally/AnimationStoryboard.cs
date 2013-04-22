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
    /// <summary>
    /// A helper class to allow FadeIn and FadeOut animations on XAML-Elements programatically.
    /// Orginal by http://eclipsed4utoo.azurewebsites.net/wpf-create-animation-programmatically/
    /// </summary>
    public static class ControlAnimationExtensionMethods
    {
        public static Storyboard FadeIn(this UIElement targetControl)
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(1.5)));
            Storyboard.SetTarget(fadeInAnimation, targetControl);
            Storyboard.SetTargetPrsoperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));
            Storyboard sb = new Storyboard();
            sb.Children.Add(fadeInAnimation);

            return sb;
        }

        public static Storyboard FadeOut(this UIElement targetControl)
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(4)));
            Storyboard.SetTarget(fadeInAnimation, targetControl);
            Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));
            Storyboard sb = new Storyboard();
            sb.Children.Add(fadeInAnimation);

            return sb;

        }

    }
}
