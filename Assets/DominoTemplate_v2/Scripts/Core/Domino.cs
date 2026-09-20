using System;
using System.Collections.Generic;

namespace DominoTemplate.Core
{
    [Serializable]
    public class Domino
    {
        public int id = -1;
        public int TopIndex;
        public int BottomIndex;

        public bool Available;
        public bool PortraitOrientation;

        public bool IsValue(int value1, int value2)
        {
            return (TopIndex == value1 && BottomIndex == value2) 
                || (TopIndex == value2 && BottomIndex == value1);
        }

        public bool HasValue(int value)
        {
            return TopIndex == value || BottomIndex == value;
        }

        public int OtherValue(int known)
        {
            if (TopIndex == known) return BottomIndex;
            if (BottomIndex == known) return TopIndex;
            return known; // fallback
        }

        public bool IsDouble()
        {
            return TopIndex == BottomIndex;
        }

        /// <summary>
        /// Returns the sum of TopIndex and BottomIndex.
        /// </summary>
        /// <returns>The sum of TopIndex and BottomIndex.</returns>
        public int ValueSum()
        {
            return TopIndex + BottomIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is Domino other && other.id == id 
                && other.TopIndex == TopIndex 
                && other.BottomIndex == BottomIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(id, TopIndex, BottomIndex);
        }

        /// <summary>
        /// Determines whether the specified tile ID represents a double tile in a standard double-six domino set.
        /// </summary>
        /// <param name="tileId">The ID of the tile to check.</param>
        /// <returns>True if the tile is a double; otherwise, false.</returns>
        public static bool IsDouble_ViaID(int tileId)
        {
            // Check if the tile is a double (i.e., both ends have the same value)
            if (tileId < 0 || tileId >= 28) return false;

            // In a standard double-six domino set, the double tiles are those where both ends have the same number. The IDs for these tiles are typically:
            var doublesIds = new HashSet<int> { 0, 7, 13, 18, 22, 25, 27 };

            // Return true if the tile ID is in the set of double tile IDs
            return doublesIds.Contains(tileId);
        }
    }
}