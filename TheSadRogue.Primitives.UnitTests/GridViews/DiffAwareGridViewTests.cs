﻿using System;
using System.Collections.Generic;
using System.Linq;
using SadRogue.Primitives.GridViews;
using SadRogue.Primitives.UnitTests.Mocks;
using Xunit;
using XUnit.ValueTuples;

namespace SadRogue.Primitives.UnitTests.GridViews
{
    public class DiffValueChangeTests
    {
        private static readonly ValueChange<int> s_equivalentValue = new ValueChange<int>((1, 2), 1, 2);
        public static readonly ValueChange<int>[] EquivalencyTestCases =
        {
            new ValueChange<int>((1, 2), 1, 2),
            new ValueChange<int>((1, 2), 1, 3),
            new ValueChange<int>((1, 2), 3, 2),
            new ValueChange<int>((2, 3), 1, 2),
        };

        [Fact]
        public void Construction()
        {
            var change = new ValueChange<int>((1, 2), 1, 2);
            Assert.Equal(new Point(1, 2), change.Position);
            Assert.Equal(1, change.OldValue);
            Assert.Equal(2, change.NewValue);
        }

        [Fact]
        public void EquivalencyTests()
        {
            var expected = s_equivalentValue;
            var actual = EquivalencyTestCases[0];
            Assert.True(expected.Equals(actual));
            Assert.True(expected.Equals((object)actual));
            Assert.True(expected.Matches(actual));
            Assert.True(expected == actual);

            expected = EquivalencyTestCases[0];
            actual = s_equivalentValue;
            Assert.True(expected.Equals(actual));
            Assert.True(expected.Equals((object)actual));
            Assert.True(expected.Matches(actual));
            Assert.True(expected == actual);
        }

        [Theory]
        [MemberDataEnumerable(nameof(EquivalencyTestCases))]
        public void EqualityInequalityRelationshipTests(ValueChange<int> value)
        {
            foreach (var otherValue in EquivalencyTestCases)
            {
                Assert.Equal(otherValue.Equals(value), otherValue.Equals((object)value));
                Assert.Equal(otherValue.Equals(value), otherValue.Matches(value));
                Assert.Equal(otherValue.Equals(value), otherValue == value);

                Assert.Equal(!otherValue.Equals(value), otherValue != value);
                Assert.Equal(!otherValue.Equals(value), value != otherValue);
            }
        }
    }
    public class DiffTests
    {
        [Fact]
        public void Construction()
        {
            // Create object
            var diff = new Diff<bool>();

            // Should be no changes
            Assert.Empty(diff.Changes);
            // Compressed state should be true since there are no items
            Assert.True(diff.IsCompressed);
            // Diffs start out non-finalized
            Assert.False(diff.IsFinalized);
        }

        [Fact]
        public void AddDiffs()
        {
            // Create object and ensure starting point is as expected
            var diff = new Diff<bool>();
            Assert.Empty(diff.Changes);

            // Add new change and verify its addition
            var change1 = new ValueChange<bool>((1, 2), false, true);
            diff.Add(change1);
            Assert.False(diff.IsCompressed);
            Assert.Single(diff.Changes);
            Assert.Equal(change1, diff.Changes[0]);
            Assert.False(diff.IsFinalized);

            // Add another change (at same location) and verify its addition
            var change2 = new ValueChange<bool>((1, 2), false, true);
            diff.Add(change2);
            Assert.False(diff.IsCompressed);
            Assert.Equal(2, diff.Changes.Count);
            Assert.Equal(change2, diff.Changes[1]);
            Assert.False(diff.IsFinalized);

            // Validate that we can't add a change which doesn't change anything
            var change3 = new ValueChange<bool>((1, 2), false, false);
            Assert.Throws<Exception>(() => diff.Add(change3));

            diff.FinalizeChanges();
            Assert.True(diff.IsFinalized);

            // Verify we can't add changes to a finalized set
            var change4 = new ValueChange<bool>((1, 3), false, true);
            Assert.Throws<Exception>(() => diff.Add(change4));
        }

