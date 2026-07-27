using System;
using System.Drawing;
using System.Windows.Forms;

namespace WorkStatusLight
{
    internal sealed partial class StatusLightForm
    {
        private static readonly int SnapDistance = ScaleDimension(24);

        private bool HasDragStarted(Point mouse)
        {
            Size dragSize = SystemInformation.DragSize;
            return Math.Abs(mouse.X - dragStartMouse.X) >= Math.Max(1, dragSize.Width / 2)
                || Math.Abs(mouse.Y - dragStartMouse.Y) >= Math.Max(1, dragSize.Height / 2);
        }

        private bool ShouldUndockForDrag(Point mouse)
        {
            Size dragSize = SystemInformation.DragSize;
            int horizontalThreshold = Math.Max(1, dragSize.Width / 2);
            int verticalThreshold = Math.Max(1, dragSize.Height / 2);
            int deltaX = mouse.X - dragStartMouse.X;
            int deltaY = mouse.Y - dragStartMouse.Y;

            if (String.Equals(dockedEdge, DockEdgeTopValue, StringComparison.OrdinalIgnoreCase))
            {
                return deltaY >= verticalThreshold;
            }
            if (String.Equals(dockedEdge, DockEdgeBottomValue, StringComparison.OrdinalIgnoreCase))
            {
                return deltaY <= -verticalThreshold;
            }
            if (String.Equals(dockedEdge, DockEdgeLeftValue, StringComparison.OrdinalIgnoreCase))
            {
                return deltaX >= horizontalThreshold;
            }
            if (String.Equals(dockedEdge, DockEdgeRightValue, StringComparison.OrdinalIgnoreCase))
            {
                return deltaX <= -horizontalThreshold;
            }

            return false;
        }

        private void MoveAlongDockedEdge(Point mouse)
        {
            Rectangle startBounds = new Rectangle(dragStartWindow, ClientSize);
            Rectangle workingArea = Screen.FromRectangle(startBounds).WorkingArea;

            if (IsDockedToHorizontalEdge())
            {
                int x = Clamp(dragStartWindow.X + mouse.X - dragStartMouse.X, workingArea.Left, workingArea.Right - ClientSize.Width);
                int y = String.Equals(dockedEdge, DockEdgeTopValue, StringComparison.OrdinalIgnoreCase)
                    ? workingArea.Top
                    : workingArea.Bottom - ClientSize.Height;
                Location = new Point(x, y);
                return;
            }

            int verticalX = String.Equals(dockedEdge, DockEdgeLeftValue, StringComparison.OrdinalIgnoreCase)
                ? workingArea.Left
                : workingArea.Right - ClientSize.Width;
            int verticalY = Clamp(dragStartWindow.Y + mouse.Y - dragStartMouse.Y, workingArea.Top, workingArea.Bottom - ClientSize.Height);
            Location = new Point(verticalX, verticalY);
        }

        private void UndockForDrag(Point mouse)
        {
            dockedEdge = DockEdgeNoneValue;
            ApplyLightWindowSize();

            int anchorX = (int)Math.Round(ClientSize.Width * ClampRatio(dragAnchorRatioX));
            int anchorY = (int)Math.Round(ClientSize.Height * ClampRatio(dragAnchorRatioY));
            Location = new Point(mouse.X - anchorX, mouse.Y - anchorY);
            dragStartMouse = mouse;
            dragStartWindow = Location;
            RenderLayeredWindow();
        }

        private void SnapToNearestScreenEdge()
        {
            if (!edgeSnapEnabled)
            {
                return;
            }

            string edge;
            Screen screen;
            if (!TryGetNearestScreenEdge(new Rectangle(Location, ClientSize), Cursor.Position, out edge, out screen))
            {
                dockedEdge = DockEdgeNoneValue;
                return;
            }

            Rectangle originalBounds = new Rectangle(Location, ClientSize);
            dockedEdge = edge;
            ApplyLightWindowSize();
            Location = GetDockedLocation(originalBounds, ClientSize, screen.WorkingArea, dockedEdge);
            RenderLayeredWindow();
        }

        private static bool TryGetNearestScreenEdge(Rectangle windowBounds, Point cursor, out string edge, out Screen screen)
        {
            screen = Screen.FromPoint(cursor);
            Rectangle workingArea = screen.WorkingArea;
            int bestDistance = Int32.MaxValue;
            edge = DockEdgeNoneValue;

            SetNearestEdge(DistanceToMinimumEdge(windowBounds.Top, workingArea.Top), DockEdgeTopValue, ref bestDistance, ref edge);
            SetNearestEdge(DistanceToMaximumEdge(windowBounds.Bottom, workingArea.Bottom), DockEdgeBottomValue, ref bestDistance, ref edge);
            SetNearestEdge(DistanceToMinimumEdge(windowBounds.Left, workingArea.Left), DockEdgeLeftValue, ref bestDistance, ref edge);
            SetNearestEdge(DistanceToMaximumEdge(windowBounds.Right, workingArea.Right), DockEdgeRightValue, ref bestDistance, ref edge);

            return bestDistance <= SnapDistance;
        }

