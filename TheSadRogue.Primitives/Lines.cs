﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SadRogue.Primitives
{
    /// <summary>
    /// A custom enumerator used to iterate over all positions on the on a line using the
    /// <see cref="Lines.Algorithm.Bresenham"/> line algorithm efficiently.
    ///
    /// Generally, you should use <see cref="Lines.GetBresenhamLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/>
    /// to get an instance of this, rather than creating one yourself.
    /// </summary>
    /// <remarks>
    /// This type is a struct, and as such is much more efficient when used in a foreach loop than a function returning
    /// IEnumerable&lt;Point&gt; by using "yield return".  This type does implement <see cref="IEnumerable{Point}"/>,
    /// so you can pass it to functions which require one (for example, System.LINQ).  However, this will have reduced
    /// performance due to boxing of the iterator.
    /// </remarks>
    public struct BresenhamEnumerator : IEnumerator<Point>, IEnumerable<Point>
    {
        // Suppress warning stating to use auto-property because we want to guarantee micro-performance
        // characteristics.
        #pragma warning disable IDE0032 // Use auto property
        private Point _current;
        #pragma warning restore IDE0032 // Use auto property

        /// <summary>
        /// The current value for enumeration.
        /// </summary>
        public Point Current => _current;

        private int _startX;
        private int _startY;

        private int _numerator;
        private readonly int _shortest;
        private readonly int _longest;

        private readonly int _dx1;
        private readonly int _dy1;
        private readonly int _dx2;
        private readonly int _dy2;

        private int _idx;
        object IEnumerator.Current => _current;

        /// <summary>
        /// Creates an enumerator which iterates over all positions on the line.
        /// </summary>
        /// <param name="start">Starting point for the line.</param>
        /// <param name="end">Ending point for the line.</param>
        public BresenhamEnumerator(Point start, Point end)
        {
            _current = Point.None;

            (_startX, _startY) = start;

            int w = end.X - _startX;
            int h = end.Y - _startY;
            _dx1 = 0;
            _dy1 = 0;
            _dx2 = 0;
            _dy2 = 0;

            if (w < 0) _dx1 = -1;
            else if (w > 0) _dx1 = 1;
            if (h < 0) _dy1 = -1;
            else if (h > 0) _dy1 = 1;
            if (w < 0) _dx2 = -1;
            else if (w > 0) _dx2 = 1;
            _longest = Math.Abs(w);
            _shortest = Math.Abs(h);
            if (!(_longest > _shortest))
            {
                _longest = Math.Abs(h);
                _shortest = Math.Abs(w);
                if (h < 0) _dy2 = -1;
                else if (h > 0) _dy2 = 1;
                _dx2 = 0;
            }

            _numerator = _longest >> 1;

            _idx = 0;
        }

        /// <summary>
        /// Advances the iterator to the next position.
        /// </summary>
        /// <returns>True if the a new position on the line exists; false otherwise.</returns>
        public bool MoveNext()
        {
            if (_idx == _longest + 1)
                return false;

            _current = new Point(_startX, _startY);
            _numerator += _shortest;
            if (!(_numerator < _longest))
            {
                _numerator -= _longest;
                _startX += _dx1;
                _startY += _dy1;
            }
            else
            {
                _startX += _dx2;
                _startY += _dy2;
            }

            _idx++;
            return true;
        }

        /// <summary>
        /// Returns this enumerator.
        /// </summary>
        /// <returns>This enumerator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BresenhamEnumerator GetEnumerator() => this;

        /// <summary>
        /// Obsolete.
        /// </summary>
        /// <returns/>
        [Obsolete(
            "This method is obsolete; this structure itself implements IEnumerable directly and provides equivalent behavior, so you should no longer call this function.")]
        public IEnumerable<Point> ToEnumerable() => this;

        // Explicitly implemented to ensure we prefer the non-boxing versions where possible
        #region Explicit Interface Implementations
        /// <summary>
        /// This iterator does not support resetting.
        /// </summary>
        /// <exception cref="NotSupportedException"/>
        void IEnumerator.Reset() => throw new NotSupportedException();
        IEnumerator<Point> IEnumerable<Point>.GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;

        void IDisposable.Dispose()
        { }
        #endregion
    }

    /// <summary>
    /// A custom enumerator used to iterate over all positions on the on a line using the
    /// <see cref="Lines.Algorithm.DDA"/> line algorithm efficiently.
    ///
    /// Generally, you should use <see cref="Lines.GetDDALine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/>
    /// to get an instance of this, rather than creating one yourself.
    /// </summary>
    /// <remarks>
    /// This type is a struct, and as such is much more efficient when used in a foreach loop than a function returning
    /// IEnumerable&lt;Point&gt; by using "yield return".  This type does implement <see cref="IEnumerable{Point}"/>,
    /// so you can pass it to functions which require one (for example, System.LINQ).  However, this will have reduced
    /// performance due to boxing of the iterator.
    /// </remarks>
    public struct DDAEnumerator : IEnumerator<Point>, IEnumerable<Point>
    {
        // Suppress warning stating to use auto-property because we want to guarantee micro-performance
        // characteristics.
        #pragma warning disable IDE0032 // Use auto property
        private Point _current;
        #pragma warning restore IDE0032 // Use auto property

        /// <summary>
        /// The current value for enumeration.
        /// </summary>
        public Point Current => _current;

        private readonly int _octantState;
        private readonly int _startX;
        private readonly int _startY;
        private readonly int _endX;
        private readonly int _endY;
        private int _primary;
        private int _fraction;
        private readonly int _move;

        private const int ModifierX = 0x7fff;
        private const int ModifierY = 0x7fff;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Creates an enumerator which iterates over all positions on the line.
        /// </summary>
        /// <param name="start">Starting point for the line.</param>
        /// <param name="end">Ending point for the line.</param>
        public DDAEnumerator(Point start, Point end)
        {
            (_startX, _startY) = (start.X, start.Y);
            (_endX, _endY) = (end.X, end.Y);
            _current = Point.None;

            int dx = _endX - _startX;
            int dy = _endY - _startY;

            int nx = Math.Abs(dx);
            int ny = Math.Abs(dy);

            // Calculate octant/state value.  0-7 represent octants, 8-12 are special
            // case states for horizontal and vertical lines that cause MoveNext
            // to behave differently
            _octantState = (dy < 0 ? 4 : 0) | (dx < 0 ? 2 : 0) | (ny > nx ? 1 : 0);
            _fraction = 0;
            int mn = Math.Max(nx, ny);

            _move = 0;
            _primary = 0;

            if (mn == 0)
            {
                _primary = 0;
                _octantState = 8;
            }
            else if (ny == 0)
            {
                _primary = _startX;
                _octantState = dx > 0 ? 9 : 10;
            }
            else if (nx == 0)
            {
                _primary = _startY;
                _octantState = dy > 0 ? 11 : 12;
            }
            else
            {
                switch (_octantState)
                {
                    case 0: // +x, +y
                        _primary = _startX;
                        _move = (ny << 16) / nx;
                        break;
                    case 1:
                        _primary = _startY;
                        _move = (nx << 16) / ny;
                        break;
                    case 2: // -x, +y
                        _primary = _startX;
                        _move = (ny << 16) / nx;
                        break;
                    case 3:
                        _primary = _startY;
                        _move = (nx << 16) / ny;
                        break;
                    case 6: // -x, -y
                        _primary = _startX;
                        _move = (ny << 16) / nx;
                        break;
                    case 7:
                        _primary = _startY;
                        _move = (nx << 16) / ny;
                        break;

                    case 4: // +x, -y
                        _primary = _startX;
                        _move = (ny << 16) / nx;
                        break;

                    case 5:
                        _primary = _startY;
                        _move = (nx << 16) / ny;
                        break;
                }
            }
        }

        /// <summary>
        /// Advances the iterator to the next position.
        /// </summary>
        /// <returns>True if the a new position on the line exists; false otherwise.</returns>
        public bool MoveNext()
        {
            switch (_octantState)
            {
                case 0: // +x, +y
                    if (_primary > _endX)
                        return false;

                    _current = new Point(_primary, _startY + ((_fraction + ModifierY) >> 16));
                    _primary++;
                    _fraction += _move;
                    return true;
                case 1:
                    if (_primary > _endY)
                        return false;

                    _current = new Point(_startX + ((_fraction + ModifierX) >> 16), _primary);
                    _primary++;
                    _fraction += _move;
                    return true;
                case 2: // -x, +y
                    if (_primary < _endX)
                        return false;

                    _current = new Point(_primary, _startY + ((_fraction + ModifierY) >> 16));
                    _primary--;
                    _fraction += _move;
                    return true;
                case 3:
                    if (_primary > _endY)
                        return false;

                    _current = new Point(_startX - ((_fraction + ModifierX) >> 16), _primary);
                    _primary++;
                    _fraction += _move;
                    return true;
                case 6: // -x, -y
                    if (_primary < _endX)
                        return false;

                    _current = new Point(_primary, _startY - ((_fraction + ModifierY) >> 16));
                    _primary--;
                    _fraction += _move;
                    return true;
                case 7:
                    if (_primary < _endY)
                        return false;

                    _current = new Point(_startX - ((_fraction + ModifierX) >> 16), _primary);
                    _primary--;
                    _fraction += _move;
                    return true;
                case 4: // +x, -y
                    if (_primary > _endX)
                        return false;

                    _current = new Point(_primary, _startY - ((_fraction + ModifierY) >> 16));
                    _primary++;
                    _fraction += _move;
                    return true;
                case 5:
                    if (_primary < _endY)
                        return false;

                    _current = new Point(_startX + ((_fraction + ModifierX) >> 16), _primary);
                    _primary--;
                    _fraction += _move;
                    return true;
                case 8: // start == end
                    if (_current.X == _startX && _current.Y == _startY)
                        return false;
                    _current = new Point(_startX, _startY);
                    return true;
                case 9: // Horizontal, dx > 0
                    if (_primary > _endX)
                        return false;

                    _current = new Point(_primary, _startY);
                    _primary++;
                    return true;
                case 10: // Horizontal, dx < 0
                    if (_primary < _endX)
                        return false;

                    _current = new Point(_primary, _startY);
                    _primary--;
                    return true;
                case 11: // Vertical, dy > 0
                    if (_primary > _endY)
                        return false;

                    _current = new Point(_startX, _primary);
                    _primary++;
                    return true;
                case 12: // Vertical, dy < 0
                    if (_primary < _endY)
                        return false;

                    _current = new Point(_startX, _primary);
                    _primary--;
                    return true;
            }

            // Unreachable
            return false;
        }

        /// <summary>
        /// Returns this enumerator.
        /// </summary>
        /// <returns>This enumerator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DDAEnumerator GetEnumerator() => this;

        /// <summary>
        /// Obsolete.
        /// </summary>
        /// <returns/>
        [Obsolete(
            "This method is obsolete; this structure itself implements IEnumerable directly and provides equivalent behavior, so you should no longer call this function.")]
        public IEnumerable<Point> ToEnumerable() => this;

        // Explicitly implemented to ensure we prefer the non-boxing versions where possible
        #region Explicit Interface Implementations
        /// <summary>
        /// This iterator does not support resetting.
        /// </summary>
        /// <exception cref="NotSupportedException"/>
        void IEnumerator.Reset() => throw new NotSupportedException();
        IEnumerator<Point> IEnumerable<Point>.GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;

        void IDisposable.Dispose()
        { }
        #endregion
    }

    /// <summary>
    /// A custom enumerator used to iterate over all positions on the on a line using the
    /// <see cref="Lines.Algorithm.Orthogonal"/> line algorithm efficiently.
    ///
    /// Generally, you should use <see cref="Lines.GetOrthogonalLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/>
    /// to get an instance of this, rather than creating one yourself.
    /// </summary>
    /// <remarks>
    /// This type is a struct, and as such is much more efficient when used in a foreach loop than a function returning
    /// IEnumerable&lt;Point&gt; by using "yield return".  This type does implement <see cref="IEnumerable{Point}"/>,
    /// so you can pass it to functions which require one (for example, System.LINQ).  However, this will have reduced
    /// performance due to boxing of the iterator.
    /// </remarks>
    public struct OrthogonalEnumerator : IEnumerator<Point>, IEnumerable<Point>
    {
        // Suppress warning stating to use auto-property because we want to guarantee micro-performance
        // characteristics.
        #pragma warning disable IDE0032 // Use auto property
        private Point _current;
        #pragma warning restore IDE0032 // Use auto property

        /// <summary>
        /// The current value for enumeration.
        /// </summary>
        public Point Current => _current;

        private bool _first;
        private int _workX;
        private int _workY;
        private int _ix;
        private int _iy;
        private readonly int _nx;
        private readonly int _ny;
        private readonly int _signX;
        private readonly int _signY;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Creates an enumerator which iterates over all positions on the line.
        /// </summary>
        /// <param name="start">Starting point for the line.</param>
        /// <param name="end">Ending point for the line.</param>
        public OrthogonalEnumerator(Point start, Point end)
        {
            _current = Point.None;

            int dx = end.X - start.X;
            int dy = end.Y - start.Y;

            _nx = Math.Abs(dx);
            _ny = Math.Abs(dy);

            _signX = dx > 0 ? 1 : -1;
            _signY = dy > 0 ? 1 : -1;

            _workX = start.X;
            _workY = start.Y;

            _ix = 0;
            _iy = 0;

            _first = true;
        }

        /// <summary>
        /// Advances the iterator to the next position.
        /// </summary>
        /// <returns>True if the a new position on the line exists; false otherwise.</returns>
        public bool MoveNext()
        {
            if (_first)
            {
                _first = false;
                _current = new Point(_workX, _workY);
                return true;
            }

            if (_ix < _nx || _iy < _ny)
            {
                // Optimized version of `if ((0.5 + ix) / nx < (0.5 + iy) / ny)`
                if ((1 + _ix + _ix) * _ny < (1 + _iy + _iy) * _nx)
                {
                    _workX += _signX;
                    _ix++;
                }
                else
                {
                    _workY += _signY;
                    _iy++;
                }

                _current = new Point(_workX, _workY);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns this enumerator.
        /// </summary>
        /// <returns>This enumerator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrthogonalEnumerator GetEnumerator() => this;

        /// <summary>
        /// Obsolete.
        /// </summary>
        /// <returns/>
        [Obsolete(
            "This method is obsolete; this structure itself implements IEnumerable directly and provides equivalent behavior, so you should no longer call this function.")]
        public IEnumerable<Point> ToEnumerable() => this;

        // Explicitly implemented to ensure we prefer the non-boxing versions where possible
        #region Explicit Interface Implementations
        /// <summary>
        /// This iterator does not support resetting.
        /// </summary>
        /// <exception cref="NotSupportedException"/>
        void IEnumerator.Reset() => throw new NotSupportedException();
        IEnumerator<Point> IEnumerable<Point>.GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;

        void IDisposable.Dispose()
        { }
        #endregion
    }

    /// <summary>
    /// Provides implementations of various line-drawing algorithms which are useful for generating lines on a 2D
    /// integer grid.
    /// </summary>
    public static class Lines
    {
        /// <summary>
        /// Various supported line-drawing algorithms.
        /// </summary>
        public enum Algorithm
        {
            /// <summary>
            /// Bresenham line algorithm.  Points are guaranteed to be in order from start to finish.
            /// </summary>
            Bresenham,

            /// <summary>
            /// Digital Differential Analyzer line algorithm.  It will produce slightly different lines compared to
            /// Bresenham, and it takes approximately the same time as Bresenham (very slightly slower) for most inputs.
            /// Points are guaranteed to be in order from start to finish.
            /// </summary>
            DDA,

            /// <summary>
            /// Line algorithm that takes only orthogonal steps (each grid location on the line
            /// returned is within one cardinal direction of the previous one). Potentially useful
            /// for LOS in games that use MANHATTAN distance. Based on the algorithm
            /// <a href="http://www.redblobgames.com/grids/line-drawing.html#stepping">here</a>.
            /// Points are guaranteed to be in order from start to finish.
            /// </summary>
            Orthogonal
        }

        /// <summary>
        /// Returns an IEnumerable of every point, in order, closest to a line between the two points
        /// specified, using the line drawing algorithm given. The start and end points will be included.
        /// Slower than functions such as <see cref="GetBresenhamLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/>,
        /// <see cref="GetDDALine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/>, and
        /// <see cref="GetOrthogonalLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/>.
        /// </summary>
        /// <remarks>
        /// You should only use this function if you need a single function which takes an arbitrary line algorithm,
        /// or you need an IEnumerable object specifically (for example, to use with LINQ).  If you know what line
        /// algorithm you want to use and you just need to iterate over the points in a foreach loop, you should use
        /// <see cref="GetBresenhamLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/>,
        /// <see cref="GetDDALine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/>,
        /// and <see cref="GetOrthogonalLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point)"/> as applicable
        /// instead, since they offer significantly better performance.
        /// </remarks>
        /// <param name="start">The starting point of the line.</param>
        /// <param name="end">The ending point of the line.</param>
        /// <param name="type">The line-drawing algorithm to use to generate the line.</param>
        /// <returns>
        /// An IEnumerable of every point, in order, closest to a line between the two points
        /// specified (according to the algorithm given).
        /// </returns>
        public static IEnumerable<Point> GetLine(Point start, Point end, Algorithm type = Algorithm.Bresenham)
        {
            switch (type)
            {
                case Algorithm.Bresenham:
                    return new BresenhamEnumerator(start, end);
                case Algorithm.DDA:
                    return new DDAEnumerator(start, end);
                case Algorithm.Orthogonal:
                    return new OrthogonalEnumerator(start, end);


                default:
                    throw new ArgumentException("Unsupported line-drawing algorithm.", nameof(type));
            }
        }

        /// <summary>
        /// Returns an IEnumerable of every point, in order, closest to a line between the two points
        /// specified, using the line drawing algorithm given. The start and end points will be included.
        /// Slower than functions such as <see cref="GetBresenhamLine(int, int, int, int)"/>,
        /// <see cref="GetDDALine(int, int, int, int)"/>, and
        /// <see cref="GetOrthogonalLine(int, int, int, int)"/>.
        /// </summary>
        /// <remarks>
        /// You should only use this function if you need a single function which takes an arbitrary line algorithm,
        /// or you need an IEnumerable object specifically (for example, to use with LINQ).  If you know what line
        /// algorithm you want to use and you just need to iterate over the points in a foreach loop, you should use
        /// <see cref="GetBresenhamLine(int, int, int, int)"/>,
        /// <see cref="GetDDALine(int, int, int, int)"/>,
        /// and <see cref="GetOrthogonalLine(int, int, int, int)"/> as applicable
        /// instead, since they offer significantly better performance.
        /// </remarks>
        /// <param name="startX">X-coordinate of the starting point of the line.</param>
        /// <param name="startY">Y-coordinate of the starting point of the line.</param>
        /// <param name="endX">X-coordinate of the ending point of the line.</param>
        /// <param name="endY">Y-coordinate of the ending point of the line.</param>
        /// <param name="type">The line-drawing algorithm to use to generate the line.</param>
        /// <returns>
        /// An IEnumerable of every point, in order, closest to a line between the two points
        /// specified (according to the algorithm given).
        /// </returns>
        public static IEnumerable<Point> GetLine(int startX, int startY, int endX, int endY,
                                                 Algorithm type = Algorithm.Bresenham)
            => GetLine(new Point(startX, startY), new Point(endX, endY), type);

        /// <summary>
        /// Returns all points on the given line using the <see cref="Algorithm.Bresenham"/> line algorithm.
        /// </summary>
        /// <remarks>
        /// This function returns a custom iterator which is very fast when used in a foreach loop.
        /// If you need an IEnumerable to use with LINQ or other code, the returned struct does implement that interface;
        /// however note that iterating over it this way will not perform as well as iterating directly over this object.
        ///
        /// If you need a single function which takes any of the supported line drawing algorithms as a parameter and
        /// uses that to draw, you should use <see cref="GetLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point,SadRogue.Primitives.Lines.Algorithm)"/>;
        /// however again this will be slower than using these functions directly.
        /// </remarks>
        /// <param name="start">The start point of the line.</param>
        /// <param name="end">The end point of the line.</param>
        /// <returns>
        /// Every point, in order, closest to a line between the two points specified (according to Bresenham's line algorithm).
        /// </returns>
        public static BresenhamEnumerator GetBresenhamLine(Point start, Point end)
            => new BresenhamEnumerator(start, end);

        /// <summary>
        /// Returns all points on the given line using the <see cref="Algorithm.Bresenham"/> line algorithm.
        /// </summary>
        /// <remarks>
        /// This function returns a custom iterator which is very fast when used in a foreach loop.
        /// If you need an IEnumerable to use with LINQ or other code, the returned struct does implement that interface;
        /// however note that iterating over it this way will not perform as well as iterating directly over this object.
        ///
        /// If you need a single function which takes any of the supported line drawing algorithms as a parameter and
        /// uses that to draw, you should use <see cref="GetLine(int, int, int, int,SadRogue.Primitives.Lines.Algorithm)"/>;
        /// however again this will be slower than using these functions directly.
        /// </remarks>
        /// <param name="startX">X-coordinate of the starting point of the line.</param>
        /// <param name="startY">Y-coordinate of the starting point of the line.</param>
        /// <param name="endX">X-coordinate of the ending point of the line.</param>
        /// <param name="endY">Y-coordinate of the ending point of the line.</param>
        /// <returns>
        /// Every point, in order, closest to a line between the two points specified (according to Bresenham's line algorithm).
        /// </returns>
        public static BresenhamEnumerator GetBresenhamLine(int startX, int startY, int endX, int endY)
            => GetBresenhamLine(new Point(startX, startY), new Point(endX, endY));

        /// <summary>
        /// Returns all points on the given line using the <see cref="Algorithm.DDA"/> line algorithm.
        /// </summary>
        /// <remarks>
        /// This function returns a custom iterator which is very fast when used in a foreach loop.
        /// If you need an IEnumerable to use with LINQ or other code, the returned struct does implement that interface;
        /// however note that iterating over it this way will not perform as well as iterating directly over this object.
        ///
        /// If you need a single function which takes any of the supported line drawing algorithms as a parameter and
        /// uses that to draw, you should use <see cref="GetLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point,SadRogue.Primitives.Lines.Algorithm)"/>;
        /// however again this will be slower than using these functions directly.
        /// </remarks>
        /// <param name="start">The start point of the line.</param>
        /// <param name="end">The end point of the line.</param>
        /// <returns>
        /// Every point, in order, closest to a line between the two points specified (according to the DDA line algorithm).
        /// </returns>
        public static DDAEnumerator GetDDALine(Point start, Point end)
            => new DDAEnumerator(start, end);

        /// <summary>
        /// Returns all points on the given line using the <see cref="Algorithm.DDA"/> line algorithm.
        /// </summary>
        /// <remarks>
        /// This function returns a custom iterator which is very fast when used in a foreach loop.
        /// If you need an IEnumerable to use with LINQ or other code, the returned struct does implement that interface;
        /// however note that iterating over it this way will not perform as well as iterating directly over this object.
        ///
        /// If you need a single function which takes any of the supported line drawing algorithms as a parameter and
        /// uses that to draw, you should use <see cref="GetLine(int, int, int, int,SadRogue.Primitives.Lines.Algorithm)"/>;
        /// however again this will be slower than using these functions directly.
        /// </remarks>
        /// <param name="startX">X-coordinate of the starting point of the line.</param>
        /// <param name="startY">Y-coordinate of the starting point of the line.</param>
        /// <param name="endX">X-coordinate of the ending point of the line.</param>
        /// <param name="endY">Y-coordinate of the ending point of the line.</param>
        /// <returns>
        /// Every point, in order, closest to a line between the two points specified (according to the DDA line algorithm).
        /// </returns>
        public static DDAEnumerator GetDDALine(int startX, int startY, int endX, int endY)
            => GetDDALine(new Point(startX, startY), new Point(endX, endY));

        /// <summary>
        /// Returns all points on the given line using the <see cref="Algorithm.Orthogonal"/> line algorithm.
        /// </summary>
        /// <remarks>
        /// This function returns a custom iterator which is very fast when used in a foreach loop.
        /// If you need an IEnumerable to use with LINQ or other code, the returned struct does implement that interface;
        /// however note that iterating over it this way will not perform as well as iterating directly over this object.
        ///
        /// If you need a single function which takes any of the supported line drawing algorithms as a parameter and
        /// uses that to draw, you should use <see cref="GetLine(SadRogue.Primitives.Point,SadRogue.Primitives.Point,SadRogue.Primitives.Lines.Algorithm)"/>;
        /// however again this will be slower than using these functions directly.
        /// </remarks>
        /// <param name="start">The start point of the line.</param>
        /// <param name="end">The end point of the line.</param>
        /// <returns>
        /// Every point, in order, closest to a line between the two points specified (according to the "orthogonal" line algorithm).
        /// </returns>
        public static OrthogonalEnumerator GetOrthogonalLine(Point start, Point end)
            => new OrthogonalEnumerator(start, end);

        /// <summary>
        /// Returns all points on the given line using the <see cref="Algorithm.Orthogonal"/> line algorithm.
        /// </summary>
        /// <remarks>
        /// This function returns a custom iterator which is very fast when used in a foreach loop.
        /// If you need an IEnumerable to use with LINQ or other code, the returned struct does implement that interface;
        /// however note that iterating over it this way will not perform as well as iterating directly over this object.
        ///
        /// If you need a single function which takes any of the supported line drawing algorithms as a parameter and
        /// uses that to draw, you should use <see cref="GetLine(int, int, int, int, SadRogue.Primitives.Lines.Algorithm)"/>;
        /// however again this will be slower than using these functions directly.
        /// </remarks>
        /// <param name="startX">X-coordinate of the starting point of the line.</param>
        /// <param name="startY">Y-coordinate of the starting point of the line.</param>
        /// <param name="endX">X-coordinate of the ending point of the line.</param>
        /// <param name="endY">Y-coordinate of the ending point of the line.</param>
        /// <returns>
        /// Every point, in order, closest to a line between the two points specified (according to the "orthogonal" line algorithm).
        /// </returns>
        public static OrthogonalEnumerator GetOrthogonalLine(int startX, int startY, int endX, int endY)
            => GetOrthogonalLine(new Point(startX, startY), new Point(endX, endY));
    }
}