        [Fact]
        public void CompressionBasic()
        {
            // Create diff and two uncompressable changes
            var diff = new Diff<int>();
            var initialChange1 = new ValueChange<int>((1, 2), 0, 1);
            var initialChange2 = new ValueChange<int>((5, 6), 0, 1);
            diff.Add(initialChange1);
            diff.Add(initialChange2);
            Assert.Equal(2, diff.Changes.Count);
            Assert.False(diff.IsCompressed);

            // Compress (which should not remove either change since they're not to the same position);
            // but it will mark the diff as fully compressed
            diff.Compress();
            Assert.Equal(2, diff.Changes.Count);
            Assert.True(diff.IsCompressed);

            // Add a compressable change
            var change1TrueToFalse = new ValueChange<int>((1, 2), 1, 0);
            diff.Add(change1TrueToFalse);
            Assert.Equal(3, diff.Changes.Count);
            Assert.False(diff.IsCompressed);

            // Compress (which should remove one position entirely since the changes offset)
            diff.Compress();
            Assert.Single(diff.Changes);
            Assert.True(diff.IsCompressed);
            Assert.Equal(diff.Changes[0], initialChange2);

            // Add a series of changes, 2 of which offset
            var uniquePositionChange = new ValueChange<int>((20, 20), 0, 1);
            var nonDuplicateForFirstPos = new ValueChange<int>((1, 2), 0, 2);
            diff.Add(initialChange1);
            diff.Add(uniquePositionChange);
            diff.Add(change1TrueToFalse);
            diff.Add(nonDuplicateForFirstPos);
            Assert.Equal(5, diff.Changes.Count);
            Assert.False(diff.IsCompressed);

            // Compress, which should remove the two offsetting changes, keep the most recent one, but not necessarily
            // preserve the relative ordering.
            diff.Compress();
            Assert.Equal(3, diff.Changes.Count);
            Assert.Equal(
                diff.Changes.ToHashSet(),
                new HashSet<ValueChange<int>>{initialChange2, uniquePositionChange, nonDuplicateForFirstPos});
            Assert.True(diff.IsCompressed);
        }

        [Fact]
        public void CompressionProducesMinimalSet()
        {
            var rand = new Random();

            // Create diff and add random changes that will have duplicates
            var diff = new Diff<int>();

            var positions = new Point[] { (1, 2), (5, 6), (20, 21) };
            var currentValues = new Dictionary<Point, int>();
            for (int i = 0; i < 100; i++)
            {
                foreach (var pos in positions)
                {
                    int oldVal = currentValues.GetValueOrDefault(pos);
                    int newVal;
                    do
                    {
                        newVal = rand.Next(1, 6);
                    } while (oldVal == newVal);
                    diff.Add(new ValueChange<int>(pos, oldVal, newVal));
                    currentValues[pos] = newVal;
                }
            }
            Assert.Equal(100 * positions.Length, diff.Changes.Count);

            Assert.False(diff.IsCompressed);

            // We know what the current values are; since they all started at 0 and got changed precisely once,
            // there will always be 3 changes
            diff.Compress();
            Assert.True(diff.IsCompressed);
            Assert.Equal(3, diff.Changes.Count);
            foreach (var change in diff.Changes)
            {
                Assert.True(currentValues.ContainsKey(change.Position));
                Assert.Equal(0, change.OldValue);
                Assert.Equal(currentValues[change.Position], change.NewValue);
            }
        }
    }

    public class DiffAwareGridViewTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ConstructionViaWrapper(bool autoCompress)
        {
            // Create the wrapper
            var arrayView = new ArrayView<bool>(80, 25);
            var diffAwareView = new DiffAwareGridView<bool>(arrayView, autoCompress);

