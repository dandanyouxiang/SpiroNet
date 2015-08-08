﻿/*
SpiroNet.Editor
Copyright (C) 2015 Wiesław Šoltés

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 3
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
02110-1301, USA.

*/
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SpiroNet.Editor
{
    public class SpiroEditor : ObservableObject
    {
        private EditorState _state = null;
        private EditorCommands _commands = null;
        private Action _invalidate = null;
        private Action _capture = null;
        private Action _release = null;
        private SpiroDrawing _drawing = null;
        private IDictionary<SpiroShape, string> _data = null;
        private IDictionary<SpiroShape, IList<SpiroKnot>> _knots = null;
        private double _startX;
        private double _startY;

        public EditorState State
        {
            get { return _state; }
            set { Update(ref _state, value); }
        }

        public EditorCommands Commands
        {
            get { return _commands; }
            set { Update(ref _commands, value); }
        }

        public Action Invalidate
        {
            get { return _invalidate; }
            set { Update(ref _invalidate, value); }
        }

        public Action Capture
        {
            get { return _capture; }
            set { Update(ref _capture, value); }
        }

        public Action Release
        {
            get { return _release; }
            set { Update(ref _release, value); }
        }

        public SpiroDrawing Drawing
        {
            get { return _drawing; }
            set { Update(ref _drawing, value); }
        }

        public IDictionary<SpiroShape, string> Data
        {
            get { return _data; }
            set { Update(ref _data, value); }
        }

        public IDictionary<SpiroShape, IList<SpiroKnot>> Knots
        {
            get { return _knots; }
            set { Update(ref _knots, value); }
        }

        public static double Snap(double value, double snap)
        {
            double r = value % snap;
            return r >= snap / 2.0 ? value + snap - r : value - r;
        }

        public void ToggleIsStroked()
        {
            _state.IsStroked = !_state.IsStroked;

            if (_state.Shape != null)
            {
                _state.Shape.IsStroked = _state.IsStroked;
                _invalidate();
            }

            if (_state.HitShape != null && _state.HitShapePointIndex == -1)
            {
                _state.HitShape.IsStroked = _state.IsStroked;
                _invalidate();
            }
        }

        public void ToggleIsFilled()
        {
            _state.IsFilled = !_state.IsFilled;

            if (_state.Shape != null)
            {
                _state.Shape.IsFilled = _state.IsFilled;
                _invalidate();
            }

            if (_state.HitShape != null && _state.HitShapePointIndex == -1)
            {
                _state.HitShape.IsFilled = _state.IsFilled;
                _invalidate();
            }
        }

        public void ToggleIsClosed()
        {
            _state.IsClosed = !_state.IsClosed;

            if (_state.Shape != null)
            {
                _state.Shape.IsClosed = _state.IsClosed;
                RunSpiro(_state.Shape);
                _invalidate();
            }

            if (_state.HitShape != null && _state.HitShapePointIndex == -1)
            {
                _state.HitShape.IsClosed = _state.IsClosed;
                RunSpiro(_state.HitShape);
                _invalidate();
            }
        }

        public void ToggleIsTagged()
        {
            _state.IsTagged = !_state.IsTagged;

            if (_state.Shape != null)
            {
                _state.Shape.IsTagged = _state.IsTagged;
                RunSpiro(_state.Shape);
                _invalidate();
            }

            if (_state.HitShape != null && _state.HitShapePointIndex == -1)
            {
                _state.HitShape.IsTagged = _state.IsTagged;
                RunSpiro(_state.HitShape);
                _invalidate();
            }
        }

        public void TogglePointType(string value)
        {
            var type = (SpiroPointType)Enum.Parse(typeof(SpiroPointType), value);
            _state.PointType = type;

            // Change last point type.
            if (_state.Shape != null && _state.Shape.Points.Count >= 1)
            {
                SetPointType(_state.Shape, _state.Shape.Points.Count - 1, type);
                RunSpiro(_state.Shape);
            }

            // Change selected point type.
            if (_state.HitShape != null && _state.HitShapePointIndex != -1)
            {
                SetPointType(_state.HitShape, _state.HitShapePointIndex, type);
                RunSpiro(_state.HitShape);
            }

            _invalidate();
        }

        private void SetPointPositionDelta(SpiroShape shape, int index, double dx, double dy)
        {
            var old = shape.Points[index];
            double x = old.X + dx;
            double y = old.Y + dy;
            double sx = _state.EnableSnap && _state.SnapX != 0 ? Snap(x, _state.SnapX) : x;
            double sy = _state.EnableSnap && _state.SnapY != 0 ? Snap(y, _state.SnapY) : y;
            var point = new SpiroControlPoint();
            point.X = sx;
            point.Y = sy;
            point.Type = old.Type;
            shape.Points[index] = point;
        }

        private void SetPointPosition(SpiroShape shape, int index, double x, double y)
        {
            var old = shape.Points[index];
            var point = new SpiroControlPoint();
            point.X = x;
            point.Y = y;
            point.Type = old.Type;
            shape.Points[index] = point;
        }

        private void SetPointType(SpiroShape shape, int index, SpiroPointType type)
        {
            var old = shape.Points[index];
            var point = new SpiroControlPoint();
            point.X = old.X;
            point.Y = old.Y;
            point.Type = type;
            shape.Points[index] = point;
        }

        private static bool TryToRunSpiro(SpiroShape shape, IBezierContext bc)
        {
            if (shape == null || bc == null)
                return false;

            try
            {
                var points = shape.Points.ToArray();
                if (shape.IsTagged)
                    return Spiro.TaggedSpiroCPsToBezier0(points, bc);
                else
                    return Spiro.SpiroCPsToBezier0(points, points.Length, shape.IsClosed, bc);
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
                Debug.Print(ex.StackTrace);
            }

            return false;
        }

        private void RunSpiro(SpiroShape shape)
        {
            if (shape == null)
                return;

            var bc = new PathBezierContext();
            var result = TryToRunSpiro(shape, bc);
            if (_data.ContainsKey(shape))
            {
                _data[shape] = result ? bc.ToString() : null;
                _knots[shape] = result ? bc.GetKnots() : null;
            }
            else
            {
                _data.Add(shape, result ? bc.ToString() : null);
                _knots.Add(shape, result ? bc.GetKnots() : null);
            }
        }

        private static double DistanceSquared(double x0, double y0, double x1, double y1)
        {
            double dx = x0 - x1;
            double dy = y0 - y1;
            return dx * dx + dy * dy;
        }

        private static bool HitTestForPoint(IList<SpiroShape> shapes, double x, double y, double tresholdSquared, out SpiroShape hitShape, out int hitShapePointIndex)
        {
            foreach (var shape in shapes)
            {
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    var point = shape.Points[i];
                    var distance = DistanceSquared(x, y, point.X, point.Y);
                    if (distance < tresholdSquared)
                    {
                        hitShape = shape;
                        hitShapePointIndex = i;
                        return true;
                    }
                }
            }

            hitShape = null;
            hitShapePointIndex = -1;
            return false;
        }

        private static bool HitTestForShape(IList<SpiroShape> shapes, double x, double y, double treshold, out SpiroShape hitShape, out int hitShapePointIndex)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                var bc = new HitTestBezierContext(x, y);
                var shape = shapes[i];
                int knontIndex;

                try
                {
                    if (!TryToRunSpiro(shape, bc))
                        continue;
                }
                catch (Exception ex)
                {
                    Debug.Print(ex.Message);
                    Debug.Print(ex.StackTrace);
                    continue;
                }

                if (bc.Report(out knontIndex) < treshold)
                {
                    hitShape = shape;
                    hitShapePointIndex = knontIndex;
                    return true;
                }
            }

            hitShape = null;
            hitShapePointIndex = -1;
            return false;
        }

        private void NewShape()
        {
            _state.Shape = new SpiroShape()
            {
                IsStroked = _state.IsStroked,
                IsFilled = _state.IsFilled,
                IsClosed = _state.IsClosed,
                IsTagged = _state.IsTagged,
                Points = new ObservableCollection<SpiroControlPoint>()
            };
            _drawing.Shapes.Add(_state.Shape);
        }

        private void NewPoint(SpiroShape shape, double x, double y)
        {
            var point = new SpiroControlPoint();
            point.X = x;
            point.Y = y;
            point.Type = _state.PointType;
            shape.Points.Add(point);
        }

        private void NewPointAt(SpiroShape shape, double x, double y, int index)
        {
            var point = new SpiroControlPoint();
            point.X = x;
            point.Y = y;
            point.Type = _state.PointType;
            shape.Points.Insert(index, point);
        }

        private void Create(double x, double y)
        {
            // Add new shape.
            if (_state.Shape == null)
                NewShape();

            // Add new point.
            NewPoint(_state.Shape, x, y);

            RunSpiro(_state.Shape);
            _invalidate();
        }

        private void Finish()
        {
            // Finish create.
            RunSpiro(_state.Shape);
            _invalidate();
            _state.Shape = null;
        }

        private void InsertPoint(double x, double y, SpiroShape hitShape, int hitShapePointIndex)
        {
            // Insert new point.
            SpiroShape shape = hitShape;
            int index = hitShapePointIndex + 1;
            NewPointAt(shape, x, y, index);

            RunSpiro(shape);
        }

        private void RemovePoint(SpiroShape shape, int index)
        {
            shape.Points.RemoveAt(index);

            if (shape.Points.Count == 0)
            {
                RemoveShape(shape);
            }
            else
            {
                RunSpiro(shape);
            }
        }

        private void RemoveShape(SpiroShape shape)
        {
            _drawing.Shapes.Remove(shape);
            _data.Remove(shape);
            _knots.Remove(shape);
        }

        private void Delete(double x, double y)
        {
            SpiroShape hitShape;
            int hitShapePointIndex;
            var result = HitTestForPoint(_drawing.Shapes, x, y, _state.HitTresholdSquared, out hitShape, out hitShapePointIndex);
            if (result)
            {
                RemovePoint(hitShape, hitShapePointIndex);
            }
            else
            {
                if (HitTestForShape(_drawing.Shapes, x, y, _state.HitTreshold, out hitShape, out hitShapePointIndex))
                {
                    RemoveShape(hitShape);
                }
            }
        }

        private void Select(SpiroShape hitShape, int hitShapePointIndex)
        {
            // Select shape.
            _state.HitShape = hitShape;
            // Select point.
            _state.HitShapePointIndex = hitShapePointIndex;
            // Begin point move.
            _state.Mode = EditorMode.Move;

            // Update point type.
            if (_state.HitShapePointIndex != -1)
            {
                _state.PointType = _state.HitShape.Points[_state.HitShapePointIndex].Type;
            }

            _invalidate();
        }

        private void Select(SpiroShape hitShape)
        {
            // Select shape.
            _state.HitShape = hitShape;
            // Deselect point.
            _state.HitShapePointIndex = -1;
            // Begin shape move.
            _state.Mode = EditorMode.Move;

            // Update shape info.
            if (_state.HitShape != null)
            {
                var shape = _state.HitShape;
                _state.IsStroked = shape.IsStroked;
                _state.IsFilled = shape.IsFilled;
                _state.IsClosed = shape.IsClosed;
                _state.IsTagged = shape.IsTagged;
                RunSpiro(shape);
            }

            _invalidate();
        }

        private void Deselect()
        {
            // Deselect shape.
            _state.HitShape = null;
            // Deselect point.
            _state.HitShapePointIndex = -1;
            // Begin edit.
            _state.Mode = EditorMode.Create;

            _invalidate();
        }

        public static bool TryToSnapToGuideLine(IList<GuideLine> guides, GuideSnapMode mode, double treshold, GuidePoint point, out GuidePoint snap, out GuideSnapMode result)
        {
            snap = default(GuidePoint);
            result = default(GuideSnapMode);

            if (guides.Count == 0 || mode == GuideSnapMode.None)
            {
                return false;
            }

            if (mode.HasFlag(GuideSnapMode.Point))
            {
                foreach (var guide in guides)
                {
                    var distance0 = GuideHelpers.Distance(guide.Point0, point);
                    if (distance0 < treshold)
                    {
                        snap = new GuidePoint(guide.Point0.X, guide.Point0.Y);
                        result = GuideSnapMode.Point;
                        return true;
                    }

                    var distance1 = GuideHelpers.Distance(guide.Point1, point);
                    if (distance1 < treshold)
                    {
                        snap = new GuidePoint(guide.Point1.X, guide.Point1.Y);
                        result = GuideSnapMode.Point;
                        return true;
                    }
                }
            }

            if (mode.HasFlag(GuideSnapMode.Middle))
            {
                foreach (var guide in guides)
                {
                    var middle = GuideHelpers.Middle(guide.Point0, guide.Point1);
                    var distance = GuideHelpers.Distance(middle, point);
                    if (distance < treshold)
                    {
                        snap = middle;
                        result = GuideSnapMode.Middle;
                        return true;
                    }
                }
            }

            if (mode.HasFlag(GuideSnapMode.Nearest))
            {
                foreach (var guide in guides)
                {
                    var nearest = GuideHelpers.NearestPointOnLine(guide.Point0, guide.Point1, point);
                    var distance = GuideHelpers.Distance(nearest, point);
                    if (distance < treshold)
                    {
                        snap = nearest;
                        result = GuideSnapMode.Nearest;
                        return true;
                    }
                }
            }

            if (mode.HasFlag(GuideSnapMode.Intersection))
            {
                foreach (var guide0 in guides)
                {
                    foreach (var guide1 in guides)
                    {
                        if (guide0 == guide1)
                            continue;

                        GuidePoint intersection;
                        if (GuideHelpers.LineLineIntersection(guide0.Point0, guide0.Point1, guide1.Point0, guide1.Point1, out intersection))
                        {
                            var distance = GuideHelpers.Distance(intersection, point);
                            if (distance < treshold)
                            {
                                snap = intersection;
                                result = GuideSnapMode.Intersection;
                                return true;
                            }
                        }
                    }
                }
            }

            double horizontal = default(double);
            double vertical = default(double);

            if (mode.HasFlag(GuideSnapMode.Horizontal))
            {
                foreach (var guide in guides)
                {
                    if (point.Y >= guide.Point0.Y - treshold && point.Y <= guide.Point0.Y + treshold)
                    {
                        snap = new GuidePoint(point.X, guide.Point0.Y);
                        result |= GuideSnapMode.Horizontal;
                        horizontal = guide.Point0.Y;
                        break;
                    }

                    if (point.Y >= guide.Point1.Y - treshold && point.Y <= guide.Point1.Y + treshold)
                    {
                        snap = new GuidePoint(point.X, guide.Point1.Y);
                        result |= GuideSnapMode.Horizontal;
                        horizontal = guide.Point1.Y;
                        break;
                    }
                }
            }

            if (mode.HasFlag(GuideSnapMode.Vertical))
            {
                foreach (var guide in guides)
                {
                    if (point.X >= guide.Point0.X - treshold && point.X <= guide.Point0.X + treshold)
                    {
                        snap = new GuidePoint(guide.Point0.X, point.Y);
                        result |= GuideSnapMode.Vertical;
                        vertical = guide.Point0.X;
                        break;
                    }

                    if (point.X >= guide.Point1.X - treshold && point.X <= guide.Point1.X + treshold)
                    {
                        snap = new GuidePoint(guide.Point1.X, point.Y);
                        result |= GuideSnapMode.Vertical;
                        vertical = guide.Point1.X;
                        break;
                    }
                }
            }

            if (result.HasFlag(GuideSnapMode.Horizontal) || result.HasFlag(GuideSnapMode.Vertical))
            {
                snap = new GuidePoint(
                    result.HasFlag(GuideSnapMode.Vertical) ? vertical : point.X,
                    result.HasFlag(GuideSnapMode.Horizontal) ? horizontal : point.Y);
                return true;
            }

            return false;
        }

        private void TryToSnapToGuideLine()
        {
            GuidePoint snapPoint;
            GuideSnapMode snapResult;

            _state.HaveSnapPoint = TryToSnapToGuideLine(
                _drawing.Guides,
                _state.SnapMode,
                _state.SnapTreshold,
                _state.Point1,
                out snapPoint,
                out snapResult);

            _state.SnapPoint = snapPoint;
            _state.SnapResult = snapResult;
        }

        private bool HitTestForGuideLine(IList<GuideLine> guides, double x, double y, double treshold, out GuideLine hitGuide)
        {
            var point = new GuidePoint(x, y);
            foreach (var guide in _drawing.Guides)
            {
                var nearest = GuideHelpers.NearestPointOnLine(guide.Point0, guide.Point1, point);
                var distance = GuideHelpers.Distance(nearest, point);
                if (distance < treshold)
                {
                    hitGuide = guide;
                    return true;
                }
            }

            hitGuide = null;
            return false;
        }

        private void DeleteGuide(double x, double y)
        {
            GuideLine hitGuide;
            var result = HitTestForGuideLine(_drawing.Guides, x, y, _state.HitTreshold, out hitGuide);
            if (result)
            {
                _drawing.Guides.Remove(hitGuide);
            }
        }

        public void GuideLeftDown(double x, double y)
        {
            double sx = _state.EnableSnap && _state.SnapX != 0 ? Snap(x, _state.SnapX) : x;
            double sy = _state.EnableSnap && _state.SnapY != 0 ? Snap(y, _state.SnapY) : y;

            if (_state.IsCaptured)
            {
                _state.Point1 = new GuidePoint(sx, sy);

                TryToSnapToGuideLine();

                if (_state.HaveSnapPoint)
                {
                    _state.GuidePosition = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                    _state.Point1 = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                }

                _state.IsCaptured = false;
                _release();
                _drawing.Guides.Add(new GuideLine(_state.Point0, _state.Point1));
                _invalidate();
            }
            else
            {
                _state.Point0 = new GuidePoint(sx, sy);
                _state.Point1 = new GuidePoint(sx, sy);

                TryToSnapToGuideLine();

                if (_state.HaveSnapPoint)
                {
                    _state.GuidePosition = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                    _state.Point0 = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                    _state.Point1 = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                }

                _state.IsCaptured = true;
                _capture();
                _invalidate();
            }
        }

        public void GuideMiddleDown(double x, double y)
        {
            if (!_state.IsCaptured
                && _drawing != null 
                && _drawing.Guides != null
                && _drawing.Guides.Count > 0)
            {
                DeleteGuide(x, y);
                _invalidate();
            }
        }

        public void GuideRightDown(double x, double y)
        {
            _state.IsCaptured = false;
            _release();
            _invalidate();
        }

        public void GuideMove(double x, double y)
        {
            double sx = _state.EnableSnap && _state.SnapX != 0 ? Snap(x, _state.SnapX) : x;
            double sy = _state.EnableSnap && _state.SnapY != 0 ? Snap(y, _state.SnapY) : y;

            _state.GuidePosition = new GuidePoint(sx, sy);
            _state.Point1 = new GuidePoint(sx, sy);

            TryToSnapToGuideLine();

            if (_state.HaveSnapPoint)
            {
                _state.GuidePosition = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                _state.Point1 = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
            }
#if DEBUG
            double distance = GuideHelpers.Distance(_state.Point0, _state.Point1);
            double angle = GuideHelpers.LineSegmentAngle(_state.Point0, _state.Point1);
            Debug.Print("X: {0} Y: {1} D: {2} A: {3}, S: {4}",
                _state.Point1.X,
                _state.Point1.Y,
                distance,
                angle,
                _state.SnapResult);
#endif
            _invalidate();
        }

        public void SpiroLeftDown(double x, double y)
        {
            double sx = _state.EnableSnap && _state.SnapX != 0 ? Snap(x, _state.SnapX) : x;
            double sy = _state.EnableSnap && _state.SnapY != 0 ? Snap(y, _state.SnapY) : y;

            if (_state.EnableSnap)
            {
                _state.GuidePosition = new GuidePoint(sx, sy);
                _state.Point1 = new GuidePoint(x, y);

                TryToSnapToGuideLine();

                if (_state.HaveSnapPoint)
                {
                    _state.GuidePosition = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                    _state.Point1 = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);

                    sx = _state.SnapPoint.X;
                    sy = _state.SnapPoint.Y;
                }
            }

            _startX = sx;
            _startY = sy;

            if (_state.Shape == null)
            {
                SpiroShape hitShape;
                int hitShapePointIndex;

                // Hit test for point.
                var result = HitTestForPoint(_drawing.Shapes, x, y, _state.HitTresholdSquared, out hitShape, out hitShapePointIndex);
                if (result)
                {
                    Select(hitShape, hitShapePointIndex);
                    return;
                }
                else
                {
                    // Hit test for shape and nearest point.
                    if (HitTestForShape(_drawing.Shapes, x, y, _state.HitTreshold, out hitShape, out hitShapePointIndex))
                    {
                        Select(hitShape);
                        return;
                    }

                    if (_state.HitShape != null)
                    {
                        Deselect();
                        return;
                    }
                }
            }

            if (_state.Mode == EditorMode.Create)
            {
                Create(sx, sy);
            }
        }

        public void SpiroLeftUp(double x, double y)
        {
            if (_state.Mode == EditorMode.Move)
            {
                // Finish move.
                _state.Mode = EditorMode.Selected;
            }
        }

        public void SpiroMiddleDown(double x, double y)
        {
            if (_state.Shape == null)
            {
                Delete(x, y);
                Deselect();
            }
        }

        public void SpiroRightDown(double x, double y)
        {
            double sx = _state.EnableSnap && _state.SnapX != 0 ? Snap(x, _state.SnapX) : x;
            double sy = _state.EnableSnap && _state.SnapY != 0 ? Snap(y, _state.SnapY) : y;

            if (_state.EnableSnap)
            {
                _state.Point1 = new GuidePoint(x, y);

                TryToSnapToGuideLine();

                if (_state.HaveSnapPoint)
                {
                    _state.GuidePosition = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                    _state.Point1 = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);

                    sx = _state.SnapPoint.X;
                    sy = _state.SnapPoint.Y;
                }
            }

            if (_state.Shape != null)
            {
                Finish();
            }
            else
            {
                SpiroShape hitShape;
                int hitShapePointIndex;

                // Hit test for shape and nearest point.
                if (HitTestForShape(_drawing.Shapes, x, y, _state.HitTreshold, out hitShape, out hitShapePointIndex))
                {
                    InsertPoint(sx, sy, hitShape, hitShapePointIndex);
                    _invalidate();
                    return;
                }

                if (_state.HitShape != null)
                {
                    Deselect();
                }
            }
        }

        public void SpiroMove(double x, double y)
        {
            double sx = _state.EnableSnap && _state.SnapX != 0 ? Snap(x, _state.SnapX) : x;
            double sy = _state.EnableSnap && _state.SnapY != 0 ? Snap(y, _state.SnapY) : y;

            if (_state.EnableSnap)
            {
                _state.GuidePosition = new GuidePoint(sx, sy);
                _state.Point1 = new GuidePoint(x, y);

                TryToSnapToGuideLine();

                if (_state.HaveSnapPoint)
                {
                    _state.GuidePosition = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);
                    _state.Point1 = new GuidePoint(_state.SnapPoint.X, _state.SnapPoint.Y);

                    sx = _state.SnapPoint.X;
                    sy = _state.SnapPoint.Y;
                }
            }

            if (_state.Shape != null)
            {
                if (_state.Shape.Points.Count > 1)
                {
                    SetPointPosition(_state.Shape, _state.Shape.Points.Count - 1, sx, sy);
                    RunSpiro(_state.Shape);
                }

                _invalidate();
            }
            else
            {
                if (_state.Mode == EditorMode.Create)
                {
                    SpiroShape hitShape;
                    int hitShapePointIndex;
                    var result = HitTestForPoint(_drawing.Shapes, x, y, _state.HitTresholdSquared, out hitShape, out hitShapePointIndex);
                    if (result)
                    {
                        // Hover shape.
                        _state.HitShape = hitShape;
                        // Hover point.
                        _state.HitShapePointIndex = hitShapePointIndex;

                        _invalidate();
                    }
                    else
                    {
                        if (HitTestForShape(_drawing.Shapes, x, y, _state.HitTreshold, out hitShape, out hitShapePointIndex))
                        {
                            // Hover shape.
                            _state.HitShape = hitShape;
                            // Dehover point.
                            _state.HitShapePointIndex = -1;
                        }
                        else
                        {
                            // Dehover shape.
                            _state.HitShape = null;
                            // Dehover point.
                            _state.HitShapePointIndex = -1;
                        }

                        _invalidate();
                    }
                }
                else if (_state.Mode == EditorMode.Move)
                {
                    // Move selected shape.
                    if (_state.HitShape != null && _state.HitShapePointIndex == -1)
                    {
                        // Calculate move offset.
                        double dx = sx - _startX;
                        double dy = sy - _startY;
                        // Update start position.
                        _startX = sx;
                        _startY = sy;
                        // Move all shape points.
                        for (int i = 0; i < _state.HitShape.Points.Count; i++)
                            SetPointPositionDelta(_state.HitShape, i, dx, dy);

                        RunSpiro(_state.HitShape);
                        _invalidate();
                    }

                    // Move selected point.
                    if (_state.HitShapePointIndex != -1)
                    {
                        SetPointPosition(_state.HitShape, _state.HitShapePointIndex, sx, sy);

                        RunSpiro(_state.HitShape);
                        _invalidate();
                    }
                }
            }
        }

        public void LeftDown(double x, double y)
        {
            switch (_state.Tool)
            {
                case EditorTool.None:
                    break;
                case EditorTool.Spiro:
                    SpiroLeftDown(x , y);
                    break;
                case EditorTool.Guide:
                    GuideLeftDown(x, y);
                    break;
            }
        }

        public void LeftUp(double x, double y)
        {
            switch (_state.Tool)
            {
                case EditorTool.None:
                    break;
                case EditorTool.Spiro:
                    SpiroLeftUp(x, y);
                    break;
                case EditorTool.Guide:
                    break;
            }
        }

        public void MiddleDown(double x, double y)
        {
            switch (_state.Tool)
            {
                case EditorTool.None:
                    break;
                case EditorTool.Spiro:
                    SpiroMiddleDown(x, y);
                    break;
                case EditorTool.Guide:
                    GuideMiddleDown(x, y);
                    break;
            }
        }

        public void RightDown(double x, double y)
        {
            switch (_state.Tool)
            {
                case EditorTool.None:
                    break;
                case EditorTool.Spiro:
                    SpiroRightDown(x, y);
                    break;
                case EditorTool.Guide:
                    GuideRightDown(x, y);
                    break;
            }
        }

        public void Move(double x, double y)
        {
            switch (_state.Tool)
            {
                case EditorTool.None:
                    break;
                case EditorTool.Spiro:
                    SpiroMove(x, y);
                    break;
                case EditorTool.Guide:
                    GuideMove(x, y);
                    break;
            }
        }

        public void NewDrawing()
        {
            var drawing = new SpiroDrawing()
            {
                Width = _drawing.Width,
                Height = _drawing.Height,
                Shapes = new ObservableCollection<SpiroShape>(),
                Guides = new ObservableCollection<GuideLine>()
            };

            OpenDrawing(drawing);
        }

        public void OpenDrawing(string path)
        {
            using (var f = System.IO.File.OpenText(path))
            {
                var json = f.ReadToEnd();
                var drawing = JsonSerializer.Deserialize<SpiroDrawing>(json);
                OpenDrawing(drawing);
            }
        }

        public void OpenDrawing(SpiroDrawing drawing)
        {
            Drawing = drawing;
            Data = new Dictionary<SpiroShape, string>();
            Knots = new Dictionary<SpiroShape, IList<SpiroKnot>>();

            foreach (var shape in _drawing.Shapes)
            {
                RunSpiro(shape);
            }

            _invalidate();
        }

        public void OpenPlate(string path)
        {
            using (var f = System.IO.File.OpenText(path))
            {
                var plate = f.ReadToEnd();
                var shapes = SpiroPlate.ToShapes(plate);
                if (shapes != null)
                {
                    var drawing = new SpiroDrawing()
                    {
                        Width = _drawing.Width,
                        Height = _drawing.Height,
                        Shapes = shapes,
                        Guides = _drawing.Guides
                    };

                    OpenDrawing(drawing);
                }
            }
        }

        public void SaveAsDrawing(string path)
        {
            using (var f = System.IO.File.CreateText(path))
            {
                var json = JsonSerializer.Serialize(_drawing);
                f.Write(json);
            }
        }

        public void SaveAsPlate(string path)
        {
            using (var f = System.IO.File.CreateText(path))
            {
                var plate = SpiroPlate.FromShapes(_drawing.Shapes);
                f.Write(plate);
            }
        }

        public void ExportAsSvg(string path)
        {
            using (var f = System.IO.File.CreateText(path))
            {
                var sb = new StringBuilder();
                var suffix = Environment.NewLine + "           ";

                sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                sb.AppendLine(
                    string.Format(
                        "<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"{0}\" height=\"{1}\">",
                        _drawing.Width,
                        _drawing.Height));

                foreach (var shape in _drawing.Shapes)
                {
                    sb.AppendLine(
                        string.Format(
                            "  <path style=\"{0};{1}\"",
                            shape.IsStroked ? "stroke:#000000;stroke-opacity:1;stroke-width:2" : "stroke:none",
                            shape.IsFilled ? "fill:#808080;fill-opacity:0.5" : "fill:none"));
                    sb.AppendLine(
                        string.Format(
                            "        d=\"{0}\"/>",
                            Data[shape].Replace(Environment.NewLine, suffix)));
                }

                sb.AppendLine("</svg>");

                f.Write(sb);
            }
        }

        public void ExportAsPs(string path)
        {
            using (var f = System.IO.File.CreateText(path))
            {
                var sb = new StringBuilder();

                sb.Append(PsBezierContext.PsProlog);
                sb.Append(string.Format(PsBezierContext.PsSize, _drawing.Width, _drawing.Height));

                foreach (var shape in _drawing.Shapes)
                {
                    var bc = new PsBezierContext();

                    try
                    {
                        if (TryToRunSpiro(shape, bc))
                        {
                            sb.Append(bc);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Print(ex.Message);
                        Debug.Print(ex.StackTrace);
                    }
                }

                sb.Append(PsBezierContext.PsPostlog);

                f.Write(sb);
            }
        }

        public void ExecuteScript(string script)
        {
            var shapes = SpiroPlate.ToShapes(script);
            if (shapes != null)
            {
                foreach (var shape in shapes)
                {
                    _drawing.Shapes.Add(shape);
                    RunSpiro(shape);
                }

                _invalidate();
            }
        }
    }
}
