using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SelfControl.Core;

namespace SelfControl.App.Views;

/// <summary>The hourly pause: a small prompt in the corner, then guided stretches or the eye exercise.</summary>
public static class BreakWindows
{
    static readonly IBrush Dim = new SolidColorBrush(Color.Parse("#9A9A9A"));
    static Window? _prompt;

    /// <summary>L.T("Time for a break") in the bottom-right corner, on top but not stealing the whole screen.</summary>
    public static void ShowPrompt()
    {
        if (_prompt != null) return;
        var stretch = RoutineDialogs.Btn(L.T("Stretch"), true);
        var eyes = RoutineDialogs.Btn(L.T("Eyes"), true);
        var later = RoutineDialogs.Btn(L.T("Not now"));
        var win = new Window
        {
            Title = "SelfControl", Width = 360, SizeToContent = SizeToContent.Height, CanResize = false, Topmost = true,
            ShowInTaskbar = false, SystemDecorations = SystemDecorations.BorderOnly, Background = new SolidColorBrush(Color.Parse("#0D0D0D")),
            Content = new StackPanel
            {
                Margin = new Thickness(18), Spacing = 10,
                Children =
                {
                    new TextBlock { Text = L.T("Time for a break"), FontSize = 18, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = L.T("You've been at the computer for an hour. How about stretching or resting your eyes for a minute?"), TextWrapping = TextWrapping.Wrap, Foreground = Dim },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { stretch, eyes, later } },
                },
            },
        };
        win.Opened += (_, _) =>
        {
            if (win.Screens.Primary is { } s)
            {
                var wa = s.WorkingArea; var scale = s.Scaling;
                win.Position = new PixelPoint(wa.Right - (int)((win.Bounds.Width + 24) * scale), wa.Bottom - (int)((win.Bounds.Height + 24) * scale));
            }
        };
        win.Closed += (_, _) => _prompt = null;
        stretch.Click += (_, _) => { win.Close(); ShowStretches(); };
        eyes.Click += (_, _) => { win.Close(); ShowEyes(); };
        later.Click += (_, _) => win.Close();
        _prompt = win;
        win.Show();
    }

    static Window FullScreen(string title, Control content)
    {
        var close = RoutineDialogs.Btn(L.T("Close"));
        var root = new Grid();
        root.Children.Add(content);
        close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(24);
        root.Children.Add(close);
        var win = new Window { Title = title, WindowState = WindowState.Maximized, Background = Brushes.Black, Content = root };
        close.Click += (_, _) => win.Close();
        win.KeyDown += (_, e) => { if (e.Key == Key.Escape) win.Close(); };
        return win;
    }

    /// <summary>One stretch at a time with a countdown; moves on by itself.</summary>
    public static Window ShowStretches()
    {
        var steps = Exercises.Stretches;
        var title = new TextBlock { FontSize = 40, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        var text = new TextBlock { FontSize = 22, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 700, HorizontalAlignment = HorizontalAlignment.Center };
        var count = new TextBlock { FontSize = 72, FontWeight = FontWeight.Light, HorizontalAlignment = HorizontalAlignment.Center };
        var progress = new TextBlock { Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center };
        var next = RoutineDialogs.Btn(L.T("Skip")); next.HorizontalAlignment = HorizontalAlignment.Center;
        var panel = new StackPanel { Spacing = 18, VerticalAlignment = VerticalAlignment.Center, Children = { progress, title, text, count, next } };
        var win = FullScreen(L.T("Stretch"), panel);
        int i = -1, left = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        void Go(int to)
        {
            i = to;
            if (i >= steps.Count)
            {
                timer.Stop();
                progress.Text = ""; title.Text = L.T("Done!"); text.Text = L.T("Good work. You can get back to what you were doing."); count.Text = ""; next.IsVisible = false;
                return;
            }
            left = steps[i].Seconds;
            progress.Text = L.F("{0} of {1}", i + 1, steps.Count); title.Text = L.T(steps[i].Title); text.Text = L.T(steps[i].Text); count.Text = left.ToString();
        }
        timer.Tick += (_, _) => { if (--left <= 0) Go(i + 1); else count.Text = left.ToString(); };
        next.Click += (_, _) => Go(i + 1);
        win.Closed += (_, _) => timer.Stop();
        Go(0); timer.Start();
        win.Show();
        return win;
    }

    /// <summary>Phases of the eye exercise: (instruction, seconds).</summary>
    internal static readonly (string Text, int Seconds)[] EyePhases =
    {
        ("Follow the ball with your eyes only, without moving your head", 12),   // left ↔ right
        ("Now up and down", 12),
        ("Diagonally", 12),
        ("In circles", 14),
        ("It will come closer and move away: keep your focus on it", 14),
        ("Close your eyes and breathe deeply", 8),
        ("Look at something far away, about 6 meters (20 ft), for 20 seconds", 20),
    };

    /// <summary>Where the ball is at time t (seconds into a phase), as fractions of the area, plus its radius scale.</summary>
    internal static (double X, double Y, double Scale) EyeBall(int phase, double t)
    {
        var w = 2 * Math.PI * t / 4.0;   // one sweep every 4 s
        return phase switch
        {
            0 => (0.5 + 0.4 * Math.Sin(w), 0.5, 1),
            1 => (0.5, 0.5 + 0.38 * Math.Sin(w), 1),
            2 => (0.5 + 0.38 * Math.Sin(w), 0.5 + 0.38 * Math.Sin(w), 1),
            3 => (0.5 + 0.35 * Math.Cos(w), 0.5 + 0.35 * Math.Sin(w), 1),
            4 => (0.5, 0.5, 1.6 + 1.4 * Math.Sin(2 * Math.PI * t / 5.0)),   // near (big) ↔ far (small)
            _ => (0.5, 0.5, 0),
        };
    }

    /// <summary>A white ball that moves around (left-right, up-down, circles, near-far) for the eyes to follow.</summary>
    public static Window ShowEyes()
    {
        var label = new TextBlock { FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0), TextAlignment = TextAlignment.Center };
        var ball = new Ellipse { Fill = Brushes.White, Width = 28, Height = 28 };
        var canvas = new Canvas { Children = { ball } };
        var root = new DockPanel();
        root.Children.Add(RoutineDialogs.Docked(label, Dock.Top));
        root.Children.Add(canvas);
        var win = FullScreen(L.T("Eye exercise"), root);
        int phase = 0; var started = DateTime.Now;
        label.Text = L.T(EyePhases[0].Text);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var t = (DateTime.Now - started).TotalSeconds;
            if (t >= EyePhases[phase].Seconds)
            {
                if (++phase >= EyePhases.Length) { timer.Stop(); label.Text = L.T("Done! Your eyes thank you."); ball.IsVisible = false; return; }
                started = DateTime.Now; t = 0; label.Text = L.T(EyePhases[phase].Text);
            }
            var (x, y, s) = EyeBall(phase, t);
            var size = 28 * s;
            ball.IsVisible = s > 0;
            ball.Width = ball.Height = Math.Max(1, size);
            Canvas.SetLeft(ball, x * canvas.Bounds.Width - size / 2);
            Canvas.SetTop(ball, y * canvas.Bounds.Height - size / 2);
        };
        win.Closed += (_, _) => timer.Stop();
        timer.Start();
        win.Show();
        return win;
    }
}
