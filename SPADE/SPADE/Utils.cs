using System;
using System.Collections.Generic;
using System.Text;

namespace SPADE {
    internal static class Utils {
        public static unsafe void Copy(byte* pSource, byte* pDest, int nSize) {
            while (nSize > 0) {
                *pDest = *pSource;
                pSource++;
                pDest++;
                nSize--;
            }
        }

        public static unsafe void Invert(byte[] arrData) {
            for (int i = 0; i < arrData.Length; i++)
                arrData[i] = unchecked((byte)~(uint)arrData[i]);
        }

        public static unsafe bool IsDataEqual(byte[] arrData1, byte* pData2) {
            for (int i = 0; i < arrData1.Length; i++) {
                if (arrData1[i] != pData2[i])
                    return false;
            }

            return true;
        }
    }
}
