﻿using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using XUnit.ValueTuples;

namespace SadRogue.Primitives.UnitTests
{
    public class LinesTests
    {
        #region TestData
        // Test cases should include:
        //    - Positive and negative x/y values
        //    - Lines covering all octants
        //    - Slopes which are not an integer value
        //    - A line such that start == end
        //    - Vertical lines
        //    - Horizontal lines
        private static readonly (Point start, Point End)[] s_testLines =
        {
            // Octant 1: 0 < dx < dy
            ((-1, -2), (25, 40)),
            // Octant 0: 0 < dy <= dx
            ((-1, -2), (40, 25)),
            // Octant 7: 0 < -dy <= dx
            ((-1, -2), (40, -25)),
            // Octant 6: 0 < dx < -dy
            ((-1, -2), (25, -40)),
            // Octant 5: 0 < -dx < -dy
            ((-1, -2), (-25, -40)),
            // Octant 4: 0 < -dy <= -dx
            ((-1, -2), (-40, -25)),
            // Octant 3: 0 < dy <= -dx
            ((-1, -2), (-40, 25)),
            // Octant 2: 0 < -dx < dy
            ((-1, -2), (-25, 40)),
            // Start == end
            ((10, 11), (10, 11)),
            // Vertical lines
            ((5, 6), (5, 10)),
            ((5, 10), (5, 6)),
            // Horizontal lines
            ((6, 5), (10, 5)),
            ((10, 5), (6, 5)),
        };

        // Algorithms which are guaranteed to return items in order from start to finish.
        private static readonly Lines.Algorithm[] s_orderedAlgorithms =
        {
            Lines.Algorithm.Bresenham,
            Lines.Algorithm.DDA,
            Lines.Algorithm.Orthogonal
        };

        // Each line algorithm paired with how it defines adjacency/distance between points.
        private static readonly (Lines.Algorithm, Distance distanceRule)[] s_adjacency =
        {
            (Lines.Algorithm.Bresenham, Distance.Chebyshev),
            (Lines.Algorithm.DDA, Distance.Chebyshev),
            (Lines.Algorithm.Orthogonal, Distance.Manhattan)
        };

        private static readonly Lines.Algorithm[] s_allLineAlgorithms = Enum.GetValues<Lines.Algorithm>().ToArray();

        public static IEnumerable<(Lines.Algorithm algo, (Point start, Point end) points)> OrderedTestCases =
            s_orderedAlgorithms.Combinate(s_testLines);

        public static IEnumerable<(Lines.Algorithm algo, Distance distanceRule, (Point start, Point end) points)>
            AllTestCasesWithDistance =
                s_adjacency.Combinate(s_testLines);

        public static IEnumerable<(Lines.Algorithm algo, (Point start, Point end) points)> AllTestCases =
            s_allLineAlgorithms.Combinate(s_testLines);

        #endregion

        [Theory]
        [MemberDataTuple(nameof(OrderedTestCases))]
        public void LineOrderingTests(Lines.Algorithm algo, (Point start, Point end) points)
        {
            var line = Lines.GetLine(points.start, points.end, algo).ToArray();
            Assert.Equal(points.start, line[0]);
            Assert.Equal(points.end, line[^1]);
        }

        [Theory]
        [MemberDataTuple(nameof(AllTestCasesWithDistance))]
        public void LineAdjacencyTests(Lines.Algorithm algo, Distance distanceRule, (Point start, Point end) points)
        {
            var line = Lines.GetLine(points.start, points.end, algo).ToArray();

            for (int i = 1; i < line.Length; i++)
                Assert.Equal(1, distanceRule.Calculate(line[i - 1], line[i]));
        }

        [Theory]
        [MemberDataTuple(nameof(AllTestCases))]
        public void LineBoundsTests(Lines.Algorithm algo, (Point start, Point end) points)
        {
            var min = new Point(Math.Min(points.start.X, points.end.X), Math.Min(points.start.Y, points.end.Y));
            var max = new Point(Math.Max(points.start.X, points.end.X), Math.Max(points.start.Y, points.end.Y));
            var expectedBounds = new Rectangle(min, max);

            var line = Lines.GetLine(points.start, points.end, algo).ToArray();
            foreach (var point in line)
                Assert.True(expectedBounds.Contains(point));
        }

        [Fact]
        public void BadAlgorithmTest()
        {
            Assert.Throws<ArgumentException>(() => Lines.GetLine((1, 2), (3, 4), (Lines.Algorithm)100));
        }
    }
}