            // Validate beginning state
            Assert.Equal(autoCompress, diffAwareView.AutoCompress);
            Assert.Equal(arrayView, diffAwareView.BaseGrid);
            Assert.Equal(arrayView.Width, diffAwareView.Width);
            Assert.Equal(arrayView.Height, diffAwareView.Height);
            Assert.Equal(-1, diffAwareView.CurrentDiffIndex);
            Assert.Empty(diffAwareView.Diffs);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ConstructionViaWidthHeight(bool autoCompress)
        {
            // Create the wrapper
            var diffAwareView = new DiffAwareGridView<bool>(80, 25, autoCompress);

            // Validate beginning state
            Assert.Equal(autoCompress, diffAwareView.AutoCompress);
            Assert.IsType<ArrayView<bool>>(diffAwareView.BaseGrid);
            Assert.Equal(80, diffAwareView.BaseGrid.Width);
            Assert.Equal(25, diffAwareView.BaseGrid.Height);
            Assert.Equal(80, diffAwareView.Width);
            Assert.Equal(25, diffAwareView.Height);
            Assert.Equal(-1, diffAwareView.CurrentDiffIndex);
            Assert.Empty(diffAwareView.Diffs);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetAndSetValuesToGridView(bool autoCompress)
        {
            // Create a new grid view and change 1 value
            var view = new DiffAwareGridView<bool>(80, 25, autoCompress)
            {
                [1, 2] = true
            };

            // Verify appropriate value changed
            Assert.True(view[1, 2]);
            Assert.False(view[5, 6]);

            // Verify change recorded appropriately
            Assert.Single(view.Diffs);
            Assert.Single(view.Diffs[0].Changes);
            var change = view.Diffs[0].Changes[0];
            Assert.Equal<Point>((1, 2), change.Position);
            Assert.False(change.OldValue);
            Assert.True(change.NewValue);
            Assert.Equal(0, view.CurrentDiffIndex);
            Assert.False(view.Diffs[0].IsFinalized);

            // Change value 2
            view[5, 6] = true;

            // Verify appropriate value changed
            Assert.True(view[1, 2]);
            Assert.True(view[5, 6]);

            // Verify change recorded appropriately
            Assert.Single(view.Diffs);
            Assert.Equal(2, view.Diffs[0].Changes.Count);
            change = view.Diffs[0].Changes[1];
            Assert.Equal<Point>((5, 6), change.Position);
            Assert.False(change.OldValue);
            Assert.True(change.NewValue);
            Assert.Equal(0, view.CurrentDiffIndex);
            Assert.False(view.Diffs[0].IsFinalized);

            // Change value 1 again
            view[1, 2] = false;

            // Verify appropriate value changed
            Assert.False(view[1, 2]);
            Assert.True(view[5, 6]);

            // Verify change recorded appropriately
            Assert.Single(view.Diffs);
            Assert.Equal(3, view.Diffs[0].Changes.Count);
            change = view.Diffs[0].Changes[2];
            Assert.Equal<Point>((1, 2), change.Position);
            Assert.True(change.OldValue);
            Assert.False(change.NewValue);
            Assert.Equal(0, view.CurrentDiffIndex);
            Assert.False(view.Diffs[0].IsFinalized);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DiffRecordingAndTraversal(bool autoCompress)
        {
            // Create a new grid view
            var view = new DiffAwareGridView<int>(80, 25, autoCompress);

            // Create 3 states for which we will record diffs
            var stateChangeSet1 = new Dictionary<Point, int>
            {
                [(1, 2)] = 1,
                [(5, 6)] = 2,
                [(15, 20)] = 3,
            };

            var stateChangeSet2 = new Dictionary<Point, int>(stateChangeSet1)
            {
                [(25, 10)] = 4
            };
            var stateChangeSet3 = new Dictionary<Point, int>(stateChangeSet2)
            {
                [(1, 2)] = 10,
                [(7, 8)] = 100,
            };

            var forwardOrderStates = new List<Dictionary<Point, int>>
            {
                stateChangeSet1,
                stateChangeSet2,
                stateChangeSet3
            };

            // Apply each state to the view and record diffs as appropriate
            foreach (var state in forwardOrderStates)
            {
                foreach (var (pos, val) in state)
                    view[pos] = val;
                view.FinalizeCurrentDiff();
            }
            // Should have 3 diffs with changes, all of which are finalized.  The CurrentDiffIndex still points to the
            // last diff which is fully applied.
            Assert.Equal(3, view.Diffs.Count);
            Assert.Equal(2, view.CurrentDiffIndex);
            foreach (var change in view.Diffs)
                Assert.True(change.IsFinalized);

            // Going to next diff should fail since there is no next diff
            Assert.Throws<InvalidOperationException>(view.ApplyNextDiff);
            CheckViewState(view, forwardOrderStates[^1]);

            // We should be able to go to all the previous diffs back to the start without exception
            // and the states should match the expected.  We skip the first one, since it represents the current state
            foreach (var prevState in Enumerable.Reverse(forwardOrderStates).Skip(1).Append(new Dictionary<Point, int>()))
            {
                view.RevertToPreviousDiff();
                CheckViewState(view, prevState);
            }

            // Validate we reverted through the last previous state
            Assert.Equal(-1, view.CurrentDiffIndex);

            // Since there are no previous states, the next call should fail
            Assert.Throws<InvalidOperationException>(view.RevertToPreviousDiff);

            // Next, traverse the states in the other direction (forward), using the next-or-finalize function
            foreach (var nextState in forwardOrderStates)
            {
                Assert.True(view.ApplyNextDiffOrFinalize());
                CheckViewState(view, nextState);
            }
            // One more will not apply any state, instead finalizing the last one that actually has changes
            Assert.False(view.ApplyNextDiffOrFinalize());

            // No new diffs should have been created, but they should all be finalized and we should be in a good state
            // to add subsequent changes
            Assert.Equal(3, view.Diffs.Count);
            Assert.Equal(2, view.CurrentDiffIndex);
            foreach (var change in view.Diffs)
                Assert.True(change.IsFinalized);

            // This call will not fail but should instead finalize the current one (which is already finalized,
            // so in this case is no-op)
            Assert.False(view.ApplyNextDiffOrFinalize());
            Assert.Equal(3, view.Diffs.Count);
            Assert.Equal(2, view.CurrentDiffIndex);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FinalizeWithNoDiffs(bool autoCompress)
        {
            // Create a new grid view
            var view = new DiffAwareGridView<int>(80, 25, autoCompress);

            // Attempt to finalize with 0 diffs.  This should do nothing, since it leaves a valid operational
            // state and is a valid operation, but there is no diff to mark.
            view.FinalizeCurrentDiff();

            // Should still be no diffs
            Assert.Equal(-1, view.CurrentDiffIndex);
            Assert.Empty(view.Diffs);

            // Should also work with next-or-finalize
            Assert.False(view.ApplyNextDiffOrFinalize());

            // Should still be no diffs
            Assert.Equal(-1, view.CurrentDiffIndex);
            Assert.Empty(view.Diffs);

            // Creating changes should still create a new diff as normal
            view[1, 2] = 10;
            view[2, 3] = 11;
            Assert.Equal(0, view.CurrentDiffIndex);
            Assert.Single(view.Diffs);
        }

        [Fact]
        public void SetValuesWithUnappliedDiffsThrowsException()
        {
            // Create a new grid view
            var view = new DiffAwareGridView<int>(80, 25) { [1, 0] = 1 };

            view.FinalizeCurrentDiff();

            view[1, 2] = 10;
            view.RevertToPreviousDiff();
            Assert.Throws<InvalidOperationException>(() => view[1, 3] = 20);
        }

        [Fact]
        public void EmptyDiffsRemovedOnPrevious()
        {
            // Create a new grid view and record its state
            // ReSharper disable once RedundantArgumentDefaultValue
            var view = new DiffAwareGridView<int>(80, 25, true) { [1, 0] = 1 };

            // Finalize current diff
            view.FinalizeCurrentDiff();

            // Check that reverting a blank diff (made blank by compression) does nothing to the number of diffs
            view[1, 0] = 2;
            view[1, 0] = 1;
            view.RevertToPreviousDiff();
            Assert.Equal(1, view.Diffs.Count);
        }

        [Fact]
        public void EmptyDiffsRemovedOnFinalize()
        {
            // Create a new grid view and record its state
            var view = new DiffAwareGridView<int>(80, 25) { [1, 0] = 1 };
            var arrayView = new ArrayView<int>(view.Width, view.Height);
            arrayView.ApplyOverlay(view);

            // Finalize current diff
            view.FinalizeCurrentDiff();

            // Check that finalizing a blank diff (made blank by compression) does nothing
            view[1, 0] = 2;
            view[1, 0] = 1;
            view.FinalizeCurrentDiff();
            Assert.Equal(1, view.Diffs.Count);
            foreach (var pos in arrayView.Positions())
                Assert.Equal(arrayView[pos], view[pos]);
        }

        [Fact]
        public void FinalizingDiffWhenNotCurrentStateThrowsException()
        {
            // Create a new grid view and record a diff.
            var view = new DiffAwareGridView<int>(80, 25) { [1, 0] = 1 };
            view.FinalizeCurrentDiff();

            // Revert to previous diff and validate that trying to finalize the current one throws exception.
            view.RevertToPreviousDiff();
            Assert.Throws<InvalidOperationException>(() => view.FinalizeCurrentDiff());
        }

        [Fact]
        public void SetHistory()
        {
            // Create a valid history by creating a view and changing some things
            var underlyingView = new ArrayView<int>(10, 10);
            var diffView = new DiffAwareGridView<int>(underlyingView);
            diffView[1, 2] = 10;
            diffView[5, 6] = 12;
            diffView.FinalizeCurrentDiff();

            diffView[1, 2] = 5;
            diffView[5, 6] = 7;
            diffView[9, 8] = 9;
            diffView.FinalizeCurrentDiff();

            diffView[5, 6] = 2;
            diffView.FinalizeCurrentDiff();

            var diffs = diffView.Diffs.ToList();

            // Validate that we can wrap the current view in a diff-aware one and set the history to it
            var newView = new DiffAwareGridView<int>(underlyingView);
            Assert.Empty(newView.Diffs);
            newView.SetHistory(diffs);
            Assert.Equal(diffs, newView.Diffs);
            Assert.Equal(diffs.Count - 1, newView.CurrentDiffIndex);

            // Validate same thing for each possible index
            while (diffView.CurrentDiffIndex > -1)
                diffView.RevertToPreviousDiff();

            for (int i = -1; i < diffs.Count; i++)
            {
                Assert.Equal(i, diffView.CurrentDiffIndex);

                newView = new DiffAwareGridView<int>(underlyingView);
                newView.SetHistory(diffs, i);
                Assert.Equal(diffs, newView.Diffs);
                Assert.Equal(i, newView.CurrentDiffIndex);

                if (i + 1 < diffs.Count)
                    diffView.ApplyNextDiff();
            }

            // Change the diff so it isn't a valid history for the grid view we have
            var newDiff = new Diff<int>();
            foreach (var change in diffs[1])
                newDiff.Add(change.Position == (5, 6) ? new ValueChange<int>(change.Position, 17, 7) : change);

            diffs[1] = newDiff;

            // Make sure we throw exception when trying to set history
            newView = new DiffAwareGridView<int>(underlyingView);
            Assert.Throws<ArgumentException>(() => newView.SetHistory(diffs));

            // Validate same thing for each possible index
            // Change the diff so it isn't a valid history for the grid view we have
            newDiff = new Diff<int>();
            foreach (var change in diffs[1])
                newDiff.Add(change.Position == (5, 6) ? new ValueChange<int>(change.Position, 12, 77) : change);
            diffs[1] = newDiff;

            while (diffView.CurrentDiffIndex > -1)
                diffView.RevertToPreviousDiff();

            for (int i = -1; i < diffs.Count; i++)
            {
                Assert.Equal(i, diffView.CurrentDiffIndex);
                newView = new DiffAwareGridView<int>(underlyingView);
                Assert.Throws<ArgumentException>(() => newView.SetHistory(diffs, i));
                if (i + 1 < diffs.Count)
                    diffView.ApplyNextDiff();
            }
        }

        [Fact]
        public void SetEmptyHistoryThrowsException()
        {
            var view = new DiffAwareGridView<int>(80, 25);
            Assert.Throws<ArgumentException>(() => view.SetHistory(new List<Diff<int>>()));
        }

        [Fact]
        public void SetEmptyHistoryOutOfRangeCurrentIndexThrowsException()
        {
            var view = new DiffAwareGridView<int>(80, 25) { [1, 2] = 3 };
            view.FinalizeCurrentDiff();

            var history = view.Diffs.ToList();
            Assert.Single(history);

            Assert.Throws<ArgumentException>(() => view.SetHistory(history, history.Count));
            Assert.Throws<ArgumentException>(() => view.SetHistory(history, -1));
        }

        [Fact]
        public void SetHistoryWithBlankDiffThrowsException()
        {
            var view = new DiffAwareGridView<int>(80, 25);

            var history = new List<Diff<int>> { new Diff<int>() };

            Assert.Throws<ArgumentException>(() => view.SetHistory(history));
            Assert.Throws<ArgumentException>(() => view.SetHistory(history, -1));
        }

        [Fact]
        public void SetBaseline()
        {
            var rect = MockGridViews.RectangleBooleanGrid(80, 25);

            var diffView = new DiffAwareGridView<bool>(rect.Width, rect.Height);
            diffView.SetBaseline(rect);

            diffView.FinalizeCurrentDiff();

            foreach (var pos in rect.Positions())
                Assert.Equal(rect[pos], diffView[pos]);

            Assert.Empty(diffView.Diffs);
            Assert.Equal(-1, diffView.CurrentDiffIndex);
        }

        [Fact]
        public void SetBaselineErrors()
        {
            var diffView = new DiffAwareGridView<bool>(80, 25);

            var badSizeRect = MockGridViews.RectangleBooleanGrid(diffView.Width, diffView.Height - 1);
            Assert.Throws<ArgumentException>(() => diffView.SetBaseline(badSizeRect));

            diffView[1, 2] = true;
            var rect = MockGridViews.RectangleBooleanGrid(diffView.Width, diffView.Height);
            Assert.Throws<InvalidOperationException>(() => diffView.SetBaseline(rect));

            diffView.FinalizeCurrentDiff();
            Assert.Throws<InvalidOperationException>(() => diffView.SetBaseline(rect));
        }

        [Fact]
        public void ClearHistory()
        {
            var diffView = new DiffAwareGridView<int>(80, 25) { [1, 2] = 10 };

            diffView.FinalizeCurrentDiff();
            Assert.Single(diffView.Diffs);
            Assert.Equal(0, diffView.CurrentDiffIndex);
            var arrayView = new ArrayView<int>(diffView.Width, diffView.Height);
            arrayView.ApplyOverlay(diffView);

            diffView.ClearHistory();
            Assert.Empty(diffView.Diffs);
            Assert.Equal(-1, diffView.CurrentDiffIndex);
            foreach (var pos in arrayView.Positions())
                Assert.Equal(arrayView[pos], diffView[pos]);
        }

        private static void CheckViewState(IGridView<int> view, Dictionary<Point, int> fullChanges)
        {
            foreach (var pos in view.Positions())
                Assert.Equal(fullChanges.GetValueOrDefault(pos), view[pos]);
        }
    }
}
