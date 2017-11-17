using System;

namespace Ochrona_Danych_Stenografia
{

    public static class Crr     // Crypt
    {
     
        //  byte -> 8* (int) bit    ; GetBitArr
        public static int[] GetBitArray(byte numa)
        {
            int[] bits = new int[8];

            if (numa == 0)
            {
                foreach( int a in bits)
                {
                    bits[a] = 0;
                }
                return bits;
            }

            for (int i = 7; i >= 0; i--)
            {
                if (numa == 0)
                {
                    bits[i] = 0;
                }

                bits[i] = numa % 2;
                numa /= 2;
            }
            return bits;
        }

        public static byte GetByte(int[] bits)
        {
            byte res = (byte)bits[0];

            for (int i = 1; i < 8; i++)
                res = (byte)((int)res * 2 + bits[i]);

            return res;
        }

    }
}