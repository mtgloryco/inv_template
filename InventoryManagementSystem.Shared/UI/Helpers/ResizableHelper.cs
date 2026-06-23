using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace InventoryManagementSystem.UI.Helpers
{
    public static class ResizableHelper
    {
        public static readonly AttachedProperty<bool> CanResizeProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("CanResize", typeof(ResizableHelper), false);

        public static bool GetCanResize(Control element) => element.GetValue(CanResizeProperty);
        public static void SetCanResize(Control element, bool value) => element.SetValue(CanResizeProperty, value);

        static ResizableHelper()
        {
            CanResizeProperty.Changed.AddClassHandler<Control, bool>((control, args) =>
            {
                if (args.NewValue.Value)
                {
                    control.PointerPressed += Control_PointerPressed;
                    control.PointerMoved += Control_PointerMoved;
                    control.PointerReleased += Control_PointerReleased;
                }
                else
                {
                    control.PointerPressed -= Control_PointerPressed;
                    control.PointerMoved -= Control_PointerMoved;
                    control.PointerReleased -= Control_PointerReleased;
                }
            });
        }

        private static bool _isResizing;
        private static Point _startPointerPosition;
        private static Size _startControlSize;
        private static ResizeDirection _resizeDir;
        private const double ResizeMargin = 16.0; // Margin detection area

        private enum ResizeDirection
        {
            None,
            Width,
            Height,
            Both
        }

        private static void Control_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control) return;

            var pos = e.GetPosition(control);
            var dir = GetResizeDirection(control, pos);

            if (dir != ResizeDirection.None)
            {
                _isResizing = true;
                _resizeDir = dir;
                _startPointerPosition = e.GetPosition((Visual?)control.Parent ?? control);
                
                _startControlSize = new Size(
                    double.IsNaN(control.Width) ? control.Bounds.Width : control.Width,
                    double.IsNaN(control.Height) ? control.Bounds.Height : control.Height
                );

                e.Pointer.Capture(control);
                e.Handled = true;
            }
        }

        private static void Control_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;

            if (_isResizing)
            {
                var currentPos = e.GetPosition((Visual?)control.Parent ?? control);
                var deltaX = currentPos.X - _startPointerPosition.X;
                var deltaY = currentPos.Y - _startPointerPosition.Y;

                if (_resizeDir == ResizeDirection.Width || _resizeDir == ResizeDirection.Both)
                {
                    var newWidth = Math.Max(300, _startControlSize.Width + deltaX);
                    if (!double.IsNaN(control.MaxWidth)) newWidth = Math.Min(newWidth, control.MaxWidth);
                    control.Width = newWidth;
                }

                if (_resizeDir == ResizeDirection.Height || _resizeDir == ResizeDirection.Both)
                {
                    var newHeight = Math.Max(200, _startControlSize.Height + deltaY);
                    if (!double.IsNaN(control.MaxHeight)) newHeight = Math.Min(newHeight, control.MaxHeight);
                    control.Height = newHeight;
                }

                e.Handled = true;
            }
            else
            {
                var pos = e.GetPosition(control);
                var dir = GetResizeDirection(control, pos);
                UpdateCursor(control, dir);
            }
        }

        private static void Control_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                _resizeDir = ResizeDirection.None;
                if (sender is Control control)
                {
                    e.Pointer.Capture(null);
                    control.Cursor = Cursor.Default;
                }
                e.Handled = true;
            }
        }

        private static ResizeDirection GetResizeDirection(Control control, Point pos)
        {
            bool nearRight = pos.X >= control.Bounds.Width - ResizeMargin && pos.X <= control.Bounds.Width + 6;
            bool nearBottom = pos.Y >= control.Bounds.Height - ResizeMargin && pos.Y <= control.Bounds.Height + 6;

            if (nearRight && nearBottom) return ResizeDirection.Both;
            if (nearRight) return ResizeDirection.Width;
            if (nearBottom) return ResizeDirection.Height;
            return ResizeDirection.None;
        }

        private static void UpdateCursor(Control control, ResizeDirection dir)
        {
            switch (dir)
            {
                case ResizeDirection.Both:
                    control.Cursor = new Cursor(StandardCursorType.BottomRightCorner);
                    break;
                case ResizeDirection.Width:
                    control.Cursor = new Cursor(StandardCursorType.SizeWestEast);
                    break;
                case ResizeDirection.Height:
                    control.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
                    break;
                default:
                    control.Cursor = Cursor.Default;
                    break;
            }
        }
    }
}
