using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using WinKuake.Native;

namespace WinKuake.Services;

/// <summary>
/// Animaciones slide-down / slide-up sobre la propiedad <see cref="Window.Top"/>.
/// La ventana arranca posicionada justo encima del área visible y desliza hasta Top=0
/// (relativo al área del monitor activo).
/// </summary>
public sealed class WindowAnimator
{
    private readonly Window _window;
    private bool _isAnimating;

    public bool IsVisible { get; private set; }

    public WindowAnimator(Window window)
    {
        _window = window;
    }

    public void Show(double topTarget, int durationMs, Action? onCompleted = null)
    {
        if (_isAnimating) return;
        _isAnimating = true;

        _window.Top = topTarget - _window.Height;
        _window.Visibility = Visibility.Visible;

        // Activate() de WPF se traduce en SetForegroundWindow, que Windows
        // ignora cuando venimos de segundo plano (F12 con otra app al frente):
        // la ventana se ve pero el teclado sigue yendo a la app anterior.
        // ForceForeground se salta ese bloqueo para que el terminal reciba foco.
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd != IntPtr.Zero) NativeMethods.ForceForeground(hwnd);
        else _window.Activate();

        var anim = new DoubleAnimation(_window.Top, topTarget, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) =>
        {
            _isAnimating = false;
            IsVisible = true;
            onCompleted?.Invoke();
        };
        _window.BeginAnimation(Window.TopProperty, anim);
    }

    public void Hide(double topTarget, int durationMs, Action? onCompleted = null)
    {
        if (_isAnimating) return;
        _isAnimating = true;

        var anim = new DoubleAnimation(_window.Top, topTarget - _window.Height,
            TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) =>
        {
            _window.BeginAnimation(Window.TopProperty, null);
            _window.Visibility = Visibility.Collapsed;
            _isAnimating = false;
            IsVisible = false;
            onCompleted?.Invoke();
        };
        _window.BeginAnimation(Window.TopProperty, anim);
    }
}
