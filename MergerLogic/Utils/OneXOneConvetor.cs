using MergerLogic.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MergerLogic.Utils
{
    public class OneXOneConvetor
    {

        /// <summary>
        /// convert 2X1 coords to 1X1 coords.
        /// returns null with invalid z
        /// </summary>
        /// <param name="z"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Coord? TryFromTwoXOne (int z, int x, int y)
        {   if(z < 1)
            {
                return null;
            }
            return FromTwoXOne(z, x, y);
        }

        /// <summary>
        /// convert 2X1 coords to 1X1 coords.
        /// note that this only works when z >= 1
        /// </summary>
        /// <param name="twoXOneCoords"></param>
        /// <returns></returns>
        public Coord FromTwoXOne(Coord twoXOneCoords)
        {
            return FromTwoXOne(twoXOneCoords.z, twoXOneCoords.x, twoXOneCoords.y);
        }

        /// <summary>
        /// convert 2X1 coords to 1X1 coords.
        /// note that this only works when z >= 1
        /// </summary>
        /// <param name="z"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Coord FromTwoXOne(int z, int x, int y)
        {
            int newZ = z + 1;
            y += (1 << (z - 1));
            return new Coord(newZ, x, y);
        }

        /// <summary>
        /// convert 2X1 coords to 1X1 coords.
        /// returns null with invalid z
        /// </summary>
        /// <param name="z"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Coord? TryToTwoXOne(int z, int x, int y)
        {
            if (z < 2)
            {
                return null;
            }
            return ToTwoXOne(z, x, y);
        }

        /// <summary>
        /// convert 1X1 coords to 2X1 coords.
        /// note that this only works when z >= 2
        /// </summary>
        /// <param name="oneXOneCoords"></param>
        /// <returns></returns>
        public Coord ToTwoXOne(Coord oneXOneCoords)
        {
            int z = oneXOneCoords.z - 1;
            int y = oneXOneCoords.y - (1 << (z - 1));
            return new Coord(z, oneXOneCoords.x, y);
        }

        /// <summary>
        /// convert 1X1 coords to 2X1 coords.
        /// note that this only works when z >= 2
        /// </summary>
        /// <param name="z"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Coord ToTwoXOne(int z, int x, int y)
        {
            z -= 1;
            y -= (1 << (z - 1));
            return new Coord(z, x, y);
        }
    }
}