        private static int DistanceToMinimumEdge(int windowEdge, int workingAreaEdge)
        {
            return Math.Max(0, windowEdge - workingAreaEdge);
        }

        private static int DistanceToMaximumEdge(int windowEdge, int workingAreaEdge)
        {
            return Math.Max(0, workingAreaEdge - windowEdge);
        }

        private static void SetNearestEdge(int distance, string candidate, ref int bestDistance, ref string edge)
        {
            if (distance >= bestDistance)
            {
                return;
            }

            bestDistance = distance;
            edge = candidate;
        }

        private static Point GetDockedLocation(Rectangle sourceBounds, Size dockedSize, Rectangle workingArea, string edge)
        {
            int centerX = sourceBounds.Left + sourceBounds.Width / 2;
            int centerY = sourceBounds.Top + sourceBounds.Height / 2;
            int x = Clamp(centerX - dockedSize.Width / 2, workingArea.Left, workingArea.Right - dockedSize.Width);
            int y = Clamp(centerY - dockedSize.Height / 2, workingArea.Top, workingArea.Bottom - dockedSize.Height);

            if (String.Equals(edge, DockEdgeTopValue, StringComparison.OrdinalIgnoreCase))
            {
                y = workingArea.Top;
            }
            else if (String.Equals(edge, DockEdgeBottomValue, StringComparison.OrdinalIgnoreCase))
            {
                y = workingArea.Bottom - dockedSize.Height;
            }
            else if (String.Equals(edge, DockEdgeLeftValue, StringComparison.OrdinalIgnoreCase))
            {
                x = workingArea.Left;
            }
            else if (String.Equals(edge, DockEdgeRightValue, StringComparison.OrdinalIgnoreCase))
            {
                x = workingArea.Right - dockedSize.Width;
            }

            return new Point(x, y);
        }

        private void ToggleEdgeSnap()
        {
            edgeSnapEnabled = !edgeSnapEnabled;
            if (!edgeSnapEnabled && IsDockedToEdge())
            {
                RestoreFullWindowFromDockedEdge();
            }

            UpdateEdgeSnapMenuCheck();
            WriteSettings();
            RenderLayeredWindow();
            ApplyTopMost();
        }

        private void RestoreFullWindowFromDockedEdge()
        {
            Rectangle dockedBounds = new Rectangle(Location, ClientSize);
            Screen screen = Screen.FromRectangle(dockedBounds);
            Rectangle workingArea = screen.WorkingArea;
            int centerX = dockedBounds.Left + dockedBounds.Width / 2;
            int centerY = dockedBounds.Top + dockedBounds.Height / 2;

            dockedEdge = DockEdgeNoneValue;
            ApplyLightWindowSize();
            int x = Clamp(centerX - ClientSize.Width / 2, workingArea.Left, workingArea.Right - ClientSize.Width);
            int y = Clamp(centerY - ClientSize.Height / 2, workingArea.Top, workingArea.Bottom - ClientSize.Height);
            Location = new Point(x, y);
        }

        private void EnsureDockedWindowOnScreen()
        {
            if (!IsDockedToEdge() || IsDisposed)
            {
                return;
            }

            Rectangle currentBounds = new Rectangle(Location, ClientSize);
            Screen screen = Screen.FromRectangle(currentBounds);
            ApplyLightWindowSize();
            Location = GetDockedLocation(currentBounds, ClientSize, screen.WorkingArea, dockedEdge);
            SaveWindowLocation();
            RenderLayeredWindow();
            ApplyTopMost();
        }

        private bool IsDockedToEdge()
        {
            return IsDockedToHorizontalEdge() || IsDockedToVerticalEdge();
        }

        private bool IsDockedToHorizontalEdge()
        {
            return String.Equals(dockedEdge, DockEdgeTopValue, StringComparison.OrdinalIgnoreCase)
                || String.Equals(dockedEdge, DockEdgeBottomValue, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDockedToVerticalEdge()
        {
            return String.Equals(dockedEdge, DockEdgeLeftValue, StringComparison.OrdinalIgnoreCase)
                || String.Equals(dockedEdge, DockEdgeRightValue, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDockedToRightEdge()
        {
            return String.Equals(dockedEdge, DockEdgeRightValue, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateEdgeSnapMenuCheck()
        {
            edgeSnapItem.Checked = edgeSnapEnabled;
        }

        private static string NormalizeDockEdge(string edge)
        {
            edge = (edge ?? String.Empty).Trim().ToLowerInvariant();
            if (edge == DockEdgeTopValue
                || edge == DockEdgeBottomValue
                || edge == DockEdgeLeftValue
                || edge == DockEdgeRightValue)
            {
                return edge;
            }

            return DockEdgeNoneValue;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (maximum < minimum)
            {
                return minimum;
            }
            if (value < minimum)
            {
                return minimum;
            }
            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        private static float ClampRatio(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }
            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
